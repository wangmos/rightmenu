using RightMenuMaster.Helpers;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace RightMenuMaster.Imaging;

/// <summary>
/// 系统 DLL 中提取出的图标。
/// </summary>
public sealed record SystemIconInfo(int Index, string SourceFile, BitmapSource Image)
{
    /// <summary>可直接写入注册表 Icon 值的字符串。</summary>
    public string IconLocation => $"{SourceFile},{Index}";
}

/// <summary>
/// 图标读取、提取、转换与保存服务。
/// </summary>
public static class IconService
{
    /// <summary>应用保存自定义图标（ICO）的目录（程序目录下）。</summary>
    public static string IconsDir => AppPaths.IconsDir;

    public static string DefaultIconPath => Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\shell32.dll");

    // ---------------------------------------------------------------- 解析显示

    /// <summary>
    /// 把注册表 Icon 值解析为可显示的图片。支持 "文件" 与 "文件,索引" 两种格式。
    /// <paramref name="size"/> 为期望的像素边长，会真实传递给图标提取 API（而非事后放大）。
    /// </summary>
    public static BitmapSource? ResolveIcon(string? iconLocation, int size = 32)
    {
        if (string.IsNullOrWhiteSpace(iconLocation)) return null;
        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(iconLocation.Trim());
            int index = 0;
            var file = expanded;

            var comma = expanded.LastIndexOf(',');
            if (comma > 1 && int.TryParse(expanded.AsSpan(comma + 1).Trim(), out var parsed))
            {
                index = Math.Abs(parsed);
                file = expanded[..comma].Trim();
            }
            file = file.Trim('"');
            if (!File.Exists(file)) return null;

            var ext = Path.GetExtension(file).ToLowerInvariant();
            return ext switch
            {
                ".exe" or ".dll" or ".cpl" or ".ocx" or ".scr" or ".icl" => ExtractIconFromFile(file, index, size),
                ".ico" or ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" => LoadImage(file, size),
                _ => ExtractIconFromFile(file, index, size),
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从 exe/dll/icl 中提取指定索引的图标。
    /// 优先用 SHDefExtractIcon（可指定尺寸，能取到 48/64/256 等大尺寸位图），
    /// 失败时退回 ExtractIconEx（只有 32px 大图标）。
    /// </summary>
    private static BitmapSource? ExtractIconFromFile(string file, int index, int size = 32)
    {
        var icon = ExtractHIcon(file, index, size);
        // 索引无效时退回第一个图标
        if (icon == IntPtr.Zero && index != 0) icon = ExtractHIcon(file, 0, size);
        if (icon == IntPtr.Zero) return null;

        try
        {
            var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch
        {
            return null;
        }
        finally
        {
            NativeMethods.DestroyIcon(icon);
        }
    }

    /// <summary>按指定尺寸取一个 HICON，取不到返回 IntPtr.Zero（调用方负责 DestroyIcon）。</summary>
    private static IntPtr ExtractHIcon(string file, int index, int size)
    {
        if (size > 0)
        {
            try
            {
                // nIconSize 的低 16 位 = 大图标尺寸，高 16 位 = 小图标尺寸（此处不需要小图标）
                if (NativeMethods.SHDefExtractIcon(file, index, 0, out var large, out _, (uint)size) == 0
                    && large != IntPtr.Zero)
                    return large;
            }
            catch { /* 退回 ExtractIconEx */ }
        }

        var handles = new IntPtr[1];
        try
        {
            int count = NativeMethods.ExtractIconEx(file, index, handles, null, 1);
            return count > 0 ? handles[0] : IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static BitmapSource? LoadImage(string file, int size = 32)
    {
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(Path.GetFullPath(file));
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            // 解码尺寸取 2 倍，保证高 DPI 下不糊；上限 256 避免大图占内存
            bi.DecodePixelWidth = Math.Clamp(size * 2, 32, 256);
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch
        {
            return null;
        }
    }

    // ---------------------------------------------------------------- 系统图标库

    /// <summary>
    /// 从系统 DLL 批量提取图标，用于"系统图标"选择库。
    /// </summary>
    public static List<SystemIconInfo> ExtractSystemIcons(string file, int maxCount = 300)
    {
        var list = new List<SystemIconInfo>();
        if (!File.Exists(file)) return list;

        int total;
        try
        {
            total = NativeMethods.ExtractIconEx(file, -1, null, null, 0);
        }
        catch
        {
            return list;
        }
        total = Math.Min(total, maxCount);

        for (int i = 0; i < total; i++)
        {
            var img = ExtractIconFromFile(file, i);
            if (img != null) list.Add(new SystemIconInfo(i, file, img));
        }
        return list;
    }

    /// <summary>常用的系统图标库文件。</summary>
    public static (string Name, string Path)[] SystemIconLibraries { get; } =
    {
        ("shell32.dll（系统常用）", Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\shell32.dll")),
        ("imageres.dll（媒体图标）", Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\imageres.dll")),
        ("wmploc.dll（媒体播放）", Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\wmploc.dll")),
        ("pifmgr.dll（其他）", Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\pifmgr.dll")),
    };

    // ---------------------------------------------------------------- 保存为 ICO

    /// <summary>
    /// 保存内置图标为 ICO 文件，返回文件路径。
    /// </summary>
    public static string SaveBuiltinIcon(BuiltinIcon icon)
    {
        Directory.CreateDirectory(IconsDir);
        var img = BuiltinIcons.Render(icon, 256);
        var path = Path.Combine(IconsDir, $"{SanitizeName(icon.Name)}.ico");
        WriteIco(img, path);
        return path;
    }

    /// <summary>
    /// 把任意图片文件转换为 ICO 保存到图标目录，返回路径。
    /// 如果本身就是 .ico 则直接复制。
    /// </summary>
    public static string SaveImageAsIcon(string imageFile)
    {
        Directory.CreateDirectory(IconsDir);

        // 基名限长后再拼 32 位 GUID：不能对整串取 [..64]，短文件名会直接越界
        var baseName = SanitizeName(Path.GetFileNameWithoutExtension(imageFile));
        if (baseName.Length > 24) baseName = baseName[..24];
        var target = Path.Combine(IconsDir, $"{baseName}_{Guid.NewGuid():N}.ico");

        var ext = Path.GetExtension(imageFile).ToLowerInvariant();
        if (ext == ".ico")
        {
            File.Copy(imageFile, target, overwrite: true);
            return target;
        }

        var bi = new BitmapImage();
        bi.BeginInit();
        bi.UriSource = new Uri(Path.GetFullPath(imageFile));
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.EndInit();

        WriteIco(Resize(bi, 256), target);
        return target;
    }

    /// <summary>
    /// 从可执行文件提取主图标并保存为 ICO。
    /// </summary>
    public static string? SaveExtractedIcon(string exeFile, int index)
    {
        var img = ExtractIconFromFile(exeFile, index);
        if (img == null) return null;
        Directory.CreateDirectory(IconsDir);
        var target = Path.Combine(IconsDir, $"{SanitizeName(Path.GetFileNameWithoutExtension(exeFile))}_{index}.ico");
        WriteIco(Resize(img, 256), target);
        return target;
    }

    /// <summary>
    /// 将 BitmapSource 写为 PNG 压缩格式的 ICO（256px，Windows Vista+ 支持）。
    /// </summary>
    public static void WriteIco(BitmapSource source, string path)
    {
        var img = (source.PixelWidth == 256 && source.PixelHeight == 256) ? source : Resize(source, 256);

        byte[] png;
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(img));
        using (var ms = new MemoryStream())
        {
            encoder.Save(ms);
            png = ms.ToArray();
        }

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        // ICONDIR
        bw.Write((short)0);           // reserved
        bw.Write((short)1);           // type: icon
        bw.Write((short)1);           // count
        // ICONDIRENTRY
        bw.Write((byte)0);            // width 0 = 256
        bw.Write((byte)0);            // height 0 = 256
        bw.Write((byte)0);            // color count
        bw.Write((byte)0);            // reserved
        bw.Write((short)1);           // planes
        bw.Write((short)32);          // bit count
        bw.Write(png.Length);         // size of image data
        bw.Write(22);                 // offset of image data (6 + 16)
        bw.Write(png);
    }

    private static BitmapSource Resize(BitmapSource source, int size)
    {
        var visual = new System.Windows.Media.DrawingVisual();
        using (var dc = visual.RenderOpen())
            dc.DrawImage(source, new Rect(0, 0, size, size));
        var rtb = new RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var result = new string(chars).Trim().Replace(" ", "_");
        return string.IsNullOrWhiteSpace(result) ? "icon" : result;
    }
}

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
                ".exe" or ".dll" or ".cpl" or ".ocx" or ".scr" or ".icl" => ExtractIconFromFile(file, index),
                ".ico" or ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" => LoadImage(file),
                _ => ExtractIconFromFile(file, index),
            };
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource? ExtractIconFromFile(string file, int index)
    {
        var handles = new IntPtr[1];
        try
        {
            int count = NativeMethods.ExtractIconEx(file, index, handles, null, 1);
            if (count <= 0 || handles[0] == IntPtr.Zero)
            {
                // 索引无效时退回第一个图标
                if (index != 0)
                {
                    count = NativeMethods.ExtractIconEx(file, 0, handles, null, 1);
                    if (count <= 0 || handles[0] == IntPtr.Zero) return null;
                }
                else return null;
            }

            var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(handles[0], Int32Rect.Empty,
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
            if (handles[0] != IntPtr.Zero) NativeMethods.DestroyIcon(handles[0]);
        }
    }

    private static BitmapSource? LoadImage(string file)
    {
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(Path.GetFullPath(file));
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bi.DecodePixelWidth = 128;
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
        var target = Path.Combine(IconsDir, $"{SanitizeName(Path.GetFileNameWithoutExtension(imageFile))}_{Guid.NewGuid():N}"[..64] + ".ico");

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

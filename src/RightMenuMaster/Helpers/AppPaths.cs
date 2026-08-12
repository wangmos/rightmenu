using System.IO;

namespace RightMenuMaster.Helpers;

/// <summary>
/// 数据保存位置。
///
/// 默认放在程序目录（exe 所在目录），整个文件夹拷走即可迁移配置与图标（绿色软件）。
/// 但程序被放到 Program Files 等受保护目录时该目录不可写，此时自动回退到
/// %LOCALAPPDATA%\RightMenuMaster，避免「保存图标」「AI 接口设置」直接报错。
/// </summary>
public static class AppPaths
{
    /// <summary>实际使用的数据目录（程序目录或用户数据目录）。</summary>
    public static string DataDir { get; }

    /// <summary>数据是否回退到了用户目录（程序目录不可写）。</summary>
    public static bool IsUsingFallback { get; }

    public static string IconsDir => Path.Combine(DataDir, "Icons");

    public static string LlmSettingsFile => Path.Combine(DataDir, "llm.json");

    static AppPaths()
    {
        var baseDir = AppContext.BaseDirectory;
        if (IsWritable(baseDir))
        {
            DataDir = baseDir;
            IsUsingFallback = false;
            return;
        }

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RightMenuMaster");
        try { Directory.CreateDirectory(fallback); } catch { /* 交给后续写入报错 */ }

        DataDir = fallback;
        IsUsingFallback = true;
    }

    /// <summary>实际写一个临时文件来判断目录可写（权限位判断在 Windows 上不可靠）。</summary>
    private static bool IsWritable(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return false;
            var probe = Path.Combine(dir, $".write_probe_{Guid.NewGuid():N}");
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch
        {
            return false;
        }
    }
}

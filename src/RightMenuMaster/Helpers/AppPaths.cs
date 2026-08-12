using System.IO;

namespace RightMenuMaster.Helpers;

/// <summary>
/// 数据保存位置：全部放在程序目录（exe 所在目录），整个文件夹拷走即可迁移配置与图标。
/// </summary>
public static class AppPaths
{
    public static string DataDir { get; } = AppContext.BaseDirectory;

    public static string IconsDir { get; } = Path.Combine(AppContext.BaseDirectory, "Icons");

    public static string LlmSettingsFile { get; } = Path.Combine(AppContext.BaseDirectory, "llm.json");
}

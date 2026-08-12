using Microsoft.Win32;
using RightMenuMaster.Models;
using System.Diagnostics;
using System.IO;

namespace RightMenuMaster.Services;

/// <summary>
/// 默认程序查询与设置。
///
/// Windows 8 以后直接写 UserChoice 需要计算受保护的哈希值，因此修改默认程序统一
/// 通过调用系统"打开方式"对话框（可勾选"始终使用"）或跳转系统设置页来完成，
/// 这是最稳妥、兼容性最好的做法。
/// </summary>
public static class DefaultProgramService
{
    /// <summary>
    /// 读取某个扩展名当前的默认程序显示名称。
    /// </summary>
    public static string GetDefaultAppName(string ext)
    {
        ext = MenuCategoryInfo.NormalizeExt(ext);
        try
        {
            using var userChoice = Registry.CurrentUser.OpenSubKey(
                $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{ext}\UserChoice");
            var progId = userChoice?.GetValue("ProgId") as string;
            if (string.IsNullOrEmpty(progId)) return "(未设置)";

            // 优先取应用的友好名称
            using var appKey = Registry.ClassesRoot.OpenSubKey($@"{progId}\Application");
            var friendly = appKey?.GetValue("ApplicationName") as string;
            if (!string.IsNullOrEmpty(friendly)) return friendly;

            using var progIdKey = Registry.ClassesRoot.OpenSubKey(progId);
            var desc = progIdKey?.GetValue(null) as string;
            if (!string.IsNullOrEmpty(desc)) return desc;

            return progId;
        }
        catch
        {
            return "(未知)";
        }
    }

    /// <summary>
    /// 打开系统"打开方式"对话框，让用户为指定扩展名选择默认程序。
    /// </summary>
    public static void ChangeDefaultViaOpenWith(string ext)
    {
        ext = MenuCategoryInfo.NormalizeExt(ext);

        // 创建一个该扩展名的临时文件，供"打开方式"对话框识别
        var dir = Path.Combine(Path.GetTempPath(), "RightMenuMaster_Dummy");
        Directory.CreateDirectory(dir);
        var dummy = Path.Combine(dir, "sample" + ext);
        if (!File.Exists(dummy))
        {
            try { File.WriteAllText(dummy, string.Empty); }
            catch { /* 忽略 */ }
        }

        try
        {
            // 新版 Windows（11 / Server 2025 等）上 rundll32 的 OpenAs_RunDLL 经常无任何反应，
            // 优先用系统的 OpenWith.exe 弹出"打开方式"对话框
            var openWith = Path.Combine(
                Environment.ExpandEnvironmentVariables("%SystemRoot%"), @"System32\OpenWith.exe");
            if (File.Exists(openWith))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = openWith,
                    Arguments = $"\"{dummy}\"",
                    UseShellExecute = true,
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"shell32.dll,OpenAs_RunDLL \"{dummy}\"",
                    UseShellExecute = true,
                });
            }
        }
        catch
        {
            // 回退：直接打开系统默认应用设置页
            OpenDefaultAppsSettings();
        }
    }

    /// <summary>
    /// 打开 Windows 设置中的"默认应用"页面。
    /// </summary>
    public static void OpenDefaultAppsSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
        }
        catch { /* 忽略 */ }
    }
}

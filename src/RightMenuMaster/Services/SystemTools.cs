using Microsoft.Win32;
using RightMenuMaster.Helpers;
using System.Diagnostics;

namespace RightMenuMaster.Services;

/// <summary>
/// 系统小工具：重启资源管理器、隐藏文件/扩展名开关等。
/// </summary>
public static class SystemTools
{
    private const string AdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    /// <summary>
    /// 重启 explorer.exe。
    /// </summary>
    public static void RestartExplorer()
    {
        foreach (var p in Process.GetProcessesByName("explorer"))
        {
            try { p.Kill(); p.WaitForExit(3000); } catch { /* 忽略 */ }
            finally { p.Dispose(); }
        }

        // 通常系统会自动重启 explorer；若没有则手动拉起
        System.Threading.Thread.Sleep(800);
        if (Process.GetProcessesByName("explorer").Length == 0)
        {
            try { Process.Start("explorer.exe"); } catch { /* 忽略 */ }
        }
    }

    /// <summary>是否显示隐藏文件。</summary>
    public static bool GetShowHiddenFiles() => ReadAdvancedDword("Hidden") == 1;

    /// <summary>设置是否显示隐藏文件。Hidden: 1=显示, 2=不显示。</summary>
    public static void SetShowHiddenFiles(bool show)
    {
        WriteAdvancedDword("Hidden", show ? 1 : 2);
        NativeMethods.NotifyAssociationChanged();
    }

    /// <summary>是否显示文件扩展名（HideFileExt: 0=显示, 1=隐藏）。</summary>
    public static bool GetShowExtensions() => ReadAdvancedDword("HideFileExt") == 0;

    public static void SetShowExtensions(bool show)
    {
        WriteAdvancedDword("HideFileExt", show ? 0 : 1);
        NativeMethods.NotifyAssociationChanged();
    }

    private static int ReadAdvancedDword(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AdvancedKey);
            return key?.GetValue(name) as int? ?? 0;
        }
        catch { return 0; }
    }

    private static void WriteAdvancedDword(string name, int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(AdvancedKey);
        key?.SetValue(name, value, RegistryValueKind.DWord);
    }
}

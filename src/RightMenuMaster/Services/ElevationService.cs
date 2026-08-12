using System.Diagnostics;
using System.Security.Principal;

namespace RightMenuMaster.Services;

/// <summary>
/// 管理员权限检测与应用提权重启。
/// </summary>
public static class ElevationService
{
    public static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 以管理员身份重启当前应用。成功返回 true（此时当前进程应退出）。
    /// </summary>
    public static bool RestartAsAdmin()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return false;

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas",
            };
            Process.Start(psi);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // 用户在 UAC 弹窗中取消
            return false;
        }
    }
}

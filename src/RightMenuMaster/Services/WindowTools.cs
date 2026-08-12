using RightMenuMaster.Helpers;
using System.Diagnostics;
using System.Text;

namespace RightMenuMaster.Services;

/// <summary>
/// 窗口信息（用于置顶工具）。
/// </summary>
public class WindowInfo
{
    public IntPtr Handle { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public bool IsTopMost { get; set; }
}

/// <summary>
/// 窗口置顶相关功能。
/// </summary>
public static class WindowTools
{
    /// <summary>
    /// 枚举所有可见的顶层窗口。
    /// </summary>
    public static List<WindowInfo> GetVisibleWindows()
    {
        var list = new List<WindowInfo>();
        var shellWnd = NativeMethods.GetShellWindow();
        var desktopWnd = NativeMethods.GetDesktopWindow();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            try
            {
                if (hWnd == shellWnd || hWnd == desktopWnd) return true;
                if (!NativeMethods.IsWindowVisible(hWnd)) return true;

                int len = NativeMethods.GetWindowTextLength(hWnd);
                if (len == 0) return true;

                var sb = new StringBuilder(len + 1);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                var title = sb.ToString();
                if (string.IsNullOrWhiteSpace(title)) return true;

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                string procName = "(未知)";
                try
                {
                    using var p = Process.GetProcessById((int)pid);
                    procName = p.ProcessName;
                }
                catch { /* 进程可能已退出或无权限 */ }

                bool topmost = (NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE)
                                & NativeMethods.WS_EX_TOPMOST) != 0;

                list.Add(new WindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    ProcessName = procName,
                    IsTopMost = topmost,
                });
            }
            catch { /* 忽略单个窗口错误 */ }
            return true;
        }, IntPtr.Zero);

        return list;
    }

    /// <summary>
    /// 设置/取消窗口置顶。
    /// </summary>
    public static void SetTopMost(IntPtr hWnd, bool topMost)
    {
        NativeMethods.SetWindowPos(hWnd,
            topMost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }
}

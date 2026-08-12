using System.Runtime.InteropServices;
using System.Text;

namespace RightMenuMaster.Helpers;

/// <summary>
/// Win32 P/Invoke 声明。用于窗口置顶、图标提取、通知 shell 刷新等。
/// </summary>
internal static class NativeMethods
{
    // ---- 窗口置顶 / 枚举 ----
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOPMOST = 0x00000008;

    public const int SWP_NOSIZE = 0x0001;
    public const int SWP_NOMOVE = 0x0002;
    public const int SWP_NOACTIVATE = 0x0010;

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    /// <summary>
    /// 读取窗口的扩展样式等 long 值。64 位进程必须走 GetWindowLongPtr，
    /// 32 位下该导出不存在，需回退到 GetWindowLong。
    /// </summary>
    public static long GetWindowLongValue(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex).ToInt64() : GetWindowLong32(hWnd, nIndex);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    public static extern IntPtr GetDesktopWindow();

    // ---- 图标提取 ----
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int ExtractIconEx(string szFileName, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, int nIcons);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr handle);

    /// <summary>
    /// 按指定尺寸提取图标（ExtractIconEx 的大图标固定 32px，取不到更清晰的 48/64/256）。
    /// nIconSize：低 16 位 = 大图标边长，高 16 位 = 小图标边长。返回 S_OK(0) 表示成功。
    /// </summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHDefExtractIcon(string pszIconFile, int iIndex, uint uFlags,
        out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIconSize);

    // ---- Shell 刷新通知 ----
    public const int SHCNE_ASSOCCHANGED = 0x08000000;
    public const uint SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll")]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    public static void NotifyAssociationChanged() =>
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

    // ---- 间接字符串解析（形如 @%SystemRoot%\system32\shell32.dll,-345 的资源引用）----
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    public static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf,
        uint cchOutBuf, IntPtr ppvReserved);
}

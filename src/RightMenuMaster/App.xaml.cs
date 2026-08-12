using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace RightMenuMaster;

/// <summary>
/// 应用程序入口。
/// </summary>
public partial class App : Application
{
    public static string CrashLogPath { get; } =
        Path.Combine(Path.GetTempPath(), "RightMenuMaster_crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            WriteCrashLog((Exception)args.ExceptionObject);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
        MessageBox.Show(
            "发生了一个未处理的异常：\n\n" + e.Exception.Message +
            "\n\n详细堆栈已写入：" + CrashLogPath,
            "右键菜单管家",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    /// <summary>
    /// 追加而非覆盖：连续崩溃时只留最后一条会丢掉最初的线索。
    /// 文件超过 512KB 时整体重置，避免无限增长。
    /// </summary>
    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            const long maxBytes = 512 * 1024;
            if (File.Exists(CrashLogPath) && new FileInfo(CrashLogPath).Length > maxBytes)
                File.Delete(CrashLogPath);

            File.AppendAllText(CrashLogPath,
                $"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ====={Environment.NewLine}"
                + ex + Environment.NewLine + Environment.NewLine);
        }
        catch { /* 忽略 */ }
    }
}

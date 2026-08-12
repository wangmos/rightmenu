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

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            File.WriteAllText(CrashLogPath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + ex.ToString());
        }
        catch { /* 忽略 */ }
    }
}

using RightMenuMaster.Services;
using System.Windows;
using System.Windows.Controls;

namespace RightMenuMaster.Views.Pages;

/// <summary>
/// 小工具箱页：窗口置顶、密码速记框、重启资源管理器、隐藏文件与扩展名开关。
/// </summary>
public partial class ToolboxPage : UserControl
{
    /// <summary>初始化开关状态期间抑制事件，避免把「读到的状态」又写回注册表。</summary>
    private bool _initializing = true;

    private IShellHost? Host => Window.GetWindow(this) as IShellHost;

    public ToolboxPage()
    {
        InitializeComponent();
    }

    /// <summary>读取资源管理器当前设置并同步到开关（由宿主在加载完成后调用）。</summary>
    public void InitSwitches()
    {
        try
        {
            HiddenSwitch.IsChecked = SystemTools.GetShowHiddenFiles();
            ExtSwitch.IsChecked = SystemTools.GetShowExtensions();
        }
        catch { /* 忽略 */ }
        finally { _initializing = false; }
    }

    private void OpenTopMost_Click(object sender, RoutedEventArgs e)
        => new TopMostWindow { Owner = Window.GetWindow(this) }.Show();

    private void OpenPasswordBox_Click(object sender, RoutedEventArgs e)
        => new PasswordNoteWindow().Show();

    private async void RestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this)!;
        var confirm = MessageBox.Show(owner,
            "将关闭并重启 Windows 资源管理器（桌面与任务栏会短暂消失）。\n继续吗？",
            "重启资源管理器", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        BtnRestartExplorer.IsEnabled = false;
        Host?.ShowToast("正在重启资源管理器…");
        try
        {
            await SystemTools.RestartExplorerAsync();
            Host?.ShowToast("资源管理器已重启");
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, "重启失败：" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            BtnRestartExplorer.IsEnabled = true;
        }
    }

    private void HiddenSwitch_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        try
        {
            SystemTools.SetShowHiddenFiles(HiddenSwitch.IsChecked == true);
            Host?.ShowToast(HiddenSwitch.IsChecked == true ? "已显示隐藏文件" : "已隐藏隐藏文件");
        }
        catch { /* 忽略 */ }
    }

    private void ExtSwitch_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        try
        {
            SystemTools.SetShowExtensions(ExtSwitch.IsChecked == true);
            Host?.ShowToast(ExtSwitch.IsChecked == true ? "已显示文件扩展名" : "已隐藏文件扩展名");
        }
        catch { /* 忽略 */ }
    }
}

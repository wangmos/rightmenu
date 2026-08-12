using RightMenuMaster.Helpers;
using RightMenuMaster.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RightMenuMaster.Views;

/// <summary>
/// 窗口置顶工具：枚举可见窗口，一键设置 / 取消"总在最前"。
/// </summary>
public partial class TopMostWindow : Window
{
    private readonly GridSorter _sorter;

    public TopMostWindow()
    {
        InitializeComponent();
        Icon = MainWindow.MakeAppIcon();

        // 列头点击排序
        _sorter = new GridSorter(WinList, new Dictionary<string, string>
        {
            ["窗口标题"] = nameof(WindowInfo.Title),
            ["进程"] = nameof(WindowInfo.ProcessName),
            ["置顶"] = nameof(WindowInfo.IsTopMost),
        });
        _sorter.Attach();

        LoadWindows();
    }

    private void LoadWindows()
    {
        try
        {
            var windows = WindowTools.GetVisibleWindows();
            WinList.ItemsSource = windows;
            _sorter.Reapply(); // ItemsSource 被整体替换，恢复当前排序
            var topCount = windows.Count(w => w.IsTopMost);
            StatusText.Text = $"共 {windows.Count} 个窗口，其中 {topCount} 个已置顶";
        }
        catch (Exception ex)
        {
            StatusText.Text = "枚举窗口失败：" + ex.Message;
        }
    }

    private void ToggleWindow(WindowInfo info)
    {
        try
        {
            var target = !info.IsTopMost;
            WindowTools.SetTopMost(info.Handle, target);
            info.IsTopMost = target;
            LoadWindows();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "操作失败：" + ex.Message, "窗口置顶工具",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool _suppressTopSwitch;

    /// <summary>置顶列开关：切换窗口置顶状态。初始绑定/容器复用与模型一致时忽略；失败时回弹。</summary>
    private void TopMostSwitch_Change(object sender, RoutedEventArgs e)
    {
        if (_suppressTopSwitch) return;
        if (sender is not CheckBox cb || cb.Tag is not WindowInfo info) return;
        var target = cb.IsChecked == true; // 开关 ON = 置顶
        if (info.IsTopMost == target) return; // 与模型一致：初始绑定/容器复用，非用户操作
        try
        {
            WindowTools.SetTopMost(info.Handle, target);
            info.IsTopMost = target;
            _suppressTopSwitch = true;
            try { LoadWindows(); }
            finally { _suppressTopSwitch = false; }
        }
        catch (Exception ex)
        {
            RevertTopSwitch(cb);
            MessageBox.Show(this, "操作失败：" + ex.Message, "窗口置顶工具",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RevertTopSwitch(CheckBox cb)
    {
        _suppressTopSwitch = true;
        cb.IsChecked = cb.IsChecked != true;
        _suppressTopSwitch = false;
    }

    private void WinList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (WinList.SelectedItem is WindowInfo info)
            ToggleWindow(info);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadWindows();
}

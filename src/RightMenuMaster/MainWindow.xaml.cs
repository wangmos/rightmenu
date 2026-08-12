using RightMenuMaster.Imaging;
using RightMenuMaster.Models;
using RightMenuMaster.Services;
using RightMenuMaster.Views;
using RightMenuMaster.Views.Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace RightMenuMaster;

/// <summary>
/// 主窗口：只负责侧边导航、页面切换、权限状态与轻提示，
/// 四个功能页各自封装在 <see cref="Views.Pages"/> 下的 UserControl 中。
/// </summary>
public partial class MainWindow : Window, IShellHost
{
    private DispatcherTimer? _toastTimer;
    private bool _isAdmin;

    public MainWindow()
    {
        InitializeComponent();

        // 窗口图标
        Icon = MakeAppIcon();

        _isAdmin = ElevationService.IsAdministrator();
        UpdateAdminUi();

        Loaded += MainWindow_Loaded;
    }

    /// <summary>
    /// 首屏之后再做磁盘/注册表 IO：窗口先显示出来，避免启动时白屏几百毫秒。
    /// </summary>
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;

        await PageMenus.InitialLoadAsync();
        PageTools.InitSwitches();
    }

    internal static ImageSource MakeAppIcon()
    {
        var def = new BuiltinIcon("App", "#2F6FED", (dc, s, _) =>
        {
            // Geometry.Parse 返回冻结对象，Clone 后才能设置 Transform
            var g = Geometry.Parse("M5,3 L14,12 L10.5,12.8 L13,18.2 L10.6,19.3 L8.2,13.9 L5,16.5 Z").Clone();
            g.Transform = new ScaleTransform(s / 24.0, s / 24.0);
            dc.DrawGeometry(Brushes.White, null, g);
        });
        return BuiltinIcons.Render(def, 64);
    }

    // ================================================================== IShellHost

    public bool IsAdmin => _isAdmin;

    public MenuListPage MenuList => PageMenus;

    public void ShowToast(string message)
    {
        ToastText.Text = message;
        Toast.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer!.Stop();
            Toast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(300)));
        };
        _toastTimer.Start();
    }

    public void PromptElevation()
    {
        var choice = MessageBox.Show(this,
            "此操作需要管理员权限。\n\n是否以管理员身份重新启动本应用？",
            "需要管理员权限", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (choice != MessageBoxResult.Yes) return;

        if (ElevationService.RestartAsAdmin())
            Application.Current.Shutdown();
    }

    // ================================================================== 导航

    private void NavScope_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }) return;
        if (!Enum.TryParse<MenuCategory>(tag, out var category)) return;

        ShowPage(PageMenus);
        PageMenus?.ShowCategory(category); // 初始化期间页面可能尚未创建
    }

    private void NavPage_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }) return;
        switch (tag)
        {
            case "templates":
                ShowPage(PageTemplates);
                break;
            case "defaults":
                ShowPage(PageDefaults);
                PageDefaults?.EnsureLoaded();
                break;
            case "tools":
                ShowPage(PageTools);
                break;
        }
    }

    private void ShowPage(UIElement page)
    {
        if (PageMenus is null || PageTemplates is null || PageDefaults is null || PageTools is null) return;
        PageMenus.Visibility = ReferenceEquals(page, PageMenus) ? Visibility.Visible : Visibility.Collapsed;
        PageTemplates.Visibility = ReferenceEquals(page, PageTemplates) ? Visibility.Visible : Visibility.Collapsed;
        PageDefaults.Visibility = ReferenceEquals(page, PageDefaults) ? Visibility.Visible : Visibility.Collapsed;
        PageTools.Visibility = ReferenceEquals(page, PageTools) ? Visibility.Visible : Visibility.Collapsed;
    }

    // ================================================================== 权限与关于

    private void UpdateAdminUi()
    {
        if (_isAdmin)
        {
            AdminDot.Fill = (SolidColorBrush)FindResource("SuccessBrush");
            AdminText.Text = "管理员模式";
            AdminButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            AdminDot.Fill = (SolidColorBrush)FindResource("WarningBrush");
            AdminText.Text = "普通权限";
            AdminButton.Visibility = Visibility.Visible;
        }
    }

    private void AdminButton_Click(object sender, RoutedEventArgs e) => PromptElevation();

    private void About_Click(object sender, RoutedEventArgs e)
        => new AboutDialog { Owner = this }.ShowDialog();
}

using RightMenuMaster.Models;
using RightMenuMaster.Services;
using RightMenuMaster.Views;
using RightMenuMaster.Views.Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
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

    private static ImageSource? _appIcon;

    /// <summary>
    /// 应用图标。与 exe 的 ApplicationIcon 同一个 app.ico，
    /// 这样文件图标、任务栏图标、各窗口标题栏图标完全一致。
    /// </summary>
    internal static ImageSource MakeAppIcon()
    {
        if (_appIcon != null) return _appIcon;

        var decoder = new IconBitmapDecoder(
            new Uri("pack://application:,,,/app.ico"),
            BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad);

        // 取 64px 那一帧：标题栏与任务栏都足够清晰，又不必解码 256px
        var frame = decoder.Frames.FirstOrDefault(f => f.PixelWidth == 64)
            ?? decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
        frame.Freeze();

        _appIcon = frame;
        return frame;
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

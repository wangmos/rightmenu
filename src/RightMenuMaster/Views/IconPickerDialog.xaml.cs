using RightMenuMaster.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace RightMenuMaster.Views;

/// <summary>
/// 图标选择对话框：内置小图标 / 系统图标库 / 图片文件。
/// </summary>
public partial class IconPickerDialog : Window
{
    /// <summary>内置图标条目（渲染后的图片 + 定义）。</summary>
    public class BuiltinIconEntry
    {
        public string Name { get; init; } = string.Empty;
        public BitmapSource Image { get; init; } = null!;
        public BuiltinIcon Def { get; init; } = null!;
    }

    private sealed record LibOption(string Name, string Path)
    {
        public override string ToString() => Name;
    }

    private enum PickKind { None, Builtin, System, File }

    private PickKind _kind = PickKind.None;
    private object? _picked;
    private bool _suppress;

    private static readonly Dictionary<string, List<SystemIconInfo>> SysCache = new();

    /// <summary>确定后返回的图标位置字符串（可写入注册表 Icon 值）。</summary>
    public string? SelectedIconLocation { get; private set; }

    public IconPickerDialog(string? currentIconLocation)
    {
        InitializeComponent();
        Icon = MainWindow.MakeAppIcon();

        // 内置图标渲染（84px，显示 42px 足够清晰）
        BuiltinList.ItemsSource = BuiltinIcons.All.Select(def => new BuiltinIconEntry
        {
            Name = def.Name,
            Def = def,
            Image = BuiltinIcons.Render(def, 84),
        }).ToList();

        LibCombo.ItemsSource = IconService.SystemIconLibraries
            .Select(l => new LibOption(l.Name, l.Path))
            .ToList();
        LibCombo.SelectedIndex = 0;

        if (!string.IsNullOrEmpty(currentIconLocation))
        {
            ResultPreview.Source = IconService.ResolveIcon(currentIconLocation, 64);
            ResultText.Text = "当前图标：" + currentIconLocation;
        }
    }

    // ---------------------------------------------------------------- 选择同步

    private void Pick_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;

        if (ReferenceEquals(sender, BuiltinList) && BuiltinList.SelectedItem is BuiltinIconEntry entry)
        {
            _kind = PickKind.Builtin;
            _picked = entry;
            ClearSelectionSilently(SystemList);
            UpdateResult(entry.Image, "内置图标：" + entry.Name);
        }
        else if (ReferenceEquals(sender, SystemList) && SystemList.SelectedItem is SystemIconInfo info)
        {
            _kind = PickKind.System;
            _picked = info;
            ClearSelectionSilently(BuiltinList);
            UpdateResult(info.Image, info.IconLocation);
        }
    }

    private void ClearSelectionSilently(ListBox list)
    {
        if (list.SelectedItem == null) return;
        _suppress = true;
        list.SelectedItem = null;
        _suppress = false;
    }

    private void UpdateResult(BitmapSource? image, string text)
    {
        ResultPreview.Source = image;
        ResultText.Text = text;
    }

    // ---------------------------------------------------------------- 系统图标库

    private async void LibCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LibCombo.SelectedItem is not LibOption lib || SystemList == null) return;

        SysLoadingText.Text = "正在提取图标…";
        SysLoadingText.Visibility = Visibility.Visible;
        SystemList.ItemsSource = null;

        List<SystemIconInfo> icons;
        if (SysCache.TryGetValue(lib.Path, out var cached))
        {
            icons = cached;
        }
        else
        {
            icons = await Task.Run(() => IconService.ExtractSystemIcons(lib.Path));
            SysCache[lib.Path] = icons;
            // 若期间用户已切换库，丢弃结果
            if (LibCombo.SelectedItem is not LibOption current || current.Path != lib.Path) return;
        }

        SysLoadingText.Text = $"共 {icons.Count} 个图标";
        SystemList.ItemsSource = icons;
    }

    // ---------------------------------------------------------------- 文件选择

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择图标文件",
            Filter = "图标/图片|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.gif|图标文件|*.ico|图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|程序或图标库|*.exe;*.dll|所有文件|*.*",
        };
        if (dlg.ShowDialog(this) != true) return;

        var file = dlg.FileName;
        FilePathBox.Text = file;
        ClearSelectionSilently(BuiltinList);
        ClearSelectionSilently(SystemList);

        var preview = IconService.ResolveIcon(file, 64);
        FilePreview.Source = preview;

        if (preview != null)
        {
            _kind = PickKind.File;
            _picked = file;
            UpdateResult(preview, file);
            FilePreviewHint.Text = "预览（右键菜单中将显示此图标）";
        }
        else
        {
            _kind = PickKind.None;
            _picked = null;
            FilePreviewHint.Text = "无法读取该文件的图标";
        }
    }

    // ---------------------------------------------------------------- 确定/取消

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            switch (_kind)
            {
                case PickKind.Builtin when _picked is BuiltinIconEntry entry:
                    SelectedIconLocation = IconService.SaveBuiltinIcon(entry.Def);
                    break;

                case PickKind.System when _picked is SystemIconInfo info:
                    SelectedIconLocation = info.IconLocation;
                    break;

                case PickKind.File when _picked is string file:
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    SelectedIconLocation = ext is ".exe" or ".dll"
                        ? file + ",0"
                        : IconService.SaveImageAsIcon(file);
                    break;

                default:
                    MessageBox.Show(this, "请先选择一个图标。", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存图标失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

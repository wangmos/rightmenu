using Microsoft.Win32;
using RightMenuMaster.Helpers;
using RightMenuMaster.Imaging;
using RightMenuMaster.Models;
using RightMenuMaster.Services;
using RightMenuMaster.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace RightMenuMaster;

/// <summary>
/// 主窗口：分类管理右键菜单、快捷模板、默认程序、小工具箱。
/// </summary>
public partial class MainWindow : Window
{
    private MenuCategory _currentCategory = MenuCategory.Background;
    private string? _currentExt;
    private readonly ObservableCollection<MenuItemModel> _entries = new();
    private ICollectionView? _entryView;
    private ICollectionView? _extView;
    private bool _isAdmin;
    private bool _initializing = true;
    private bool _defaultsLoaded;
    private DispatcherTimer? _toastTimer;
    private System.Windows.Media.Imaging.BitmapSource? _fallbackIcon;

    public MainWindow()
    {
        InitializeComponent();

        // 窗口图标
        Icon = MakeAppIcon();

        EntryList.ItemsSource = _entries;
        _entryView = CollectionViewSource.GetDefaultView(_entries);
        _entryView.Filter = EntryFilter;

        // 列头点击排序（映射：列标题 → 排序属性；图标列不参与排序）
        new GridSorter(EntryList, new Dictionary<string, string>
        {
            ["标题"] = nameof(MenuItemModel.DisplayTitle),
            ["命令"] = nameof(MenuItemModel.Command),
            ["选项"] = nameof(MenuItemModel.ShiftExtended),
            ["状态"] = nameof(MenuItemModel.IsDisabled),
            ["来源"] = nameof(MenuItemModel.Source),
        }).Attach();

        TemplateList.ItemsSource = BuiltinTemplates.All;

        _fallbackIcon = IconService.ResolveIcon(@"%SystemRoot%\System32\shell32.dll,0");

        _isAdmin = ElevationService.IsAdministrator();
        UpdateAdminUi();
        UpdateScopeHeader();
        RefreshEntries();
        InitToolboxState();

        _initializing = false;
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

    // ================================================================== 导航

    private void NavScope_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }) return;
        if (!Enum.TryParse<MenuCategory>(tag, out var category)) return;

        _currentCategory = category;
        ShowPage(PageMenus);

        if (EntryList is null) return; // 初始化期间

        ExtSelector.Visibility = category == MenuCategory.Extension ? Visibility.Visible : Visibility.Collapsed;
        if (category == MenuCategory.Extension)
        {
            EnsureExtensionComboLoaded();
            if (string.IsNullOrEmpty(_currentExt))
            {
                _currentExt = ".txt";
                ExtCombo.Text = _currentExt;
            }
        }

        UpdateScopeHeader();
        RefreshEntries();
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
                EnsureDefaultsLoaded();
                break;
            case "tools":
                ShowPage(PageTools);
                break;
        }
    }

    private void ShowPage(Grid page)
    {
        if (PageMenus is null || PageTemplates is null || PageDefaults is null || PageTools is null) return;
        PageMenus.Visibility = ReferenceEquals(page, PageMenus) ? Visibility.Visible : Visibility.Collapsed;
        PageTemplates.Visibility = ReferenceEquals(page, PageTemplates) ? Visibility.Visible : Visibility.Collapsed;
        PageDefaults.Visibility = ReferenceEquals(page, PageDefaults) ? Visibility.Visible : Visibility.Collapsed;
        PageTools.Visibility = ReferenceEquals(page, PageTools) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateScopeHeader()
    {
        MenuPageTitle.Text = _currentCategory == MenuCategory.Extension
            ? $"指定扩展名（{MenuCategoryInfo.NormalizeExt(_currentExt)}）"
            : _currentCategory.DisplayName();
        MenuPageSubtitle.Text = _currentCategory.Description();
    }

    // ================================================================== 菜单列表

    private bool EntryFilter(object obj)
    {
        if (obj is not MenuItemModel item) return false;
        var kw = SearchBox?.Text?.Trim();
        if (string.IsNullOrEmpty(kw)) return true;
        return item.DisplayTitle.Contains(kw, StringComparison.OrdinalIgnoreCase)
            || item.Command.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshEntries()
    {
        try
        {
            var items = RegistryService.GetEntries(_currentCategory, _currentExt);
            foreach (var item in items)
                item.Icon = IconService.ResolveIcon(item.IconPath) ?? _fallbackIcon;

            _entries.Clear();
            foreach (var item in items) _entries.Add(item);

            var sysCount = items.Count(i => i.Source == RegistrySource.LocalMachine);
            StatusText.Text = $"共 {items.Count} 项"
                + (sysCount > 0 ? $"，其中系统级 {sysCount} 项（修改/删除需管理员权限）" : "")
                + (_isAdmin ? "　·　当前为管理员模式" : "");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "读取菜单列表失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshEntries();

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => _entryView?.Refresh();

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (EntryList.Items.Count > 0 && EntryList.SelectedItems.Count == EntryList.Items.Count)
            EntryList.UnselectAll();
        else
            EntryList.SelectAll();
    }

    private void EntryList_DoubleClick(object sender, MouseButtonEventArgs e) => Edit_Click(sender, e);

    // ---------------------------------------------------------------- 增删改

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var item = new MenuItemModel
        {
            Category = _currentCategory,
            Extension = _currentExt,
            Source = RegistrySource.CurrentUser,
        };
        SaveViaDialog(item, isNew: true);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (EntryList.SelectedItem is not MenuItemModel item)
        {
            ShowToast("请先选择要编辑的菜单项");
            return;
        }
        if (EntryList.SelectedItems.Count > 1)
        {
            ShowToast("选中了多项，编辑请只保留一项");
            return;
        }
        if (item.IsCascade)
        {
            MessageBox.Show(this, "该项是系统级联子菜单（SubCommands），结构复杂，仅支持查看与删除。",
                "右键菜单管家", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (item.IsShellExtension)
        {
            MessageBox.Show(this,
                "该项是 Shell 扩展（由 COM 组件提供的右键功能，带「扩展」标记），不能像普通菜单那样编辑命令。\n\n" +
                "可以使用「启用/禁用」开关控制它，或直接删除。",
                "右键菜单管家", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 系统项且无管理员权限：给出两条出路
        if (item.Source == RegistrySource.LocalMachine && !_isAdmin)
        {
            var choice = MessageBox.Show(this,
                "该菜单项属于系统级（所有用户），直接修改需要管理员权限。\n\n" +
                "点击「是」以管理员身份重启应用后修改；\n" +
                "点击「否」将其复制为当前用户项再修改（原系统项保留）。",
                "需要管理员权限", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Yes)
            {
                PromptElevation();
                return;
            }
            if (choice != MessageBoxResult.No) return;

            try
            {
                var newKey = RegistryService.CopyToCurrentUser(item);
                ShowToast($"已复制为用户项：{newKey}");
                RefreshEntries();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "复制失败：" + ex.Message, "右键菜单管家",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return;
        }

        SaveViaDialog(item, isNew: false);
    }

    private void SaveViaDialog(MenuItemModel item, bool isNew)
    {
        var dlg = new EditEntryDialog(item, isNew) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var key = RegistryService.SaveEntry(item, isNew);
            ShowToast(isNew ? $"已添加菜单「{item.DisplayTitle}」" : "已保存修改");
            RefreshEntries();
            SelectEntryByKey(key);
        }
        catch (ElevationRequiredException)
        {
            PromptElevation();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var items = EntryList.SelectedItems.OfType<MenuItemModel>().ToList();
        if (items.Count == 0)
        {
            ShowToast("请先选择要删除的菜单项");
            return;
        }

        // 级联子菜单（SubCommands）结构复杂且多为系统内置，删除不可逆，一律跳过
        var cascades = items.Count(i => i.IsCascade);
        items = items.Where(i => !i.IsCascade).ToList();
        if (items.Count == 0)
        {
            MessageBox.Show(this,
                "选中的是系统级联子菜单（SubCommands），结构复杂且删除后难以恢复，本程序不支持删除。",
                "右键菜单管家", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var what = items.Count == 1 ? $"「{items[0].DisplayTitle}」" : $"选中的 {items.Count} 项";
        if (cascades > 0)
            what += $"（已跳过 {cascades} 个级联子菜单）";
        var confirm = MessageBox.Show(this,
            $"确定删除菜单{what}吗？\n此操作不可撤销。",
            "删除确认", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        int done = 0, failed = 0;
        var needAdmin = false;
        // 删掉用户项后，同名的系统项会「浮现」出来，菜单看上去没被删掉，需要解释
        var shadowed = new List<string>();
        foreach (var item in items)
        {
            try
            {
                RegistryService.DeleteEntry(item);
                done++;
                if (item.Source == RegistrySource.CurrentUser && RegistryService.ExistsInLocalMachine(item))
                    shadowed.Add(item.DisplayTitle);
            }
            catch (ElevationRequiredException) { failed++; needAdmin = true; }
            catch { failed++; }
        }
        ShowToast(failed == 0
            ? $"已删除 {done} 项"
            : $"已删除 {done} 项，{failed} 项失败（可能需管理员权限）");
        RefreshEntries();

        if (shadowed.Count > 0)
        {
            var names = string.Join("、", shadowed.Take(3).Select(t => $"「{t}」"))
                + (shadowed.Count > 3 ? $" 等 {shadowed.Count} 项" : "");
            MessageBox.Show(this,
                $"{names} 的用户配置已删除，但系统（所有用户）中还存在同名项，\n" +
                "因此右键菜单里仍会看到它。\n\n如需一并删除，请以管理员身份重启后再删除来源为「系统」的那一项。",
                "右键菜单管家", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        if (needAdmin) PromptElevation();
    }

    private void CopyToUser_Click(object sender, RoutedEventArgs e)
    {
        if (EntryList.SelectedItem is not MenuItemModel item) return;
        try
        {
            var key = RegistryService.CopyToCurrentUser(item);
            ShowToast($"已复制为当前用户项：{key}，现在可以修改或删除它");
            RefreshEntries();
            SelectEntryByKey(key, item.IsShellExtension);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "复制失败：" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ---------------------------------------------------------------- 启用/禁用

    private void ToggleDisabled_Click(object sender, RoutedEventArgs e)
    {
        // 状态灯按钮通过 Tag 携带数据；工具栏/右键菜单则取当前选中项（可能多选）
        if (sender is FrameworkElement { Tag: MenuItemModel m })
        {
            ToggleItem(m);
            return;
        }

        var items = EntryList.SelectedItems.OfType<MenuItemModel>().ToList();
        if (items.Count == 0)
        {
            ShowToast("请先选择要启用/禁用的菜单项");
            return;
        }
        if (items.Count == 1)
        {
            ToggleItem(items[0]);
            return;
        }

        int done = 0, skipped = 0;
        var needAdmin = false;
        foreach (var item in items)
        {
            if (item.IsCascade) { skipped++; continue; }
            try { RegistryService.SetDisabled(item, !item.IsDisabled); done++; }
            catch (ElevationRequiredException) { skipped++; needAdmin = true; }
            catch { skipped++; }
        }
        ShowToast($"已切换 {done} 项" + (skipped > 0 ? $"，跳过 {skipped} 项（级联菜单或需管理员权限）" : ""));
        RefreshEntries();
        // 与删除保持一致：确有项目因权限失败时给出提权入口
        if (needAdmin) PromptElevation();
    }

    private void ToggleItem(MenuItemModel item)
    {
        if (item.IsCascade)
        {
            ShowToast("级联子菜单暂不支持此操作");
            return;
        }

        try
        {
            RegistryService.SetDisabled(item, !item.IsDisabled);
            ShowToast(item.IsDisabled
                ? item.IsShellExtension
                    ? $"已禁用「{item.DisplayTitle}」（已从右键菜单中移除）"
                    : $"已禁用「{item.DisplayTitle}」（菜单中变灰失效）"
                : $"已启用「{item.DisplayTitle}」");
            var key = item.KeyName;
            RefreshEntries();
            SelectEntryByKey(key, item.IsShellExtension);
        }
        catch (ElevationRequiredException)
        {
            PromptElevation();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "操作失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool _suppressStatusSwitch;

    /// <summary>状态列开关：切换单项启用/禁用。初始绑定/容器复用与模型一致时忽略；级联项或失败时回弹。</summary>
    private void StatusSwitch_Change(object sender, RoutedEventArgs e)
    {
        if (_suppressStatusSwitch) return;
        if (sender is not CheckBox cb || cb.Tag is not MenuItemModel item) return;
        var enable = cb.IsChecked == true; // 开关 ON = 生效
        if (item.IsDisabled == !enable) return; // 与模型一致：初始绑定/容器复用，非用户操作

        if (item.IsCascade)
        {
            ShowToast("级联子菜单暂不支持此操作");
            RevertStatusSwitch(cb);
            return;
        }

        try
        {
            RegistryService.SetDisabled(item, !enable); // 成功后内部更新 item.IsDisabled
            ShowToast(enable
                ? $"已启用「{item.DisplayTitle}」"
                : item.IsShellExtension
                    ? $"已禁用「{item.DisplayTitle}」（已从右键菜单中移除）"
                    : $"已禁用「{item.DisplayTitle}」（菜单中变灰失效）");
            var key = item.KeyName;
            _suppressStatusSwitch = true;
            try
            {
                RefreshEntries();
                SelectEntryByKey(key, item.IsShellExtension);
            }
            finally { _suppressStatusSwitch = false; }
        }
        catch (ElevationRequiredException)
        {
            PromptElevation();
            RevertStatusSwitch(cb);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "操作失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            RevertStatusSwitch(cb);
        }
    }

    private void RevertStatusSwitch(CheckBox cb)
    {
        _suppressStatusSwitch = true;
        cb.IsChecked = cb.IsChecked != true;
        _suppressStatusSwitch = false;
    }

    // ---------------------------------------------------------------- 导出/导入

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        // 优先导出勾选（= 选中）的项；一项都没勾时导出当前分类的全部可导出项
        var selected = EntryList.SelectedItems.OfType<MenuItemModel>()
            .Where(i => !i.IsCascade && !i.IsShellExtension)
            .ToList();
        var exportAll = selected.Count == 0;
        var items = exportAll
            ? _entries.Where(i => !i.IsCascade && !i.IsShellExtension).ToList()
            : selected;

        if (items.Count == 0)
        {
            ShowToast("当前分类下没有可导出的菜单项");
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = exportAll ? "导出当前分类的全部菜单项" : $"导出选中的 {items.Count} 个菜单项",
            Filter = "右键菜单管家导出文件 (*.json)|*.json",
            DefaultExt = ".json",
            FileName = $"RightMenuMaster_{_currentCategory}_{DateTime.Now:yyyyMMdd_HHmmss}.json",
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var n = ExportImportService.Export(items, dlg.FileName);
            ShowToast(exportAll ? $"已导出当前分类全部 {n} 个菜单项" : $"已导出选中的 {n} 个菜单项");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导出失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "导入菜单项",
            Filter = "右键菜单管家导出文件 (*.json)|*.json",
            DefaultExt = ".json",
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            // 先解析给用户过目（导入的命令会进右键菜单并被执行），确认后才写注册表
            var candidates = ExportImportService.Parse(dlg.FileName);
            if (candidates.Count == 0)
            {
                ShowToast("该文件中没有可导入的菜单项");
                return;
            }

            var preview = new ImportPreviewDialog(candidates, Path.GetFileName(dlg.FileName)) { Owner = this };
            if (preview.ShowDialog() != true) return;

            var (imported, skipped) = ExportImportService.Apply(preview.Confirmed, preview.OverwriteExisting);
            ShowToast($"已导入 {imported} 个菜单项"
                + (skipped > 0 ? $"，跳过 {skipped} 个同名项" : ""));
            RefreshEntries();
        }
        catch (ElevationRequiredException)
        {
            PromptElevation();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导入失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>按键名重新选中某项。shell 与 shellex 下可能有同名键，需一并区分。</summary>
    private void SelectEntryByKey(string key, bool isShellExtension = false)
    {
        var item = _entries.FirstOrDefault(i =>
            i.IsShellExtension == isShellExtension
            && string.Equals(i.KeyName, key, StringComparison.OrdinalIgnoreCase));
        if (item != null)
        {
            EntryList.SelectedItem = item;
            EntryList.ScrollIntoView(item);
        }
    }

    // ---------------------------------------------------------------- 扩展名选择

    private void EnsureExtensionComboLoaded()
    {
        if (ExtCombo.Items.Count > 0) return;
        try
        {
            var exts = RegistryService.GetRegisteredExtensions();
            foreach (var ext in CommonExtensions.All.Select(c => c.Ext).Distinct())
                if (!exts.Contains(ext)) exts.Add(ext);
            exts.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var ext in exts) ExtCombo.Items.Add(ext);
        }
        catch { /* 忽略 */ }
    }

    private void ExtApply_Click(object sender, RoutedEventArgs e)
    {
        var ext = MenuCategoryInfo.NormalizeExt(ExtCombo.Text);
        if (string.IsNullOrEmpty(ext))
        {
            ShowToast("请输入扩展名，例如 .txt");
            return;
        }
        _currentExt = ext;
        ExtCombo.Text = ext;
        UpdateScopeHeader();
        RefreshEntries();
    }

    // ================================================================== 快捷模板

    private void TemplateCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: BuiltinTemplate tpl }) return;

        var item = new MenuItemModel
        {
            Category = tpl.Category,
            Extension = null,
            Title = tpl.Name,
            Command = Environment.ExpandEnvironmentVariables(tpl.Command),
            IconPath = tpl.IconPath == null ? null : Environment.ExpandEnvironmentVariables(tpl.IconPath),
            ShiftExtended = tpl.ShiftExtended,
            Source = RegistrySource.CurrentUser,
        };
        SaveViaDialog(item, isNew: true);
    }

    /// <summary>
    /// 模板卡片上的「运行看效果」：把 %V/%1 替换为真实路径后实际执行命令，供用户预览。
    /// </summary>
    private void TemplateRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: BuiltinTemplate tpl }) return;
        e.Handled = true;

        try
        {
            var command = Environment.ExpandEnvironmentVariables(tpl.Command);

            // %V = 当前目录、%1 = 选中对象。一律用临时目录里的示例文件演示，
            // 不拿用户桌面上的真实文件当参数（可能是隐私文件）
            var dir = EnsureSampleDir();
            var target = tpl.Category is MenuCategory.File or MenuCategory.Extension
                ? SampleTargetFile(dir)
                : dir;
            command = command.Replace("%V", dir).Replace("%1", target);

            SplitCommandLine(command, out var program, out var args);
            Process.Start(new ProcessStartInfo
            {
                FileName = program,
                Arguments = args,
                UseShellExecute = true,
            });
            ShowToast($"已运行模板「{tpl.Name}」，看看效果吧");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "运行失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>模板预览用的示例目录（临时目录下，内容可随意被模板命令操作）。</summary>
    private static string EnsureSampleDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RightMenuMaster_模板预览");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>为文件类模板准备一个示例文件，避免拿用户的真实文件做演示。</summary>
    private static string SampleTargetFile(string dir)
    {
        var file = Path.Combine(dir, "示例文件.txt");
        try
        {
            if (!File.Exists(file))
                File.WriteAllText(file, "这是「右键菜单管家」用于预览模板效果的示例文件，可以随意删除。");
        }
        catch { /* 忽略，交给命令自己报错 */ }
        return file;
    }

    /// <summary>把命令行拆成程序与参数（支持带引号的程序路径）。</summary>
    private static void SplitCommandLine(string command, out string program, out string args)
    {
        program = string.Empty;
        args = string.Empty;
        if (string.IsNullOrWhiteSpace(command)) return;

        var cmd = command.Trim();
        if (cmd.StartsWith('"'))
        {
            int end = cmd.IndexOf('"', 1);
            if (end > 0)
            {
                program = cmd[1..end];
                args = cmd[(end + 1)..].Trim();
                return;
            }
        }

        int space = cmd.IndexOf(' ');
        if (space > 0)
        {
            program = cmd[..space];
            args = cmd[(space + 1)..].Trim();
        }
        else
        {
            program = cmd;
        }
    }

    // ================================================================== 默认程序

    private void EnsureDefaultsLoaded()
    {
        if (_defaultsLoaded) return;
        _defaultsLoaded = true;

        var rows = CommonExtensions.All
            .Select(e => new ExtRow { Ext = e.Ext, Description = $"{e.Description}（{e.Group}）" })
            .ToList();
        foreach (var row in rows) row.DefaultApp = DefaultProgramService.GetDefaultAppName(row.Ext);

        ExtList.ItemsSource = rows;
        _extView = CollectionViewSource.GetDefaultView(rows);
        _extView.Filter = ExtFilter;

        // 列头点击排序（「操作」列是按钮，不参与排序）
        new GridSorter(ExtList, new Dictionary<string, string>
        {
            ["扩展名"] = nameof(ExtRow.Ext),
            ["说明"] = nameof(ExtRow.Description),
            ["当前默认程序"] = nameof(ExtRow.DefaultApp),
        }).Attach();
    }

    private bool ExtFilter(object obj)
    {
        if (obj is not ExtRow row) return false;
        var kw = ExtFilterBox?.Text?.Trim();
        if (string.IsNullOrEmpty(kw)) return true;
        return row.Ext.Contains(kw, StringComparison.OrdinalIgnoreCase)
            || row.Description.Contains(kw, StringComparison.OrdinalIgnoreCase)
            || row.DefaultApp.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }

    private void ExtFilter_Changed(object sender, TextChangedEventArgs e) => _extView?.Refresh();

    private void ChangeDefault_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string ext }) return;
        DefaultProgramService.ChangeDefaultViaOpenWith(ext);

        // 用户选择完成后刷新该行（系统对话框为模态，此处延迟多次刷新以覆盖大多数情况）
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        int ticks = 0;
        timer.Tick += (_, _) =>
        {
            if (ExtList.ItemsSource is IEnumerable<ExtRow> rows)
            {
                var row = rows.FirstOrDefault(r => r.Ext == ext);
                if (row != null) row.DefaultApp = DefaultProgramService.GetDefaultAppName(ext);
            }
            if (++ticks >= 5) timer.Stop();
        };
        timer.Start();
    }

    private void OpenSystemDefaultApps_Click(object sender, RoutedEventArgs e)
        => DefaultProgramService.OpenDefaultAppsSettings();

    /// <summary>默认程序页的行数据。</summary>
    public class ExtRow : ViewModels.ViewModelBase
    {
        public string Ext { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        private string _defaultApp = string.Empty;
        public string DefaultApp { get => _defaultApp; set => Set(ref _defaultApp, value); }
    }

    // ================================================================== 小工具箱

    private void InitToolboxState()
    {
        try
        {
            HiddenSwitch.IsChecked = SystemTools.GetShowHiddenFiles();
            ExtSwitch.IsChecked = SystemTools.GetShowExtensions();
        }
        catch { /* 忽略 */ }
    }

    private void OpenTopMost_Click(object sender, RoutedEventArgs e)
        => new TopMostWindow { Owner = this }.Show();

    private void OpenPasswordBox_Click(object sender, RoutedEventArgs e)
        => new PasswordNoteWindow().Show();

    private void RestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(this,
            "将关闭并重启 Windows 资源管理器（桌面与任务栏会短暂消失）。\n继续吗？",
            "重启资源管理器", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        try
        {
            SystemTools.RestartExplorer();
            ShowToast("资源管理器已重启");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "重启失败：" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void HiddenSwitch_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        try
        {
            SystemTools.SetShowHiddenFiles(HiddenSwitch.IsChecked == true);
            ShowToast(HiddenSwitch.IsChecked == true ? "已显示隐藏文件" : "已隐藏隐藏文件");
        }
        catch { /* 忽略 */ }
    }

    private void ExtSwitch_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        try
        {
            SystemTools.SetShowExtensions(ExtSwitch.IsChecked == true);
            ShowToast(ExtSwitch.IsChecked == true ? "已显示文件扩展名" : "已隐藏文件扩展名");
        }
        catch { /* 忽略 */ }
    }

    // ================================================================== 其他

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

    private void PromptElevation()
    {
        var choice = MessageBox.Show(this,
            "此操作需要管理员权限。\n\n是否以管理员身份重新启动本应用？",
            "需要管理员权限", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (choice != MessageBoxResult.Yes) return;

        if (ElevationService.RestartAsAdmin())
            Application.Current.Shutdown();
    }

    private void About_Click(object sender, RoutedEventArgs e)
        => new AboutDialog { Owner = this }.ShowDialog();

    private void ShowToast(string message)
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
}

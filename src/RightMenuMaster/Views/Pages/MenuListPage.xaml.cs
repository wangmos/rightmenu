using Microsoft.Win32;
using RightMenuMaster.Helpers;
using RightMenuMaster.Imaging;
using RightMenuMaster.Models;
using RightMenuMaster.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace RightMenuMaster.Views.Pages;

/// <summary>
/// 菜单管理页：按作用域列出右键菜单项，负责增删改、启用禁用与导入导出。
/// </summary>
public partial class MenuListPage : UserControl
{
    private MenuCategory _currentCategory = MenuCategory.Background;
    private string? _currentExt;
    private readonly ObservableCollection<MenuItemModel> _entries = new();
    private readonly ICollectionView _entryView;
    private DispatcherTimer? _searchDebounce;

    private IShellHost? Host => Window.GetWindow(this) as IShellHost;

    public MenuListPage()
    {
        InitializeComponent();

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

        UpdateScopeHeader();
    }

    // ================================================================== 对外接口

    /// <summary>切换到指定作用域并刷新列表（由导航调用）。</summary>
    public void ShowCategory(MenuCategory category)
    {
        _currentCategory = category;

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

    /// <summary>首次加载（宿主窗口显示后调用，避免构造期做 IO）。</summary>
    public Task InitialLoadAsync() => RefreshEntriesAsync();

    private void UpdateScopeHeader()
    {
        MenuPageTitle.Text = _currentCategory == MenuCategory.Extension
            ? $"指定扩展名（{MenuCategoryInfo.NormalizeExt(_currentExt)}）"
            : _currentCategory.DisplayName();
        MenuPageSubtitle.Text = _currentCategory.Description();
    }

    // ================================================================== 列表

    private bool EntryFilter(object obj)
    {
        if (obj is not MenuItemModel item) return false;
        var kw = SearchBox?.Text?.Trim();
        if (string.IsNullOrEmpty(kw)) return true;
        return item.DisplayTitle.Contains(kw, StringComparison.OrdinalIgnoreCase)
            || item.Command.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 重新读取当前分类的菜单项。注册表枚举与图标提取都在后台线程完成
    /// （图标是冻结的 BitmapSource，可跨线程使用），填充列表回到 UI 线程。
    /// </summary>
    private async Task RefreshEntriesAsync()
    {
        var category = _currentCategory;
        var ext = _currentExt;

        try
        {
            var items = await Task.Run(() =>
            {
                var list = RegistryService.GetEntries(category, ext);
                // 没设置图标的项就留空，不要塞一个 shell32 默认图标——
                // 那会让人以为已经设过图标，也和右键菜单里的真实观感不一致
                foreach (var item in list)
                    item.Icon = IconService.ResolveIconCached(item.IconPath);
                return list;
            });

            // 等待期间用户可能已经切换了分类，此时结果作废
            if (category != _currentCategory || ext != _currentExt) return;

            _entries.Clear();
            foreach (var item in items) _entries.Add(item);

            var sysCount = items.Count(i => i.Source == RegistrySource.LocalMachine);
            StatusText.Text = $"共 {items.Count} 项"
                + (sysCount > 0 ? $"，其中系统级 {sysCount} 项（修改/删除需管理员权限）" : "")
                + (Host?.IsAdmin == true ? "　·　当前为管理员模式" : "");
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this)!, "读取菜单列表失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>同步入口：供事件处理器「发起刷新但不等待」时使用。</summary>
    private void RefreshEntries() => _ = RefreshEntriesAsync();

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        // 手动刷新时丢掉图标缓存，用户可能刚在外部换过图标文件
        IconService.ClearIconCache();
        RefreshEntries();
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 防抖：逐字过滤会对整表反复跑一遍过滤器
        _searchDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _searchDebounce.Stop();
        _searchDebounce.Tick -= SearchDebounce_Tick;
        _searchDebounce.Tick += SearchDebounce_Tick;
        _searchDebounce.Start();
    }

    private void SearchDebounce_Tick(object? sender, EventArgs e)
    {
        _searchDebounce?.Stop();
        _entryView.Refresh();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (EntryList.Items.Count > 0 && EntryList.SelectedItems.Count == EntryList.Items.Count)
            EntryList.UnselectAll();
        else
            EntryList.SelectAll();
    }

    private void EntryList_DoubleClick(object sender, MouseButtonEventArgs e) => Edit_Click(sender, e);

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

    // ================================================================== 增删改

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
        var owner = Window.GetWindow(this)!;

        if (EntryList.SelectedItem is not MenuItemModel item)
        {
            Host?.ShowToast("请先选择要编辑的菜单项");
            return;
        }
        if (EntryList.SelectedItems.Count > 1)
        {
            Host?.ShowToast("选中了多项，编辑请只保留一项");
            return;
        }
        if (item.IsCascade)
        {
            MessageBox.Show(owner, "该项是系统级联子菜单（SubCommands），结构复杂，仅支持查看与删除。",
                "右键菜单管家", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (item.IsShellExtension)
        {
            MessageBox.Show(owner,
                "该项是 Shell 扩展（由 COM 组件提供的右键功能，带「扩展」标记），不能像普通菜单那样编辑命令。\n\n" +
                "可以使用「启用/禁用」开关控制它，或直接删除。",
                "右键菜单管家", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 系统项且无管理员权限：给出两条出路
        if (item.Source == RegistrySource.LocalMachine && Host?.IsAdmin != true)
        {
            var choice = MessageBox.Show(owner,
                "该菜单项属于系统级（所有用户），直接修改需要管理员权限。\n\n" +
                "点击「是」以管理员身份重启应用后修改；\n" +
                "点击「否」将其复制为当前用户项再修改（原系统项保留）。",
                "需要管理员权限", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Yes)
            {
                Host?.PromptElevation();
                return;
            }
            if (choice != MessageBoxResult.No) return;

            try
            {
                var newKey = RegistryService.CopyToCurrentUser(item);
                Host?.ShowToast($"已复制为用户项：{newKey}");
                RefreshEntries();
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "复制失败：" + ex.Message, "右键菜单管家",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return;
        }

        SaveViaDialog(item, isNew: false);
    }

    /// <summary>打开编辑对话框并保存。模板页生成菜单项时也复用这条路径。</summary>
    internal void SaveViaDialog(MenuItemModel item, bool isNew)
    {
        var owner = Window.GetWindow(this)!;
        var dlg = new EditEntryDialog(item, isNew) { Owner = owner };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var key = RegistryService.SaveEntry(item, isNew);
            Host?.ShowToast(isNew ? $"已添加菜单「{item.DisplayTitle}」" : "已保存修改");
            RefreshEntries();
            SelectEntryByKey(key);
        }
        catch (ElevationRequiredException)
        {
            Host?.PromptElevation();
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, "保存失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this)!;

        var items = EntryList.SelectedItems.OfType<MenuItemModel>().ToList();
        if (items.Count == 0)
        {
            Host?.ShowToast("请先选择要删除的菜单项");
            return;
        }

        // 级联子菜单（SubCommands）结构复杂且多为系统内置，删除不可逆，一律跳过
        var cascades = items.Count(i => i.IsCascade);
        items = items.Where(i => !i.IsCascade).ToList();
        if (items.Count == 0)
        {
            MessageBox.Show(owner,
                "选中的是系统级联子菜单（SubCommands），结构复杂且删除后难以恢复，本程序不支持删除。",
                "右键菜单管家", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var what = items.Count == 1 ? $"「{items[0].DisplayTitle}」" : $"选中的 {items.Count} 项";
        if (cascades > 0)
            what += $"（已跳过 {cascades} 个级联子菜单）";

        var confirm = MessageBox.Show(owner,
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
        Host?.ShowToast(failed == 0
            ? $"已删除 {done} 项"
            : $"已删除 {done} 项，{failed} 项失败（可能需管理员权限）");
        RefreshEntries();

        if (shadowed.Count > 0)
        {
            var names = string.Join("、", shadowed.Take(3).Select(t => $"「{t}」"))
                + (shadowed.Count > 3 ? $" 等 {shadowed.Count} 项" : "");
            MessageBox.Show(owner,
                $"{names} 的用户配置已删除，但系统（所有用户）中还存在同名项，\n" +
                "因此右键菜单里仍会看到它。\n\n如需一并删除，请以管理员身份重启后再删除来源为「系统」的那一项。",
                "右键菜单管家", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        if (needAdmin) Host?.PromptElevation();
    }

    private void CopyToUser_Click(object sender, RoutedEventArgs e)
    {
        if (EntryList.SelectedItem is not MenuItemModel item) return;
        try
        {
            var key = RegistryService.CopyToCurrentUser(item);
            Host?.ShowToast($"已复制为当前用户项：{key}，现在可以修改或删除它");
            RefreshEntries();
            SelectEntryByKey(key, item.IsShellExtension);
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this)!, "复制失败：" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ================================================================== 启用/禁用

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
            Host?.ShowToast("请先选择要启用/禁用的菜单项");
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
        Host?.ShowToast($"已切换 {done} 项" + (skipped > 0 ? $"，跳过 {skipped} 项（级联菜单或需管理员权限）" : ""));
        RefreshEntries();
        // 与删除保持一致：确有项目因权限失败时给出提权入口
        if (needAdmin) Host?.PromptElevation();
    }

    private void ToggleItem(MenuItemModel item)
    {
        if (item.IsCascade)
        {
            Host?.ShowToast("级联子菜单暂不支持此操作");
            return;
        }

        try
        {
            // SetDisabled 成功后会更新 item.IsDisabled，这一行靠绑定自动刷新，无需重建列表
            RegistryService.SetDisabled(item, !item.IsDisabled);
            Host?.ShowToast(item.IsDisabled
                ? item.IsShellExtension
                    ? $"已禁用「{item.DisplayTitle}」（已从右键菜单中移除）"
                    : $"已禁用「{item.DisplayTitle}」（菜单中变灰失效）"
                : $"已启用「{item.DisplayTitle}」");
        }
        catch (ElevationRequiredException)
        {
            Host?.PromptElevation();
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this)!, "操作失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// 状态列开关：切换单项启用/禁用。
    ///
    /// IsChecked 是 OneWay 绑定到 IsDisabled 的，模型变了这一行会自动更新，
    /// 因此这里不需要（也不应该）重建整个列表。判断是否为真实用户操作只看
    /// 「UI 状态与模型是否已经一致」：一致说明是容器复用/初始绑定触发的。
    /// </summary>
    private void StatusSwitch_Change(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not MenuItemModel item) return;
        var enable = cb.IsChecked == true; // 开关 ON = 生效
        if (item.IsDisabled == !enable) return; // 与模型一致：初始绑定/容器复用，非用户操作

        if (item.IsCascade)
        {
            Host?.ShowToast("级联子菜单暂不支持此操作");
            RevertStatusSwitch(cb);
            return;
        }

        try
        {
            RegistryService.SetDisabled(item, !enable); // 成功后内部更新 item.IsDisabled，绑定随之刷新
            Host?.ShowToast(enable
                ? $"已启用「{item.DisplayTitle}」"
                : item.IsShellExtension
                    ? $"已禁用「{item.DisplayTitle}」（已从右键菜单中移除）"
                    : $"已禁用「{item.DisplayTitle}」（菜单中变灰失效）");
        }
        catch (ElevationRequiredException)
        {
            RevertStatusSwitch(cb);
            Host?.PromptElevation();
        }
        catch (Exception ex)
        {
            RevertStatusSwitch(cb);
            MessageBox.Show(Window.GetWindow(this)!, "操作失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>操作失败时让开关回到模型的真实状态（重新拉一次绑定即可）。</summary>
    private static void RevertStatusSwitch(CheckBox cb)
        => cb.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)
             ?.UpdateTarget();

    // ================================================================== 导出/导入

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this)!;

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
            Host?.ShowToast("当前分类下没有可导出的菜单项");
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = exportAll ? "导出当前分类的全部菜单项" : $"导出选中的 {items.Count} 个菜单项",
            Filter = "右键菜单管家导出文件 (*.json)|*.json",
            DefaultExt = ".json",
            FileName = $"RightMenuMaster_{_currentCategory}_{DateTime.Now:yyyyMMdd_HHmmss}.json",
        };
        if (dlg.ShowDialog(owner) != true) return;

        try
        {
            var n = ExportImportService.Export(items, dlg.FileName);
            Host?.ShowToast(exportAll ? $"已导出当前分类全部 {n} 个菜单项" : $"已导出选中的 {n} 个菜单项");
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, "导出失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this)!;

        var dlg = new OpenFileDialog
        {
            Title = "导入菜单项",
            Filter = "右键菜单管家导出文件 (*.json)|*.json",
            DefaultExt = ".json",
        };
        if (dlg.ShowDialog(owner) != true) return;

        try
        {
            // 先解析给用户过目（导入的命令会进右键菜单并被执行），确认后才写注册表
            var candidates = ExportImportService.Parse(dlg.FileName);
            if (candidates.Count == 0)
            {
                Host?.ShowToast("该文件中没有可导入的菜单项");
                return;
            }

            var preview = new ImportPreviewDialog(candidates, Path.GetFileName(dlg.FileName)) { Owner = owner };
            if (preview.ShowDialog() != true) return;

            var (imported, skipped) = ExportImportService.Apply(preview.Confirmed, preview.OverwriteExisting);
            Host?.ShowToast($"已导入 {imported} 个菜单项"
                + (skipped > 0 ? $"，跳过 {skipped} 个同名项" : ""));
            RefreshEntries();
        }
        catch (ElevationRequiredException)
        {
            Host?.PromptElevation();
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, "导入失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ================================================================== 扩展名选择

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
            Host?.ShowToast("请输入扩展名，例如 .txt");
            return;
        }
        _currentExt = ext;
        ExtCombo.Text = ext;
        UpdateScopeHeader();
        RefreshEntries();
    }
}

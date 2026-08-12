using RightMenuMaster.Helpers;
using RightMenuMaster.Models;
using RightMenuMaster.Services;
using RightMenuMaster.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace RightMenuMaster.Views.Pages;

/// <summary>
/// 默认程序页：列出常见扩展名当前的默认打开程序，并调起系统「打开方式」对话框修改。
/// </summary>
public partial class DefaultProgramsPage : UserControl
{
    /// <summary>默认程序页的行数据。</summary>
    public class ExtRow : ViewModelBase
    {
        public string Ext { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        private string _defaultApp = string.Empty;
        public string DefaultApp { get => _defaultApp; set => Set(ref _defaultApp, value); }
    }

    private ICollectionView? _extView;
    private bool _loaded;
    private DispatcherTimer? _refreshTimer;

    public DefaultProgramsPage()
    {
        InitializeComponent();
    }

    /// <summary>切到本页时才真正读取默认程序（要查几十次注册表）。</summary>
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

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

        // 用户选择完成后刷新该行（系统对话框为模态，此处延迟多次刷新以覆盖大多数情况）。
        // 复用同一个计时器，避免连点「更改…」时堆出多个各跑各的计时器。
        _refreshTimer?.Stop();
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        int ticks = 0;
        _refreshTimer.Tick += (_, _) =>
        {
            if (ExtList.ItemsSource is IEnumerable<ExtRow> rows)
            {
                var row = rows.FirstOrDefault(r => r.Ext == ext);
                if (row != null) row.DefaultApp = DefaultProgramService.GetDefaultAppName(ext);
            }
            if (++ticks >= 5) _refreshTimer?.Stop();
        };
        _refreshTimer.Start();
    }

    private void OpenSystemDefaultApps_Click(object sender, RoutedEventArgs e)
        => DefaultProgramService.OpenDefaultAppsSettings();
}

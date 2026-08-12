using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;

namespace RightMenuMaster.Helpers;

/// <summary>
/// 为 ListView + GridView 提供「点击列头排序」能力（含 ▲/▼ 指示）。
/// 构造时传入「列标题 → 排序属性名」映射，不在映射中的列标题不参与排序
/// （如纯操作按钮列）。整体替换 ItemsSource 后调用 <see cref="Reapply"/> 可保持排序。
/// </summary>
public sealed class GridSorter
{
    private readonly ListView _list;
    private readonly IReadOnlyDictionary<string, string> _propertyMap;
    private string? _sortedTitle;
    private string? _sortedProperty;
    private bool _ascending = true;
    private GridViewColumnHeader? _sortedHeader;

    public GridSorter(ListView list, IReadOnlyDictionary<string, string> propertyMap)
    {
        _list = list;
        _propertyMap = propertyMap;
    }

    /// <summary>开始监听列头点击（每个列表只需调用一次）。</summary>
    public void Attach() =>
        _list.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(OnHeaderClick));

    /// <summary>按当前排序状态重新排序（用于列表 ItemsSource 被整体替换之后）。</summary>
    public void Reapply()
    {
        if (_sortedProperty == null || _list.ItemsSource == null) return;
        var view = CollectionViewSource.GetDefaultView(_list.ItemsSource);
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(_sortedProperty,
            _ascending ? ListSortDirection.Ascending : ListSortDirection.Descending));
    }

    private void OnHeaderClick(object sender, RoutedEventArgs e)
    {
        // 行内按钮（如状态灯、切换）也会冒泡 Click 事件，只处理真正的列头
        if (e.OriginalSource is not GridViewColumnHeader header
            || header.Role == GridViewColumnHeaderRole.Padding)
            return;

        var title = header.Column?.Header as string ?? string.Empty;
        if (!_propertyMap.TryGetValue(title, out var property) || _list.ItemsSource == null) return;

        // 同一列再点一次切换升/降序；换列则默认升序
        _ascending = string.Equals(_sortedTitle, title) ? !_ascending : true;
        _sortedTitle = title;
        _sortedProperty = property;

        var view = CollectionViewSource.GetDefaultView(_list.ItemsSource);
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(property,
            _ascending ? ListSortDirection.Ascending : ListSortDirection.Descending));
        view.Refresh();

        // 排序箭头指示
        if (_sortedHeader != null && !ReferenceEquals(_sortedHeader, header))
        {
            _sortedHeader.Content = _sortedHeader.Column?.Header as string ?? string.Empty;
            _sortedHeader.ClearValue(AutomationProperties.NameProperty); // 名称恢复为纯文本内容
        }
        _sortedHeader = header;
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(new TextBlock
        {
            Text = _ascending ? " ▲" : " ▼",
            FontSize = 9,
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Content = panel;
        // 复合内容不会自动生成可访问名称，显式设置（同时方便 UIA 测试断言）
        AutomationProperties.SetName(header, title + (_ascending ? " ▲" : " ▼"));
    }
}

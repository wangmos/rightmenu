using RightMenuMaster.ViewModels;
using System.Windows.Media.Imaging;

namespace RightMenuMaster.Models;

/// <summary>
/// 一个右键菜单项的完整模型，与注册表结构对应。
///
/// 会随操作变化的属性（状态、标题、命令、图标）都带变更通知，
/// 这样切换单项的启用状态只需更新这一行，不必重建整个列表。
/// </summary>
public class MenuItemModel : ViewModelBase
{
    /// <summary>注册表子键名（唯一标识，如 "MyTool"）。</summary>
    public string KeyName { get; set; } = string.Empty;

    public MenuCategory Category { get; set; }

    /// <summary>扩展名分类下的扩展名（含点，如 .txt）。</summary>
    public string? Extension { get; set; }

    private string _title = string.Empty;

    /// <summary>显示的标题（默认值）。为空时资源管理器显示键名。</summary>
    public string Title
    {
        get => _title;
        set { if (Set(ref _title, value)) OnPropertyChanged(nameof(DisplayTitle)); }
    }

    private string _command = string.Empty;

    /// <summary>要执行的命令行（command 子键的默认值），可含 %1 %V 等占位符。</summary>
    public string Command
    {
        get => _command;
        set => Set(ref _command, value);
    }

    private string? _iconPath;

    /// <summary>图标位置，格式：文件路径 或 "文件路径,索引"。</summary>
    public string? IconPath
    {
        get => _iconPath;
        set => Set(ref _iconPath, value);
    }

    /// <summary>菜单项在列表中的位置（Top/Bottom/空）。</summary>
    public string? Position { get; set; }

    private bool _shiftExtended;

    /// <summary>是否需要按住 Shift 右键才显示。</summary>
    public bool ShiftExtended
    {
        get => _shiftExtended;
        set => Set(ref _shiftExtended, value);
    }

    private bool _isDisabled;

    /// <summary>是否已禁用（注册表 LegacyDisable 值；禁用后菜单中呈灰色不可用）。</summary>
    public bool IsDisabled
    {
        get => _isDisabled;
        set => Set(ref _isDisabled, value);
    }

    /// <summary>是否禁止设置工作目录（NoWorkingDirectory）。</summary>
    public bool NoWorkingDirectory { get; set; }

    /// <summary>该项来自哪个注册表根键（用于判断是否需要管理员权限）。</summary>
    public RegistrySource Source { get; set; } = RegistrySource.CurrentUser;

    /// <summary>是否为级联子菜单（含 SubCommands），此类项只读展示。</summary>
    public bool IsCascade { get; set; }

    /// <summary>
    /// 是否为 Shell 扩展处理程序（shellex\ContextMenuHandlers 下的 COM 组件）。
    /// 此类项没有命令行，Command 中存放 CLSID；只能启用/禁用或删除，不能编辑命令。
    /// </summary>
    public bool IsShellExtension { get; set; }

    /// <summary>是否为系统内置的不可写项。</summary>
    public bool IsReadOnly => IsCascade;

    private BitmapSource? _icon;

    /// <summary>用于列表显示的图标（已解析）。</summary>
    public BitmapSource? Icon
    {
        get => _icon;
        set => Set(ref _icon, value);
    }

    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? KeyName : Title;

    public MenuItemModel Clone()
    {
        var copy = (MenuItemModel)MemberwiseClone();
        copy.ClearPropertyChangedSubscribers();
        return copy;
    }
}

/// <summary>
/// 菜单项所在的注册表根键来源。
/// </summary>
public enum RegistrySource
{
    /// <summary>HKEY_CURRENT_USER\Software\Classes —— 当前用户，无需管理员。</summary>
    CurrentUser,

    /// <summary>HKEY_LOCAL_MACHINE\SOFTWARE\Classes —— 所有用户，需要管理员。</summary>
    LocalMachine,
}

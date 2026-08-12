namespace RightMenuMaster.Models;

/// <summary>
/// 右键菜单的作用域分类。对应注册表中不同的 shell 挂载点。
/// </summary>
public enum MenuCategory
{
    /// <summary>目录 —— 右键点击某个文件夹时。HKCR\Directory\shell</summary>
    Directory,

    /// <summary>背景 —— 在文件夹空白处右键时。HKCR\Directory\Background\shell</summary>
    Background,

    /// <summary>文件夹 —— 针对所有文件夹对象。HKCR\Folder\shell</summary>
    Folder,

    /// <summary>文件 —— 针对所有文件。HKCR\*\shell</summary>
    File,

    /// <summary>指定扩展名 —— HKCR\SystemFileAssociations\.ext\shell</summary>
    Extension,
}

/// <summary>
/// 分类的展示信息与注册表路径映射。
/// </summary>
public static class MenuCategoryInfo
{
    public static string DisplayName(this MenuCategory c) => c switch
    {
        MenuCategory.Directory => "目录",
        MenuCategory.Background => "目录背景",
        MenuCategory.Folder => "文件夹",
        MenuCategory.File => "文件",
        MenuCategory.Extension => "指定扩展名",
        _ => c.ToString(),
    };

    public static string Description(this MenuCategory c) => c switch
    {
        MenuCategory.Directory => "右键点击某个文件夹时出现的菜单",
        MenuCategory.Background => "在文件夹空白处右键时出现的菜单",
        MenuCategory.Folder => "针对所有文件夹对象的菜单",
        MenuCategory.File => "针对所有文件的菜单",
        MenuCategory.Extension => "仅针对特定扩展名文件的菜单",
        _ => string.Empty,
    };

    /// <summary>
    /// 返回该分类下 shell 子键的相对路径（相对于 Software\Classes 或根类）。
    /// 对于扩展名分类，需要传入具体扩展名。
    /// </summary>
    public static string ShellPath(this MenuCategory c, string? extension = null) => c switch
    {
        MenuCategory.Directory => @"Directory\shell",
        MenuCategory.Background => @"Directory\Background\shell",
        MenuCategory.Folder => @"Folder\shell",
        MenuCategory.File => @"*\shell",
        MenuCategory.Extension => $@"SystemFileAssociations\{NormalizeExt(extension)}\shell",
        _ => string.Empty,
    };

    /// <summary>
    /// 返回该分类下 Shell 扩展处理程序（shellex\ContextMenuHandlers）的相对路径。
    /// 大量第三方右键菜单（如解压缩软件、网盘、Git 等）以 COM 组件形式注册在这里。
    /// </summary>
    public static string ShellexPath(this MenuCategory c, string? extension = null) => c switch
    {
        MenuCategory.Directory => @"Directory\shellex\ContextMenuHandlers",
        MenuCategory.Background => @"Directory\Background\shellex\ContextMenuHandlers",
        MenuCategory.Folder => @"Folder\shellex\ContextMenuHandlers",
        MenuCategory.File => @"*\shellex\ContextMenuHandlers",
        MenuCategory.Extension => $@"SystemFileAssociations\{NormalizeExt(extension)}\shellex\ContextMenuHandlers",
        _ => string.Empty,
    };

    public static string NormalizeExt(string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return string.Empty;
        ext = ext.Trim();
        return ext.StartsWith('.') ? ext.ToLowerInvariant() : "." + ext.ToLowerInvariant();
    }
}

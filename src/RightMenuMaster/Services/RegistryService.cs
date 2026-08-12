using Microsoft.Win32;
using RightMenuMaster.Helpers;
using RightMenuMaster.Models;
using System.IO;
using System.Security;
using System.Text;

namespace RightMenuMaster.Services;

/// <summary>
/// 需要管理员权限时抛出的异常。
/// </summary>
public class ElevationRequiredException : Exception
{
    public ElevationRequiredException(string message) : base(message) { }
    public ElevationRequiredException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// 右键菜单注册表读写核心服务。
///
/// 读取时同时扫描 HKCU\Software\Classes 与 HKLM\SOFTWARE\Classes（两者合并即为 HKCR 的视图），
/// 同名项以 HKCU 优先。写入默认放在 HKCU（无需管理员权限）；
/// 修改/删除 HKLM 中的项需要管理员权限，此时抛出 <see cref="ElevationRequiredException"/>。
/// </summary>
public static class RegistryService
{
    private const string ClassesHKCU = @"Software\Classes";
    private const string ClassesHKLM = @"SOFTWARE\Classes";

    /// <summary>禁用 Shell 扩展时备份原 CLSID 所用的值名。</summary>
    private const string HandlerBackupValue = "RightMenuMaster_OriginalClsid";

    /// <summary>空 CLSID：资源管理器无法实例化它，等效于禁用该处理程序。</summary>
    private const string NullClsid = "{00000000-0000-0000-0000-000000000000}";

    // ---------------------------------------------------------------- 枚举

    /// <summary>
    /// 列出指定分类下的所有右键菜单项。
    /// 同时扫描两类注册位置：
    /// 1. shell\* —— 普通命令式菜单项（含命令行）；
    /// 2. shellex\ContextMenuHandlers —— Shell 扩展（COM 处理程序，大量第三方菜单注册于此）。
    /// </summary>
    public static List<MenuItemModel> GetEntries(MenuCategory category, string? extension = null)
    {
        // key 必须区分 shell 与 shellex：两处可能存在同名子键（如 shell\7-Zip 与
        // shellex\ContextMenuHandlers\7-Zip），只用键名会互相覆盖导致列表丢项
        var result = new Dictionary<(bool IsShellex, string Name), MenuItemModel>(
            new EntryKeyComparer());
        var shellRelative = category.ShellPath(extension);
        var shellexRelative = category.ShellexPath(extension);

        // HKLM 先读，HKCU 后读并覆盖同名项（与资源管理器合并规则一致）
        foreach (var (source, classesRoot) in new[]
        {
            (RegistrySource.LocalMachine, ClassesHKLM),
            (RegistrySource.CurrentUser, ClassesHKCU),
        })
        {
            var hive = source == RegistrySource.CurrentUser ? Registry.CurrentUser : Registry.LocalMachine;

            using (var shellKey = hive.OpenSubKey($@"{classesRoot}\{shellRelative}"))
            {
                if (shellKey != null)
                {
                    foreach (var name in shellKey.GetSubKeyNames())
                    {
                        using var sub = shellKey.OpenSubKey(name);
                        if (sub == null) continue;
                        result[(false, name)] = ReadEntry(sub, name, category, extension, source);
                    }
                }
            }

            using (var shellexKey = hive.OpenSubKey($@"{classesRoot}\{shellexRelative}"))
            {
                if (shellexKey != null)
                {
                    foreach (var name in shellexKey.GetSubKeyNames())
                    {
                        using var sub = shellexKey.OpenSubKey(name);
                        if (sub == null) continue;
                        result[(true, name)] = ReadHandlerEntry(sub, name, category, extension, source);
                    }
                }
            }
        }

        return result.Values
            .OrderBy(i => i.Position == "Top" ? 0 : 1)
            .ThenBy(i => i.DisplayTitle, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>菜单项去重用的 key 比较器：键名不区分大小写，shell / shellex 分开计。</summary>
    private sealed class EntryKeyComparer : IEqualityComparer<(bool IsShellex, string Name)>
    {
        public bool Equals((bool IsShellex, string Name) x, (bool IsShellex, string Name) y) =>
            x.IsShellex == y.IsShellex
            && StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name);

        public int GetHashCode((bool IsShellex, string Name) obj) =>
            HashCode.Combine(obj.IsShellex, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
    }

    private static MenuItemModel ReadEntry(RegistryKey key, string name, MenuCategory category,
        string? extension, RegistrySource source)
    {
        // 标题：优先默认值，其次 MUIVerb；两者都可能是 @dll,-id 形式的资源引用，需解析成本地化文字
        var title = (key.GetValue(null) as string)?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = (key.GetValue("MUIVerb") as string)?.Trim();

        var item = new MenuItemModel
        {
            KeyName = name,
            Category = category,
            Extension = extension,
            Source = source,
            Title = ResolveIndirectString(title ?? string.Empty),
            IconPath = key.GetValue("Icon") as string,
            Position = key.GetValue("Position") as string,
            ShiftExtended = key.GetValue("Extended") != null,
            IsDisabled = key.GetValue("LegacyDisable") != null,
            NoWorkingDirectory = key.GetValue("NoWorkingDirectory") != null,
            IsCascade = key.GetValue("SubCommands") != null,
        };

        if (!item.IsCascade)
        {
            using var cmdKey = key.OpenSubKey("command");
            item.Command = (cmdKey?.GetValue(null) as string)?.Trim() ?? string.Empty;
        }

        return item;
    }

    /// <summary>读取 shellex\ContextMenuHandlers 下的一个 Shell 扩展项。</summary>
    private static MenuItemModel ReadHandlerEntry(RegistryKey key, string name, MenuCategory category,
        string? extension, RegistrySource source)
    {
        var defaultValue = (key.GetValue(null) as string)?.Trim() ?? string.Empty;
        var backup = key.GetValue(HandlerBackupValue) as string;

        // CLSID 优先取默认值；没有默认值时资源管理器以键名本身作为 CLSID
        var clsid = string.IsNullOrWhiteSpace(defaultValue) ? name : defaultValue;
        bool disabled = backup != null
            && string.Equals(clsid, NullClsid, StringComparison.OrdinalIgnoreCase);

        // 被本程序禁用后，用备份的原始 CLSID 做名称/图标解析
        var lookupClsid = disabled && !string.IsNullOrWhiteSpace(backup) ? backup.Trim() : clsid;

        var item = new MenuItemModel
        {
            KeyName = name,
            Category = category,
            Extension = extension,
            Source = source,
            IsShellExtension = true,
            IsDisabled = disabled,
            Command = lookupClsid,
            Title = IsClsidLike(name) ? (GetClsidDescription(lookupClsid) ?? name) : name,
            IconPath = GetClsidModuleIcon(lookupClsid),
        };
        return item;
    }

    // ---------------------------------------------------------------- 保存

    /// <summary>
    /// 保存（新增或修改）一个菜单项。返回最终使用的键名。
    /// </summary>
    public static string SaveEntry(MenuItemModel item, bool isNew)
    {
        var hive = item.Source == RegistrySource.LocalMachine ? Registry.LocalMachine : Registry.CurrentUser;
        var classesRoot = item.Source == RegistrySource.LocalMachine ? ClassesHKLM : ClassesHKCU;
        var shellRelative = item.Category.ShellPath(item.Extension);

        var keyName = isNew ? MakeKeyName(item.Title) : item.KeyName;
        if (isNew) keyName = GetUniqueKeyName(hive, classesRoot, shellRelative, keyName);

        var path = $@"{classesRoot}\{shellRelative}\{keyName}";

        try
        {
            using var key = hive.CreateSubKey(path) ?? throw new IOException("无法创建注册表项: " + path);

            key.SetValue(null, item.Title);

            if (!string.IsNullOrWhiteSpace(item.IconPath))
                key.SetValue("Icon", item.IconPath);
            else
                SafeDeleteValue(key, "Icon");

            if (!string.IsNullOrWhiteSpace(item.Position))
                key.SetValue("Position", item.Position);
            else
                SafeDeleteValue(key, "Position");

            if (item.ShiftExtended) key.SetValue("Extended", "");
            else SafeDeleteValue(key, "Extended");

            if (item.IsDisabled) key.SetValue("LegacyDisable", "");
            else SafeDeleteValue(key, "LegacyDisable");

            if (item.NoWorkingDirectory) key.SetValue("NoWorkingDirectory", "");
            else SafeDeleteValue(key, "NoWorkingDirectory");

            using var cmdKey = key.CreateSubKey("command") ?? throw new IOException("无法创建 command 子项");
            cmdKey.SetValue(null, item.Command);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ElevationRequiredException("写入该注册表位置需要管理员权限。", ex);
        }
        catch (SecurityException ex)
        {
            throw new ElevationRequiredException("写入该注册表位置需要管理员权限。", ex);
        }

        NotifyShell();
        return keyName;
    }

    // ---------------------------------------------------------------- 启用/禁用

    /// <summary>
    /// 设置菜单项启用状态。
    /// 普通菜单项通过写入/移除 LegacyDisable 值实现（菜单中该项变灰失效）；
    /// Shell 扩展不支持 LegacyDisable，采用「备份原 CLSID + 替换为空 CLSID」的方式禁用，
    /// 启用时还原。两种方式都不破坏原有配置。
    /// </summary>
    public static void SetDisabled(MenuItemModel item, bool disabled)
    {
        var hive = item.Source == RegistrySource.LocalMachine ? Registry.LocalMachine : Registry.CurrentUser;
        var classesRoot = item.Source == RegistrySource.LocalMachine ? ClassesHKLM : ClassesHKCU;
        var relative = item.IsShellExtension
            ? item.Category.ShellexPath(item.Extension)
            : item.Category.ShellPath(item.Extension);
        var path = $@"{classesRoot}\{relative}\{item.KeyName}";

        try
        {
            using var key = hive.OpenSubKey(path, writable: true)
                ?? throw new IOException("未找到注册表项: " + path);
            if (item.IsShellExtension)
                SetHandlerDisabled(key, disabled);
            else if (disabled) key.SetValue("LegacyDisable", "");
            else SafeDeleteValue(key, "LegacyDisable");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ElevationRequiredException("修改该注册表项需要管理员权限。", ex);
        }
        catch (SecurityException ex)
        {
            throw new ElevationRequiredException("修改该注册表项需要管理员权限。", ex);
        }

        item.IsDisabled = disabled;
        NotifyShell();
    }

    /// <summary>
    /// Shell 扩展的禁用/启用：把处理程序的 CLSID 换成空 CLSID 使资源管理器无法实例化它（等效禁用），
    /// 原 CLSID 备份在 RightMenuMaster_OriginalClsid 值中，启用时还原。
    /// </summary>
    private static void SetHandlerDisabled(RegistryKey key, bool disabled)
    {
        if (disabled)
        {
            var original = (key.GetValue(null) as string)?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(original))
            {
                // 没有默认值时 CLSID 即键名本身
                var keyName = key.Name.Split('\\').Last();
                original = IsClsidLike(keyName) ? keyName : string.Empty;
            }
            if (string.Equals(original, NullClsid, StringComparison.OrdinalIgnoreCase)) return;

            key.SetValue(HandlerBackupValue, original);
            key.SetValue(null, NullClsid);
        }
        else
        {
            var backup = (key.GetValue(HandlerBackupValue) as string)?.Trim();
            if (!string.IsNullOrEmpty(backup))
                key.SetValue(null, backup);
            else
                key.DeleteValue(string.Empty, throwOnMissingValue: false);
            key.DeleteValue(HandlerBackupValue, throwOnMissingValue: false);
        }
    }

    // ---------------------------------------------------------------- 删除

    public static void DeleteEntry(MenuItemModel item)
    {
        var hive = item.Source == RegistrySource.LocalMachine ? Registry.LocalMachine : Registry.CurrentUser;
        var classesRoot = item.Source == RegistrySource.LocalMachine ? ClassesHKLM : ClassesHKCU;
        var relative = item.IsShellExtension
            ? item.Category.ShellexPath(item.Extension)
            : item.Category.ShellPath(item.Extension);
        var path = $@"{classesRoot}\{relative}\{item.KeyName}";

        try
        {
            hive.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ElevationRequiredException("删除该注册表项需要管理员权限。", ex);
        }
        catch (SecurityException ex)
        {
            throw new ElevationRequiredException("删除该注册表项需要管理员权限。", ex);
        }

        NotifyShell();
    }

    /// <summary>
    /// 把系统（HKLM）中的项复制为当前用户（HKCU）项，便于无管理员权限时修改。
    /// 对 Shell 扩展：在用户位置创建同名处理程序项（HKCU 优先于 HKLM），
    /// 之后即可通过禁用用户副本来屏蔽系统级扩展。
    /// </summary>
    public static string CopyToCurrentUser(MenuItemModel item)
    {
        if (item.IsShellExtension)
            return CopyHandlerToCurrentUser(item);

        var copy = item.Clone();
        copy.Source = RegistrySource.CurrentUser;
        return SaveEntry(copy, isNew: true);
    }

    private static string CopyHandlerToCurrentUser(MenuItemModel item)
    {
        var path = $@"{ClassesHKCU}\{item.Category.ShellexPath(item.Extension)}\{item.KeyName}";
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(path)
                ?? throw new IOException("无法创建注册表项: " + path);
            key.SetValue(null, item.Command); // Command 中存放的是 CLSID
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ElevationRequiredException("写入该注册表位置需要管理员权限。", ex);
        }
        catch (SecurityException ex)
        {
            throw new ElevationRequiredException("写入该注册表位置需要管理员权限。", ex);
        }

        NotifyShell();
        return item.KeyName;
    }

    // ---------------------------------------------------------------- 查询

    /// <summary>把菜单标题转成注册表子键名（导入预览需要提前知道会落到哪个键）。</summary>
    public static string KeyNameFor(string title) => MakeKeyName(title);

    /// <summary>当前用户下该分类是否已存在同名菜单项（用于导入时的重名判断）。</summary>
    public static bool EntryExists(MenuCategory category, string? extension, string keyName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                $@"{ClassesHKCU}\{category.ShellPath(extension)}\{keyName}");
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 系统（HKLM）下是否还存在同名项。删除用户项后菜单仍然出现时，用它给出解释。
    /// </summary>
    public static bool ExistsInLocalMachine(MenuItemModel item)
    {
        var relative = item.IsShellExtension
            ? item.Category.ShellexPath(item.Extension)
            : item.Category.ShellPath(item.Extension);
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"{ClassesHKLM}\{relative}\{item.KeyName}");
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------------------------------------------- 扩展名

    /// <summary>
    /// 列出系统中已注册的扩展名（SystemFileAssociations 下以 . 开头的键），用于扩展名分类的选择列表。
    /// </summary>
    public static List<string> GetRegisteredExtensions()
    {
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (hive, root) in new[] { ((RegistryKey)Registry.CurrentUser, ClassesHKCU), (Registry.LocalMachine, ClassesHKLM) })
        {
            using var sfa = hive.OpenSubKey($@"{root}\SystemFileAssociations");
            if (sfa == null) continue;
            foreach (var name in sfa.GetSubKeyNames())
                if (name.StartsWith('.')) set.Add(name.ToLowerInvariant());
        }
        return set.ToList();
    }

    // ---------------------------------------------------------------- 辅助

    private static void SafeDeleteValue(RegistryKey key, string valueName)
    {
        try { if (key.GetValue(valueName) != null) key.DeleteValue(valueName); }
        catch { /* 忽略 */ }
    }

    private static bool IsClsidLike(string s) =>
        Guid.TryParseExact(s.Trim().Trim('{', '}'), "D", out _);

    private static string EnsureBraces(string clsid)
    {
        clsid = clsid.Trim();
        return clsid.StartsWith('{') ? clsid : "{" + clsid + "}";
    }

    /// <summary>查 HKCR\CLSID\{...} 的默认值，得到 COM 组件的描述文字（如 "7-Zip Shell Extension"）。</summary>
    private static string? GetClsidDescription(string clsid)
    {
        if (!IsClsidLike(clsid)) return null;
        try
        {
            using var clsidKey = Registry.ClassesRoot.OpenSubKey($@"CLSID\{EnsureBraces(clsid)}");
            var name = (clsidKey?.GetValue(null) as string)?.Trim();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch { return null; }
    }

    /// <summary>取 COM 组件的图标：优先 DefaultIcon，其次实现 DLL（InprocServer32）的主图标。</summary>
    private static string? GetClsidModuleIcon(string clsid)
    {
        if (!IsClsidLike(clsid)) return null;
        try
        {
            using var clsidKey = Registry.ClassesRoot.OpenSubKey($@"CLSID\{EnsureBraces(clsid)}");
            if (clsidKey == null) return null;

            string? icon;
            using (var iconKey = clsidKey.OpenSubKey("DefaultIcon"))
                icon = (iconKey?.GetValue(null) as string)?.Trim();

            if (string.IsNullOrWhiteSpace(icon))
            {
                using var inproc = clsidKey.OpenSubKey("InprocServer32");
                var dll = (inproc?.GetValue(null) as string)?.Trim();
                if (!string.IsNullOrWhiteSpace(dll) && !dll.StartsWith('{'))
                    icon = dll;
            }

            if (string.IsNullOrWhiteSpace(icon)) return null;
            // 个别注册写成 @dll,-id 的间接形式，去掉 @ 后即为普通图标位置
            return icon.TrimStart('@');
        }
        catch { return null; }
    }

    /// <summary>解析 @dll,-id 形式的间接资源字符串为本地化文字。</summary>
    private static string ResolveIndirectString(string value)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith('@')) return value;
        try
        {
            var sb = new StringBuilder(1024);
            if (NativeMethods.SHLoadIndirectString(value, sb, (uint)sb.Capacity, IntPtr.Zero) == 0)
            {
                var resolved = sb.ToString();
                if (!string.IsNullOrWhiteSpace(resolved)) return resolved;
            }
        }
        catch { /* 解析失败时原样返回 */ }
        return value;
    }

    private static string MakeKeyName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = title.Trim().Select(c => invalid.Contains(c) || c == '\\' || c == '/' ? '_' : c).ToArray();
        var name = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(name) ? "NewMenu" : name;
    }

    private static string GetUniqueKeyName(RegistryKey hive, string classesRoot, string shellRelative, string baseName)
    {
        using var shellKey = hive.OpenSubKey($@"{classesRoot}\{shellRelative}");
        var existing = new HashSet<string>(shellKey?.GetSubKeyNames() ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(baseName)) return baseName;
        for (int i = 2; i < 1000; i++)
        {
            var candidate = $"{baseName}_{i}";
            if (!existing.Contains(candidate)) return candidate;
        }
        return $"{baseName}_{Guid.NewGuid():N}";
    }

    private static void NotifyShell() => NativeMethods_SHChangeNotify();

    private static void NativeMethods_SHChangeNotify()
        => RightMenuMaster.Helpers.NativeMethods.NotifyAssociationChanged();
}

using RightMenuMaster.Imaging;
using RightMenuMaster.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RightMenuMaster.Services;

/// <summary>
/// 菜单项的导出与导入。
/// 采用 JSON 格式；由本程序生成的图标文件（图标目录下）会以 base64 内嵌进文件，
/// 保证导入到其他电脑后图标依然可用。
/// </summary>
public static class ExportImportService
{
    private const string AppId = "RightMenuMaster";

    private sealed class ExportFile
    {
        public string App { get; set; } = AppId;
        public int Version { get; set; } = 1;
        public string ExportedAt { get; set; } = string.Empty;
        public List<ExportItem> Items { get; set; } = new();
    }

    private sealed class ExportItem
    {
        public string Category { get; set; } = string.Empty;
        public string? Extension { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string? IconPath { get; set; }
        /// <summary>内嵌图标的文件名（原文件位于本程序图标目录时）。</summary>
        public string? IconFileName { get; set; }
        /// <summary>内嵌图标的 base64 数据。</summary>
        public string? IconData { get; set; }
        public string? Position { get; set; }
        public bool ShiftExtended { get; set; }
        public bool IsDisabled { get; set; }
        public bool NoWorkingDirectory { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// 把给定的菜单项导出到 JSON 文件，返回导出数量。
    /// （级联子菜单与 shellex 扩展处理程序结构特殊，会被自动过滤掉。）
    /// </summary>
    public static int Export(IEnumerable<MenuItemModel> source, string filePath)
    {
        var items = source
            .Where(i => !i.IsCascade && !i.IsShellExtension)
            .ToList();

        var file = new ExportFile { ExportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };

        foreach (var item in items)
        {
            var exp = new ExportItem
            {
                Category = item.Category.ToString(),
                Extension = item.Extension,
                Title = item.Title,
                Command = item.Command,
                IconPath = item.IconPath,
                Position = item.Position,
                ShiftExtended = item.ShiftExtended,
                IsDisabled = item.IsDisabled,
                NoWorkingDirectory = item.NoWorkingDirectory,
            };

            // 图标若为本程序生成的文件（图标目录下），则内嵌数据，跨机导入仍可用
            try
            {
                var iconFile = ExtractIconFile(item.IconPath);
                if (iconFile != null
                    && iconFile.StartsWith(IconService.IconsDir, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(iconFile))
                {
                    exp.IconFileName = Path.GetFileName(iconFile);
                    exp.IconData = Convert.ToBase64String(File.ReadAllBytes(iconFile));
                }
            }
            catch { /* 图标读取失败不影响菜单项导出 */ }

            file.Items.Add(exp);
        }

        File.WriteAllText(filePath, JsonSerializer.Serialize(file, JsonOpts));
        return items.Count;
    }

    /// <summary>
    /// 导入文件中的一个待导入项。解析阶段不碰注册表，交给用户确认后再写入。
    /// </summary>
    public sealed class ImportCandidate
    {
        public required MenuItemModel Item { get; init; }

        /// <summary>写入后会使用的注册表子键名。</summary>
        public required string KeyName { get; init; }

        /// <summary>当前用户下是否已有同名项。</summary>
        public required bool AlreadyExists { get; init; }

        /// <summary>内嵌图标数据，确认导入后才落盘。</summary>
        public string? IconFileName { get; init; }
        public string? IconData { get; init; }

        /// <summary>用户是否勾选导入（默认全选）。</summary>
        public bool Selected { get; set; } = true;

        public string Title => Item.Title;
        public string Command => Item.Command;

        public string ScopeName => Item.Category == MenuCategory.Extension
            ? $"{Item.Category.DisplayName()}（{Item.Extension}）"
            : Item.Category.DisplayName();

        public string StatusText => AlreadyExists ? "已存在同名项" : "新增";
    }

    /// <summary>
    /// 解析导出文件，返回待导入项列表（只读，不写注册表）。
    /// 文件格式非法时抛 <see cref="InvalidDataException"/>。
    /// </summary>
    public static List<ImportCandidate> Parse(string filePath)
    {
        ExportFile? file;
        try
        {
            file = JsonSerializer.Deserialize<ExportFile>(File.ReadAllText(filePath), JsonOpts);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("文件内容不是有效的 JSON：" + ex.Message, ex);
        }

        if (file == null || file.App != AppId || file.Items == null)
            throw new InvalidDataException("不是有效的「右键菜单管家」导出文件。");

        var result = new List<ImportCandidate>();
        foreach (var exp in file.Items)
        {
            if (string.IsNullOrWhiteSpace(exp.Title) || string.IsNullOrWhiteSpace(exp.Command)) continue;
            if (!Enum.TryParse<MenuCategory>(exp.Category, out var category)) continue;

            var item = new MenuItemModel
            {
                Category = category,
                Extension = category == MenuCategory.Extension ? exp.Extension : null,
                Title = exp.Title,
                Command = exp.Command,
                IconPath = exp.IconPath,
                Position = exp.Position,
                ShiftExtended = exp.ShiftExtended,
                IsDisabled = exp.IsDisabled,
                NoWorkingDirectory = exp.NoWorkingDirectory,
                Source = RegistrySource.CurrentUser,
            };

            var keyName = RegistryService.KeyNameFor(item.Title);
            result.Add(new ImportCandidate
            {
                Item = item,
                KeyName = keyName,
                AlreadyExists = RegistryService.EntryExists(category, item.Extension, keyName),
                IconFileName = exp.IconFileName,
                IconData = exp.IconData,
            });
        }
        return result;
    }

    /// <summary>
    /// 把用户确认后的项写入当前用户（无需管理员权限）。
    /// 同名项按 <paramref name="overwriteExisting"/> 覆盖或跳过。
    /// 返回（导入数, 跳过数）。
    /// </summary>
    public static (int Imported, int Skipped) Apply(
        IEnumerable<ImportCandidate> candidates, bool overwriteExisting)
    {
        int imported = 0, skipped = 0;
        foreach (var c in candidates.Where(c => c.Selected))
        {
            if (c.AlreadyExists && !overwriteExisting) { skipped++; continue; }

            var item = c.Item;

            // 还原内嵌图标（失败不影响菜单项本身）
            if (!string.IsNullOrEmpty(c.IconData) && !string.IsNullOrEmpty(c.IconFileName))
            {
                try { item.IconPath = WriteEmbeddedIcon(c.IconFileName, c.IconData); }
                catch { /* 忽略 */ }
            }

            if (c.AlreadyExists)
            {
                // 覆盖：沿用已有键名，走「修改」路径，避免生成 Foo_2 副本
                item.KeyName = c.KeyName;
                RegistryService.SaveEntry(item, isNew: false);
            }
            else
            {
                RegistryService.SaveEntry(item, isNew: true);
            }
            imported++;
        }
        return (imported, skipped);
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>从 Icon 值（"文件" 或 "文件,索引"）中提取文件部分。</summary>
    private static string? ExtractIconFile(string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath)) return null;
        var expanded = Environment.ExpandEnvironmentVariables(iconPath.Trim());
        var comma = expanded.LastIndexOf(',');
        if (comma > 1 && int.TryParse(expanded.AsSpan(comma + 1).Trim(), out _))
            expanded = expanded[..comma];
        return expanded.Trim().Trim('"');
    }

    private static string WriteEmbeddedIcon(string fileName, string base64)
    {
        Directory.CreateDirectory(IconService.IconsDir);
        var safeName = string.Join("_",
            fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "icon.ico";

        var target = Path.Combine(IconService.IconsDir, safeName);
        if (File.Exists(target))
        {
            var name = Path.GetFileNameWithoutExtension(safeName);
            var ext = Path.GetExtension(safeName);
            target = Path.Combine(IconService.IconsDir, $"{name}_{Guid.NewGuid():N}{ext}");
        }

        File.WriteAllBytes(target, Convert.FromBase64String(base64));
        return target;
    }
}

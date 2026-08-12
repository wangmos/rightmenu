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
    /// 导出指定分类下的所有非级联、非 Shell 扩展菜单项到 JSON 文件，返回导出数量。
    /// （级联子菜单与 shellex 扩展处理程序结构特殊，不参与导出。）
    /// </summary>
    public static int Export(MenuCategory category, string? extension, string filePath)
    {
        var items = RegistryService.GetEntries(category, extension)
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
    /// 从 JSON 文件导入菜单项（全部写入当前用户，无需管理员权限），返回导入数量。
    /// </summary>
    public static int Import(string filePath)
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

        int count = 0;
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

            // 还原内嵌图标（失败不影响菜单项本身）
            if (!string.IsNullOrEmpty(exp.IconData) && !string.IsNullOrEmpty(exp.IconFileName))
            {
                try
                {
                    item.IconPath = WriteEmbeddedIcon(exp.IconFileName, exp.IconData);
                }
                catch { /* 忽略 */ }
            }

            RegistryService.SaveEntry(item, isNew: true);
            count++;
        }
        return count;
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

using RightMenuMaster.Models;
using RightMenuMaster.Services;
using System.IO;
using System.Text.Json;
using Xunit;

namespace RightMenuMaster.Tests;

/// <summary>
/// 导出行为。Import 会真实写注册表，此处只覆盖 Export 与文件格式校验。
/// </summary>
public class ExportImportServiceTests : IDisposable
{
    private readonly string _tempDir;

    public ExportImportServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "RmmExp_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* 忽略 */ }
        GC.SuppressFinalize(this);
    }

    private string TempFile() => Path.Combine(_tempDir, Guid.NewGuid().ToString("N")[..8] + ".json");

    private static MenuItemModel Item(string title, string command = "notepad.exe \"%1\"") => new()
    {
        KeyName = title,
        Title = title,
        Command = command,
        Category = MenuCategory.Background,
        Source = RegistrySource.CurrentUser,
    };

    /// <summary>
    /// 回归：Export 曾经忽略入参、直接重新枚举整个分类，
    /// 导致工具栏「导出」永远导出全部而不是勾选的那几项。
    /// </summary>
    [Fact]
    public void Export_只导出传入的项()
    {
        var path = TempFile();
        var items = new[] { Item("甲"), Item("乙"), Item("丙") };

        var n = ExportImportService.Export(items.Take(2), path);

        Assert.Equal(2, n);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var arr = doc.RootElement.GetProperty("Items");
        Assert.Equal(2, arr.GetArrayLength());
        Assert.Equal("甲", arr[0].GetProperty("Title").GetString());
        Assert.Equal("乙", arr[1].GetProperty("Title").GetString());
    }

    /// <summary>级联子菜单与 shellex 扩展结构特殊，不能进导出文件。</summary>
    [Fact]
    public void Export_过滤级联与shellex项()
    {
        var path = TempFile();
        var cascade = Item("级联"); cascade.IsCascade = true;
        var shellex = Item("扩展"); shellex.IsShellExtension = true;

        var n = ExportImportService.Export(new[] { Item("正常"), cascade, shellex }, path);

        Assert.Equal(1, n);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(1, doc.RootElement.GetProperty("Items").GetArrayLength());
    }

    [Fact]
    public void Export_空集合生成空文件而不抛异常()
    {
        var path = TempFile();

        var n = ExportImportService.Export(Array.Empty<MenuItemModel>(), path);

        Assert.Equal(0, n);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Parse_非本程序的文件应被拒绝()
    {
        var path = TempFile();
        File.WriteAllText(path, """{"App":"SomethingElse","Version":1,"Items":[]}""");

        Assert.Throws<InvalidDataException>(() => ExportImportService.Parse(path));
    }

    [Fact]
    public void Parse_损坏的JSON应被拒绝()
    {
        var path = TempFile();
        File.WriteAllText(path, "{ 这不是 json");

        Assert.Throws<InvalidDataException>(() => ExportImportService.Parse(path));
    }

    /// <summary>
    /// 导出再解析应能完整读回。Parse 只读注册表判断重名，不写入任何内容。
    /// </summary>
    [Fact]
    public void Parse_能读回导出的内容且不写注册表()
    {
        var path = TempFile();
        var original = Item("往返测试项", "cmd.exe /c echo hi");
        ExportImportService.Export(new[] { original }, path);

        var candidates = ExportImportService.Parse(path);

        var c = Assert.Single(candidates);
        Assert.Equal("往返测试项", c.Title);
        Assert.Equal("cmd.exe /c echo hi", c.Command);
        Assert.True(c.Selected, "默认应为勾选状态");
        Assert.False(c.AlreadyExists, "测试项不应存在于注册表中");
        Assert.Equal("往返测试项", c.KeyName);
        // Parse 是只读的：解析后注册表里不应凭空出现该项
        Assert.False(RegistryService.EntryExists(MenuCategory.Background, null, c.KeyName));
    }

    /// <summary>键名要过滤掉路径分隔符等非法字符，否则会写到别的注册表层级去。</summary>
    [Theory]
    [InlineData("正常标题", "正常标题")]
    [InlineData(@"带\斜杠", "带_斜杠")]
    [InlineData("带/斜杠", "带_斜杠")]
    [InlineData("   ", "NewMenu")]
    public void KeyNameFor_应过滤非法字符(string title, string expected)
    {
        Assert.Equal(expected, RegistryService.KeyNameFor(title));
    }
}

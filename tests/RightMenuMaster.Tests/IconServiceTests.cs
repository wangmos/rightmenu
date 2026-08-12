using RightMenuMaster.Imaging;
using System.IO;
using Xunit;

namespace RightMenuMaster.Tests;

/// <summary>
/// 图标解析与转换。这里的用例都对应真实修复过的缺陷，删改前请先确认原缺陷不会复发。
/// </summary>
public class IconServiceTests : IDisposable
{
    private readonly string _tempDir;

    public IconServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "RmmTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* 忽略 */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>生成一张最小可用的 PNG（1x1 白点）。</summary>
    private string MakePng(string fileName)
    {
        var path = Path.Combine(_tempDir, fileName);
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        File.WriteAllBytes(path, png);
        return path;
    }

    /// <summary>
    /// 回归：曾经对 "名字_GUID" 整串取 [..64]，文件名短于 31 字符时直接
    /// ArgumentOutOfRangeException，导致「选择图片作为图标」功能全线失效。
    /// </summary>
    [Theory]
    [InlineData("a.png")]
    [InlineData("logo.png")]
    [InlineData("很短.png")]
    [InlineData("一个名字非常非常非常非常长的图片文件名用来验证截断逻辑是否正确.png")]
    public void SaveImageAsIcon_短文件名也不应越界(string fileName)
    {
        var src = MakePng(fileName);

        var ico = IconService.SaveImageAsIcon(src);

        Assert.False(string.IsNullOrWhiteSpace(ico));
        Assert.True(File.Exists(ico), $"ICO 未生成: {ico}");
        Assert.EndsWith(".ico", ico, StringComparison.OrdinalIgnoreCase);
        Assert.True(new FileInfo(ico).Length > 0, "ICO 文件为空");

        File.Delete(ico);
    }

    /// <summary>同一张图片多次保存不应互相覆盖（文件名带 GUID 后缀）。</summary>
    [Fact]
    public void SaveImageAsIcon_重复保存生成不同文件()
    {
        var src = MakePng("dup.png");

        var a = IconService.SaveImageAsIcon(src);
        var b = IconService.SaveImageAsIcon(src);

        Assert.NotEqual(a, b);
        File.Delete(a);
        File.Delete(b);
    }

    /// <summary>.ico 源文件走直接复制分支，同样不能越界。</summary>
    [Fact]
    public void SaveImageAsIcon_ico源文件直接复制()
    {
        var pngPath = MakePng("seed.png");
        var seedIco = IconService.SaveImageAsIcon(pngPath);
        var copyTarget = Path.Combine(_tempDir, "x.ico");
        File.Copy(seedIco, copyTarget, overwrite: true);

        var ico = IconService.SaveImageAsIcon(copyTarget);

        Assert.True(File.Exists(ico));
        Assert.NotEqual(copyTarget, ico);

        File.Delete(seedIco);
        File.Delete(ico);
    }

    /// <summary>
    /// 回归：ResolveIcon 的 size 参数曾经完全未被使用，
    /// 调用方以为拿到的是 64px，实际永远是 ExtractIconEx 的 32px。
    /// </summary>
    [Fact]
    public void ResolveIcon_size参数应真实生效()
    {
        var shell32 = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\shell32.dll");
        Assert.True(File.Exists(shell32), "测试环境缺少 shell32.dll");

        var small = IconService.ResolveIcon(shell32 + ",0", 32);
        var large = IconService.ResolveIcon(shell32 + ",0", 128);

        Assert.NotNull(small);
        Assert.NotNull(large);
        Assert.True(large!.PixelWidth > small!.PixelWidth,
            $"size 未生效：32 → {small.PixelWidth}px，128 → {large.PixelWidth}px");
    }

    [Fact]
    public void ResolveIcon_路径无效时返回null()
    {
        Assert.Null(IconService.ResolveIcon(null));
        Assert.Null(IconService.ResolveIcon("   "));
        Assert.Null(IconService.ResolveIcon(@"C:\这个文件肯定不存在_zzz.dll,3"));
    }

    /// <summary>"文件,索引" 里的索引不能被当成路径的一部分。</summary>
    [Fact]
    public void ResolveIcon_应正确拆分索引()
    {
        var shell32 = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\shell32.dll");

        Assert.NotNull(IconService.ResolveIcon(shell32 + ",3"));
        Assert.NotNull(IconService.ResolveIcon("\"" + shell32 + "\",3"));
        Assert.NotNull(IconService.ResolveIcon(@"%SystemRoot%\System32\shell32.dll,3"));
    }
}

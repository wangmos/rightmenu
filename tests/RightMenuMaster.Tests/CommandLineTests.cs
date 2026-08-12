using RightMenuMaster.Helpers;
using System.Text;
using Xunit;

namespace RightMenuMaster.Tests;

/// <summary>
/// 命令行拆分/拼装/转义。编辑对话框「保存 → 重新打开 → 再保存」必须稳定，
/// 否则用户的脚本会在每次往返中被逐渐改坏。
/// </summary>
public class CommandLineTests
{
    // ---------------------------------------------------------------- 拆分

    [Theory]
    [InlineData("notepad.exe", "notepad.exe", "")]
    [InlineData("notepad.exe \"%1\"", "notepad.exe", "\"%1\"")]
    [InlineData("\"C:\\Program Files\\App\\a.exe\" -x \"%1\"", "C:\\Program Files\\App\\a.exe", "-x \"%1\"")]
    [InlineData("", "", "")]
    [InlineData("   ", "", "")]
    public void Split_常见形式(string command, string expectedProgram, string expectedArgs)
    {
        CommandLine.Split(command, out var program, out var args);

        Assert.Equal(expectedProgram, program);
        Assert.Equal(expectedArgs, args);
    }

    [Fact]
    public void Build_含空格的路径自动补引号()
    {
        Assert.Equal("\"C:\\Program Files\\a.exe\" \"%1\"",
            CommandLine.Build(@"C:\Program Files\a.exe", "\"%1\""));
        Assert.Equal("notepad.exe", CommandLine.Build("notepad.exe", ""));
        // 已经带引号的不再重复加
        Assert.Equal("\"C:\\p f\\a.exe\"", CommandLine.Build("\"C:\\p f\\a.exe\"", ""));
    }

    [Fact]
    public void Split_Build_往返一致()
    {
        const string original = "\"C:\\Program Files\\App\\a.exe\" -x \"%1\"";

        CommandLine.Split(original, out var p, out var a);

        Assert.Equal(original, CommandLine.Build(p, a));
    }

    // ---------------------------------------------------------------- 转义

    [Theory]
    [InlineData("简单文本", "简单文本")]
    [InlineData("含\"引号\"", "含\\\"引号\\\"")]
    [InlineData(@"C:\path\to", @"C:\path\to")]
    public void EscapeForQuoted_按CommandLineToArgvW规则(string input, string expected)
    {
        Assert.Equal(expected, CommandLine.EscapeForQuoted(input));
    }

    /// <summary>
    /// 回归：旧实现只做 Replace("\"", "\\\"")，路径以反斜杠结尾时
    /// 收尾的引号会被转义掉，整条命令散架。
    /// </summary>
    [Fact]
    public void EscapeForQuoted_结尾反斜杠必须翻倍()
    {
        var escaped = CommandLine.EscapeForQuoted(@"cd C:\temp\");

        Assert.EndsWith(@"\\", escaped);
        // 拼进引号后，结尾的引号不会被吃掉
        var full = $"\"{escaped}\"";
        Assert.EndsWith("\\\\\"", full);
    }

    [Theory]
    [InlineData("简单文本")]
    [InlineData("含\"引号\"的脚本")]
    [InlineData(@"路径 C:\temp\ 结尾带反斜杠")]
    [InlineData(@"混合 \\server\share\ 和 ""引号""")]
    [InlineData(@"C:\a\\b\\\c")]
    public void Escape_Unescape_往返一致(string original)
    {
        var escaped = CommandLine.EscapeForQuoted(original);

        Assert.Equal(original, CommandLine.UnescapeFromQuoted(escaped));
    }

    // ---------------------------------------------------------------- CMD

    [Fact]
    public void Cmd_往返一致()
    {
        const string script = "dir \"%1\" & pause";

        var command = CommandLine.BuildCmd(script);
        CommandLine.Split(command, out var p, out var a);

        Assert.True(CommandLine.TryParseCmd(p, a, out var parsed));
        Assert.Equal(script, parsed);
    }

    [Fact]
    public void TryParseCmd_不是cmd时返回false()
    {
        Assert.False(CommandLine.TryParseCmd("notepad.exe", "\"%1\"", out _));
        // 是 cmd 但没有 /c，属于用户自己写的程序调用
        Assert.False(CommandLine.TryParseCmd("cmd.exe", "/k something", out _));
    }

    // ---------------------------------------------------------------- PowerShell

    /// <summary>不含占位符的脚本用 -EncodedCommand，彻底免疫引号问题。</summary>
    [Fact]
    public void PowerShell_无占位符时使用EncodedCommand()
    {
        const string script = "Get-Process | Where-Object { $_.Name -eq \"explorer\" }";

        var command = CommandLine.BuildPowerShell(script);

        Assert.Contains("-EncodedCommand", command);
        Assert.DoesNotContain("Get-Process", command); // 已被编码

        CommandLine.Split(command, out var p, out var a);
        Assert.True(CommandLine.TryParsePowerShell(p, a, out var parsed));
        Assert.Equal(script, parsed);
    }

    /// <summary>
    /// 含 %1 / %V 的脚本必须保持明文，否则资源管理器无法替换占位符。
    /// </summary>
    [Theory]
    [InlineData("Get-ChildItem \"%1\"")]
    [InlineData("Set-Location -LiteralPath '%V'")]
    [InlineData("echo %L")]
    public void PowerShell_含占位符时保持明文(string script)
    {
        var command = CommandLine.BuildPowerShell(script);

        Assert.Contains("-Command", command);
        Assert.DoesNotContain("-EncodedCommand", command);
        Assert.True(CommandLine.ContainsPlaceholder(command),
            "占位符必须原样出现在命令行里，否则 shell 无法替换");
    }

    [Theory]
    [InlineData("Get-ChildItem \"%1\" | Out-GridView")]
    [InlineData("Set-Location -LiteralPath '%V'; Get-Date")]
    [InlineData("echo \"%1 里有引号\"")]
    [InlineData(@"cd ""%V""; dir C:\temp\")]
    public void PowerShell_含占位符也能往返(string script)
    {
        var command = CommandLine.BuildPowerShell(script);
        CommandLine.Split(command, out var p, out var a);

        Assert.True(CommandLine.TryParsePowerShell(p, a, out var parsed));
        Assert.Equal(script, parsed);
    }

    /// <summary>保存两次的结果必须完全一致，否则反复编辑会让命令逐渐变形。</summary>
    [Theory]
    [InlineData("Get-ChildItem \"%1\"")]
    [InlineData("Get-Process | Sort-Object CPU")]
    [InlineData(@"cd ""%V""; dir C:\temp\")]
    public void PowerShell_多次往返保持稳定(string script)
    {
        var first = CommandLine.BuildPowerShell(script);
        CommandLine.Split(first, out var p1, out var a1);
        CommandLine.TryParsePowerShell(p1, a1, out var back1);

        var second = CommandLine.BuildPowerShell(back1);

        Assert.Equal(first, second);
        Assert.Equal(script, back1);
    }

    /// <summary>旧版本写下的 -Command 明文命令仍要能正确读回。</summary>
    [Fact]
    public void PowerShell_兼容旧的Command形式()
    {
        const string legacy = "powershell.exe -NoProfile -Command \"Get-ChildItem \\\"%1\\\"\"";

        CommandLine.Split(legacy, out var p, out var a);

        Assert.True(CommandLine.TryParsePowerShell(p, a, out var parsed));
        Assert.Equal("Get-ChildItem \"%1\"", parsed);
    }

    [Fact]
    public void TryParsePowerShell_损坏的base64不应抛异常()
    {
        Assert.False(CommandLine.TryParsePowerShell(
            "powershell.exe", "-NoProfile -EncodedCommand 这不是base64", out _));
    }

    [Fact]
    public void EncodedCommand_使用UTF16LE编码()
    {
        const string script = "echo 中文测试";

        var command = CommandLine.BuildPowerShell(script);
        var b64 = command[(command.IndexOf("-EncodedCommand", StringComparison.Ordinal)
            + "-EncodedCommand".Length)..].Trim();

        // PowerShell 规定 -EncodedCommand 为 UTF-16LE 的 base64
        Assert.Equal(script, Encoding.Unicode.GetString(Convert.FromBase64String(b64)));
    }

    [Theory]
    [InlineData("含 %1 的", true)]
    [InlineData("含 %V 的", true)]
    [InlineData("含 %L 的", true)]
    [InlineData("什么都没有", false)]
    public void ContainsPlaceholder_识别三种占位符(string s, bool expected)
    {
        Assert.Equal(expected, CommandLine.ContainsPlaceholder(s));
    }
}

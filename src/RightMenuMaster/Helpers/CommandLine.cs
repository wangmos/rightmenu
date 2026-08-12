using System.Text;

namespace RightMenuMaster.Helpers;

/// <summary>
/// 注册表 command 值的拆分与拼装。
///
/// 这里的函数都是纯函数，编辑对话框（把界面字段变成命令行、以及反过来回填）
/// 和模板预览都用它，避免各写一份逐渐走样。
/// </summary>
public static class CommandLine
{
    /// <summary>右键菜单命令行里可用的占位符：%1 选中项、%V 当前目录、%L 长路径。</summary>
    public static bool ContainsPlaceholder(string s) =>
        s.Contains("%1") || s.Contains("%V", StringComparison.OrdinalIgnoreCase)
        || s.Contains("%L", StringComparison.OrdinalIgnoreCase);

    // ---------------------------------------------------------------- 拆分 / 拼装

    /// <summary>把命令行拆成程序与参数（支持带引号的程序路径）。</summary>
    public static void Split(string command, out string program, out string args)
    {
        program = string.Empty;
        args = string.Empty;
        if (string.IsNullOrWhiteSpace(command)) return;

        var cmd = command.Trim();
        if (cmd.StartsWith('"'))
        {
            int end = cmd.IndexOf('"', 1);
            if (end > 0)
            {
                program = cmd[1..end];
                args = cmd[(end + 1)..].Trim();
                return;
            }
        }

        int space = cmd.IndexOf(' ');
        if (space > 0)
        {
            program = cmd[..space];
            args = cmd[(space + 1)..].Trim();
        }
        else
        {
            program = cmd;
        }
    }

    /// <summary>把程序与参数拼成命令行，程序路径含空格时补引号。</summary>
    public static string Build(string program, string args)
    {
        program = program.Trim();
        args = args.Trim();
        if (program.Length == 0) return args;
        var prog = program.Contains(' ') && !program.StartsWith('"') ? $"\"{program}\"" : program;
        return string.IsNullOrEmpty(args) ? prog : $"{prog} {args}";
    }

    // ---------------------------------------------------------------- 引号转义

    /// <summary>
    /// 按 CommandLineToArgvW 的规则转义一段文本，使其能安全放进一对双引号中间。
    /// 规则：引号前的反斜杠要翻倍，引号本身再多加一个反斜杠；
    /// 结尾的反斜杠也要翻倍（否则会把收尾的引号转义掉）。
    /// </summary>
    public static string EscapeForQuoted(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        int backslashes = 0;

        foreach (var c in s)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                sb.Append('\\', backslashes * 2 + 1).Append('"');
            }
            else
            {
                sb.Append('\\', backslashes).Append(c);
            }
            backslashes = 0;
        }

        sb.Append('\\', backslashes * 2);
        return sb.ToString();
    }

    /// <summary><see cref="EscapeForQuoted"/> 的逆运算，用于把命令行回填到编辑框。</summary>
    public static string UnescapeFromQuoted(string s)
    {
        var sb = new StringBuilder(s.Length);
        int backslashes = 0;

        foreach (var c in s)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                // 前面的反斜杠成对折半，最后一个（奇数个时）用于转义这个引号
                sb.Append('\\', backslashes / 2).Append('"');
            }
            else
            {
                sb.Append('\\', backslashes).Append(c);
            }
            backslashes = 0;
        }

        sb.Append('\\', backslashes % 2 == 0 ? backslashes / 2 : backslashes);
        return sb.ToString();
    }

    /// <summary>去掉最外层的一对双引号（若存在）。</summary>
    public static string TrimOuterQuotes(string s)
    {
        s = s.Trim();
        return s.Length >= 2 && s.StartsWith('"') && s.EndsWith('"') ? s[1..^1] : s;
    }

    // ---------------------------------------------------------------- CMD

    public static string BuildCmd(string script) => $"cmd.exe /c {script.Trim()}";

    /// <summary>识别 cmd.exe /c ... 形式，取出其中的脚本内容。</summary>
    public static bool TryParseCmd(string program, string args, out string script)
    {
        script = string.Empty;
        var prog = Normalize(program);
        if (prog is not ("cmd" or "cmd.exe") && !prog.EndsWith(@"\cmd.exe")) return false;

        var a = args.Trim();
        if (!a.StartsWith("/c", StringComparison.OrdinalIgnoreCase)) return false;

        script = a.Length > 2 ? a[2..].TrimStart() : string.Empty;
        return true;
    }

    // ---------------------------------------------------------------- PowerShell

    /// <summary>
    /// 生成 PowerShell 命令行。
    ///
    /// 不含占位符时用 -EncodedCommand（脚本整体 base64，彻底绕开引号与转义问题）；
    /// 含 %1/%V/%L 时必须保留明文，否则资源管理器无法替换占位符，
    /// 此时按 CommandLineToArgvW 规则严格转义。
    /// </summary>
    public static string BuildPowerShell(string script)
    {
        script = script.Trim();
        if (ContainsPlaceholder(script))
            return $"powershell.exe -NoProfile -Command \"{EscapeForQuoted(script)}\"";

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return $"powershell.exe -NoProfile -EncodedCommand {encoded}";
    }

    /// <summary>识别 powershell.exe -Command / -EncodedCommand 形式，取出脚本内容。</summary>
    public static bool TryParsePowerShell(string program, string args, out string script)
    {
        script = string.Empty;
        var prog = Normalize(program);
        bool isPs = prog is "powershell" or "powershell.exe" or "pwsh" or "pwsh.exe"
            || prog.EndsWith(@"\powershell.exe") || prog.EndsWith(@"\pwsh.exe");
        if (!isPs) return false;

        var a = args.Trim();

        int enc = a.IndexOf("-EncodedCommand", StringComparison.OrdinalIgnoreCase);
        if (enc >= 0)
        {
            var payload = a[(enc + "-EncodedCommand".Length)..].Trim();
            // 后面可能还跟着别的参数，只取第一段
            var space = payload.IndexOf(' ');
            if (space > 0) payload = payload[..space];
            try
            {
                script = Encoding.Unicode.GetString(Convert.FromBase64String(payload));
                return true;
            }
            catch
            {
                return false; // base64 损坏，按普通程序处理
            }
        }

        int idx = a.IndexOf("-Command", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;

        var p = a[(idx + "-Command".Length)..].Trim();
        if (p.Length >= 2 && p.StartsWith('"') && p.EndsWith('"'))
            p = UnescapeFromQuoted(p[1..^1]);
        script = p;
        return true;
    }

    private static string Normalize(string program) =>
        program.Trim().Trim('"').ToLowerInvariant().Replace('/', '\\');
}

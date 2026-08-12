using RightMenuMaster.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RightMenuMaster.Services;

/// <summary>
/// LLM API 连接设置，保存在数据目录下的 llm.json。
///
/// API Key 用 DPAPI 按当前用户加密后存 <see cref="ProtectedApiKey"/>，明文不落盘；
/// 旧版本写下的明文 <see cref="ApiKey"/> 仍可读入，下次保存时自动升级为密文。
/// </summary>
public sealed class LlmSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    /// <summary>DPAPI 密文（base64）。正常情况下 Key 只以这种形式落盘。</summary>
    public string? ProtectedApiKey { get; set; }

    /// <summary>旧版本的明文字段，仅为兼容读取而保留，保存时始终写空。</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>内存中的明文 Key，不参与序列化。</summary>
    [JsonIgnore]
    public string Key { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Key)
        && !string.IsNullOrWhiteSpace(Model);

    private static string FilePath => RightMenuMaster.Helpers.AppPaths.LlmSettingsFile;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static LlmSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new LlmSettings();

            var s = JsonSerializer.Deserialize<LlmSettings>(File.ReadAllText(FilePath)) ?? new LlmSettings();

            // 优先用密文；解不开（换机器/换用户）时退回明文字段，二者都没有就算未配置
            s.Key = RightMenuMaster.Helpers.DataProtection.Unprotect(s.ProtectedApiKey) ?? string.Empty;
            if (string.IsNullOrEmpty(s.Key)) s.Key = s.ApiKey;
            return s;
        }
        catch { /* 配置损坏时按未配置处理 */ }
        return new LlmSettings();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var encrypted = RightMenuMaster.Helpers.DataProtection.Protect(Key);
        var toWrite = new LlmSettings
        {
            BaseUrl = BaseUrl,
            Model = Model,
            // DPAPI 不可用时宁可不写 Key，也不把明文落盘
            ProtectedApiKey = encrypted,
            ApiKey = string.Empty,
        };

        File.WriteAllText(FilePath, JsonSerializer.Serialize(toWrite, JsonOpts));
    }
}

/// <summary>大模型返回的菜单定义草稿（字段与编辑对话框一一对应）。</summary>
public sealed class MenuDraft
{
    public string? Title { get; set; }
    public string? Category { get; set; }
    public string? Extension { get; set; }
    public string? CommandKind { get; set; }
    public string? Program { get; set; }
    public string? Args { get; set; }
    public string? Script { get; set; }
    public string? Icon { get; set; }
    public string? Position { get; set; }
    public bool? ShiftExtended { get; set; }
    public bool? NoWorkingDirectory { get; set; }
}

/// <summary>调用 OpenAI 兼容的 chat/completions 接口，把自然语言描述转成 MenuDraft。</summary>
public static class LlmService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(90) };

    public static string BuildSystemPrompt()
    {
        var icons = string.Join("、", BuiltinIcons.All.Select(i => i.Name));
        return """
            你是 Windows 右键菜单管理工具「右键菜单管家」内置的智能填写助手。
            用户会用自然语言描述一个想要创建的右键菜单项，你需要理解意图并返回一个纯 JSON 对象，
            只输出 JSON 本身：不要解释、不要 markdown 代码块、不要代码围栏。

            字段定义（全部可选，按推断填写）：
            - title：菜单显示文字，简洁（不超过 20 字）。
            - category：菜单出现的作用域，只能取：
              "Directory" 右键文件夹对象本身时；
              "Background" 在文件夹空白处右键时（「在此处打开…」类场景默认用它）；
              "Folder" 针对所有文件夹对象；
              "File" 右键任意文件时；
              "Extension" 仅针对某扩展名文件（必须同时给 extension）。
              用户未说明时默认 "Background"。
            - extension：category 为 Extension 时使用，带点，如 ".md"。
            - commandKind：命令类型，取 "program" / "cmd" / "powershell"：
              "program" 启动某个可执行程序（需 program，可选 args）；
              "cmd" 适合 dir、copy、echo、pause 等 CMD 一行命令（需 script）；
              "powershell" 需要 PowerShell cmdlet、管道或 .NET 能力时（需 script）。
            - program：可执行程序路径或命令名。
            - args：程序参数。占位符：%1 = 右键的对象，%V = 当前目录，%L = 长路径。
            - script：cmd / powershell 的完整命令内容；涉及当前目录用 %V，涉及选中对象用 %1。
            - icon：从内置图标名中挑一个语义最贴近的：{ICONS}；都不合适填 null。
            - position："" 或 "Top" 或 "Bottom"，用户要求置顶/置底才填，否则 ""。
            - shiftExtended：仅当用户要求按住 Shift 才显示时 true，否则 false。
            - noWorkingDirectory：一般 false。

            示例：
            用户：右键空白处出现「打开终端」，在当前目录启动 cmd
            返回：{"title":"打开终端","category":"Background","commandKind":"cmd","script":"cmd.exe /k cd /d \"%V\"","icon":"终端","position":"","shiftExtended":false,"noWorkingDirectory":false}
            用户：右键 .md 文件时能「预览 Markdown」，用浏览器打开
            返回：{"title":"预览 Markdown","category":"Extension","extension":".md","commandKind":"program","program":"msedge.exe","args":"\"%1\"","icon":"地球","position":"","shiftExtended":false,"noWorkingDirectory":false}
            """.Replace("{ICONS}", icons);
    }

    public static async Task<MenuDraft> GenerateAsync(string description, LlmSettings settings,
        CancellationToken cancellationToken = default)
    {
        var url = settings.BaseUrl.Trim().TrimEnd('/');
        if (!url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            url += "/chat/completions";

        var request = new
        {
            model = settings.Model.Trim(),
            temperature = 0.2,
            messages = new object[]
            {
                new { role = "system", content = BuildSystemPrompt() },
                new { role = "user", content = description },
            },
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, url);
        msg.Headers.TryAddWithoutValidation("Authorization", "Bearer " + settings.Key.Trim());
        msg.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(msg, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"API 返回 {(int)resp.StatusCode}：{Truncate(body)}");

        string content;
        try
        {
            using var doc = JsonDocument.Parse(body);
            content = doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }
        catch
        {
            throw new InvalidOperationException("API 响应不是 OpenAI 兼容格式：" + Truncate(body));
        }

        return ParseDraft(content);
    }

    private static MenuDraft ParseDraft(string content)
    {
        content = content.Trim();
        if (content.StartsWith("```"))
        {
            int nl = content.IndexOf('\n');
            if (nl > 0) content = content[(nl + 1)..];
            if (content.EndsWith("```")) content = content[..^3];
            content = content.Trim();
        }
        if (!content.StartsWith("{"))
        {
            int s = content.IndexOf('{');
            int e = content.LastIndexOf('}');
            if (s >= 0 && e > s) content = content[s..(e + 1)];
        }

        var draft = JsonSerializer.Deserialize<MenuDraft>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
        });
        if (draft is null || string.IsNullOrWhiteSpace(draft.Title))
            throw new InvalidOperationException("无法从模型响应中解析出菜单定义");
        return draft;
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200] + "…";
}

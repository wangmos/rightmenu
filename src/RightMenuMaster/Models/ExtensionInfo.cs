namespace RightMenuMaster.Models;

/// <summary>
/// 默认程序管理中使用的常用扩展名信息。
/// </summary>
public class ExtensionInfo
{
    public string Ext { get; init; } = string.Empty;
    public string Group { get; init; } = "其他";
    public string Description { get; init; } = string.Empty;
}

public static class CommonExtensions
{
    public static IReadOnlyList<ExtensionInfo> All { get; } = new List<ExtensionInfo>
    {
        // 文档
        new() { Ext = ".txt",  Group = "文档", Description = "纯文本" },
        new() { Ext = ".md",   Group = "文档", Description = "Markdown" },
        new() { Ext = ".pdf",  Group = "文档", Description = "PDF 文档" },
        new() { Ext = ".doc",  Group = "文档", Description = "Word 97-2003" },
        new() { Ext = ".docx", Group = "文档", Description = "Word 文档" },
        new() { Ext = ".xls",  Group = "文档", Description = "Excel 97-2003" },
        new() { Ext = ".xlsx", Group = "文档", Description = "Excel 工作簿" },
        new() { Ext = ".ppt",  Group = "文档", Description = "PPT 97-2003" },
        new() { Ext = ".pptx", Group = "文档", Description = "PPT 演示文稿" },
        new() { Ext = ".csv",  Group = "文档", Description = "逗号分隔值" },
        new() { Ext = ".rtf",  Group = "文档", Description = "富文本" },
        new() { Ext = ".log",  Group = "文档", Description = "日志文件" },
        new() { Ext = ".ini",  Group = "文档", Description = "配置文件" },
        new() { Ext = ".json", Group = "文档", Description = "JSON" },
        new() { Ext = ".xml",  Group = "文档", Description = "XML" },

        // 图片
        new() { Ext = ".jpg",  Group = "图片", Description = "JPEG 图片" },
        new() { Ext = ".jpeg", Group = "图片", Description = "JPEG 图片" },
        new() { Ext = ".png",  Group = "图片", Description = "PNG 图片" },
        new() { Ext = ".gif",  Group = "图片", Description = "GIF 动图" },
        new() { Ext = ".bmp",  Group = "图片", Description = "位图" },
        new() { Ext = ".webp", Group = "图片", Description = "WebP 图片" },
        new() { Ext = ".svg",  Group = "图片", Description = "矢量图" },
        new() { Ext = ".ico",  Group = "图片", Description = "图标文件" },
        new() { Ext = ".psd",  Group = "图片", Description = "Photoshop" },
        new() { Ext = ".tif",  Group = "图片", Description = "TIFF 图片" },

        // 音视频
        new() { Ext = ".mp3",  Group = "音视频", Description = "MP3 音频" },
        new() { Ext = ".wav",  Group = "音视频", Description = "波形音频" },
        new() { Ext = ".flac", Group = "音视频", Description = "无损音频" },
        new() { Ext = ".m4a",  Group = "音视频", Description = "M4A 音频" },
        new() { Ext = ".mp4",  Group = "音视频", Description = "MP4 视频" },
        new() { Ext = ".mkv",  Group = "音视频", Description = "MKV 视频" },
        new() { Ext = ".avi",  Group = "音视频", Description = "AVI 视频" },
        new() { Ext = ".mov",  Group = "音视频", Description = "QuickTime 视频" },
        new() { Ext = ".wmv",  Group = "音视频", Description = "WMV 视频" },
        new() { Ext = ".webm", Group = "音视频", Description = "WebM 视频" },

        // 压缩包
        new() { Ext = ".zip",  Group = "压缩包", Description = "ZIP 压缩包" },
        new() { Ext = ".rar",  Group = "压缩包", Description = "RAR 压缩包" },
        new() { Ext = ".7z",   Group = "压缩包", Description = "7-Zip 压缩包" },
        new() { Ext = ".tar",  Group = "压缩包", Description = "TAR 归档" },
        new() { Ext = ".gz",   Group = "压缩包", Description = "GZip 压缩" },

        // 代码
        new() { Ext = ".html", Group = "代码", Description = "网页" },
        new() { Ext = ".css",  Group = "代码", Description = "样式表" },
        new() { Ext = ".js",   Group = "代码", Description = "JavaScript" },
        new() { Ext = ".ts",   Group = "代码", Description = "TypeScript" },
        new() { Ext = ".py",   Group = "代码", Description = "Python" },
        new() { Ext = ".java", Group = "代码", Description = "Java" },
        new() { Ext = ".c",    Group = "代码", Description = "C 语言" },
        new() { Ext = ".cpp",  Group = "代码", Description = "C++" },
        new() { Ext = ".cs",   Group = "代码", Description = "C#" },
        new() { Ext = ".go",   Group = "代码", Description = "Go" },
        new() { Ext = ".rs",   Group = "代码", Description = "Rust" },
        new() { Ext = ".sh",   Group = "代码", Description = "Shell 脚本" },
        new() { Ext = ".bat",  Group = "代码", Description = "批处理" },
        new() { Ext = ".ps1",  Group = "代码", Description = "PowerShell 脚本" },
        new() { Ext = ".sql",  Group = "代码", Description = "SQL 脚本" },
        new() { Ext = ".php",  Group = "代码", Description = "PHP" },
        new() { Ext = ".yml",  Group = "代码", Description = "YAML" },

        // 其他
        new() { Ext = ".exe",     Group = "其他", Description = "可执行程序" },
        new() { Ext = ".msi",     Group = "其他", Description = "安装包" },
        new() { Ext = ".iso",     Group = "其他", Description = "光盘映像" },
        new() { Ext = ".ttf",     Group = "其他", Description = "字体文件" },
        new() { Ext = ".chm",     Group = "其他", Description = "帮助文档" },
        new() { Ext = ".torrent", Group = "其他", Description = "种子文件" },
    };
}

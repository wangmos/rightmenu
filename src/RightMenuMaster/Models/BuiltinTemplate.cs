namespace RightMenuMaster.Models;

/// <summary>
/// 系统内置功能的快捷模板。选中后可一键生成右键菜单项。
/// </summary>
public class BuiltinTemplate
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>建议的作用域。</summary>
    public MenuCategory Category { get; init; } = MenuCategory.Background;

    /// <summary>要执行的命令行。</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>图标位置（可含 %SystemRoot% 等环境变量）。</summary>
    public string? IconPath { get; init; }

    /// <summary>分组。</summary>
    public string Group { get; init; } = "常用";

    public bool ShiftExtended { get; init; }
}

/// <summary>
/// 内置模板库。
/// </summary>
public static class BuiltinTemplates
{
    public static IReadOnlyList<BuiltinTemplate> All { get; } = new List<BuiltinTemplate>
    {
        // ===== 在此处打开 =====
        new() { Name = "在此处打开命令提示符", Description = "在当前目录打开 cmd 窗口", Group = "在此处打开",
            Category = MenuCategory.Background, Command = """cmd.exe /k cd /d "%V" """,
            IconPath = @"%SystemRoot%\System32\cmd.exe" },
        new() { Name = "在此处打开 PowerShell", Description = "在当前目录打开 PowerShell", Group = "在此处打开",
            Category = MenuCategory.Background,
            Command = """powershell.exe -NoExit -Command "Set-Location -LiteralPath '%V'" """,
            IconPath = @"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" },
        new() { Name = "在此处打开 Windows 终端", Description = "用 Windows Terminal 打开当前目录", Group = "在此处打开",
            Category = MenuCategory.Background, Command = """wt.exe -d "%V" """,
            IconPath = @"%LOCALAPPDATA%\Microsoft\WindowsApps\wt.exe" },
        new() { Name = "在终端中打开此文件夹", Description = "对文件夹右键，在终端中打开", Group = "在此处打开",
            Category = MenuCategory.Directory, Command = """wt.exe -d "%1" """,
            IconPath = @"%LOCALAPPDATA%\Microsoft\WindowsApps\wt.exe" },

        // ===== 系统工具 =====
        new() { Name = "打开环境变量", Description = "打开系统环境变量编辑窗口", Group = "系统工具",
            Category = MenuCategory.Background, Command = "rundll32.exe sysdm.cpl,EditEnvironmentVariables",
            IconPath = @"%SystemRoot%\System32\sysdm.cpl" },
        new() { Name = "编辑 hosts 文件", Description = "用记事本打开 hosts（保存需要管理员权限）", Group = "系统工具",
            Category = MenuCategory.Background, Command = @"notepad.exe %SystemRoot%\System32\drivers\etc\hosts",
            IconPath = @"%SystemRoot%\System32\notepad.exe" },
        new() { Name = "打开组策略编辑器", Description = "打开本地组策略 gpedit.msc（家庭版无此功能）", Group = "系统工具",
            Category = MenuCategory.Background, Command = "gpedit.msc",
            IconPath = @"%SystemRoot%\System32\shell32.dll,27" },
        new() { Name = "打开注册表编辑器", Description = "打开 regedit", Group = "系统工具",
            Category = MenuCategory.Background, Command = "regedit.exe",
            IconPath = @"%SystemRoot%\regedit.exe" },
        new() { Name = "打开设备管理器", Description = "打开 devmgmt.msc", Group = "系统工具",
            Category = MenuCategory.Background, Command = "devmgmt.msc",
            IconPath = @"%SystemRoot%\System32\devmgr.dll,0" },
        new() { Name = "打开计算机管理", Description = "打开 compmgmt.msc", Group = "系统工具",
            Category = MenuCategory.Background, Command = "compmgmt.msc",
            IconPath = @"%SystemRoot%\System32\mycomput.dll,2" },
        new() { Name = "打开服务", Description = "打开 services.msc", Group = "系统工具",
            Category = MenuCategory.Background, Command = "services.msc",
            IconPath = @"%SystemRoot%\System32\shell32.dll,166" },
        new() { Name = "打开磁盘管理", Description = "打开 diskmgmt.msc", Group = "系统工具",
            Category = MenuCategory.Background, Command = "diskmgmt.msc",
            IconPath = @"%SystemRoot%\System32\diskmgmt.msc" },
        new() { Name = "打开任务计划程序", Description = "打开 taskschd.msc", Group = "系统工具",
            Category = MenuCategory.Background, Command = "taskschd.msc",
            IconPath = @"%SystemRoot%\System32\taskschd.msc" },
        new() { Name = "打开事件查看器", Description = "打开 eventvwr.msc", Group = "系统工具",
            Category = MenuCategory.Background, Command = "eventvwr.msc",
            IconPath = @"%SystemRoot%\System32\eventvwr.exe" },
        new() { Name = "打开控制面板", Description = "打开经典控制面板", Group = "系统工具",
            Category = MenuCategory.Background, Command = "control.exe",
            IconPath = @"%SystemRoot%\System32\control.exe" },
        new() { Name = "打开任务管理器", Description = "打开 taskmgr", Group = "系统工具",
            Category = MenuCategory.Background, Command = "taskmgr.exe",
            IconPath = @"%SystemRoot%\System32\taskmgr.exe" },
        new() { Name = "打开程序和功能", Description = "卸载或更改程序 appwiz.cpl", Group = "系统工具",
            Category = MenuCategory.Background, Command = "appwiz.cpl",
            IconPath = @"%SystemRoot%\System32\appwiz.cpl" },
        new() { Name = "打开网络连接", Description = "网络适配器设置 ncpa.cpl", Group = "系统工具",
            Category = MenuCategory.Background, Command = "ncpa.cpl",
            IconPath = @"%SystemRoot%\System32\ncpa.cpl" },
        new() { Name = "打开声音设置", Description = "播放/录音设备 mmsys.cpl", Group = "系统工具",
            Category = MenuCategory.Background, Command = "mmsys.cpl",
            IconPath = @"%SystemRoot%\System32\mmsys.cpl" },
        new() { Name = "关于 Windows", Description = "查看 Windows 版本 winver", Group = "系统工具",
            Category = MenuCategory.Background, Command = "winver.exe",
            IconPath = @"%SystemRoot%\System32\winver.exe" },

        // ===== 常用程序 =====
        new() { Name = "打开记事本", Description = "启动记事本", Group = "常用程序",
            Category = MenuCategory.Background, Command = "notepad.exe",
            IconPath = @"%SystemRoot%\System32\notepad.exe" },
        new() { Name = "打开画图", Description = "启动画图 mspaint", Group = "常用程序",
            Category = MenuCategory.Background, Command = "mspaint.exe",
            IconPath = @"%SystemRoot%\System32\mspaint.exe" },
        new() { Name = "打开计算器", Description = "启动计算器", Group = "常用程序",
            Category = MenuCategory.Background, Command = "calc.exe",
            IconPath = @"%SystemRoot%\System32\calc.exe" },
        new() { Name = "打开写字板", Description = "启动写字板 write", Group = "常用程序",
            Category = MenuCategory.Background, Command = "write.exe",
            IconPath = @"%SystemRoot%\System32\write.exe" },
        new() { Name = "打开远程桌面", Description = "启动 mstsc", Group = "常用程序",
            Category = MenuCategory.Background, Command = "mstsc.exe",
            IconPath = @"%SystemRoot%\System32\mstsc.exe" },
        new() { Name = "打开字符映射表", Description = "启动 charmap", Group = "常用程序",
            Category = MenuCategory.Background, Command = "charmap.exe",
            IconPath = @"%SystemRoot%\System32\charmap.exe" },
        new() { Name = "屏幕截图", Description = "打开截图工具 (Win+Shift+S)", Group = "常用程序",
            Category = MenuCategory.Background, Command = "explorer.exe ms-screenclip:",
            IconPath = @"%SystemRoot%\System32\SnippingTool.exe" },

        // ===== 文件操作 =====
        new() { Name = "复制文件路径", Description = "将选中文件的路径复制到剪贴板", Group = "文件操作",
            Category = MenuCategory.File, Command = @"cmd.exe /c echo ""%1"" | clip",
            IconPath = @"%SystemRoot%\System32\imageres.dll,167" },
        new() { Name = "用记事本打开", Description = "强制用记事本打开任意文件", Group = "文件操作",
            Category = MenuCategory.File, Command = @"notepad.exe ""%1""",
            IconPath = @"%SystemRoot%\System32\notepad.exe" },
        new() { Name = "用画图打开", Description = "强制用画图打开任意文件", Group = "文件操作",
            Category = MenuCategory.File, Command = @"mspaint.exe ""%1""",
            IconPath = @"%SystemRoot%\System32\mspaint.exe" },
    };
}

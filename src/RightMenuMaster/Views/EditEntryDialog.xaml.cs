using RightMenuMaster.Helpers;
using RightMenuMaster.Imaging;
using RightMenuMaster.Models;
using RightMenuMaster.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace RightMenuMaster.Views;

/// <summary>
/// 新增/编辑右键菜单项对话框。
/// </summary>
public partial class EditEntryDialog : Window
{
    private record CategoryOption(MenuCategory Category, string Name)
    {
        public override string ToString() => Name;
    }

    private enum CommandKind { Program, Cmd, PowerShell }

    private sealed record CommandTypeOption(CommandKind Kind, string Name)
    {
        public override string ToString() => Name;
    }

    private readonly MenuItemModel _item;
    private readonly bool _isNew;
    private string? _iconLocation;

    public EditEntryDialog(MenuItemModel item, bool isNew)
    {
        InitializeComponent();
        Icon = MainWindow.MakeAppIcon();

        _item = item;
        _isNew = isNew;

        DialogTitle.Text = isNew ? "新增菜单项" : "编辑菜单项";
        Title = isNew ? "新增菜单项 - 右键菜单管家" : $"编辑「{item.DisplayTitle}」 - 右键菜单管家";

        // 作用域
        CategoryCombo.ItemsSource = new[]
        {
            new CategoryOption(MenuCategory.Directory, "目录 —— 右键文件夹时"),
            new CategoryOption(MenuCategory.Background, "目录背景 —— 文件夹空白处右键时"),
            new CategoryOption(MenuCategory.Folder, "文件夹 —— 所有文件夹对象"),
            new CategoryOption(MenuCategory.File, "文件 —— 所有文件"),
            new CategoryOption(MenuCategory.Extension, "指定扩展名 —— 特定类型文件"),
        };
        CategoryCombo.SelectedItem = CategoryCombo.ItemsSource
            .Cast<CategoryOption>()
            .FirstOrDefault(o => o.Category == item.Category);

        if (!isNew)
        {
            // 编辑现有项时不允许改作用域（否则键位置会混乱）；AI 填写仅用于创建
            CategoryCombo.IsEnabled = false;
            ExtBox.IsEnabled = false;
            AiPanel.Visibility = Visibility.Collapsed;
            Width = 600;
        }

        ExtBox.Text = item.Extension ?? string.Empty;
        TitleBox.Text = item.Title;

        // 命令类型（可执行程序 / CMD / PowerShell）
        CommandTypeCombo.ItemsSource = new[]
        {
            new CommandTypeOption(CommandKind.Program, "可执行程序"),
            new CommandTypeOption(CommandKind.Cmd, "CMD 命令"),
            new CommandTypeOption(CommandKind.PowerShell, "PowerShell 命令"),
        };
        var kind = DetectCommandKind(item.Command, out var payload);
        CommandTypeCombo.SelectedItem = CommandTypeCombo.ItemsSource
            .Cast<CommandTypeOption>()
            .First(o => o.Kind == kind);

        ProgramBox.Text = string.Empty;
        ArgsBox.Text = string.Empty;
        if (kind == CommandKind.Program)
        {
            CommandLine.Split(item.Command, out var prog, out var args);
            ProgramBox.Text = prog;
            ArgsBox.Text = args;
        }
        else
        {
            ScriptBox.Text = payload;
        }
        UpdateCommandRows();

        _iconLocation = item.IconPath;
        UpdateIconPreview();

        PositionCombo.ItemsSource = new[]
        {
            new PositionOption("", "默认（按名称排序）"),
            new PositionOption("Top", "顶部"),
            new PositionOption("Bottom", "底部"),
        };
        PositionCombo.SelectedItem = PositionCombo.ItemsSource
            .Cast<PositionOption>()
            .FirstOrDefault(o => o.Value == (item.Position ?? string.Empty))
            ?? PositionCombo.ItemsSource.Cast<PositionOption>().First();

        ShiftCheck.IsChecked = item.ShiftExtended;
        NoWorkDirCheck.IsChecked = item.NoWorkingDirectory;

        UpdateExtRowVisibility();
    }

    private sealed record PositionOption(string Value, string Name)
    {
        public override string ToString() => Name;
    }

    // ---------------------------------------------------------------- 命令拆分/组合

    /// <summary>
    /// 根据已有命令自动识别类型：cmd.exe /c … → CMD；
    /// powershell.exe -Command / -EncodedCommand … → PowerShell；其余按程序处理。
    /// </summary>
    private static CommandKind DetectCommandKind(string command, out string payload)
    {
        payload = string.Empty;
        if (string.IsNullOrWhiteSpace(command)) return CommandKind.Program;

        CommandLine.Split(command.Trim(), out var prog, out var args);

        if (CommandLine.TryParseCmd(prog, args, out var cmdScript))
        {
            payload = cmdScript;
            return CommandKind.Cmd;
        }

        if (CommandLine.TryParsePowerShell(prog, args, out var psScript))
        {
            payload = psScript;
            return CommandKind.PowerShell;
        }

        return CommandKind.Program;
    }

    // ---------------------------------------------------------------- 事件

    private void CategoryCombo_Changed(object sender, SelectionChangedEventArgs e)
        => UpdateExtRowVisibility();

    private void UpdateExtRowVisibility()
    {
        if (ExtRow == null) return;
        bool isExt = (CategoryCombo.SelectedItem as CategoryOption)?.Category == MenuCategory.Extension;
        ExtRow.Height = isExt ? GridLength.Auto : new GridLength(0);
    }

    private void CommandTypeCombo_Changed(object sender, SelectionChangedEventArgs e)
        => UpdateCommandRows();

    /// <summary>按命令类型切换「程序+参数」与「命令内容」行的显示。</summary>
    private void UpdateCommandRows()
    {
        if (ProgramRow == null || ArgsRow == null || ScriptRow == null) return;
        var kind = (CommandTypeCombo.SelectedItem as CommandTypeOption)?.Kind ?? CommandKind.Program;
        bool isProgram = kind == CommandKind.Program;

        ProgramRow.Height = isProgram ? GridLength.Auto : new GridLength(0);
        ArgsRow.Height = isProgram ? GridLength.Auto : new GridLength(0);
        ScriptRow.Height = isProgram ? new GridLength(0) : GridLength.Auto;

        if (ScriptHint != null)
        {
            ScriptHint.Text = kind switch
            {
                CommandKind.Cmd =>
                    "输入 CMD 命令，将以 cmd.exe /c 执行。可用占位符：%1 = 选中项，%V = 当前目录。" +
                    "示例：dir \"%1\" & pause",
                CommandKind.PowerShell =>
                    "输入 PowerShell 语句，用 powershell.exe -NoProfile 执行。" +
                    "不含占位符时会以 -EncodedCommand 保存，引号与转义无需担心。" +
                    "示例：Get-ChildItem \"%1\" | Out-GridView",
                _ => string.Empty,
            };
        }
    }

    private void PickIcon_Click(object sender, RoutedEventArgs e)
    {
        var picker = new IconPickerDialog(_iconLocation) { Owner = this };
        if (picker.ShowDialog() == true)
        {
            _iconLocation = picker.SelectedIconLocation;
            UpdateIconPreview();
        }
    }

    private void ClearIcon_Click(object sender, RoutedEventArgs e)
    {
        _iconLocation = null;
        UpdateIconPreview();
    }

    private void UpdateIconPreview()
    {
        IconPreview.Source = IconService.ResolveIcon(_iconLocation);
        IconPathText.Text = string.IsNullOrEmpty(_iconLocation)
            ? "（未设置，使用系统默认图标）"
            : _iconLocation;
    }

    private void BrowseProgram_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择程序",
            Filter = "可执行文件|*.exe;*.bat;*.cmd;*.msi|所有文件|*.*",
        };
        if (dlg.ShowDialog(this) == true)
            ProgramBox.Text = dlg.FileName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var category = (CategoryCombo.SelectedItem as CategoryOption)?.Category ?? _item.Category;
        var kind = (CommandTypeCombo.SelectedItem as CommandTypeOption)?.Kind ?? CommandKind.Program;

        // 校验
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show(this, "请填写菜单标题。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            TitleBox.Focus();
            return;
        }
        if (kind == CommandKind.Program && string.IsNullOrWhiteSpace(ProgramBox.Text))
        {
            MessageBox.Show(this, "请填写要运行的程序。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            ProgramBox.Focus();
            return;
        }
        if (kind != CommandKind.Program && string.IsNullOrWhiteSpace(ScriptBox.Text))
        {
            MessageBox.Show(this, "请填写命令内容。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            ScriptBox.Focus();
            return;
        }

        string? ext = null;
        if (category == MenuCategory.Extension)
        {
            ext = MenuCategoryInfo.NormalizeExt(ExtBox.Text);
            if (string.IsNullOrEmpty(ext))
            {
                MessageBox.Show(this, "请填写扩展名，例如 .txt", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                ExtBox.Focus();
                return;
            }
        }

        _item.Category = category;
        _item.Extension = ext;
        _item.Title = TitleBox.Text.Trim();
        _item.Command = kind switch
        {
            CommandKind.Cmd => CommandLine.BuildCmd(ScriptBox.Text),
            CommandKind.PowerShell => CommandLine.BuildPowerShell(ScriptBox.Text),
            _ => CommandLine.Build(ProgramBox.Text, ArgsBox.Text),
        };
        _item.IconPath = _iconLocation;
        _item.Position = (PositionCombo.SelectedItem as PositionOption)?.Value;
        if (string.IsNullOrEmpty(_item.Position)) _item.Position = null;
        _item.ShiftExtended = ShiftCheck.IsChecked == true;
        _item.NoWorkingDirectory = NoWorkDirCheck.IsChecked == true;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    // ---------------------------------------------------------------- AI 智能填写

    private void AiSettings_Click(object sender, RoutedEventArgs e)
    {
        new LlmSettingsDialog { Owner = this }.ShowDialog();
    }

    private CancellationTokenSource? _aiCts;

    private async void AiGenerate_Click(object sender, RoutedEventArgs e)
    {
        var settings = LlmSettings.Load();
        if (!settings.IsConfigured)
        {
            if (new LlmSettingsDialog { Owner = this }.ShowDialog() != true) return;
            settings = LlmSettings.Load();
            if (!settings.IsConfigured) return;
        }

        if (string.IsNullOrWhiteSpace(AiDescBox.Text))
        {
            AiStatusText.Text = "请先输入功能描述。";
            return;
        }

        BtnAiGenerate.IsEnabled = false;
        AiGenerateText.Text = "生成中…";
        AiStatusText.Text = "正在调用大模型…";

        _aiCts?.Cancel();
        _aiCts?.Dispose();
        _aiCts = new CancellationTokenSource();
        var token = _aiCts.Token;

        try
        {
            var draft = await LlmService.GenerateAsync(AiDescBox.Text.Trim(), settings, token);
            if (token.IsCancellationRequested) return; // 对话框已关闭，别再回写控件
            ApplyDraft(draft);
            AiStatusText.Text = "已按模型返回内容填写，请检查各项后保存。";
        }
        catch (OperationCanceledException)
        {
            return; // 用户关闭了对话框
        }
        catch (Exception ex)
        {
            AiStatusText.Text = "生成失败：" + ex.Message;
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                BtnAiGenerate.IsEnabled = true;
                AiGenerateText.Text = "生成菜单定义";
            }
        }
    }

    /// <summary>对话框关闭时取消仍在进行的请求（默认超时 90 秒，不能让它继续回写已关闭的窗口）。</summary>
    protected override void OnClosed(EventArgs e)
    {
        _aiCts?.Cancel();
        _aiCts?.Dispose();
        _aiCts = null;
        base.OnClosed(e);
    }

    /// <summary>把模型返回的草稿应用到表单各控件。</summary>
    private void ApplyDraft(MenuDraft d)
    {
        if (!string.IsNullOrWhiteSpace(d.Title))
            TitleBox.Text = d.Title.Trim();

        if (_isNew && !string.IsNullOrWhiteSpace(d.Category))
        {
            var cat = NormalizeCategory(d.Category);
            if (cat is not null)
            {
                CategoryCombo.SelectedItem = CategoryCombo.ItemsSource
                    .Cast<CategoryOption>().FirstOrDefault(o => o.Category == cat);
                UpdateExtRowVisibility();
            }
        }
        if (!string.IsNullOrWhiteSpace(d.Extension))
            ExtBox.Text = MenuCategoryInfo.NormalizeExt(d.Extension);

        var kindText = (d.CommandKind ?? string.Empty).Trim().ToLowerInvariant();
        CommandKind? kind = kindText switch
        {
            "cmd" => CommandKind.Cmd,
            "powershell" or "pwsh" or "ps" => CommandKind.PowerShell,
            "program" or "exe" or "app" => CommandKind.Program,
            _ => null,
        };
        kind ??= !string.IsNullOrWhiteSpace(d.Program) && string.IsNullOrWhiteSpace(d.Script)
            ? CommandKind.Program
            : !string.IsNullOrWhiteSpace(d.Script) ? CommandKind.Cmd : null;

        if (kind is not null)
        {
            CommandTypeCombo.SelectedItem = CommandTypeCombo.ItemsSource
                .Cast<CommandTypeOption>().First(o => o.Kind == kind);
            UpdateCommandRows();
            switch (kind)
            {
                case CommandKind.Program:
                    if (!string.IsNullOrWhiteSpace(d.Program)) ProgramBox.Text = d.Program.Trim();
                    ArgsBox.Text = d.Args?.Trim() ?? string.Empty;
                    break;
                default:
                    if (!string.IsNullOrWhiteSpace(d.Script)) ScriptBox.Text = d.Script.Trim();
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(d.Icon))
        {
            var def = BuiltinIcons.All.FirstOrDefault(i => i.Name == d.Icon.Trim());
            if (def is not null)
            {
                _iconLocation = IconService.SaveBuiltinIcon(def);
                UpdateIconPreview();
            }
        }

        var pos = (d.Position ?? string.Empty).Trim();
        if (pos.Length > 0 && char.IsLower(pos[0])) pos = char.ToUpper(pos[0]) + pos[1..];
        if (pos is "" or "Top" or "Bottom")
        {
            PositionCombo.SelectedItem = PositionCombo.ItemsSource
                .Cast<PositionOption>().FirstOrDefault(o => o.Value == pos)
                ?? PositionCombo.SelectedItem;
        }

        if (d.ShiftExtended is bool shift) ShiftCheck.IsChecked = shift;
        if (d.NoWorkingDirectory is bool nowd) NoWorkDirCheck.IsChecked = nowd;
    }

    private static MenuCategory? NormalizeCategory(string s)
    {
        s = s.Trim();
        if (Enum.TryParse<MenuCategory>(s, true, out var c)) return c;
        return s switch
        {
            "目录" => MenuCategory.Directory,
            "背景" or "目录背景" => MenuCategory.Background,
            "文件夹" => MenuCategory.Folder,
            "文件" => MenuCategory.File,
            "扩展名" or "指定扩展名" => MenuCategory.Extension,
            _ => null,
        };
    }
}

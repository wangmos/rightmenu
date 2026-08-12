using RightMenuMaster.Helpers;
using RightMenuMaster.Models;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RightMenuMaster.Views.Pages;

/// <summary>
/// 快捷模板页：内置常用系统功能，可先预览运行效果再生成右键菜单项。
/// </summary>
public partial class TemplatesPage : UserControl
{
    private IShellHost? Host => Window.GetWindow(this) as IShellHost;

    public TemplatesPage()
    {
        InitializeComponent();
        TemplateList.ItemsSource = BuiltinTemplates.All;
    }

    private void TemplateCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: BuiltinTemplate tpl }) return;

        var item = new MenuItemModel
        {
            Category = tpl.Category,
            Extension = null,
            Title = tpl.Name,
            Command = Environment.ExpandEnvironmentVariables(tpl.Command),
            IconPath = tpl.IconPath == null ? null : Environment.ExpandEnvironmentVariables(tpl.IconPath),
            ShiftExtended = tpl.ShiftExtended,
            Source = RegistrySource.CurrentUser,
        };

        // 复用菜单页的保存流程（同一个编辑对话框、同样的错误处理）
        Host?.MenuList.SaveViaDialog(item, isNew: true);
    }

    /// <summary>
    /// 模板卡片上的「运行看效果」：把 %V/%1 替换为示例路径后实际执行命令，供用户预览。
    /// </summary>
    private void TemplateRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: BuiltinTemplate tpl }) return;
        e.Handled = true;

        try
        {
            var command = Environment.ExpandEnvironmentVariables(tpl.Command);

            // %V = 当前目录、%1 = 选中对象。一律用临时目录里的示例文件演示，
            // 不拿用户桌面上的真实文件当参数（可能是隐私文件）
            var dir = EnsureSampleDir();
            var target = tpl.Category is MenuCategory.File or MenuCategory.Extension
                ? SampleTargetFile(dir)
                : dir;
            command = command.Replace("%V", dir).Replace("%1", target);

            CommandLine.Split(command, out var program, out var args);
            Process.Start(new ProcessStartInfo
            {
                FileName = program,
                Arguments = args,
                UseShellExecute = true,
            });
            Host?.ShowToast($"已运行模板「{tpl.Name}」，看看效果吧");
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this)!, "运行失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>模板预览用的示例目录（临时目录下，内容可随意被模板命令操作）。</summary>
    private static string EnsureSampleDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RightMenuMaster_模板预览");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>为文件类模板准备一个示例文件，避免拿用户的真实文件做演示。</summary>
    private static string SampleTargetFile(string dir)
    {
        var file = Path.Combine(dir, "示例文件.txt");
        try
        {
            if (!File.Exists(file))
                File.WriteAllText(file, "这是「右键菜单管家」用于预览模板效果的示例文件，可以随意删除。");
        }
        catch { /* 忽略，交给命令自己报错 */ }
        return file;
    }
}

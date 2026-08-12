using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RightMenuMaster.Views;

/// <summary>
/// 密码速记框：置顶浮动小窗口，临时保存密码，支持明文切换、复制、随机生成。
/// </summary>
public partial class PasswordNoteWindow : Window
{
    private bool _showPlain;
    private DispatcherTimer? _clipboardTimer;

    private const string CharSet =
        "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$%&*_+-=";

    public PasswordNoteWindow()
    {
        InitializeComponent();
        Icon = MainWindow.MakeAppIcon();
        LenCombo.ItemsSource = new[] { 8, 12, 16, 24, 32 };
    }

    private string CurrentText => _showPlain ? PlainBox.Text : MaskedBox.Password;

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        _showPlain = !_showPlain;
        if (_showPlain)
        {
            PlainBox.Text = MaskedBox.Password;
            MaskedBox.Visibility = Visibility.Collapsed;
            PlainBox.Visibility = Visibility.Visible;
            PlainBox.Focus();
            PlainBox.CaretIndex = PlainBox.Text.Length;
            ToggleText.Text = "隐藏明文";
        }
        else
        {
            MaskedBox.Password = PlainBox.Text;
            PlainBox.Visibility = Visibility.Collapsed;
            MaskedBox.Visibility = Visibility.Visible;
            MaskedBox.Focus();
            ToggleText.Text = "显示明文";
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var text = CurrentText;
        if (string.IsNullOrEmpty(text))
        {
            MessageBox.Show(this, "内容为空。", "密码速记框", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            Clipboard.SetText(text);
            Title = "密码速记框 - 已复制到剪贴板（30 秒后自动清空）";
            ScheduleClipboardClear(text);
        }
        catch
        {
            // 剪贴板被占用时忽略
        }
    }

    /// <summary>
    /// 30 秒后清空剪贴板，避免密码长期留在里面被其他程序读走。
    /// 只有内容仍是刚复制的那段时才清，不影响用户后来复制的别的东西。
    /// </summary>
    private void ScheduleClipboardClear(string copied)
    {
        _clipboardTimer?.Stop();
        _clipboardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _clipboardTimer.Tick += (_, _) =>
        {
            _clipboardTimer?.Stop();
            try
            {
                if (Clipboard.ContainsText() && Clipboard.GetText() == copied)
                {
                    Clipboard.Clear();
                    Title = "密码速记框 - 剪贴板已清空";
                }
            }
            catch { /* 剪贴板被占用时忽略 */ }
        };
        _clipboardTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _clipboardTimer?.Stop();
        _clipboardTimer = null;
        base.OnClosed(e);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        PlainBox.Clear();
        MaskedBox.Clear();
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        int length = (int)(LenCombo.SelectedItem ?? 12);
        var chars = new char[length];
        var bytes = RandomNumberGenerator.GetBytes(length * 4);
        for (int i = 0; i < length; i++)
        {
            uint value = BitConverter.ToUInt32(bytes, i * 4);
            chars[i] = CharSet[(int)(value % CharSet.Length)];
        }
        var password = new string(chars);

        if (_showPlain) PlainBox.Text = password;
        else MaskedBox.Password = password;
    }

    private void TopCheck_Changed(object sender, RoutedEventArgs e)
        => Topmost = TopCheck.IsChecked == true;
}

using RightMenuMaster.Services;
using System.Windows;

namespace RightMenuMaster.Views;

/// <summary>LLM API 设置对话框（地址 / Key / 模型）。</summary>
public partial class LlmSettingsDialog : Window
{
    public LlmSettingsDialog()
    {
        InitializeComponent();
        Icon = MainWindow.MakeAppIcon();

        var s = LlmSettings.Load();
        BaseUrlBox.Text = s.BaseUrl;
        KeyBox.Password = s.Key;
        ModelBox.Text = s.Model;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BaseUrlBox.Text))
        {
            MessageBox.Show(this, "请填写 API 地址。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            BaseUrlBox.Focus();
            return;
        }
        if (string.IsNullOrWhiteSpace(KeyBox.Password))
        {
            MessageBox.Show(this, "请填写 API Key。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            KeyBox.Focus();
            return;
        }
        if (string.IsNullOrWhiteSpace(ModelBox.Text))
        {
            MessageBox.Show(this, "请填写模型名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            ModelBox.Focus();
            return;
        }

        try
        {
            new LlmSettings
            {
                BaseUrl = BaseUrlBox.Text.Trim(),
                Key = KeyBox.Password.Trim(),
                Model = ModelBox.Text.Trim(),
            }.Save();
        }
        catch (Exception ex)
        {
            // 目录只读、磁盘满等；不能让它冒到全局未处理异常
            MessageBox.Show(this, "保存设置失败：\n" + ex.Message, "右键菜单管家",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

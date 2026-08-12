using System.Reflection;
using System.Windows;

namespace RightMenuMaster.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        Icon = MainWindow.MakeAppIcon();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"版本 {version?.Major}.{version?.Minor}.{version?.Build}";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

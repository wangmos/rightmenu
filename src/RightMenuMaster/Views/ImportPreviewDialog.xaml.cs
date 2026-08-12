using RightMenuMaster.Services;
using System.Windows;

namespace RightMenuMaster.Views;

/// <summary>
/// 导入确认对话框：导入的命令会被写进右键菜单并在点击时执行，
/// 因此先把条目摊开给用户看、逐条勾选，再决定同名项是跳过还是覆盖。
/// </summary>
public partial class ImportPreviewDialog : Window
{
    private readonly List<ExportImportService.ImportCandidate> _candidates;

    /// <summary>用户确认后要导入的项。</summary>
    public IReadOnlyList<ExportImportService.ImportCandidate> Confirmed => _candidates;

    /// <summary>同名项是否覆盖（否则跳过）。</summary>
    public bool OverwriteExisting => OverwriteRadio.IsChecked == true;

    public ImportPreviewDialog(List<ExportImportService.ImportCandidate> candidates, string fileName)
    {
        InitializeComponent();
        Icon = MainWindow.MakeAppIcon();

        _candidates = candidates;
        ItemList.ItemsSource = _candidates;

        var existing = _candidates.Count(c => c.AlreadyExists);
        SubtitleText.Text = $"来自 {fileName}，共 {_candidates.Count} 项"
            + (existing > 0 ? $"，其中 {existing} 项与现有菜单同名。" : "。")
            + " 导入的命令会写入右键菜单并在点击时执行，请先确认来源可信。";

        UpdateCount();
    }

    private void UpdateCount()
    {
        var n = _candidates.Count(c => c.Selected);
        CountText.Text = $"已选 {n} / {_candidates.Count} 项";
        ImportButtonText.Text = n > 0 ? $"导入 {n} 项" : "导入";
        BtnImport.IsEnabled = n > 0;
    }

    private void ItemCheck_Changed(object sender, RoutedEventArgs e) => UpdateCount();

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        var target = _candidates.Any(c => !c.Selected);
        foreach (var c in _candidates) c.Selected = target;

        // ImportCandidate 无变更通知，重设 ItemsSource 让勾选框刷新
        ItemList.ItemsSource = null;
        ItemList.ItemsSource = _candidates;
        UpdateCount();
    }

    private void Import_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

using RightMenuMaster.Views.Pages;

namespace RightMenuMaster.Views;

/// <summary>
/// 各页面（UserControl）对宿主窗口的最小依赖。
/// 页面通过 <c>Window.GetWindow(this) as IShellHost</c> 拿到它，
/// 避免直接依赖 MainWindow 的具体实现。
/// </summary>
internal interface IShellHost
{
    /// <summary>底部轻提示。</summary>
    void ShowToast(string message);

    /// <summary>提示用户以管理员身份重启（操作因权限失败时调用）。</summary>
    void PromptElevation();

    /// <summary>当前是否已是管理员。</summary>
    bool IsAdmin { get; }

    /// <summary>菜单列表页。模板页生成菜单项时需要复用它的保存流程。</summary>
    MenuListPage MenuList { get; }
}

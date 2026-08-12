using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RightMenuMaster.ViewModels;

/// <summary>
/// ViewModel 基类，实现 INotifyPropertyChanged。
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// 断开所有变更通知订阅者。MemberwiseClone 会把事件字段一并复制过去，
    /// 克隆对象必须清掉，否则它的属性变化会推给原对象的绑定。
    /// </summary>
    protected void ClearPropertyChangedSubscribers() => PropertyChanged = null;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BookA.Mvvm;

/// <summary>
/// プロパティの変更を通知するビューモデルの基底クラス。
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 値が変わったときだけフィールドを更新し、変更を通知する。
    /// </summary>
    /// <returns>値が変わって通知したら true。</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// 値が変わったかどうかにかかわらず変更を通知する。
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

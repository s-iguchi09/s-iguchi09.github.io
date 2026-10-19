using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BookA.Mvvm;

namespace Ch05.ValidationNotDisplayed;

/// <summary>
/// 検証の結果をプロパティ名ごとに持つビューモデルの基底クラス（INotifyDataErrorInfo）。
/// INotifyDataErrorInfo は ValidatesOnNotifyDataErrors が既定で True なので、バインドに何も書かなくても検証に参加する。
/// </summary>
public abstract class ValidatableViewModel : ObservableObject, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = [];

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public bool HasErrors => _errors.Count > 0;

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName is not null && _errors.TryGetValue(propertyName, out List<string>? list) ? list : Array.Empty<string>();

    /// <summary>値を設定し、変わったらそのプロパティを検証する。</summary>
    protected void SetAndValidate<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            Validate(propertyName!);
        }
    }

    /// <summary>
    /// プロパティのエラーを置き換える。エラーが無くなったときも ErrorsChanged を発行する（発行しないと赤枠が残る）。
    /// </summary>
    protected void SetErrors(string propertyName, IReadOnlyList<string> messages)
    {
        if (messages.Count == 0)
        {
            _errors.Remove(propertyName);
        }
        else
        {
            _errors[propertyName] = [.. messages];
        }

        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        OnPropertyChanged(nameof(HasErrors));
    }

    protected abstract void Validate(string propertyName);
}

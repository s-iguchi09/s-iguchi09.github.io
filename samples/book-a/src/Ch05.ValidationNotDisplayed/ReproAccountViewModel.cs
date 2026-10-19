using System.ComponentModel;
using BookA.Mvvm;

namespace Ch05.ValidationNotDisplayed;

/// <summary>
/// 再現版のビューモデル。IDataErrorInfo で「名前は必須」を返す。
/// ただしビューのバインドに ValidatesOnDataErrors=True が無いため、この検証は一度も呼ばれず、赤枠も出ない。
/// </summary>
public sealed class ReproAccountViewModel : ObservableObject, IDataErrorInfo
{
    private string _name = "";

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Error => "";

    public string this[string columnName] =>
        columnName == nameof(Name) && string.IsNullOrWhiteSpace(Name) ? "名前は必須です。" : "";
}

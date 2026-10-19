namespace Ch05.ValidationNotDisplayed;

/// <summary>
/// 修正版のビューモデル。名前の必須と長さを INotifyDataErrorInfo で検証する。
/// 起動直後から HasErrors が正しいよう、コンストラクターでも検証する。
/// </summary>
public sealed class AccountViewModel : ValidatableViewModel
{
    /// <summary>名前の上限の文字数。</summary>
    public const int MaxNameLength = 20;

    private string _name = "";

    public AccountViewModel() => Validate(nameof(Name));

    public string Name
    {
        get => _name;
        set => SetAndValidate(ref _name, value);
    }

    protected override void Validate(string propertyName)
    {
        if (propertyName != nameof(Name))
        {
            return;
        }

        List<string> messages = [];
        if (string.IsNullOrWhiteSpace(Name))
        {
            messages.Add("名前は必須です。");
        }
        else if (Name.Length > MaxNameLength)
        {
            messages.Add($"名前は {MaxNameLength} 文字以内にしてください。");
        }

        SetErrors(nameof(Name), messages);
    }
}

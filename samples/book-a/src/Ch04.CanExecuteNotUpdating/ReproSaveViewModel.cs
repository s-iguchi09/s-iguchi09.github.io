using BookA.Mvvm;

namespace Ch04.CanExecuteNotUpdating;

/// <summary>
/// 再現版のビューモデル。名前が入力されたら保存できるはずだが、コマンドが CanExecuteChanged を発行しないため、
/// 保存ボタンは最初の「無効」のまま変わらない。
/// </summary>
public sealed class ReproSaveViewModel : ObservableObject
{
    private string _name = "";
    private string _lastSaved = "（まだ保存していない）";

    public ReproSaveViewModel()
    {
        SaveCommand = new NeverRaisingCommand(Save, () => !string.IsNullOrEmpty(Name));
    }

    public NeverRaisingCommand SaveCommand { get; }

    /// <summary>名前。空でなければ保存できる。</summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string LastSaved
    {
        get => _lastSaved;
        private set => SetProperty(ref _lastSaved, value);
    }

    private void Save() => LastSaved = $"\"{Name}\" を保存した";
}

using BookA.Mvvm;

namespace Ch04.CanExecuteNotUpdating;

/// <summary>
/// 修正版のビューモデル。CanExecute が依存するプロパティ（Name）を変えた直後に、
/// コマンドの <see cref="RelayCommand.RaiseCanExecuteChanged"/> を呼ぶ。
/// 問い合わせ直すのは保存のコマンドだけで、その時点もビューモデルが決める。
/// </summary>
public sealed class SaveViewModel : ObservableObject
{
    private string _name = "";
    private string _lastSaved = "（まだ保存していない）";

    public SaveViewModel()
    {
        SaveCommand = new RelayCommand(Save, () => !string.IsNullOrEmpty(Name));
    }

    public RelayCommand SaveCommand { get; }

    /// <summary>名前。空でなければ保存できる。変わったら保存のコマンドに問い合わせ直させる。</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string LastSaved
    {
        get => _lastSaved;
        private set => SetProperty(ref _lastSaved, value);
    }

    private void Save() => LastSaved = $"\"{Name}\" を保存した";
}

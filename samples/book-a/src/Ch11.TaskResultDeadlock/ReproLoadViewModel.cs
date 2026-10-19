using BookA.Mvvm;

namespace Ch11.TaskResultDeadlock;

/// <summary>
/// 再現版のビューモデル。同期のコマンドの中で LoadAsync().Result を待つ。
/// UI スレッドが Result で止まり、LoadAsync の await の後（UI スレッドで再開する部分）が走れないため、どちらも進まずに固まる。
/// </summary>
public sealed class ReproLoadViewModel : ObservableObject
{
    private string _status = "";

    public ReproLoadViewModel()
    {
        LoadCommand = new RelayCommand(Load);
    }

    public RelayCommand LoadCommand { get; }

    /// <summary>読み込んだ値。読み込みが終わるまで空。</summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    private void Load() => Status = DataSource.LoadAsync().Result.ToString(); // ここから戻らない
}

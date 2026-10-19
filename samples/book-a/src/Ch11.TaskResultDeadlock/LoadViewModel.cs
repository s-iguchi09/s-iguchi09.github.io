using BookA.Mvvm;

namespace Ch11.TaskResultDeadlock;

/// <summary>
/// 修正版のビューモデル。Result を使わず、AsyncRelayCommand の中で await する。
/// 待っている間も UI スレッドは処理を進め、読み込みが終わると await の後（UI スレッド）で Status を更新する。
/// 実行中はコマンドが無効になるので、ボタンを二重に押せない。
/// </summary>
public sealed class LoadViewModel : ObservableObject
{
    private string _status = "";

    public LoadViewModel()
    {
        LoadCommand = new AsyncRelayCommand(LoadAsync);
    }

    public AsyncRelayCommand LoadCommand { get; }

    /// <summary>読み込んだ値。読み込みが終わるまで空。</summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    private async Task LoadAsync() => Status = (await DataSource.LoadAsync()).ToString();
}

using BookA.Mvvm;

namespace Ch12.ProcessNotExiting;

/// <summary>
/// 監視（ポーリング）を別スレッドで回すビューモデルの共通部分。監視は打ち切ってよい処理で、
/// サンプルでは記事の計測と同じく「6 秒眠る」処理に置き換えている（実際のアプリでは止め忘れたループに当たる）。
/// 再現版と修正版の違いは、スレッドを作るときに IsBackground を立てるかどうか（<see cref="RunInBackground"/>）だけである。
/// </summary>
public abstract class MonitorViewModelBase : ObservableObject
{
    /// <summary>監視の 1 回分の時間。記事の計測と同じ 6 秒。</summary>
    public static readonly TimeSpan PollDuration = TimeSpan.FromSeconds(6);

    private string _status = "停止中";

    protected MonitorViewModelBase()
    {
        StartMonitoringCommand = new RelayCommand(StartMonitoring);
    }

    public RelayCommand StartMonitoringCommand { get; }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>監視のスレッドをバックグラウンドにするか。</summary>
    protected abstract bool RunInBackground { get; }

    private void StartMonitoring()
    {
        var thread = new Thread(() => Thread.Sleep(PollDuration))
        {
            IsBackground = RunInBackground,
            Name = "Monitor",
        };
        thread.Start();
        Status = RunInBackground ? "監視中（バックグラウンドのスレッド）" : "監視中（フォアグラウンドのスレッド）";
    }
}

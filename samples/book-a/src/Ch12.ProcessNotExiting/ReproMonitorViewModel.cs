namespace Ch12.ProcessNotExiting;

/// <summary>
/// 再現版のビューモデル。new Thread(...) を既定のまま（フォアグラウンド）で起動する。
/// ウィンドウを閉じて Application.Exit が発生しても、このスレッドが終わるまでプロセスは残る。
/// </summary>
public sealed class ReproMonitorViewModel : MonitorViewModelBase
{
    protected override bool RunInBackground => false;
}

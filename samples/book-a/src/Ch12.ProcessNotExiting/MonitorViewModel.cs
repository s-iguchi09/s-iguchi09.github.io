namespace Ch12.ProcessNotExiting;

/// <summary>
/// 修正版のビューモデル。監視は打ち切ってよい処理なので、スレッドに IsBackground = true を立てる。
/// ウィンドウを閉じると、監視の途中でもプロセスはすぐ終わる。
///
/// 保存やフラッシュのように完了が必要な処理には、この方法を使わない（完了を保証しない）。
/// その場合はキャンセルの通知に応答させ、終了時に Join で待つ（記事の「残っているスレッドへの対処」）。
/// </summary>
public sealed class MonitorViewModel : MonitorViewModelBase
{
    protected override bool RunInBackground => true;
}

using Ch01.BindingModeOmitted;

namespace BookA.Samples;

/// <summary>
/// 起動用のアプリのビューモデル。各章の再現版と修正版に、それぞれ別のビューモデルを渡す
/// （片方の操作がもう片方に影響しないようにするため）。
/// </summary>
public sealed class MainViewModel
{
    public FolderBrowserViewModel Ch01Repro { get; } = new();

    public FolderBrowserViewModel Ch01Fixed { get; } = new();
}

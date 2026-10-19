using System.Diagnostics;

namespace ScreenshotCapture;

// tools/screenshot-capture の土台のうち、BookA のシーンが使う部分だけを .NET Framework 向けに置き直す。
// 元の IScene は既定のインターフェイス実装を使うが、.NET Framework の実行環境はそれに対応しないため、ここでは使わない。

/// <summary>1 つの章の検証手順（tools/screenshot-capture の IScene のうち、BookA のシーンが実装する部分）。</summary>
internal interface IScene
{
    string Slug { get; }

    string ImageDirectory { get; }

    IReadOnlyList<string> Verifies { get; }

    Task CaptureAsync(SceneContext context);
}

/// <summary>BookA のシーンは図を保存しないので、受け取るだけの空の文脈。</summary>
internal sealed class SceneContext
{
}

/// <summary>.NET 5 以降にしか無い、型引数の無い TaskCompletionSource（Capture.cs が使う）。</summary>
internal sealed class TaskCompletionSource
{
    private readonly TaskCompletionSource<bool> _inner = new();

    public Task Task => _inner.Task;

    public bool TrySetResult() => _inner.TrySetResult(true);
}

/// <summary>.NET Core 2.0 以降にしか無い Dictionary の GetValueOrDefault を補う。</summary>
internal static class DictionaryCompat
{
    public static TValue? GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue? defaultValue = default)
        where TKey : notnull =>
        dictionary.TryGetValue(key, out TValue? value) ? value : defaultValue;
}

/// <summary>.NET 5 以降にしか無い Process の API を補う。</summary>
internal static class ProcessCompat
{
    public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => completion.TrySetResult(true);
        if (process.HasExited)
        {
            completion.TrySetResult(true);
        }

        CancellationTokenRegistration registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        return completion.Task.ContinueWith(
            task =>
            {
                registration.Dispose();
                return task;
            },
            TaskScheduler.Default).Unwrap();
    }

    /// <summary>子プロセスは子を持たないので、ツリーごとではなく本体だけを終わらせる。</summary>
    public static void Kill(this Process process, bool entireProcessTree) => process.Kill();
}

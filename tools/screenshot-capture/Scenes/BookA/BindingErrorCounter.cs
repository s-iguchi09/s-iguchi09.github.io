using System.Diagnostics;

namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// バインドのエラーと警告の数を数える。<see cref="Start"/> から Dispose までの間、
/// <see cref="PresentationTraceSources.DataBindingSource"/> に出た Warning 以上を数える。
/// 記事の計測（<see cref="BindingModeOmittedScene"/>）と同じ数え方である。
/// </summary>
internal sealed class BindingErrorCounter : TraceListener
{
    private SourceLevels _previousLevel;

    private BindingErrorCounter()
    {
    }

    public int Count { get; private set; }

    public static BindingErrorCounter Start()
    {
        var counter = new BindingErrorCounter();
        counter._previousLevel = PresentationTraceSources.DataBindingSource.Switch.Level;
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        PresentationTraceSources.DataBindingSource.Listeners.Add(counter);
        return counter;
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
    {
        if (eventType <= TraceEventType.Warning)
        {
            Count++;
        }
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args)
    {
        if (eventType <= TraceEventType.Warning)
        {
            Count++;
        }
    }

    public override void Write(string? message)
    {
    }

    public override void WriteLine(string? message)
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(this);
            PresentationTraceSources.DataBindingSource.Switch.Level = _previousLevel;
        }

        base.Dispose(disposing);
    }
}

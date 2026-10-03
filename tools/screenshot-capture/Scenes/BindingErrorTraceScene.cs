using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF のバインディングエラーを出力ウィンドウから読み解く方法」の図。
///
/// バインドの失敗パターンを実際に起こし、<c>PresentationTraceSources.DataBindingSource</c>
/// へ流れたメッセージからエラー番号を拾う。
/// 記事はエラー番号ごとに原因を対応づけているが、番号は取り違えやすいため実測で確かめる。
/// </summary>
internal sealed class BindingErrorTraceScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "失敗パターンごとに System.Windows.Data トレースへ記録される番号を確かめる",
        "パス解決失敗が Error 40、ConvertBack 失敗が Error 7 であること",
        "Error 17 が、空のインデクサーだけでなくゲッターが例外を送出した場合にも出ること",
        "DataContext 未設定は既定の Warning では何も出力されず、Information 10 であること",
        "Switch.Level を Error・Critical にしたときに、パス解決失敗（Error 40）が記録されるか",
        "DataContext 未設定のバインドに TraceLevel=High を付け、Switch.Level が Warning のときに出る番号（Warning 71 と Information 10 のどちらが出るか）",
        "検証エラーを出してから解消したときに記録される番号（(Validation.Errors)[0] の Error 17 が解消の時点で出るか）",
    ];

    public string Slug => "wpf-binding-error-debugging-output-window";

    public async Task CaptureAsync(SceneContext context)
    {
        var listener = new CollectingListener();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);

        var rows = new List<IReadOnlyList<Loc>>();
        try
        {
            rows.Add(Run(listener, T("path not found", "パスが見つからない"), SourceLevels.Warning, BuildMissingPath));
            rows.Add(Run(listener, T("path not found", "パスが見つからない"), SourceLevels.Error, BuildMissingPath));
            rows.Add(Run(listener, T("path not found", "パスが見つからない"), SourceLevels.Critical, BuildMissingPath));
            rows.Add(Run(listener, T("DataContext not set", "DataContext が未設定"), SourceLevels.Warning, BuildNullDataContext));
            rows.Add(Run(listener, T("DataContext not set", "DataContext が未設定"), SourceLevels.Information, BuildNullDataContext));
            rows.Add(RunTraceLevelHigh(listener));
            rows.Add(Run(listener, T("ConvertBack fails", "ConvertBack が失敗"), SourceLevels.Warning, BuildConvertBackFailure));
            rows.Add(Run(listener, T("empty (Validation.Errors)[0]", "空の (Validation.Errors)[0]"), SourceLevels.Warning, BuildEmptyValidationIndexer));
            rows.Add(RunErrorCleared(listener));
            rows.Add(Run(listener, T("getter throws", "getter が例外を投げる"), SourceLevels.Warning, BuildThrowingGetter));
            rows.Add(Run(listener, T("binding that resolves", "解決できるバインド"), SourceLevels.Warning, BuildWorkingBinding));
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
        }

        await context.SaveTableAsync(
            "System.Windows.Data trace output",
            [T("binding", "バインド"), "Switch.Level", T("reported as", "記録された番号"), T("message", "メッセージ")],
            rows,
            "binding-error-trace-matrix.svg");
    }

    /// <summary>1 つのパターンを実行し、拾えたエラー番号とメッセージの要点を返す。</summary>
    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    private static IReadOnlyList<Loc> Run(
        CollectingListener listener,
        Loc label,
        SourceLevels level,
        Func<Window> build)
    {
        listener.Clear();
        PresentationTraceSources.DataBindingSource.Switch.Level = level;

        Window window = build();
        window.Width = 240;
        window.Height = 160;
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.Background = Brushes.White;
        window.Show();
        Settle(window);

        window.Content = null;
        window.Close();
        Settle(window);

        string text = listener.Text;
        Match match = Regex.Match(text, @"System\.Windows\.Data (Error|Warning|Information): (\d+)");
        if (!match.Success)
        {
            return [label, level.ToString(), Nothing, "-"];
        }

        // 隣のレコードの文言を拾わないよう、一致したレコード 1 件分だけを要約する。
        string rest = text[(match.Index + match.Length)..];
        int next = rest.IndexOf("System.Windows.Data ", StringComparison.Ordinal);
        string record = next >= 0 ? rest[..next] : rest;
        return [label, level.ToString(), $"{match.Groups[1].Value} {match.Groups[2].Value}", Summarize(record)];
    }

    /// <summary>
    /// DataContext 未設定のバインドに TraceLevel=High を付け、Switch.Level は Warning のままにする。
    /// 記事の「TraceLevel を付ければ Information 10 が見える」を確かめるため、最初の番号ではなく
    /// Warning 71（DataContext is null）と Information 10 がそれぞれ出たかを返す。
    /// </summary>
    private static IReadOnlyList<Loc> RunTraceLevelHigh(CollectingListener listener)
    {
        listener.Clear();
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        var box = new TextBox();
        var binding = new Binding("UserName");
        PresentationTraceSources.SetTraceLevel(binding, PresentationTraceLevel.High);
        box.SetBinding(TextBox.TextProperty, binding);
        ShowAndClose(new Window { Content = box });
        string text = listener.Text;
        return
        [
            T("DataContext not set, TraceLevel=High", "DataContext が未設定、TraceLevel=High"),
            SourceLevels.Warning.ToString(),
            $"Warning 71: {text.Contains("Warning: 71 :", StringComparison.Ordinal)}, Information 10: {text.Contains("Information: 10 :", StringComparison.Ordinal)}",
            Regex.IsMatch(text, @"Warning: 71 : .*DataContext is null") ? "DataContext is null" : "-",
        ];
    }

    /// <summary>
    /// 検証エラーを出し、そのあと解消する。解消した時点で記録される番号だけを拾う。
    /// 表示には (Validation.Errors)[0].ErrorContent をバインドしておく（記事の典型例）。
    /// </summary>
    private static IReadOnlyList<Loc> RunErrorCleared(CollectingListener listener)
    {
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        var source = new AgeSource();
        var box = new TextBox { DataContext = source };
        box.SetBinding(TextBox.TextProperty, new Binding(nameof(AgeSource.Age))
        {
            ValidatesOnExceptions = true,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });
        var text = new TextBlock();
        text.SetBinding(TextBlock.TextProperty, new Binding("(Validation.Errors)[0].ErrorContent") { Source = box });
        var panel = new StackPanel();
        panel.Children.Add(box);
        panel.Children.Add(text);
        var window = new Window { Content = panel, Width = 240, Height = 160, ShowInTaskbar = false, ShowActivated = false };
        listener.Clear();
        window.Show();
        Settle(window);
        string shown = Numbers(listener.Text);
        listener.Clear();
        box.Text = "not a number";
        Settle(window);
        string raised = Numbers(listener.Text);
        listener.Clear();
        box.Text = "5";
        Settle(window);
        string recorded = listener.Text;
        window.Content = null;
        window.Close();
        Settle(window);
        Match match = Regex.Match(recorded, @"System\.Windows\.Data (Error|Warning|Information): (\d+)");
        return
        [
            T("validation error raised, then cleared", "検証エラーが発生し、その後解消"),
            SourceLevels.Warning.ToString(),
            T($"shown: {shown}; raised: {raised}; cleared: {Numbers(recorded)}", $"表示時: {Ja(shown)}、発生時: {Ja(raised)}、解消時: {Ja(Numbers(recorded))}"),
            match.Success ? Summarize(recorded[(match.Index + match.Length)..]) : "-",
        ];
    }

    /// <summary>何も記録されなかったこと。<see cref="Numbers"/> も英語では同じ "nothing" を返す。</summary>
    private static readonly Loc Nothing = T("nothing", "記録なし");

    /// <summary><see cref="Numbers"/> の結果の日本語。記録が無いときだけ言葉が要る。</summary>
    private static string Ja(string numbers) => numbers == Nothing.En ? Nothing.Ja : numbers;

    /// <summary>記録されたすべての番号を、出た順に重複を除いて並べる。</summary>
    private static string Numbers(string text)
    {
        string[] numbers = Regex.Matches(text, @"System\.Windows\.Data (Error|Warning|Information): (\d+)")
            .Select(m => $"{m.Groups[1].Value} {m.Groups[2].Value}")
            .Distinct()
            .ToArray();
        return numbers.Length == 0 ? "nothing" : string.Join(", ", numbers);
    }

    public sealed class AgeSource
    {
        public int Age { get; set; } = 1;
    }

    private static void ShowAndClose(Window window)
    {
        window.Width = 240;
        window.Height = 160;
        window.ShowInTaskbar = false;
        window.ShowActivated = false;
        window.Show();
        Settle(window);
        window.Content = null;
        window.Close();
        Settle(window);
    }

    /// <summary>表に収まる長さで、メッセージの特徴的な部分だけを取り出す。</summary>
    private static Loc Summarize(string text)
    {
        (string Pattern, string Label)[] markers =
        [
            (@"property not found", "property not found"),
            (@"DataItem=null", "DataItem=null"),
            (@"ConvertBack cannot convert", "ConvertBack cannot convert"),
            (@"Cannot get 'Item\[\]' value", "Cannot get 'Item[]' value"),
            // インデクサー以外のプロパティで値の取得に失敗した場合。
            (@"Cannot get '\w+' value", "Cannot get '<property>' value"),
            (@"Cannot retrieve value using the binding", "cannot retrieve value"),
        ];

        foreach ((string pattern, string label) in markers)
        {
            if (Regex.IsMatch(text, pattern))
            {
                return label;
            }
        }

        return string.IsNullOrWhiteSpace(text) ? "-" : T("(other)", "（その他）");
    }

    /// <summary>存在しないプロパティへバインドする。</summary>
    private static Window BuildMissingPath()
    {
        var box = new TextBox { Name = "userNameBox" };
        box.SetBinding(TextBox.TextProperty, new Binding("UserNam"));
        return new Window { Content = box, DataContext = new MainViewModel() };
    }

    /// <summary><c>DataContext</c> を設定しないままバインドする。</summary>
    private static Window BuildNullDataContext()
    {
        var box = new TextBox();
        box.SetBinding(TextBox.TextProperty, new Binding("UserName"));
        return new Window { Content = box };
    }

    /// <summary>数値プロパティへ数値でない文字列を書き戻す。</summary>
    private static Window BuildConvertBackFailure()
    {
        var box = new TextBox();
        box.SetBinding(TextBox.TextProperty, new Binding("Count")
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
        });

        var window = new Window { Content = box, DataContext = new MainViewModel() };
        window.ContentRendered += (_, _) =>
        {
            box.Text = "not a number";
            BindingOperations.GetBindingExpression(box, TextBox.TextProperty)?.UpdateSource();
        };
        return window;
    }

    /// <summary>検証エラーが空のときにインデクサーへアクセスする。</summary>
    private static Window BuildEmptyValidationIndexer()
    {
        var source = new TextBox();
        var text = new TextBlock();
        text.SetBinding(TextBlock.TextProperty, new Binding("(Validation.Errors)[0].ErrorContent")
        {
            Source = source,
        });

        var panel = new StackPanel();
        panel.Children.Add(source);
        panel.Children.Add(text);
        return new Window { Content = panel };
    }

    /// <summary>
    /// 値を取り出す途中で例外が発生するバインド。
    /// インデクサー以外でも同じ番号が出るのかを確かめるために置く。
    /// </summary>
    private static Window BuildThrowingGetter()
    {
        var text = new TextBlock { DataContext = new ThrowingSource() };
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(ThrowingSource.Value)));

        return new Window { Content = text };
    }

    /// <summary>ゲッターが必ず例外を送出するソース。</summary>
    private sealed class ThrowingSource
    {
        public string Value => throw new InvalidOperationException("getter failed");
    }

    /// <summary>解決できるバインド。対照として、何も出力されないことを確かめる。</summary>
    private static Window BuildWorkingBinding()
    {
        var box = new TextBox();
        box.SetBinding(TextBox.TextProperty, new Binding("UserName"));
        return new Window { Content = box, DataContext = new MainViewModel() };
    }

    private sealed class MainViewModel
    {
        public string UserName { get; set; } = "taro";

        public int Count { get; set; }
    }

    /// <summary>トレース出力を蓄えるだけのリスナー。</summary>
    private sealed class CollectingListener : TraceListener
    {
        private readonly StringBuilder _buffer = new();

        public string Text => _buffer.ToString();

        public void Clear() => _buffer.Clear();

        public override void Write(string? message) => _buffer.Append(message);

        public override void WriteLine(string? message) => _buffer.AppendLine(message);
    }

    private static void Settle(Window window)
    {
        window.UpdateLayout();
        for (int i = 0; i < 4; i++)
        {
            var frame = new DispatcherFrame();
            window.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }
}

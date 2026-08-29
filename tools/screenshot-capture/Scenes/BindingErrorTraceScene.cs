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
        "パス解決失敗が Error 40、ConvertBack 失敗が Error 7、空のインデクサーが Error 17 であること",
        "DataContext 未設定は既定の Warning では何も出力されず、Information 10 であること",
    ];

    public string Slug => "wpf-binding-error-debugging-output-window";

    public async Task CaptureAsync(SceneContext context)
    {
        var listener = new CollectingListener();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);

        var rows = new List<IReadOnlyList<string>>();
        try
        {
            rows.Add(Run(listener, "path not found", SourceLevels.Warning, BuildMissingPath));
            rows.Add(Run(listener, "DataContext not set", SourceLevels.Warning, BuildNullDataContext));
            rows.Add(Run(listener, "DataContext not set", SourceLevels.Information, BuildNullDataContext));
            rows.Add(Run(listener, "ConvertBack fails", SourceLevels.Warning, BuildConvertBackFailure));
            rows.Add(Run(listener, "empty (Validation.Errors)[0]", SourceLevels.Warning, BuildEmptyValidationIndexer));
            rows.Add(Run(listener, "binding that resolves", SourceLevels.Warning, BuildWorkingBinding));
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
        }

        await context.SaveTableAsync(
            "System.Windows.Data trace output",
            ["binding", "Switch.Level", "reported as", "message"],
            rows,
            "binding-error-trace-matrix.svg");
    }

    /// <summary>1 つのパターンを実行し、拾えたエラー番号とメッセージの要点を返す。</summary>
    private static IReadOnlyList<string> Run(
        CollectingListener listener,
        string label,
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
            return [label, level.ToString(), "nothing", "-"];
        }

        // 隣のレコードの文言を拾わないよう、一致したレコード 1 件分だけを要約する。
        string rest = text[(match.Index + match.Length)..];
        int next = rest.IndexOf("System.Windows.Data ", StringComparison.Ordinal);
        string record = next >= 0 ? rest[..next] : rest;
        return [label, level.ToString(), $"{match.Groups[1].Value} {match.Groups[2].Value}", Summarize(record)];
    }

    /// <summary>表に収まる長さで、メッセージの特徴的な部分だけを取り出す。</summary>
    private static string Summarize(string text)
    {
        (string Pattern, string Label)[] markers =
        [
            (@"property not found", "property not found"),
            (@"DataItem=null", "DataItem=null"),
            (@"ConvertBack cannot convert", "ConvertBack cannot convert"),
            (@"Cannot get 'Item\[\]' value", "Cannot get 'Item[]' value"),
            (@"Cannot retrieve value using the binding", "cannot retrieve value"),
        ];

        foreach ((string pattern, string label) in markers)
        {
            if (Regex.IsMatch(text, pattern))
            {
                return label;
            }
        }

        return string.IsNullOrWhiteSpace(text) ? "-" : "(other)";
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

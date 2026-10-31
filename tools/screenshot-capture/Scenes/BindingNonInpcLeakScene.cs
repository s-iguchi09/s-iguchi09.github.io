using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF のバインドで INotifyPropertyChanged の無いソースがメモリに残る仕組み」の表。
///
/// バインドの書き方とソースの型が違うウィンドウを 100 回ずつ開いて閉じ、10・50・100 回の時点で
/// GC を呼んでから、ソース（またはバインドしたプロパティを持つオブジェクト）・BindingExpression・ウィンドウが
/// いくつ回収されずに残っているかを弱参照で数える。ソースはどれも 1 MB の配列を持つ。
/// もう 1 つの表は、ソースが PropertyDescriptor の内部の表に登録されてから外れるまでを、
/// 非公開のフィールドをリフレクションで読んで確かめる（観察のためだけに読み、書き換えない）。
///
/// ビューは XAML の文字列から作り、バインドだけで値を表示する（記事に載せる XAML と、ShowInTaskbar と xmlns を除いて同じ）。
/// DataContext を渡すのはシーン（合成の役）で、ビューのコードビハインドは無い。
/// </summary>
internal sealed class BindingNonInpcLeakScene : IScene
{
    private const int Rounds = 100;
    private static readonly int[] Checkpoints = [10, 50, 100];

    private const string TextBlockView = """
        <Window Width="240" Height="100" ShowInTaskbar="False">
          <TextBlock Text="{Binding Title}" />
        </Window>
        """;

    private const string TextBoxView = """
        <Window Width="240" Height="100" ShowInTaskbar="False">
          <TextBox Text="{Binding Title}" />
        </Window>
        """;

    private const string OneTimeView = """
        <Window Width="240" Height="100" ShowInTaskbar="False">
          <TextBlock Text="{Binding Title, Mode=OneTime}" />
        </Window>
        """;

    private const string RowCountView = """
        <Window Width="240" Height="100" ShowInTaskbar="False">
          <TextBlock Text="{Binding RowCount}" />
        </Window>
        """;

    private const string CountView = """
        <Window Width="240" Height="100" ShowInTaskbar="False">
          <TextBlock Text="{Binding Rows.Count}" />
        </Window>
        """;

    private static readonly ReportViewModel SharedReport = new();

    public IReadOnlyList<string> Verifies =>
    [
        "INotifyPropertyChanged を実装しないビューモデルの書き込み可能なプロパティに TextBlock.Text を OneWay でバインドしたウィンドウを 100 回開閉すると、10・50・100 回のいずれかの時点でビューモデルが 1 個以上回収されずに残り、100 回の時点で残る数は開いた数（100）より少ないこと",
        "同じビューモデルに TextBox.Text を（既定の TwoWay で）バインドした場合と、get だけのプロパティにバインドした場合も、同じく 1 個以上残り、100 回の時点では開いた数より少ないこと",
        "INotifyPropertyChanged を実装したビューモデルに {Binding Rows.Count} で List<T> の Count にバインドすると、List<T> が同じように残ること",
        "INotifyPropertyChanged を実装したビューモデル、Mode=OneTime、ObservableCollection<T> の Count では、10・50・100 回のどの時点でも残るソースが 0 であること",
        "List<T> を持つ INotifyPropertyChanged のビューモデルに件数を返す RowCount を用意して {Binding RowCount} にすると、List<T> は 10・50・100 回のどの時点でも残らないこと",
        "1 回目のウィンドウを開いている間に、バインドしたプロパティの PropertyDescriptor の _valueChangedHandlers にその持ち主がキーとして入っているのは、通知なしの OneWay・TwoWay・get だけ・共有のソースと List<T>.Count（キーは List<T>）であり、Mode=OneTime、INotifyPropertyChanged のビューモデル、ObservableCollection<T>.Count、RowCount では入っていないこと",
        "どの書き方でも、100 回開閉した後に残るウィンドウと BindingExpression は 0 であること。同じ 1 つの通知なしのソースを 100 個のウィンドウで共有した場合も、ウィンドウと BindingExpression は残らないこと",
        "TypeDescriptor.GetProperties を型で 2 回呼んだ結果とインスタンスで呼んだ結果から取り出した、同じプロパティの PropertyDescriptor は同一のインスタンスであること",
        "通知なしのソースにバインドしたウィンドウを開いている間、PropertyDescriptor の _valueChangedHandlers にそのソースをキーとする登録が 1 件あり、ハンドラーの Target が MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord で、その記録の _source フィールドもソースそのものであること",
        "ウィンドウを閉じて GC すると、BindingExpression は回収されるが、登録（キーと記録の _source）とソースは残ること。250 ms ごとに GC を呼びながら 5 秒待っても残ること",
        "その後、別の型の通知なしのソースにバインドしたウィンドウを開き、閉じる前に GC すると、登録が 0 件になり、ソースが回収されること",
    ];

    public string Slug => "wpf-memory-leak-binding-non-inpc-source";

    public async Task CaptureAsync(SceneContext context)
    {
        // 内部の表を読む計測は、他の計測が同じ仕組みを動かす前に行う。
        await SaveCleanupTimelineAsync(context);
        await SaveRetentionMatrixAsync(context);
    }

    // ------------------------------------------------------------ 表 1: 100 回開閉して残る数

    /// <param name="Tracked">残っているかを数えるオブジェクト。null なら数えない（共有のソース）。</param>
    /// <param name="Watched">開いている間に PropertyDescriptor の登録表のキーになっているかを調べる、バインドしたプロパティの持ち主とプロパティ名。</param>
    private sealed record Case(
        Loc Label,
        string Binding,
        string Xaml,
        Func<object> CreateSource,
        Func<object, object?> Tracked,
        Func<object, (object Owner, string Property)> Watched,
        bool ExpectRegistered,
        bool ExpectRetained);

    private static async Task SaveRetentionMatrixAsync(SceneContext context)
    {
        Case[] cases =
        [
            new(T("No INPC, settable property, TextBlock", "通知なし、set のあるプロパティ、TextBlock"),
                "{Binding Title}", TextBlockView, () => new ReportViewModel(), source => source, source => (source, "Title"), ExpectRegistered: true, ExpectRetained: true),
            new(T("No INPC, settable property, TextBox (TwoWay by default)", "通知なし、set のあるプロパティ、TextBox（既定で TwoWay）"),
                "{Binding Title}", TextBoxView, () => new ReportViewModel(), source => source, source => (source, "Title"), ExpectRegistered: true, ExpectRetained: true),
            new(T("No INPC, get-only property", "通知なし、get だけのプロパティ"),
                "{Binding Title}", TextBlockView, () => new ReadOnlyReportViewModel(), source => source, source => (source, "Title"), ExpectRegistered: true, ExpectRetained: true),
            new(T("No INPC, Mode=OneTime", "通知なし、Mode=OneTime"),
                "{Binding Title, Mode=OneTime}", OneTimeView, () => new ReportViewModel(), source => source, source => (source, "Title"), ExpectRegistered: false, ExpectRetained: false),
            new(T("INPC view model (PropertyChanged never raised)", "INPC のビューモデル（PropertyChanged は一度も発行しない）"),
                "{Binding Title}", TextBlockView, () => new NotifyingReportViewModel(), source => source, source => (source, "Title"), ExpectRegistered: false, ExpectRetained: false),
            new(T("INPC view model, List<T>.Count (tracking the List<T>)", "INPC のビューモデル、List<T>.Count（List<T> を追跡）"),
                "{Binding Rows.Count}", CountView, () => new RowsViewModel(), source => ((RowsViewModel)source).Rows, source => (((RowsViewModel)source).Rows, "Count"), ExpectRegistered: true, ExpectRetained: true),
            new(T("INPC view model, RowCount property over a List<T> (tracking the List<T>)", "INPC のビューモデル、List<T> の件数を返す RowCount（List<T> を追跡）"),
                "{Binding RowCount}", RowCountView, () => new RowCountViewModel(), source => ((RowCountViewModel)source).Rows, source => (source, "RowCount"), ExpectRegistered: false, ExpectRetained: false),
            new(T("INPC view model, ObservableCollection<T>.Count (tracking the collection)", "INPC のビューモデル、ObservableCollection<T>.Count（コレクションを追跡）"),
                "{Binding Rows.Count}", CountView, () => new ObservableRowsViewModel(), source => ((ObservableRowsViewModel)source).Rows, source => (((ObservableRowsViewModel)source).Rows, "Count"), ExpectRegistered: false, ExpectRetained: false),
            new(T("No INPC, one long-lived source shared by all windows", "通知なし、1 つの長寿命のソースを全ウィンドウで共有"),
                "{Binding Title}", TextBlockView, () => SharedReport, _ => null, source => (source, "Title"), ExpectRegistered: true, ExpectRetained: false),
        ];

        var rows = new List<IReadOnlyList<Loc>>();
        var mismatches = new List<string>();
        foreach (Case c in cases)
        {
            var sources = new List<WeakReference?>();
            var windows = new List<WeakReference>();
            var bindings = new List<WeakReference>();
            var counts = new List<int>();
            bool? registered = null;
            for (int round = 1; round <= Rounds; round++)
            {
                bool? found = null;
                Opened opened = await OpenAndCloseAsync(
                    c.Xaml,
                    c.CreateSource,
                    c.Tracked,
                    round == 1
                        ? (_, source) =>
                        {
                            found = IsRegistered(c.Watched(source));
                            return Task.CompletedTask;
                        }
                        : null);
                registered ??= found;
                sources.Add(opened.Source);
                windows.Add(opened.Window);
                bindings.Add(opened.Binding);
                if (Checkpoints.Contains(round))
                {
                    await IdleAsync(5, 100);
                    counts.Add(sources.Count(reference => reference?.IsAlive == true));
                }
            }

            int aliveWindows = windows.Count(reference => reference.IsAlive);
            int aliveBindings = bindings.Count(reference => reference.IsAlive);
            bool tracksSource = sources.All(reference => reference is not null);

            if (registered != c.ExpectRegistered)
            {
                mismatches.Add($"{c.Label.Ja}: 登録表のキーにあるか {registered}（想定 {c.ExpectRegistered}）");
            }

            if (aliveWindows != 0 || aliveBindings != 0)
            {
                mismatches.Add($"{c.Label.Ja}: 残ったウィンドウが {aliveWindows}、BindingExpression が {aliveBindings}");
            }

            if (tracksSource)
            {
                bool retained = counts.Any(count => count > 0);
                bool belowOpened = counts[^1] < Rounds;
                if (retained != c.ExpectRetained || !belowOpened)
                {
                    mismatches.Add($"{c.Label.Ja}: {string.Join(" / ", counts)}（残る想定 {c.ExpectRetained}）");
                }
            }

            rows.Add(
            [
                c.Label,
                c.Binding,
                registered == true ? T("Yes", "ある") : T("No", "ない"),
                .. tracksSource
                    ? counts.Select(count => (Loc)count.ToString())
                    : Checkpoints.Select(_ => Loc.Of("- (shared)", "-（共有）")),
                aliveBindings.ToString(),
                aliveWindows.ToString(),
            ]);
        }

        if (mismatches.Count > 0)
        {
            throw new InvalidOperationException($"残る数が想定と異なる: {string.Join("; ", mismatches)}");
        }

        await context.SaveTableAsync(
            $"Objects still alive after opening and closing a window {Rounds} times ({RuntimeLabel()})",
            [
                T("Source and target element", "ソースとバインド先の要素"),
                T("Binding", "バインド"),
                T("Key in _valueChangedHandlers while open", "開いている間の _valueChangedHandlers のキー"),
                T("Sources alive after 10", "10 回後に残るソース"),
                T("after 50", "50 回後"),
                T("after 100", "100 回後"),
                T("BindingExpressions alive after 100", "100 回後に残る BindingExpression"),
                T("Windows alive after 100", "100 回後に残るウィンドウ"),
            ],
            rows,
            "binding-non-inpc-retention.svg");
    }

    // ------------------------------------------------------------ 表 2: 内部の表に残る登録と、外れるきっかけ

    private static async Task SaveCleanupTimelineAsync(SceneContext context)
    {
        PropertyDescriptor descriptor = TypeDescriptor.GetProperties(typeof(TimelineViewModel))[nameof(TimelineViewModel.Title)]!;
        if (!ReferenceEquals(descriptor, TypeDescriptor.GetProperties(typeof(TimelineViewModel))[nameof(TimelineViewModel.Title)])
            || !ReferenceEquals(descriptor, TypeDescriptor.GetProperties(new TimelineViewModel())[nameof(TimelineViewModel.Title)]))
        {
            throw new InvalidOperationException("TypeDescriptor.GetProperties が同じプロパティに別の PropertyDescriptor を返した。");
        }

        var rows = new List<IReadOnlyList<Loc>>();

        Opened opened = await OpenAndCloseAsync(
            TextBlockView,
            () => new TimelineViewModel(),
            vm => vm,
            (open, _) =>
            {
                rows.Add(Row(T("While the window is open", "ウィンドウを開いている間"), descriptor, open.Source!, open.Binding));
                return Task.CompletedTask;
            });
        await IdleAsync(5, 100);
        rows.Add(Row(T("Closed the window, then GC", "ウィンドウを閉じて GC"), descriptor, opened.Source!, opened.Binding));

        await IdleAsync(20, 250);
        rows.Add(Row(T("Waited 5 s, GC every 250 ms", "250 ms ごとに GC しながら 5 秒待つ"), descriptor, opened.Source!, opened.Binding));

        await OpenAndCloseAsync(
            """
            <Window Width="240" Height="100" ShowInTaskbar="False">
              <TextBlock Text="{Binding Name}" />
            </Window>
            """,
            () => new OtherViewModel(),
            vm => vm,
            whileOpen: async (_, _) =>
            {
                await IdleAsync(5, 100);
                rows.Add(Row(
                    T("Opened another window bound to a different non-INPC type, then GC before closing it", "別の型の通知なしのソースにバインドしたウィンドウを開き、閉じる前に GC"),
                    descriptor,
                    opened.Source!,
                    opened.Binding));
            });

        string[][] actual = rows.Select(row => row.Skip(1).Select(cell => cell.En).ToArray()).ToArray();
        const string Record = "MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord";
        string[][] expected =
        [
            ["1", "True", "True", Record, "True", "True"],
            ["1", "True", "True", Record, "True", "False"],
            ["1", "True", "True", Record, "True", "False"],
            ["0", "-", "-", "-", "False", "False"],
        ];
        if (actual.Length != expected.Length || !actual.Zip(expected).All(pair => pair.First.SequenceEqual(pair.Second)))
        {
            throw new InvalidOperationException(
                $"内部の表の推移が想定と異なる: {string.Join("; ", actual.Select(row => string.Join(" / ", row)))}");
        }

        await context.SaveTableAsync(
            $"Registrations in PropertyDescriptor for TimelineViewModel.Title ({RuntimeLabel()})",
            [
                T("Step", "手順"),
                T("Entries in _valueChangedHandlers", "_valueChangedHandlers の登録数"),
                T("Key is the first window's source", "キーが最初のウィンドウのソース"),
                T("Record's _source is that source", "記録の _source がそのソース"),
                T("Handler target type", "ハンドラーの Target の型"),
                T("Source alive", "ソースが残っている"),
                T("BindingExpression alive", "BindingExpression が残っている"),
            ],
            rows,
            "binding-non-inpc-cleanup-timeline.svg");
    }

    /// <summary>
    /// PropertyDescriptor が ValueChanged の購読者を持つ非公開の辞書と、その値の記録の _source を読み、表の 1 行にする。
    /// キーと値はこのメソッドの中だけで触り、強い参照を外へ持ち出さない。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IReadOnlyList<Loc> Row(Loc step, PropertyDescriptor descriptor, WeakReference source, WeakReference binding)
    {
        IDictionary? handlers = HandlersOf(descriptor);
        int count = handlers?.Count ?? 0;
        if (count == 0)
        {
            return [step, "0", "-", "-", "-", source.IsAlive.ToString(), binding.IsAlive.ToString()];
        }

        object? target = source.Target;
        IDictionaryEnumerator entries = handlers!.GetEnumerator();
        entries.MoveNext();
        object? record = ((Delegate?)entries.Value)?.Target;
        object? recordSource = record?.GetType().GetField("_source", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(record);
        return
        [
            step,
            count.ToString(),
            ReferenceEquals(entries.Key, target).ToString(),
            ReferenceEquals(recordSource, target).ToString(),
            record?.GetType().FullName ?? "(null)",
            source.IsAlive.ToString(),
            binding.IsAlive.ToString(),
        ];
    }

    // ------------------------------------------------------------ 共通

    /// <summary>開閉したウィンドウの弱参照。Source は数えないケース（共有のソース）では null。</summary>
    private sealed record Opened(WeakReference? Source, WeakReference Binding, WeakReference Window);

    /// <summary>
    /// ビューを作ってソースを DataContext に渡し、表示して閉じる。
    /// ソース・ウィンドウ・BindingExpression への強い参照は、このメソッドと <see cref="Create"/> の中にしか置かない。
    /// </summary>
    /// <param name="whileOpen">開いている間に、弱参照と DataContext を渡して呼ぶ。</param>
    private static async Task<Opened> OpenAndCloseAsync(
        string xaml,
        Func<object> createSource,
        Func<object, object?> tracked,
        Func<Opened, object, Task>? whileOpen = null)
    {
        (Window window, Opened opened) = Create(xaml, createSource, tracked);
        window.Show();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        if (whileOpen is not null)
        {
            await whileOpen(opened, window.DataContext);
        }

        window.Close();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        return opened;
    }

    /// <summary>バインドしたプロパティの PropertyDescriptor の登録表に、その持ち主がキーとして入っているか。</summary>
    private static bool IsRegistered((object Owner, string Property) watched)
    {
        PropertyDescriptor descriptor = TypeDescriptor.GetProperties(watched.Owner)[watched.Property]
            ?? throw new InvalidOperationException($"{watched.Owner.GetType().Name}.{watched.Property} が見つからない。");
        return HandlersOf(descriptor)?.Contains(watched.Owner) == true;
    }

    private static IDictionary? HandlersOf(PropertyDescriptor descriptor)
    {
        FieldInfo field = typeof(PropertyDescriptor).GetField("_valueChangedHandlers", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("PropertyDescriptor._valueChangedHandlers が見つからない。");
        return (IDictionary?)field.GetValue(descriptor);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Window Window, Opened Opened) Create(string xaml, Func<object> createSource, Func<object, object?> tracked)
    {
        object source = createSource();
        var window = SceneContext.LoadXaml<Window>(xaml);
        window.DataContext = source;
        DependencyProperty text = window.Content is TextBox ? TextBox.TextProperty : TextBlock.TextProperty;
        BindingExpressionBase expression = BindingOperations.GetBindingExpressionBase((DependencyObject)window.Content, text)
            ?? throw new InvalidOperationException("Text にバインドが無い。");
        object? target = tracked(source);
        return (window, new Opened(target is null ? null : new WeakReference(target), new WeakReference(expression), new WeakReference(window)));
    }

    /// <summary>ディスパッチャーを空にしてから GC する手順を、指定した回数と間隔で繰り返す。</summary>
    private static async Task IdleAsync(int times, int intervalMs)
    {
        for (int i = 0; i < times; i++)
        {
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(intervalMs);
            for (int pass = 0; pass < 3; pass++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            GC.Collect();
        }
    }

    private static string RuntimeLabel() => $".NET {Environment.Version.ToString(3)}";

    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    // ------------------------------------------------------------ ビューモデル

    /// <summary>通知の仕組みを持たないビューモデル。帳票のデータとして 1 MB を持つ。</summary>
    private sealed class ReportViewModel
    {
        public string Title { get; set; } = "Monthly report";

        public byte[] Data { get; } = new byte[1_000_000];
    }

    private sealed class ReadOnlyReportViewModel
    {
        public string Title { get; } = "Monthly report";

        public byte[] Data { get; } = new byte[1_000_000];
    }

    /// <summary>表 2 だけで使う型。表 1 の計測が同じ PropertyDescriptor に登録を足さないよう分けている。</summary>
    private sealed class TimelineViewModel
    {
        public string Title { get; set; } = "Monthly report";

        public byte[] Data { get; } = new byte[1_000_000];
    }

    private sealed class OtherViewModel
    {
        public string Name { get; set; } = "Other";
    }

    private sealed class NotifyingReportViewModel : ViewModelBase
    {
        private string _title = "Monthly report";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public byte[] Data { get; } = new byte[1_000_000];
    }

    private sealed class RowsViewModel : ViewModelBase
    {
        public List<byte[]> Rows { get; } = [new byte[1_000_000]];
    }

    private sealed class RowCountViewModel : ViewModelBase
    {
        public List<byte[]> Rows { get; } = [new byte[1_000_000]];

        public int RowCount => Rows.Count;
    }

    private sealed class ObservableRowsViewModel : ViewModelBase
    {
        public ObservableCollection<byte[]> Rows { get; } = [new byte[1_000_000]];
    }

    /// <summary>記事に載せる最小の基底クラス。</summary>
    private abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}

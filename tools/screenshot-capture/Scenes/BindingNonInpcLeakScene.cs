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
/// ほかの表は、ソースが PropertyDescriptor の内部の表に登録されてから外れるまでと、外れるきっかけ・内部の参照の型・
/// OneTime と TwoWay の更新の動きを、
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
        "各回のウィンドウを開いている間に、バインドしたプロパティの PropertyDescriptor の _valueChangedHandlers にその持ち主がキーとして入っているのは、通知なしの OneWay・TwoWay・get だけ・共有のソースと List<T>.Count（キーは List<T>）では 100 回すべて、Mode=OneTime、INotifyPropertyChanged のビューモデル、ObservableCollection<T>.Count、RowCount では 0 回であること",
        "10・50・100 回のどの時点でも、残っているオブジェクトはすべて登録表のキーのままであること。オブジェクトが残る行では、前の回の BindingExpression が回収される前に次のウィンドウを開いた回が 1 回以上あること",
        "どの書き方でも、100 回開閉した後に残るウィンドウと BindingExpression は 0 であること。同じ 1 つの通知なしのソースを 100 個のウィンドウで共有した場合も、ウィンドウと BindingExpression は残らないこと",
        "TypeDescriptor.GetProperties を型で 2 回呼んだ結果とインスタンスで呼んだ結果から取り出した、同じプロパティの PropertyDescriptor は同一のインスタンスであること",
        "通知なしのソースにバインドしたウィンドウを開いている間、PropertyDescriptor の _valueChangedHandlers にそのソースをキーとする登録が 1 件あり、ハンドラーの Target が MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord で、その記録の _source フィールドもソースそのものであること",
        "ウィンドウを閉じて GC すると、BindingExpression は回収されるが、登録（キーと記録の _source）とソースは残ること。250 ms ごとに GC を呼びながら 5 秒待っても残ること",
        "その後、別の型の通知なしのソースにバインドしたウィンドウを開き、閉じる前に GC すると、登録が 0 件になり、ソースが回収されること",
        "PropertyDescriptor._valueChangedHandlers は宣言も実体も ConcurrentDictionary<object, EventHandler> で、ValueChangedRecord._source の宣言の型は object であること（どちらも弱い参照の型ではない）",
        "TimelineViewModel.Title の PropertyDescriptor の具象型は System.ComponentModel.ReflectPropertyDescriptor で、シーンが弱参照だけを残して GC した後も、TypeDescriptor から同じインスタンスが返ること",
        "OtherViewModel.Name と TimelineViewModel.Title の PropertyDescriptor は別のインスタンスであること。TextBox.Text の BindsTwoWayByDefault は true であること",
        "閉じたウィンドウの BindingExpression が回収され登録だけが残った状態で次のウィンドウを開くと、INPC のビューモデル・別の型の通知なしのプロパティ・同じ型の別のインスタンスに OneWay でバインドしたウィンドウでは、開いて表示が落ち着いた時点（GC の前）で登録が外れ、最初のソースが回収されること",
        "最初の BindingExpression が回収される前（強い参照で保持）に、同じ型の別のインスタンスへ OneWay でバインドしたウィンドウを開くと、開いた直後・開いたまま GC した後・閉じて GC した後のどれでも登録が外れないこと。参照を放した後の GC で BindingExpression は回収されること",
        "同じ状態で、バインドの無いウィンドウと Mode=OneTime でバインドしたウィンドウでは、開いた直後・開いたまま GC した後・閉じて GC した後のどれでも登録が外れないこと",
        "登録が外れなかった行も、BindingExpression が回収された後に別の型へ OneWay でバインドしたウィンドウをもう 1 つ開くと、開いた直後に登録が外れ、最初のソースが回収されること",
        "INPC のソースに OneWay でバインドした TextBlock はソースの PropertyChanged で表示が変わり、Mode=OneTime では変わらないこと。どちらも DataContext を差し替えると表示が変わること",
        "TextBox に入力して UpdateSource を呼ぶと、既定（TwoWay）のバインドではソースに値が入り、Mode=OneTime ではソースが変わらないこと（どちらもバインドは外れない）",
    ];

    public string Slug => "wpf-memory-leak-binding-non-inpc-source";

    private const string NoBindingView = """
        <Window Width="240" Height="100" ShowInTaskbar="False">
          <TextBlock Text="No binding" />
        </Window>
        """;

    private const string NameView = """
        <Window Width="240" Height="100" ShowInTaskbar="False">
          <TextBlock Text="{Binding Name}" />
        </Window>
        """;

    private const string OneTimeTextBoxView = """
        <Window Width="240" Height="100" ShowInTaskbar="False">
          <TextBox Text="{Binding Title, Mode=OneTime}" />
        </Window>
        """;

    public async Task CaptureAsync(SceneContext context)
    {
        // 内部の表を読む計測は、他の計測が同じ仕組みを動かす前に行う。
        await SaveCleanupTimelineAsync(context);
        await SaveCleanupTriggerAsync(context);
        await SaveInternalsAsync(context);
        await SaveUpdateBehaviorAsync(context);
        await SaveRetentionMatrixAsync(context);
    }

    // ------------------------------------------------------------ 表 3: 内部の型と既定値

    private static async Task SaveInternalsAsync(SceneContext context)
    {
        FieldInfo handlersField = typeof(PropertyDescriptor).GetField("_valueChangedHandlers", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Type recordType = typeof(FrameworkElement).Assembly.GetType("MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord")
            ?? throw new InvalidOperationException("ValueChangedRecord が見つからない。");
        FieldInfo sourceField = recordType.GetField("_source", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ValueChangedRecord._source が見つからない。");

        // 登録表の実体の型は、登録がある状態で読む。
        string handlersType = "(null)";
        await OpenAndCloseAsync(
            TextBlockView,
            () => new TimelineViewModel(),
            vm => vm,
            (_, _) =>
            {
                handlersType = TypeName(HandlersOf(TimelineTitle())?.GetType());
                return Task.CompletedTask;
            });
        await IdleAsync(5, 100);

        // 記述子を弱参照だけで持って GC し、TypeDescriptor が記述子を持ち続けているか（シーンの参照に頼らずに到達できるか）を確かめる。
        WeakReference weakTitle = WeakTitle();
        await IdleAsync(5, 100);
        bool descriptorKept = weakTitle.IsAlive && IsSameTitle(weakTitle);

        bool twoWayByDefault = ((FrameworkPropertyMetadata)TextBox.TextProperty.GetMetadata(typeof(TextBox))).BindsTwoWayByDefault;

        List<IReadOnlyList<Loc>> rows =
        [
            [T("Declared type of PropertyDescriptor._valueChangedHandlers", "PropertyDescriptor._valueChangedHandlers の宣言の型"), TypeName(handlersField.FieldType)],
            [T("Actual type of _valueChangedHandlers while registered", "登録がある間の _valueChangedHandlers の実際の型"), handlersType],
            [T("Declared type of ValueChangedRecord._source", "ValueChangedRecord._source の宣言の型"), TypeName(sourceField.FieldType)],
            [T("Concrete type of the PropertyDescriptor for TimelineViewModel.Title", "TimelineViewModel.Title の PropertyDescriptor の具象型"), TypeName(TimelineTitle().GetType())],
            [T("Same PropertyDescriptor returned after keeping only a weak reference and running the GC", "弱参照だけを残して GC した後も、同じ PropertyDescriptor が返る"), descriptorKept.ToString()],
            [T("OtherViewModel.Name and TimelineViewModel.Title share one PropertyDescriptor", "OtherViewModel.Name と TimelineViewModel.Title の PropertyDescriptor が同一"), ReferenceEquals(TimelineTitle(), TypeDescriptor.GetProperties(typeof(OtherViewModel))[nameof(OtherViewModel.Name)]).ToString()],
            [T("TextBox.Text BindsTwoWayByDefault", "TextBox.Text の BindsTwoWayByDefault"), twoWayByDefault.ToString()],
        ];

        const string Handlers = "System.Collections.Concurrent.ConcurrentDictionary<System.Object, System.EventHandler>";
        string[] actual = rows.Select(row => row[1].En).ToArray();
        string[] expected = [Handlers, Handlers, "System.Object", "System.ComponentModel.ReflectPropertyDescriptor", "True", "False", "True"];
        if (!actual.SequenceEqual(expected))
        {
            throw new InvalidOperationException($"内部の型と既定値が想定と異なる: {string.Join(" | ", actual)}");
        }

        await context.SaveTableAsync(
            $"Internal field types and defaults ({RuntimeLabel()})",
            [T("Item", "項目"), T("Value", "値")],
            rows,
            "binding-non-inpc-internals.svg");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference WeakTitle() => new(TimelineTitle());

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsSameTitle(WeakReference weakTitle) => ReferenceEquals(weakTitle.Target, TimelineTitle());

    private static string TypeName(Type? type)
    {
        if (type is null)
        {
            return "(null)";
        }

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        string name = type.GetGenericTypeDefinition().FullName!;
        return $"{name[..name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>";
    }

    // ------------------------------------------------------------ 表 4: 登録が外れるきっかけ

    /// <param name="HasBinding">次のウィンドウの Text にバインドがあるか。</param>
    /// <param name="GcBeforeOpening">
    /// true なら次のウィンドウを開く前に GC を呼び、最初の BindingExpression を回収しておく。
    /// false なら最初の BindingExpression を、次のウィンドウを開いて登録を読み終えるまで強い参照で持ち、回収前の状態を確実に作る。
    /// </param>
    private sealed record Trigger(Loc Label, string Xaml, Func<object> CreateSource, bool HasBinding = true, bool GcBeforeOpening = true);

    private static async Task SaveCleanupTriggerAsync(SceneContext context)
    {
        Trigger[] triggers =
        [
            new(T("Window without bindings", "バインドの無いウィンドウ"), NoBindingView, () => new OtherViewModel(), HasBinding: false),
            new(T("Window bound to an INPC view model", "INPC のビューモデルにバインドしたウィンドウ"), TextBlockView, () => new NotifyingReportViewModel()),
            new(T("Window bound with Mode=OneTime to a non-INPC type", "通知なしの型に Mode=OneTime でバインドしたウィンドウ"), OneTimeView, () => new ReportViewModel()),
            new(T("Window bound to a non-INPC property of another type", "別の型の通知なしのプロパティにバインドしたウィンドウ"), NameView, () => new OtherViewModel()),
            new(T("Window bound to another instance of the same non-INPC type", "同じ通知なしの型の別のインスタンスにバインドしたウィンドウ"), TextBlockView, () => new TimelineViewModel()),
            new(T("Same as above, but opened before the first BindingExpression is collected", "同上。ただし最初の BindingExpression が回収される前に開く"), TextBlockView, () => new TimelineViewModel(), GcBeforeOpening: false),
        ];

        var rows = new List<IReadOnlyList<Loc>>();
        foreach (Trigger trigger in triggers)
        {
            // 最初のウィンドウを開いて閉じる。GcBeforeOpening なら GC して、BindingExpression が回収され登録だけが残る状態にする。
            Opened first = await OpenAndCloseAsync(TextBlockView, () => new TimelineViewModel(), vm => vm);
            object? heldBinding = trigger.GcBeforeOpening ? null : first.Binding.Target;
            if (trigger.GcBeforeOpening)
            {
                await IdleAsync(5, 100);
            }

            bool before = IsKeyOf(first.Source!);
            bool beforeBinding = first.Binding.IsAlive;

            bool afterOpen = false;
            bool afterGc = false;
            bool bindingAfterGc = false;
            await OpenAndCloseAsync(
                trigger.Xaml,
                trigger.CreateSource,
                _ => null,
                async (_, _) =>
                {
                    afterOpen = IsKeyOf(first.Source!);
                    heldBinding = null;
                    await IdleAsync(5, 100);
                    afterGc = IsKeyOf(first.Source!);
                    bindingAfterGc = first.Binding.IsAlive;
                },
                requireBinding: trigger.HasBinding);
            await IdleAsync(5, 100);
            bool afterClose = IsKeyOf(first.Source!);

            // 最初の BindingExpression が回収された状態で、別の型に OneWay でバインドしたウィンドウをもう 1 つ開く。
            bool afterNext = false;
            await OpenAndCloseAsync(
                NameView,
                () => new OtherViewModel(),
                _ => null,
                (_, _) =>
                {
                    afterNext = IsKeyOf(first.Source!);
                    return Task.CompletedTask;
                });
            await IdleAsync(5, 100);

            rows.Add(
            [
                trigger.Label,
                before.ToString(),
                beforeBinding.ToString(),
                afterOpen.ToString(),
                afterGc.ToString(),
                bindingAfterGc.ToString(),
                afterClose.ToString(),
                afterNext.ToString(),
                first.Source!.IsAlive.ToString(),
            ]);
        }

        // 開く前は、GC を挟む行では「登録あり・BindingExpression は回収済み」。外れる行は、開いて表示が落ち着いた時点（GC の前）で外れている。
        // 外れない行も、BindingExpression が回収された状態で OneWay のウィンドウをもう 1 つ開くと外れ、最初のソースが回収される。
        string[] kept = ["True", "False", "True", "True", "False", "True", "False", "False"];
        string[] removed = ["True", "False", "False", "False", "False", "False", "False", "False"];
        string[] keptWithBinding = ["True", "True", "True", "True", "False", "True", "False", "False"];
        string[][] expectedTriggers = [kept, removed, kept, removed, removed, keptWithBinding];
        string[][] actualTriggers = rows.Select(row => row.Skip(1).Select(cell => cell.En).ToArray()).ToArray();
        if (!actualTriggers.Zip(expectedTriggers).All(pair => pair.First.SequenceEqual(pair.Second)))
        {
            throw new InvalidOperationException(
                $"登録が外れるきっかけが想定と異なる: {string.Join("; ", actualTriggers.Select(row => string.Join(" / ", row)))}");
        }

        await context.SaveTableAsync(
            $"What removes the registration of a closed window's source from TimelineViewModel.Title ({RuntimeLabel()})",
            [
                T("Window opened next", "次に開いたウィンドウ"),
                T("Registered before opening", "開く前に登録あり"),
                T("First BindingExpression alive before opening", "開く前に最初の BindingExpression が残っている"),
                T("Registered right after opening (before GC)", "開いた直後（GC の前）に登録あり"),
                T("Registered after GC while open", "開いたまま GC した後に登録あり"),
                T("First BindingExpression alive after that GC", "その GC の後に最初の BindingExpression が残っている"),
                T("Registered after closing and GC", "閉じて GC した後に登録あり"),
                T("Registered right after opening one more OneWay window", "さらに OneWay のウィンドウを開いた直後に登録あり"),
                T("First source alive at the end", "最後に最初のソースが残っている"),
            ],
            rows,
            "binding-non-inpc-cleanup-trigger.svg");
    }

    /// <summary>
    /// TimelineViewModel.Title の登録表に、弱参照の先のオブジェクトがキーとして入っているか。
    /// 記述子もソースも、このメソッドの中だけで取り出し、強い参照を外へ持ち出さない。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsKeyOf(WeakReference source)
    {
        object? target = source.Target;
        return target is not null && HandlersOf(TimelineTitle())?.Contains(target) == true;
    }

    /// <summary>TimelineViewModel.Title の PropertyDescriptor を、そのつど TypeDescriptor から取り出す（シーンが記述子を持ち続けないため）。</summary>
    private static PropertyDescriptor TimelineTitle() =>
        TypeDescriptor.GetProperties(typeof(TimelineViewModel))[nameof(TimelineViewModel.Title)]!;

    // ------------------------------------------------------------ 表 5: OneTime と TwoWay の更新

    private static async Task SaveUpdateBehaviorAsync(SceneContext context)
    {
        var rows = new List<IReadOnlyList<Loc>>();

        // ソースの変更（PropertyChanged を発行する）と DataContext の差し替えで、ターゲットが変わるか。
        foreach ((string xaml, string mode) in new[] { (TextBlockView, "OneWay"), (OneTimeView, "OneTime") })
        {
            var window = SceneContext.LoadXaml<Window>(xaml);
            var source = new NotifyingReportViewModel();
            window.DataContext = source;
            window.Show();
            await IdleAsync(1, 0);
            var text = (TextBlock)window.Content;
            string initial = text.Text;
            source.Title = "Changed";
            await IdleAsync(1, 0);
            string afterChange = text.Text;
            window.DataContext = new NotifyingReportViewModel { Title = "Replaced" };
            await IdleAsync(1, 0);
            string afterReplace = text.Text;
            window.Close();
            rows.Add([$"TextBlock, {mode}", T("Displayed text: set Title (raises PropertyChanged), then replace DataContext", "表示: Title を変更（PropertyChanged を発行）→ DataContext を差し替え"), initial, afterChange, afterReplace]);
        }

        // TextBox に入力したときと同じ経路で文字を変え、UpdateSource でソースへ送れるか。
        foreach ((string xaml, string mode) in new[] { (TextBoxView, "TwoWay (default)"), (OneTimeTextBoxView, "OneTime") })
        {
            var window = SceneContext.LoadXaml<Window>(xaml);
            var source = new NotifyingReportViewModel();
            window.DataContext = source;
            window.Show();
            await IdleAsync(1, 0);
            var box = (TextBox)window.Content;
            string initial = source.Title;
            box.SelectAll();
            box.SelectedText = "Edited";
            string result;
            try
            {
                BindingOperations.GetBindingExpressionBase(box, TextBox.TextProperty)?.UpdateSource();
                await IdleAsync(1, 0);
                result = source.Title;
            }
            catch (Exception ex)
            {
                result = ex.GetType().Name;
            }

            if (BindingOperations.GetBindingExpressionBase(box, TextBox.TextProperty) is null)
            {
                throw new InvalidOperationException($"{mode} の TextBox で、入力の後にバインドが外れた。");
            }

            window.Close();
            rows.Add([$"TextBox, {mode}", T("Source Title: type \"Edited\", then call UpdateSource", "ソースの Title: 「Edited」と入力して UpdateSource を呼ぶ"), initial, result, "-"]);
        }

        await IdleAsync(5, 100);
        string[][] actualUpdates = rows.Select(row => row.Skip(2).Select(cell => cell.En).ToArray()).ToArray();
        string[][] expectedUpdates =
        [
            ["Monthly report", "Changed", "Replaced"],
            ["Monthly report", "Monthly report", "Replaced"],
            ["Monthly report", "Edited", "-"],
            ["Monthly report", "Monthly report", "-"],
        ];
        if (!actualUpdates.Zip(expectedUpdates).All(pair => pair.First.SequenceEqual(pair.Second)))
        {
            throw new InvalidOperationException(
                $"更新の動きが想定と異なる: {string.Join("; ", actualUpdates.Select(row => string.Join(" / ", row)))}");
        }

        await context.SaveTableAsync(
            $"How OneWay, OneTime, and TwoWay bindings update ({RuntimeLabel()})",
            [
                T("Target and mode", "ターゲットとモード"),
                T("Operation", "操作"),
                T("Before", "操作の前"),
                T("After step 1", "1 つ目の操作の後"),
                T("After step 2", "2 つ目の操作の後"),
            ],
            rows,
            "binding-non-inpc-update-behavior.svg");
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
            var unregisteredAlive = new List<int>();
            int registeredRounds = 0;
            string watchedProperty = c.Watched(c.CreateSource()).Property;
            int openedWithPreviousAlive = 0;
            for (int round = 1; round <= Rounds; round++)
            {
                // 前のラウンドの BindingExpression が回収される前に、このラウンドのウィンドウを開くか。
                if (bindings.Count > 0 && bindings[^1].IsAlive)
                {
                    openedWithPreviousAlive++;
                }

                Opened opened = await OpenAndCloseAsync(
                    c.Xaml,
                    c.CreateSource,
                    c.Tracked,
                    (_, source) =>
                    {
                        if (IsRegistered(c.Watched(source)))
                        {
                            registeredRounds++;
                        }

                        return Task.CompletedTask;
                    });
                sources.Add(opened.Source);
                windows.Add(opened.Window);
                bindings.Add(opened.Binding);
                if (Checkpoints.Contains(round))
                {
                    await IdleAsync(5, 100);
                    counts.Add(sources.Count(reference => reference?.IsAlive == true));
                    unregisteredAlive.Add(CountAliveNotRegistered(sources, watchedProperty));
                }
            }

            int aliveWindows = windows.Count(reference => reference.IsAlive);
            int aliveBindings = bindings.Count(reference => reference.IsAlive);
            bool tracksSource = sources.All(reference => reference is not null);

            if (registeredRounds != (c.ExpectRegistered ? Rounds : 0))
            {
                mismatches.Add($"{c.Label.Ja}: 開いている間にキーだった回数 {registeredRounds}（想定 {(c.ExpectRegistered ? Rounds : 0)}）");
            }

            if (unregisteredAlive.Any(count => count != 0))
            {
                mismatches.Add($"{c.Label.Ja}: 登録表のキーでないのに残ったオブジェクト {string.Join(" / ", unregisteredAlive)}");
            }

            if (c.ExpectRetained && openedWithPreviousAlive == 0)
            {
                mismatches.Add($"{c.Label.Ja}: 前の BindingExpression が残ったまま開いた回数が 0");
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
                $"{registeredRounds} / {Rounds}",
                $"{openedWithPreviousAlive} / {Rounds - 1}",
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
                T("Rounds in which it was a key in _valueChangedHandlers while open", "開いている間に _valueChangedHandlers のキーだった回数"),
                T("Windows opened before the previous BindingExpression was collected", "前の BindingExpression が回収される前に開いた回数"),
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
        if (!TitleIsStable())
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
                rows.Add(Row(T("While the window is open", "ウィンドウを開いている間"), open.Source!, open.Binding));
                return Task.CompletedTask;
            });
        await IdleAsync(5, 100);
        rows.Add(Row(T("Closed the window, then GC", "ウィンドウを閉じて GC"), opened.Source!, opened.Binding));

        await IdleAsync(20, 250);
        rows.Add(Row(T("Waited 5 s, GC every 250 ms", "250 ms ごとに GC しながら 5 秒待つ"), opened.Source!, opened.Binding));

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
    private static IReadOnlyList<Loc> Row(Loc step, WeakReference source, WeakReference binding)
    {
        IDictionary? handlers = HandlersOf(TimelineTitle());
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

    /// <summary>TypeDescriptor.GetProperties を型で 2 回呼んだ結果とインスタンスで呼んだ結果が、同じ PropertyDescriptor を返すか。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TitleIsStable()
    {
        PropertyDescriptor title = TimelineTitle();
        return ReferenceEquals(title, TimelineTitle())
            && ReferenceEquals(title, TypeDescriptor.GetProperties(new TimelineViewModel())[nameof(TimelineViewModel.Title)]);
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
        Func<Opened, object, Task>? whileOpen = null,
        bool requireBinding = true)
    {
        (Window window, Opened opened) = Create(xaml, createSource, tracked, requireBinding);
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

    /// <summary>
    /// 残っているオブジェクトのうち、登録表のキーになっていないものを数える。
    /// 追跡するオブジェクトとバインドしたプロパティの持ち主が同じケースでは、残ったものがすべてキーなら 0 になる。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CountAliveNotRegistered(List<WeakReference?> sources, string property)
    {
        int count = 0;
        foreach (WeakReference? reference in sources)
        {
            if (reference?.Target is { } target && !IsRegistered((target, property)))
            {
                count++;
            }
        }

        return count;
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
    private static (Window Window, Opened Opened) Create(string xaml, Func<object> createSource, Func<object, object?> tracked, bool requireBinding)
    {
        object source = createSource();
        var window = SceneContext.LoadXaml<Window>(xaml);
        window.DataContext = source;
        DependencyProperty text = window.Content is TextBox ? TextBox.TextProperty : TextBlock.TextProperty;
        BindingExpressionBase? expression = BindingOperations.GetBindingExpressionBase((DependencyObject)window.Content, text);
        if (requireBinding && expression is null)
        {
            throw new InvalidOperationException("Text にバインドが無い。");
        }

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

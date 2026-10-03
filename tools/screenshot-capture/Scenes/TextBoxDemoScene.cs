using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「TextBox」（apps/wpf-standard-control-demo/textbox.md と日本語版）の記述を実測する。
///
/// キーボードからの文字入力は、TextCompositionManager で文字の入力（TextInput）を発生させて再現する。
/// コードから Text を設定する場合と経路が異なり、MaxLength や CharacterCasing が効くかどうかがここで分かれる。
/// </summary>
internal sealed class TextBoxDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-textbox";

    public string ImageDirectory => DemoProbe.ImageDirectory("textbox");

    public IReadOnlyList<string> Verifies =>
    [
        "TextBox の継承関係と、各プロパティの既定値（Text の既定の UpdateSourceTrigger を含む）、TextTrimming・PlaceholderText プロパティの有無",
        "TextWrapping（NoWrap / Wrap / WrapWithOverflow）ごとの、幅より長い単語を含む文字列の行数・1 行目・横にはみ出す幅（デモアプリの TextWrapping 欄と同じ文字列）",
        "MaxLength を、キーボードからの入力・コードからの Text・バインドした値のそれぞれに対して効かせたときの結果",
        "CharacterCasing を、キーボードからの入力とコードからの Text に対して効かせたときの結果",
        "MinLines / MaxLines（デモアプリの 2・4・6 行）による高さと、MaxLines を超えたときのスクロールバー",
        "MinLines が表示前（初期化子・XAML）に設定した場合に効くか、表示後に MinLines を設定した場合・表示後に Text を変えた場合・Loaded のハンドラーで設定した場合（XAML の指定なし / 同じ値あり）に効くか",
        "AcceptsReturn による Enter キーの扱いと、行数が多いときの高さ・スクロールバー（VerticalScrollBarVisibility の既定値と Auto）",
        "IsReadOnly で入力が止まり、選択はできること、既定のテンプレートに IsReadOnly のトリガーがあるか",
        "TextAlignment ごとの 1 文字目の横位置",
        "TextDecorations に複数の値を与えられるか",
        "ScrollToEnd を UI スレッド以外から呼んだときの例外と、UI スレッドから呼んだときのスクロール位置",
        "UpdateSourceTrigger が既定と PropertyChanged のときに、1 文字ずつ入力する間と、フォーカスが移った後にソースへ書き込まれる回数",
        "IsReadOnly のときに入力した文字が入るかと、全体を選択したときの Copy / Cut / Paste の実行可否",
    ];

    private const string WrapText = "ABCDEFGHIJKLMNOPQRSTUVWXYZ ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "TextBox: type and defaults",
            ["item", "value"],
            Defaults(),
            "textbox-defaults.svg");

        await context.SaveTableAsync(
            "TextBox: typed input vs. text set from code or a binding",
            [T("case", "条件"), T("Text after", "操作後の Text")],
            await InputAsync(),
            "textbox-input.svg");

        await context.SaveTableAsync(
            "TextBox: wrapping, lines, alignment and scrolling",
            [T("case", "条件"), T("measured", "計測値")],
            await LayoutAsync(),
            "textbox-layout.svg");
    }

    /// <summary>キーボードから 1 文字ずつ入力したのと同じ経路で、文字列を入力する（1 文字ごとに TextComposition を送る）。</summary>
    private static void Type(TextBox box, string text)
    {
        box.Focus();
        foreach (char c in text)
        {
            TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, box, c.ToString()));
        }
    }

    private sealed class Source : INotifyPropertyChanged
    {
        private string _text = "";

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static List<IReadOnlyList<string>> Defaults()
    {
        var box = new TextBox();
        var text = (FrameworkPropertyMetadata)TextBox.TextProperty.GetMetadata(typeof(TextBox));
        var chain = new List<string>();
        for (Type? type = typeof(TextBox).BaseType; type is not null && type != typeof(FrameworkElement); type = type.BaseType)
        {
            chain.Add(type.Name);
        }

        return
        [
            ["base types", string.Join(" > ", chain)],
            ["Text: BindsTwoWayByDefault / DefaultUpdateSourceTrigger", $"{text.BindsTwoWayByDefault} / {text.DefaultUpdateSourceTrigger}"],
            ["TextWrapping / TextAlignment / CharacterCasing", $"{box.TextWrapping} / {box.TextAlignment} / {box.CharacterCasing}"],
            ["AcceptsReturn / IsReadOnly / MaxLength", $"{box.AcceptsReturn} / {box.IsReadOnly} / {box.MaxLength}"],
            ["MinLines / MaxLines", $"{box.MinLines} / {box.MaxLines}"],
            ["VerticalScrollBarVisibility / HorizontalScrollBarVisibility",
                $"{box.VerticalScrollBarVisibility} / {box.HorizontalScrollBarVisibility}"],
            ["SelectionOpacity", D(box.SelectionOpacity)],
            ["TextBox has TextTrimming / PlaceholderText property",
                $"{typeof(TextBox).GetProperty("TextTrimming") is not null} / {typeof(TextBox).GetProperty("PlaceholderText") is not null}"],
            ["MaxLength = -1", Throws(() => new TextBox().MaxLength = -1)],
        ];
    }

    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    /// <summary>2 つの計測値を 1 つのセルに並べる。</summary>
    private static Loc Pair(string first, string second) => T($"{first}; {second}", $"{first}、{second}");

    private static Loc Height(double value) => T($"height {D(value)}", $"高さ {D(value)}");

    private static async Task<List<IReadOnlyList<Loc>>> InputAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        async Task Typed(Loc label, Action<TextBox> configure, string typed, string initial = "")
        {
            var box = new TextBox { Width = 200, Text = initial };
            configure(box);
            await ShowAsync(box, async () =>
            {
                box.CaretIndex = box.Text.Length;
                Type(box, typed);
                await Capture.SettleAsync(Window.GetWindow(box)!);
                rows.Add([label, $"\"{box.Text}\""]);
            }, activate: true);
        }

        await Typed(T("MaxLength=5, typed \"ABCDEFGH\"", "MaxLength=5、\"ABCDEFGH\" を入力"), b => b.MaxLength = 5, "ABCDEFGH");
        {
            var box = new TextBox { MaxLength = 5 };
            box.Text = "ABCDEFGH";
            rows.Add([T("MaxLength=5, Text = \"ABCDEFGH\" from code", "MaxLength=5、コードから Text = \"ABCDEFGH\""), $"\"{box.Text}\""]);
        }

        {
            // デモアプリの MaxLength 欄と同じく、別のテキストの値をバインドで流し込む。
            var source = new Source();
            var box = new TextBox { MaxLength = 5 };
            box.SetBinding(TextBox.TextProperty, new Binding(nameof(Source.Text)) { Source = source });
            source.Text = "ABCDEFGH";
            rows.Add([T("MaxLength=5, Text bound to a source set to \"ABCDEFGH\"", "MaxLength=5、\"ABCDEFGH\" にしたソースへ Text をバインド"), $"\"{box.Text}\""]);
        }

        await Typed(T("CharacterCasing=Upper, typed \"hello\"", "CharacterCasing=Upper、\"hello\" を入力"), b => b.CharacterCasing = CharacterCasing.Upper, "hello");
        {
            var box = new TextBox { CharacterCasing = CharacterCasing.Upper };
            box.Text = "hello";
            rows.Add([T("CharacterCasing=Upper, Text = \"hello\" from code", "CharacterCasing=Upper、コードから Text = \"hello\""), $"\"{box.Text}\""]);
        }

        await Typed(T("IsReadOnly=True, typed \"xyz\" after \"abc\"", "IsReadOnly=True、\"abc\" の後に \"xyz\" を入力"), b => b.IsReadOnly = true, "xyz", "abc");

        {
            var box = new TextBox { IsReadOnly = true, Text = "abc" };
            await ShowAsync(box, async () =>
            {
                box.Focus();
                box.SelectAll();
                rows.Add([T("IsReadOnly=True, SelectAll(): SelectedText", "IsReadOnly=True、SelectAll() の後の SelectedText"), $"\"{box.SelectedText}\""]);
                await Task.CompletedTask;
            }, activate: true);
        }

        foreach (bool accepts in new[] { false, true })
        {
            var box = new TextBox { Width = 200, Text = "ab", AcceptsReturn = accepts };
            await ShowAsync(box, async () =>
            {
                box.Focus();
                box.CaretIndex = 1;
                PressKey(box, Key.Enter);
                await Capture.SettleAsync(Window.GetWindow(box)!);
                rows.Add([T($"AcceptsReturn={accepts}, Enter between \"a\" and \"b\"", $"AcceptsReturn={accepts}、\"a\" と \"b\" の間で Enter"),
                    $"\"{box.Text.Replace("\r", "\\r").Replace("\n", "\\n")}\""]);
            }, activate: true);
        }


        foreach (UpdateSourceTrigger trigger in new[] { UpdateSourceTrigger.Default, UpdateSourceTrigger.PropertyChanged })
        {
            // 使用例「検索欄は UpdateSourceTrigger=PropertyChanged で 1 文字ごとに絞り込む」。ソースに何回書き込まれるか。
            var source = new Source();
            int writes = 0;
            source.PropertyChanged += (_, _) => writes++;
            var box = new TextBox { Width = 200 };
            box.SetBinding(TextBox.TextProperty, new Binding(nameof(Source.Text)) { Source = source, UpdateSourceTrigger = trigger });
            var other = new Button { Content = "Other" };
            var panel = new StackPanel { Children = { box, other } };
            await ShowAsync(panel, async () =>
            {
                Window window = Window.GetWindow(panel)!;
                Type(box, "abc");
                await Capture.SettleAsync(window, 50);
                string whileTyping = $"{writes} ({WpfProbe.Describe(source.Text)})";
                await FocusAsync(other);
                rows.Add([T($"{trigger}: \"abc\" typed: source writes while typing; after focus leaves", $"{trigger}: \"abc\" を入力。入力中のソースへの書き込み、フォーカスが移った後"),
                    T($"{whileTyping}; {writes} ({WpfProbe.Describe(source.Text)})", $"{whileTyping}、{writes} ({WpfProbe.Describe(source.Text)})")]);
            }, activate: true);
        }

        {
            // 使用例「ログやエラーの詳細は IsReadOnly にして、コピーできるようにする」。
            var box = new TextBox { Width = 200, Text = "error details", IsReadOnly = true };
            await ShowAsync(box, async () =>
            {
                box.CaretIndex = box.Text.Length;
                Type(box, "x");
                await Capture.SettleAsync(Window.GetWindow(box)!, 50);
                box.SelectAll();
                rows.Add([T("IsReadOnly: text after typing \"x\"; all selected: Copy / Cut / Paste can execute", "IsReadOnly: \"x\" を入力した後の Text、全選択で Copy / Cut / Paste が実行できるか"),
                    Pair(WpfProbe.Describe(box.Text), $"{ApplicationCommands.Copy.CanExecute(null, box)} / {ApplicationCommands.Cut.CanExecute(null, box)} / {ApplicationCommands.Paste.CanExecute(null, box)}")]);
            }, activate: true);
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<Loc>>> LayoutAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        foreach (TextWrapping wrapping in new[] { TextWrapping.NoWrap, TextWrapping.Wrap, TextWrapping.WrapWithOverflow })
        {
            var box = new TextBox { Width = 150, Text = WrapText, TextWrapping = wrapping };
            await ShowAsync(box, async () =>
            {
                int firstLine = box.GetLineText(0).TrimEnd().Length;
                rows.Add([T($"TextWrapping={wrapping}, width 150, demo text", $"TextWrapping={wrapping}、幅 150、デモの文字列"),
                    T($"{box.LineCount} lines, line 1 has {firstLine} chars, extent {D(box.ExtentWidth)} / viewport {D(box.ViewportWidth)}",
                      $"{box.LineCount} 行、1 行目は {firstLine} 文字、内容の幅 {D(box.ExtentWidth)} / 表示幅 {D(box.ViewportWidth)}")]);
                await Task.CompletedTask;
            });
        }

        string tenLines = string.Join("\n", Enumerable.Range(1, 10).Select(i => $"line {i}"));

        foreach (int minLines in new[] { 1, 2, 4, 6 })
        {
            // デモアプリの MinLines 欄と同じ指定（AcceptsReturn, VerticalAlignment=Top, VerticalScrollBarVisibility=Auto）。
            var box = new TextBox
            {
                Width = 150, AcceptsReturn = true, MinLines = minLines,
                VerticalAlignment = VerticalAlignment.Top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            await ShowAsync(box, async () =>
            {
                rows.Add([T($"MinLines={minLines}, empty", $"MinLines={minLines}、空"), Height(box.ActualHeight)]);
                await Task.CompletedTask;
            });
        }

        // MinLines が効く条件を切り分ける。
        foreach ((Loc label, Func<TextBox> build, bool inGrid) in new (Loc, Func<TextBox>, bool)[]
        {
            (T("MinLines=4, TextWrapping=Wrap, empty", "MinLines=4、TextWrapping=Wrap、空"), () => new TextBox { Width = 150, MinLines = 4, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Top }, false),
            (T("MinLines=4, one line of text", "MinLines=4、1 行の文字列"), () => new TextBox { Width = 150, MinLines = 4, AcceptsReturn = true, Text = "a", VerticalAlignment = VerticalAlignment.Top }, false),
            (T("MinLines=4, empty, in a 150 x 300 Grid, Top", "MinLines=4、空、150 x 300 の Grid の中で Top"), () => new TextBox { MinLines = 4, AcceptsReturn = true, VerticalAlignment = VerticalAlignment.Top }, true),
        })
        {
            TextBox box = build();
            FrameworkElement content = box;
            if (inGrid)
            {
                var grid = new Grid { Width = 150, Height = 300 };
                grid.Children.Add(box);
                content = grid;
            }

            await ShowAsync(content, async () =>
            {
                rows.Add([label, Height(box.ActualHeight)]);
                await Task.CompletedTask;
            });
        }

        {
            // デモアプリの MinLines 欄と同じ XAML から作る。
            var box = SceneContext.LoadXaml<TextBox>(
                """
                <TextBox Width="150" VerticalAlignment="Top" AcceptsReturn="True" MinLines="4"
                         VerticalScrollBarVisibility="Auto" />
                """);
            await ShowAsync(box, async () =>
            {
                rows.Add([T("MinLines=\"4\" written in XAML (the demo app's markup), empty", "XAML に MinLines=\"4\"（デモアプリのマークアップ）、空"), Height(box.ActualHeight)]);

                // 表示後に別のプロパティを変えると、MinLines が効き始めるか。
                box.Text = "typed";
                box.UpdateLayout();
                string afterText = D(box.ActualHeight);
                box.FontSize += 1;
                box.UpdateLayout();
                rows.Add([T("  then Text changed / then FontSize changed", "  続けて Text を変更 / FontSize を変更"), T($"height {afterText} / {D(box.ActualHeight)}", $"高さ {afterText} / {D(box.ActualHeight)}")]);
                await Task.CompletedTask;
            });
        }

        {
            // 表示してから MinLines を設定する。
            var box = new TextBox { Width = 150, AcceptsReturn = true, VerticalAlignment = VerticalAlignment.Top };
            await ShowAsync(box, async () =>
            {
                string before = D(box.ActualHeight);
                box.MinLines = 4;
                box.UpdateLayout();
                string after = D(box.ActualHeight);
                box.MaxLines = 10;
                box.UpdateLayout();
                rows.Add([T("MinLines=4 set after showing: height before / after / + MaxLines=10", "表示後に MinLines=4 を設定: 設定前 / 設定後 / MaxLines=10 を足した後の高さ"),
                    $"{before} / {after} / {D(box.ActualHeight)}"]);
                await Task.CompletedTask;
            });
        }

        foreach ((Loc label, string minLinesInXaml) in new (Loc, string)[]
        {
            (T("MinLines=4 set in a Loaded handler, not in XAML: height", "XAML ではなく Loaded ハンドラーで MinLines=4: 高さ"), ""),
            (T("MinLines=\"4\" in XAML, and 4 set again in a Loaded handler: height", "XAML に MinLines=\"4\"、Loaded ハンドラーでも 4 を設定: 高さ"), " MinLines=\"4\""),
        })
        {
            // Loaded のハンドラーで MinLines を設定する。XAML に同じ値があると、値が変わらないので効かない可能性がある。
            var box = SceneContext.LoadXaml<TextBox>(
                $"""
                <TextBox Width="150" VerticalAlignment="Top" AcceptsReturn="True"{minLinesInXaml}
                         VerticalScrollBarVisibility="Auto" />
                """);
            box.Loaded += (_, _) => box.MinLines = 4;
            await ShowAsync(box, async () =>
            {
                rows.Add([label, D(box.ActualHeight)]);
                await Task.CompletedTask;
            });
        }

        foreach (int maxLines in new[] { 2, 4, 6 })
        {
            var box = new TextBox
            {
                Width = 150, AcceptsReturn = true, MaxLines = maxLines, Text = tenLines,
                VerticalAlignment = VerticalAlignment.Top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            await ShowAsync(box, async () =>
            {
                var viewer = (ScrollViewer)box.Template.FindName("PART_ContentHost", box);
                rows.Add([T($"MaxLines={maxLines}, 10 lines", $"MaxLines={maxLines}、10 行"),
                    T($"height {D(box.ActualHeight)}, vertical bar {viewer.ComputedVerticalScrollBarVisibility}", $"高さ {D(box.ActualHeight)}、縦のバー {viewer.ComputedVerticalScrollBarVisibility}")]);
                await Task.CompletedTask;
            });
        }

        foreach (ScrollBarVisibility? visibility in new ScrollBarVisibility?[] { null, ScrollBarVisibility.Auto })
        {
            var box = new TextBox { Width = 150, Height = 60, AcceptsReturn = true, Text = tenLines };
            if (visibility is not null)
            {
                box.VerticalScrollBarVisibility = visibility.Value;
            }

            await ShowAsync(box, async () =>
            {
                var viewer = (ScrollViewer)box.Template.FindName("PART_ContentHost", box);
                rows.Add([T($"Height=60, 10 lines, VerticalScrollBarVisibility {(visibility is null ? "not set" : visibility.ToString())}", $"Height=60、10 行、VerticalScrollBarVisibility {(visibility is null ? "指定なし" : visibility.ToString())}"),
                    T($"bar {viewer.ComputedVerticalScrollBarVisibility}, extent {D(box.ExtentHeight)} / viewport {D(box.ViewportHeight)}", $"バー {viewer.ComputedVerticalScrollBarVisibility}、内容の高さ {D(box.ExtentHeight)} / 表示の高さ {D(box.ViewportHeight)}")]);
                await Task.CompletedTask;
            });
        }

        {
            var panel = new StackPanel { Width = 150 };
            var box = new TextBox { AcceptsReturn = true, Text = tenLines };
            panel.Children.Add(box);
            await ShowAsync(panel, async () =>
            {
                rows.Add([T("AcceptsReturn, 10 lines, inside a StackPanel (no height limit)", "AcceptsReturn、10 行、StackPanel の中（高さの制限なし）"), Height(box.ActualHeight)]);
                await Task.CompletedTask;
            });
        }

        foreach (TextAlignment alignment in new[] { TextAlignment.Left, TextAlignment.Center, TextAlignment.Right })
        {
            var box = new TextBox { Width = 200, Text = "123", TextAlignment = alignment };
            await ShowAsync(box, async () =>
            {
                double x = box.GetRectFromCharacterIndex(0).X;
                rows.Add([T($"TextAlignment={alignment}, \"123\" in width 200", $"TextAlignment={alignment}、幅 200 に \"123\""), T($"first character at x {D(x)}", $"1 文字目の x 座標 {D(x)}")]);
                await Task.CompletedTask;
            });
        }

        {
            var decorations = (System.Windows.TextDecorationCollection)new TextDecorationCollectionConverter()
                .ConvertFromInvariantString("Underline, Strikethrough")!;
            string locations = string.Join(", ", decorations.Select(d => d.Location));
            rows.Add(["TextDecorations=\"Underline, Strikethrough\"",
                T($"{decorations.Count} decorations: {locations}", $"装飾 {decorations.Count} 個: {locations}")]);
        }

        {
            var box = new TextBox { Width = 150, Height = 60, AcceptsReturn = true, Text = tenLines };
            await ShowAsync(box, async () =>
            {
                Exception? thrown = null;
                await Task.Run(() =>
                {
                    try
                    {
                        box.ScrollToEnd();
                    }
                    catch (Exception ex)
                    {
                        thrown = ex;
                    }
                });
                rows.Add([T("ScrollToEnd() from a worker thread", "ワーカースレッドから ScrollToEnd()"), thrown is null ? T("no exception", "例外なし") : thrown.GetType().Name]);

                box.ScrollToEnd();
                box.UpdateLayout();
                rows.Add([T("ScrollToEnd() on the UI thread: VerticalOffset / scrollable height", "UI スレッドで ScrollToEnd(): VerticalOffset / スクロールできる高さ"),
                    $"{D(box.VerticalOffset)} / {D(box.ExtentHeight - box.ViewportHeight)}"]);
            });
        }

        {
            // 既定のテンプレートが IsReadOnly で見た目を変えるか。
            var box = new TextBox();
            var host = new Grid();
            host.Children.Add(box);
            Layout(host, 200, 50);
            int readOnlyTriggers = box.Template.Triggers.OfType<Trigger>().Count(t => t.Property == TextBoxBase.IsReadOnlyProperty);
            var readOnly = new TextBox { IsReadOnly = true };
            host.Children.Add(readOnly);
            Layout(host, 200, 50);
            rows.Add([T("default template: IsReadOnly triggers / Background, BorderBrush", "既定のテンプレート: IsReadOnly のトリガー数 / Background、BorderBrush"),
                T($"{readOnlyTriggers} / {box.Background} {box.BorderBrush} (normal), {readOnly.Background} {readOnly.BorderBrush} (read-only)",
                  $"{readOnlyTriggers} / {box.Background} {box.BorderBrush}（通常）、{readOnly.Background} {readOnly.BorderBrush}（読み取り専用）")]);
        }

        return rows;
    }
}

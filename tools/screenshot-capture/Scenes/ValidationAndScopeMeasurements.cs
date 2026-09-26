using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 入力検証・UserControl の依存関係プロパティを実測する部品。
/// </summary>
internal static class ValidationAndScopeMeasurements
{
    /// <summary>INotifyDataErrorInfo の ErrorsChanged を、指定したスレッドから発生させられるソース。</summary>
    private sealed class ThreadedErrors : INotifyDataErrorInfo
    {
        private string? _error;

        public string Value { get; set; } = "value";

        public bool HasErrors => _error is not null;

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public IEnumerable GetErrors(string? propertyName) =>
            _error is not null && propertyName == nameof(Value) ? new[] { _error } : Array.Empty<string>();

        public void SetError(string error)
        {
            _error = error;
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Value)));
        }
    }

    /// <summary>
    /// ErrorsChanged を UI スレッドから発生させた場合と、バックグラウンドのスレッドから発生させた場合で、
    /// TextBox にエラーが反映されるか（Validation.HasError）を測る。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> ErrorsChangedThreadAsync()
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (bool background in new[] { false, true })
        {
            var source = new ThreadedErrors();
            var box = new TextBox { Width = 120, DataContext = source };
            box.SetBinding(TextBox.TextProperty, new Binding(nameof(ThreadedErrors.Value)));
            int? raisedOn = null;
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    background ? "ErrorsChanged raised on a background thread" : "ErrorsChanged raised on the UI thread",
                    box,
                    _ =>
                    [
                        $"Validation.HasError {WpfProbe.Describe(Validation.GetHasError(box))}" +
                        (raisedOn is { } id ? $" (raised on thread {(id == Environment.CurrentManagedThreadId ? "UI" : "other than UI")})" : ""),
                    ],
                    Act: async _ =>
                    {
                        if (background)
                        {
                            await Task.Run(() =>
                            {
                                raisedOn = Environment.CurrentManagedThreadId;
                                source.SetError("invalid");
                            });
                        }
                        else
                        {
                            raisedOn = Environment.CurrentManagedThreadId;
                            source.SetError("invalid");
                        }
                    }),
            ]));
        }

        return rows;
    }

    // ------------------------------------------------------------------
    // 入力検証のエラーが表示されない理由
    // ------------------------------------------------------------------

    /// <summary><see cref="IDataErrorInfo"/> 側のソース。常にエラーを返す。</summary>
    private sealed class DataErrorSource : IDataErrorInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Error => string.Empty;

        public string this[string columnName] => "always invalid";
    }

    /// <summary><see cref="INotifyDataErrorInfo"/> 側のソース。常にエラーを返す。</summary>
    private sealed class NotifyDataErrorSource : INotifyDataErrorInfo
    {
        public string Name { get; set; } = string.Empty;

        public bool HasErrors => true;

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged
        {
            add { }
            remove { }
        }

        public IEnumerable GetErrors(string? propertyName) => new[] { "always invalid" };
    }

    /// <summary>常に失敗する <see cref="ValidationRule"/>。</summary>
    private sealed class AlwaysInvalidRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
            new(false, "always invalid");
    }

    /// <summary>
    /// 検証の 3 段階のうち、どこで止まるかを測る。
    ///
    /// 記事の主張は「実装しただけでは検証に参加しない」ことなので、
    /// 発生（Errors の件数）と描画（アドーナーの有無）を分けて出す。
    /// </summary>
    public static Task<List<IReadOnlyList<string>>> ValidationStagesAsync() =>
        WpfProbe.MeasureAsync(
        [
            BuildValidationCase("IDataErrorInfo only", new DataErrorSource(), validatesOnDataErrors: false, rule: false, errorTemplate: true),
            BuildValidationCase("+ ValidatesOnDataErrors=True", new DataErrorSource(), validatesOnDataErrors: true, rule: false, errorTemplate: true),
            BuildValidationCase("INotifyDataErrorInfo only", new NotifyDataErrorSource(), validatesOnDataErrors: false, rule: false, errorTemplate: true),
            BuildValidationCase("ValidationRules", new DataErrorSource(), validatesOnDataErrors: false, rule: true, errorTemplate: true),
            BuildValidationCase("ValidationRules, ErrorTemplate={x:Null}", new DataErrorSource(), validatesOnDataErrors: false, rule: true, errorTemplate: false),
        ]);

    private static IReadOnlyList<string> ReadValidation(TextBox box)
    {
        Adorner[] adorners = AdornerLayer.GetAdornerLayer(box)?.GetAdorners(box) ?? [];

        return
        [
            WpfProbe.Describe(Validation.GetHasError(box)),
            Validation.GetErrors(box).Count.ToString(),
            adorners.Length.ToString(),
        ];
    }

    private static WpfProbe.Case BuildValidationCase(
        string label, object source, bool validatesOnDataErrors, bool rule, bool errorTemplate)
    {
        var box = new TextBox { Width = 160 };

        var binding = new Binding("Name")
        {
            Source = source,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            ValidatesOnDataErrors = validatesOnDataErrors,
        };

        if (rule)
        {
            binding.ValidationRules.Add(new AlwaysInvalidRule());
        }

        if (!errorTemplate)
        {
            Validation.SetErrorTemplate(box, null);
        }

        box.SetBinding(TextBox.TextProperty, binding);

        var grid = new Grid();
        grid.Children.Add(box);

        // 値を書き戻させて検証を走らせる。バインド直後は評価されない場合がある。
        box.SetCurrentValue(TextBox.TextProperty, "x");
        return new WpfProbe.Case(label, grid, _ => ReadValidation(box));
    }

    // ------------------------------------------------------------------
    // UserControl 内から自身の依存関係プロパティを参照できるか
    // ------------------------------------------------------------------

    /// <summary>記事の InfoCard に相当する UserControl。</summary>
    private sealed class InfoCard : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(InfoCard), new PropertyMetadata("(unset)"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
    }

    /// <summary>利用側の ViewModel。InfoCard は持たないプロパティを 1 つ用意する。</summary>
    private sealed class PageViewModel
    {
        public string HeaderText { get; } = "from PageViewModel";
    }

    /// <summary>
    /// 内部からの参照方法を変えて、届く値を測る。
    /// あわせて、内部要素から見た DataContext の型も出す。
    /// </summary>
    public static Task<List<IReadOnlyList<string>>> UserControlPropertyScopeAsync() =>
        WpfProbe.MeasureAsync(
        [
            BuildCardCase("{Binding Title}", null),
            BuildCardCase("RelativeSource Self (on the inner element)", new RelativeSource(RelativeSourceMode.Self)),
            BuildCardCase(
                "RelativeSource AncestorType=UserControl",
                new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(UserControl) }),
        ]);

    /// <summary>
    /// 参照の起点と置き場所を変えて、UserControl の Title へ届くかを測る。
    /// カードには名前スコープを持たせ、自身を Root という名前で登録する（x:Name="Root" と同じ）。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> UserControlResolutionAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        Binding ByAncestor() => new(nameof(InfoCard.Title)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(UserControl) } };
        Binding ByName() => new(nameof(InfoCard.Title)) { ElementName = "Root" };

        // 直接の子。ElementName と、内側のルート要素への DataContext の委譲。
        rows.Add(await ReadTextAsync("inner TextBlock, ElementName=Root", _ => ByName()));
        rows.Add(await ReadTextAsync("inner root DataContext = the control, then {Binding Title}", _ => new Binding(nameof(InfoCard.Title)), delegateDataContext: true));

        // ContextMenu の中。
        foreach ((string name, Func<Binding> make, bool delegated) in new (string, Func<Binding>, bool)[]
        {
            ("ContextMenu MenuItem.Header, AncestorType=UserControl", ByAncestor, false),
            ("ContextMenu MenuItem.Header, ElementName=Root", ByName, false),
            ("ContextMenu MenuItem.Header, {Binding Title} with DataContext delegated", () => new Binding(nameof(InfoCard.Title)), true),
        })
        {
            (InfoCard card, Grid root) = BuildNamedCard(delegated);
            var item = new MenuItem();
            item.SetBinding(HeaderedItemsControl.HeaderProperty, make());
            var menu = new ContextMenu { Items = { item } };
            var button = new Button { Content = "menu", ContextMenu = menu };
            root.Children.Add(button);
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    name,
                    Host(card),
                    _ => [WpfProbe.Describe(item.Header)],
                    Act: async _ =>
                    {
                        menu.PlacementTarget = button;
                        menu.IsOpen = true;
                        await Task.Delay(100);
                    }),
            ]));
            menu.IsOpen = false;
        }

        // インラインの Popup の中。
        foreach ((string name, Func<Binding> make) in new (string, Func<Binding>)[]
        {
            ("inline Popup, AncestorType=UserControl", ByAncestor),
            ("inline Popup, ElementName=Root", ByName),
        })
        {
            (InfoCard card, Grid root) = BuildNamedCard(false);
            var text = new TextBlock();
            text.SetBinding(TextBlock.TextProperty, make());
            var popup = new System.Windows.Controls.Primitives.Popup { Child = text };
            root.Children.Add(popup);
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    name,
                    Host(card),
                    _ => [text.Text.Length == 0 ? "(empty)" : text.Text],
                    Act: async _ =>
                    {
                        popup.IsOpen = true;
                        await Task.Delay(100);
                    }),
            ]));
            popup.IsOpen = false;
        }

        // DataTemplate の中。インラインで書いた場合と、UserControl.Resources にキー付きで置いた場合。
        foreach (bool fromResources in new[] { false, true })
        {
            foreach ((string name, Func<Binding> make) in new (string, Func<Binding>)[]
            {
                ("AncestorType=UserControl", ByAncestor),
                ("ElementName=Root", ByName),
            })
            {
                (InfoCard card, Grid root) = BuildNamedCard(false);
                var factory = new FrameworkElementFactory(typeof(TextBlock));
                factory.SetBinding(TextBlock.TextProperty, make());
                var template = new DataTemplate { VisualTree = factory };
                var presenter = new ContentControl { Content = "x" };
                if (fromResources)
                {
                    card.Resources["CardTemplate"] = template;
                    presenter.SetResourceReference(ContentControl.ContentTemplateProperty, "CardTemplate");
                }
                else
                {
                    presenter.ContentTemplate = template;
                }

                root.Children.Add(presenter);
                string label = (fromResources ? "DataTemplate in UserControl.Resources, " : "inline DataTemplate, ") + name;
                rows.AddRange(await WpfProbe.MeasureAsync(
                [
                    new WpfProbe.Case(
                        label,
                        Host(card),
                        _ =>
                        {
                            TextBlock? shown = DemoProbe.Descendants(presenter).OfType<TextBlock>().FirstOrDefault();
                            return [shown is null ? "(no element)" : shown.Text.Length == 0 ? "(empty)" : shown.Text];
                        }),
                ]));
            }
        }

        // 別の UserControl の内側。AncestorType=UserControl はどちらを選ぶか（Tag で見分ける）。
        {
            (InfoCard card, Grid root) = BuildNamedCard(false);
            card.Tag = "outer InfoCard";
            var text = new TextBlock();
            text.SetBinding(TextBlock.TextProperty, new Binding(nameof(FrameworkElement.Tag)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(UserControl) } });
            root.Children.Add(new UserControl { Tag = "inner UserControl", Content = text });
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    "UserControl nested inside the card, AncestorType=UserControl (Path=Tag)",
                    Host(card),
                    _ => [text.Text]),
            ]));
        }

        return rows;

        async Task<IReadOnlyList<string>> ReadTextAsync(string label, Func<InfoCard, Binding> make, bool delegateDataContext = false)
        {
            (InfoCard card, Grid root) = BuildNamedCard(delegateDataContext);
            var text = new TextBlock();
            text.SetBinding(TextBlock.TextProperty, make(card));
            root.Children.Add(text);
            List<IReadOnlyList<string>> measured = await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(label, Host(card), _ => [text.Text.Length == 0 ? "(empty)" : text.Text]),
            ]);
            return measured[0];
        }
    }

    /// <summary>
    /// 名前スコープを持ち、自身を Root として登録したカード。内側のルート要素は Grid。
    /// <paramref name="delegateDataContext"/> が真なら、Grid の DataContext をカード自身にする。
    /// </summary>
    private static (InfoCard Card, Grid Root) BuildNamedCard(bool delegateDataContext)
    {
        var root = new Grid();
        var card = new InfoCard { Title = "from InfoCard.Title", Content = root };
        NameScope.SetNameScope(card, new NameScope());
        card.RegisterName("Root", card);
        if (delegateDataContext)
        {
            root.DataContext = card;
        }

        return (card, root);
    }

    /// <summary>BindsTwoWayByDefault を付けて Title を登録した InfoCard。記事の対処の 1 つと同じ。</summary>
    private sealed class TwoWayInfoCard : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(TwoWayInfoCard),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
    }

    /// <summary>値を書き換えられ、変更を通知する利用側の ViewModel。Title を持つかどうかを選べる。</summary>
    private sealed class EditablePage : INotifyPropertyChanged
    {
        private string _headerText = "from PageViewModel";

        public string HeaderText
        {
            get => _headerText;
            set
            {
                _headerText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderText)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class PageWithTitle
    {
        public string HeaderText { get; } = "VM-TITLE";

        public string Title { get; } = "VM-OWN-TITLE";
    }

    private sealed class Detail
    {
        public string Title { get; } = "from Detail.Title";
    }

    private sealed class DetailWithoutTitle
    {
        public string Name { get; } = "no Title here";
    }

    /// <summary>データバインドのトレースのうち、System.Windows.Data Error の行を番号ごとに数える。</summary>
    private sealed class BindingErrorCounter : System.Diagnostics.TraceListener
    {
        private readonly List<string> _codes = [];

        public string Summary => _codes.Count == 0 ? "0 errors" : string.Join(", ", _codes.GroupBy(c => c).Select(g => $"Error {g.Key} x{g.Count()}"));

        public override void Write(string? message) => Count(message);

        public override void WriteLine(string? message) => Count(message);

        private void Count(string? message)
        {
            var match = message is null ? null : System.Text.RegularExpressions.Regex.Match(message, @"System\.Windows\.Data Error: (\d+)");
            if (match is { Success: true })
            {
                _codes.Add(match.Groups[1].Value);
            }
        }
    }

    /// <summary>計測の間だけデータバインドのトレースを数える。</summary>
    private static async Task<(T Result, string Errors)> WithBindingTraceAsync<T>(Func<Task<T>> body)
    {
        var counter = new BindingErrorCounter();
        System.Diagnostics.PresentationTraceSources.Refresh();
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Warning;
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(counter);
        try
        {
            T result = await body();
            return (result, counter.Summary);
        }
        finally
        {
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Remove(counter);
        }
    }

    private static string Shown(TextBlock text) => text.Text.Length == 0 ? "(empty)" : text.Text;

    /// <summary>
    /// 記事の本文と注意点に書いた、残りの「実測では」を測る。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> UserControlMoreAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        // 要素の組み立てもトレースを数える範囲に入れる。DataContext をローカルに持つ要素では、
        // SetBinding の時点でバインドが働き、表示より前にエラーが出るためである。
        async Task Add(string label, Func<(FrameworkElement Content, Func<string> Read, Func<Task>? Act)> build)
        {
            (List<IReadOnlyList<string>> measured, string errors) = await WithBindingTraceAsync(() =>
            {
                (FrameworkElement content, Func<string> read, Func<Task>? act) = build();
                return WpfProbe.MeasureAsync(
                [
                    new WpfProbe.Case(label, content, _ => [read()], act is null ? null : _ => act()),
                ]);
            });
            rows.Add([label, $"{measured[0][1]}; {errors}"]);
        }

        TextBlock InnerTitle()
        {
            var inner = new TextBlock();
            inner.SetBinding(TextBlock.TextProperty, new Binding(nameof(InfoCard.Title)));
            return inner;
        }

        // 1. 利用側が Title をバインドし、内部は素の {Binding Title}。
        await Add("caller Title={Binding HeaderText}, inner {Binding Title}", () =>
        {
            TextBlock inner = InnerTitle();
            var card = new InfoCard { Content = inner };
            card.SetBinding(InfoCard.TitleProperty, new Binding(nameof(PageViewModel.HeaderText)));
            return (Host(card), () => $"Title {card.Title}, inner {Shown(inner)}", null);
        });

        // 2. 利用側の ViewModel が同名の Title を持つ。
        await Add("caller's view model also has Title", () =>
        {
            TextBlock inner = InnerTitle();
            var card = new InfoCard { Content = inner };
            card.SetBinding(InfoCard.TitleProperty, new Binding(nameof(PageWithTitle.HeaderText)));
            return (new Grid { DataContext = new PageWithTitle(), Children = { card } }, () => $"Title {card.Title}, inner {Shown(inner)}", null);
        });

        // 3. DataContext の無い親に置く。
        await Add("parent without DataContext, Title set", () =>
        {
            TextBlock inner = InnerTitle();
            var card = new InfoCard { Title = "set directly", Content = inner };
            return (new Grid { Children = { card } }, () => $"inner {Shown(inner)}", null);
        });

        // 4. 内側のルート要素へ DataContext を委譲。5. そこから利用側の DataContext へ届くか。
        await Add("DataContext delegated to the inner Grid", () =>
        {
            (InfoCard card, Grid root) = BuildNamedCard(delegateDataContext: true);
            TextBlock inner = InnerTitle();
            root.Children.Add(inner);
            return (Host(card), () => $"inner sees {inner.DataContext?.GetType().Name}, card keeps {card.DataContext?.GetType().Name}", null);
        });
        await Add("delegated, DataContext.HeaderText via AncestorType", () =>
        {
            (InfoCard card, Grid root) = BuildNamedCard(delegateDataContext: true);
            var outer = new TextBlock();
            outer.SetBinding(TextBlock.TextProperty, new Binding("DataContext.HeaderText") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(InfoCard) } });
            root.Children.Add(outer);
            return (Host(card), () => Shown(outer), null);
        });

        // 6. BindsTwoWayByDefault の Title を、内部の TextBox から書き換える。
        await Add("BindsTwoWayByDefault, type xyz inside", () =>
        {
            var page = new EditablePage();
            var box = new TextBox { Width = 120 };
            box.SetBinding(TextBox.TextProperty, new Binding(nameof(TwoWayInfoCard.Title)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(UserControl) }, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            var card = new TwoWayInfoCard { Content = box };
            card.SetBinding(TwoWayInfoCard.TitleProperty, new Binding(nameof(EditablePage.HeaderText)));
            return (new Grid { DataContext = page, Children = { card } }, () => $"HeaderText {page.HeaderText}", async () =>
            {
                await DemoProbe.FocusAsync(box);
                box.SelectAll();
                DemoProbe.TypeInto(box, "xyz");
            });
        });

        // 7. コンストラクタで DataContext = this にした場合の、利用側のバインド。
        await Add("DataContext = this, caller binds Title", () =>
        {
            TextBlock inner = InnerTitle();
            var card = new InfoCard { Content = inner };
            card.DataContext = card;
            card.SetBinding(InfoCard.TitleProperty, new Binding(nameof(PageViewModel.HeaderText)));
            return (Host(card), () => $"Title {card.Title}", null);
        });

        // 8. DataContext = this の後に、利用側が DataContext を差し替える。
        foreach ((string name, object detail) in new (string, object)[] { ("lacks Title", new DetailWithoutTitle()), ("has Title", new Detail()) })
        {
            await Add($"DataContext = this, caller's DataContext {name}", () =>
            {
                TextBlock inner = InnerTitle();
                var card = new InfoCard { Content = inner };
                card.DataContext = card;
                card.DataContext = detail;
                return (Host(card), () => $"inner {Shown(inner)}", null);
            });
        }

        // 9〜11. 外側が OneWay のまま内部から値を変える。代入・内部の TwoWay・SetCurrentValue。
        foreach (string way in new[] { "Title = x", "inner TwoWay write-back", "SetCurrentValue" })
        {
            await Add($"caller OneWay, inside {way}", () =>
            {
                var page = new EditablePage();
                var box = new TextBox { Width = 120 };
                box.SetBinding(TextBox.TextProperty, new Binding(nameof(InfoCard.Title)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(UserControl) }, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                var card = new InfoCard { Content = box };
                card.SetBinding(InfoCard.TitleProperty, new Binding(nameof(EditablePage.HeaderText)));
                return (
                    new Grid { DataContext = page, Children = { card } },
                    () => $"binding {(BindingOperations.GetBindingExpression(card, InfoCard.TitleProperty) is null ? "removed" : "kept")}; HeaderText=new -> Title {card.Title}",
                    async () =>
                    {
                        switch (way)
                        {
                            case "SetCurrentValue":
                                card.SetCurrentValue(InfoCard.TitleProperty, "x");
                                break;
                            case "inner TwoWay write-back":
                                await DemoProbe.FocusAsync(box);
                                box.SelectAll();
                                DemoProbe.TypeInto(box, "x");
                                break;
                            default:
                                card.Title = "x";
                                break;
                        }

                        await Task.Delay(50);
                        page.HeaderText = "new";
                    });
            });
        }

        // 12. 利用側にも Root という名前の要素がある。
        await Add("caller also names an element Root", () =>
        {
            (InfoCard card, Grid root) = BuildNamedCard(false);
            card.Tag = "the card";
            var inner = new TextBlock();
            inner.SetBinding(TextBlock.TextProperty, new Binding(nameof(FrameworkElement.Tag)) { ElementName = "Root" });
            root.Children.Add(inner);

            var consumerRoot = new Border { Tag = "caller's Root" };
            var outer = new TextBlock();
            outer.SetBinding(TextBlock.TextProperty, new Binding(nameof(FrameworkElement.Tag)) { ElementName = "Root" });
            var page = new StackPanel { Children = { consumerRoot, outer, card } };
            NameScope.SetNameScope(page, new NameScope());
            page.RegisterName("Root", consumerRoot);
            return (page, () => $"inside -> {Shown(inner)}, caller -> {Shown(outer)}", null);
        });

        return rows;
    }

    /// <summary>利用側の ViewModel を DataContext に持つ親の下にカードを置く。</summary>
    private static Grid Host(InfoCard card) => new() { DataContext = new PageViewModel(), Children = { card } };

    private static WpfProbe.Case BuildCardCase(string label, RelativeSource? relativeSource)
    {
        var inner = new TextBlock();
        var binding = new Binding(nameof(InfoCard.Title));

        if (relativeSource is not null)
        {
            binding.RelativeSource = relativeSource;
        }

        inner.SetBinding(TextBlock.TextProperty, binding);

        var card = new InfoCard { Title = "from InfoCard.Title", Content = inner };

        var grid = new Grid { DataContext = new PageViewModel() };
        grid.Children.Add(card);

        return new WpfProbe.Case(
            label,
            grid,
            _ =>
            [
                inner.Text.Length == 0 ? "(empty)" : inner.Text,
                inner.DataContext?.GetType().Name ?? "null",
            ]);
    }
}

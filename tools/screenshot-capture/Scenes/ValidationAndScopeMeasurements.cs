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

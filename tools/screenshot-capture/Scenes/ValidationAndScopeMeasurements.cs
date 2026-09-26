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
                new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(InfoCard) }),
        ]);

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

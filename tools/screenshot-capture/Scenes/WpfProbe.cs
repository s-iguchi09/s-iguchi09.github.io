using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 「この書き方だと効かない、こう直すと効く」を実測するための共通の仕掛け。
///
/// WPF の記事の多くはこの形をしている。効く・効かないは実際に表示して
/// プロパティを読まないと分からないため、記事ごとに書き分けるのではなく
/// ここに集約する。
///
/// 各条件は、記事に載せた XAML をそのまま <see cref="SceneContext.LoadXaml{T}"/> へ
/// 渡して組み立てることを想定している。図と本文のコードが食い違わないようにするためである。
/// </summary>
internal static class WpfProbe
{
    /// <summary>
    /// 1 つの条件。
    /// </summary>
    /// <param name="Label">表の左端に出す条件名。</param>
    /// <param name="Content">ウィンドウに載せる要素。</param>
    /// <param name="Read">表示後に読み取る値。返した文字列がそのまま表のセルになる。</param>
    /// <param name="Act">読み取りの前に行う操作。入力・スクロール・プロパティ変更など。</param>
    internal sealed record Case(
        string Label,
        FrameworkElement Content,
        Func<FrameworkElement, IReadOnlyList<string>> Read,
        Func<FrameworkElement, Task>? Act = null);

    /// <summary>
    /// 各条件を実際に表示し、値を読んで表の行にする。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> MeasureAsync(IEnumerable<Case> cases)
    {
        var rows = new List<IReadOnlyList<string>>();

        foreach (Case probe in cases)
        {
            var window = new Window
            {
                Title = probe.Label,
                Content = probe.Content,
                Width = 320,
                Height = 240,
                // 条件を順に開いていくため、フォーカスを奪わない。
                ShowActivated = false,
            };

            try
            {
                await Capture.ShowAndSettleAsync(window);

                if (probe.Act is not null)
                {
                    await probe.Act(probe.Content);
                    await Capture.SettleAsync(window);
                }

                rows.Add([probe.Label, .. probe.Read(probe.Content)]);
            }
            finally
            {
                window.Close();
            }
        }

        return rows;
    }

    /// <summary>
    /// 依存関係プロパティの実効値と、その値がどこから来たかを返す。
    ///
    /// 「Trigger が効かない」類の記事では、値そのものより
    /// <see cref="BaseValueSource"/> のほうが原因を示す。
    /// ローカル値は Style の Trigger より優先されるため、
    /// <see cref="BaseValueSource.Local"/> が出ていれば Trigger は上書きできない。
    /// </summary>
    public static string ValueAndSource(DependencyObject target, DependencyProperty property)
    {
        object? value = target.GetValue(property);
        ValueSource source = DependencyPropertyHelper.GetValueSource(target, property);
        return $"{Describe(value)} ({source.BaseValueSource})";
    }

    /// <summary>バインドが張られているか、外れているかを返す。</summary>
    public static string BindingState(FrameworkElement element, DependencyProperty property)
    {
        BindingExpression? expression = BindingOperations.GetBindingExpression(element, property);
        return expression is null ? "no binding" : expression.Status.ToString();
    }

    public static string Describe(object? value) => value switch
    {
        null => "null",
        string text => text.Length == 0 ? "(empty)" : text,
        bool flag => flag ? "True" : "False",
        // 図に載る文字列なので、撮影マシンのロケールで小数点が変わらないよう固定する。
        double number => number.ToString("0.##", CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "null",
    };
}

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF で TextBox の入力を View から明示的に書き戻す UpdateSource の落とし穴」の図。
///
/// <c>GetBindingExpression</c> が <c>null</c> を返す条件と、
/// <c>UpdateSource()</c> がソースを更新する条件を実際に動かして確かめる。
/// どちらも例外ではなく「何も起きない」形で現れるため、実行しないと区別できない。
/// </summary>
internal sealed class UpdateSourcePitfallScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "GetBindingExpression が null を返す条件（リテラル・MultiBinding・TemplateBinding）",
        "バインドを張ったまま UpdateSource を呼んだ場合と、Text を書き換えてから呼んだ場合の違い",
        "OneWay / OneTime では Text への代入がバインドを外し、その後の UpdateSource が例外になること",
        "同じ OneWay / OneTime でも、TextInput 経由の入力ではバインドが外れず、ソースも更新されないこと",
    ];

    public string Slug => "wpf-textbox-updatesource-from-view-pitfalls";

    public async Task CaptureAsync(SceneContext context)
    {
        var rows = new List<IReadOnlyList<string>>();
        rows.Add(Run("Text=\"literal\"", SetLiteral));
        rows.Add(Run("MultiBinding", SetMultiBinding));
        rows.Add(Run("TemplateBinding", SetTemplateBinding));
        rows.Add(Run("Binding Mode=OneTime", box => SetBinding(box, BindingMode.OneTime)));
        rows.Add(Run("Binding Mode=OneWay", box => SetBinding(box, BindingMode.OneWay)));
        rows.Add(Run("Binding Mode=OneWayToSource", box => SetBinding(box, BindingMode.OneWayToSource)));
        rows.Add(Run("Binding Mode=TwoWay", box => SetBinding(box, BindingMode.TwoWay)));
        // 入力とコード代入では結果が違う。エディタが通る TextInput 経由も測る。
        rows.Add(RunTextInput("OneWay, typed via TextInput", BindingMode.OneWay));
        rows.Add(RunTextInput("OneTime, typed via TextInput", BindingMode.OneTime));
        rows.Add(RunTextInput("TwoWay, typed via TextInput", BindingMode.TwoWay));

        rows.Add(Run("TwoWay, then ClearBinding", box =>
        {
            SetBinding(box, BindingMode.TwoWay);
            return true;
        }, clearBeforeUpdate: true));

        await context.SaveTableAsync(
            "GetBindingExpression() and UpdateSource()",
            ["how Text is set", "GetBindingExpression", "UpdateSource() as-is", "after editing Text"],
            rows,
            "updatesource-pitfall-matrix.svg");
    }

    /// <summary>
    /// 1 つの構成について、式が取れるか・書き戻しが効くかを調べる。
    /// </summary>
    private static IReadOnlyList<string> Run(
        string label,
        Func<TextBox, bool> configure,
        bool clearBeforeUpdate = false)
    {
        var source = new AmountViewModel { Amount = "before" };
        var box = new TextBox { DataContext = source };

        if (!configure(box))
        {
            return [label, "-", "-", "-"];
        }

        BindingExpression? expression = box.GetBindingExpression(TextBox.TextProperty);
        if (expression is null)
        {
            return [label, "null", "cannot call", "cannot call"];
        }

        if (clearBeforeUpdate)
        {
            BindingOperations.ClearBinding(box, TextBox.TextProperty);
            return [label, "obtained", "-", Call(expression, source)];
        }

        // バインドを張ったまま、そのまま書き戻しを呼ぶ。
        string asIs = Call(expression, source);

        // ターゲット側の値を書き換えてから呼ぶ。
        // OneWay / OneTime では、この代入自体がバインドをローカル値で置き換える。
        source.Amount = "before";
        box.Text = "after";
        string afterEdit = Call(box.GetBindingExpression(TextBox.TextProperty) ?? expression, source);

        return [label, "obtained", asIs, afterEdit];
    }

    /// <summary>
    /// コードからの代入ではなく、<c>TextBox</c> のエディタが通る経路で 1 文字入れて測る。
    ///
    /// キーボード入力は <c>TextInput</c> イベントを経由し、この経路では
    /// ローカル値による置き換えが起きないためバインドが外れない。
    /// コードからの <c>box.Text = ...</c> とは結果が変わる。
    /// </summary>
    private static IReadOnlyList<string> RunTextInput(string label, BindingMode mode)
    {
        var source = new AmountViewModel { Amount = "before" };
        var box = new TextBox { DataContext = source };
        SetBinding(box, mode);

        var host = new Window { Content = box, Width = 200, Height = 80, ShowActivated = false };
        try
        {
            host.Show();
            box.Focus();
            box.CaretIndex = box.Text.Length;

            box.RaiseEvent(new TextCompositionEventArgs(
                Keyboard.PrimaryDevice,
                new TextComposition(InputManager.Current, box, "X"))
            {
                RoutedEvent = TextCompositionManager.TextInputEvent,
            });

            BindingExpression? after = box.GetBindingExpression(TextBox.TextProperty);
            string state = after is null ? "detached" : "still bound";
            string sourceValue = source.Amount == "before" ? "unchanged" : $"\"{source.Amount}\"";

            return [label, state, sourceValue, "-"];
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary><c>UpdateSource()</c> を呼び、ソースが変わったかどうかを返す。</summary>
    private static string Call(BindingExpression expression, AmountViewModel source)
    {
        try
        {
            expression.UpdateSource();
            return source.Amount == "after" ? "source updated" : "no change";
        }
        catch (Exception ex)
        {
            return ex.GetType().Name;
        }
    }

    private static bool SetLiteral(TextBox box)
    {
        box.Text = "fixed";
        return true;
    }

    private static bool SetBinding(TextBox box, BindingMode mode)
    {
        box.SetBinding(TextBox.TextProperty, new Binding(nameof(AmountViewModel.Amount))
        {
            Mode = mode,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
        });
        return true;
    }

    private static bool SetMultiBinding(TextBox box)
    {
        var multi = new MultiBinding { Converter = new JoinConverter() };
        multi.Bindings.Add(new Binding(nameof(AmountViewModel.Amount)));
        multi.Bindings.Add(new Binding(nameof(AmountViewModel.Amount)));
        box.SetBinding(TextBox.TextProperty, multi);
        return true;
    }

    /// <summary>
    /// <c>TemplateBinding</c> はテンプレート内でしか使えないため、
    /// <see cref="TextBox"/> をテンプレートに持つ <see cref="ContentControl"/> を組み立てる。
    /// </summary>
    private static bool SetTemplateBinding(TextBox box)
    {
        var factory = new FrameworkElementFactory(typeof(TextBox));
        factory.SetValue(FrameworkElement.NameProperty, "PART_Text");
        factory.SetValue(TextBox.TextProperty, new TemplateBindingExtension(ContentControl.ContentProperty));

        var host = new ContentControl
        {
            Content = "from template",
            Template = new ControlTemplate(typeof(ContentControl)) { VisualTree = factory },
        };

        var window = new Window { Content = host, Width = 200, Height = 120, ShowInTaskbar = false, Left = -4000 };
        window.Show();
        window.UpdateLayout();
        host.ApplyTemplate();

        var inner = host.Template.FindName("PART_Text", host) as TextBox;
        bool isNull = inner?.GetBindingExpression(TextBox.TextProperty) is null;
        window.Close();

        // テンプレート内の TextBox で判定済みのため、呼び出し元の box には設定しない。
        // 結果を伝えるため、null だった場合だけリテラルを入れて式が取れない状態にする。
        if (isNull)
        {
            box.Text = "template";
        }
        else
        {
            SetBinding(box, BindingMode.TwoWay);
        }

        return true;
    }

    private sealed class AmountViewModel : INotifyPropertyChanged
    {
        private string _amount = string.Empty;

        public string Amount
        {
            get => _amount;
            set
            {
                if (_amount == value)
                {
                    return;
                }

                _amount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Amount)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class JoinConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => string.Join("/", values);

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => [value, value];
    }
}

using System.Reflection;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「PasswordBox」（apps/wpf-standard-control-demo/passwordbox.html と日本語版）の記述を実測する。
///
/// キーボードからの文字入力は、TextCompositionManager で TextInput を発生させて再現する。
/// クリップボードは実際には操作せず、コピー・切り取りのコマンドが実行可能かどうかだけを読む。
/// </summary>
internal sealed class PasswordBoxDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-passwordbox";

    public string ImageDirectory => DemoProbe.ImageDirectory("passwordbox");

    public IReadOnlyList<string> Verifies =>
    [
        "PasswordBox の基底クラスと、Password が依存関係プロパティかどうか（バインドの対象にできるか）、PasswordChar が依存関係プロパティかどうか",
        "PasswordChar のメタデータの既定値と既定のスタイルが適用された後の値、MaxLength・SelectionOpacity・IsInactiveSelectionHighlightEnabled・CaretBrush・SelectionBrush の既定値",
        "パスワードを保持する内部のフィールドの型",
        "Password と SecurePassword が返す値（型・長さ・呼び出しごとに別のインスタンスか）",
        "MaxLength を、キーボードからの入力とコードからの Password に対して効かせたときの結果",
        "PasswordChanged イベントが、入力・コードからの設定・Clear() で発生する回数",
        "コピー・切り取り・貼り付けのコマンドが実行可能か",
        "IsSelectionActive が、フォーカスの有無と文字の選択の有無でどう変わるか",
        "XAML の Password 属性で初期値を与えられること（デモアプリのマークアップ）",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "PasswordBox: type, properties and defaults",
            ["item", "value"],
            Defaults(),
            "passwordbox-defaults.svg");

        await context.SaveTableAsync(
            "PasswordBox: input, events, commands and selection",
            ["case", "measured"],
            await BehaviorAsync(),
            "passwordbox-behavior.svg");
    }

    private static void Type(PasswordBox box, string text)
    {
        box.Focus();
        TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, box, text));
    }

    private static List<IReadOnlyList<string>> Defaults()
    {
        var rows = new List<IReadOnlyList<string>>();
        var box = new PasswordBox();

        rows.Add(["base class", typeof(PasswordBox).BaseType!.Name]);
        rows.Add(["has PasswordProperty / PasswordCharProperty",
            $"{typeof(PasswordBox).GetField("PasswordProperty", BindingFlags.Public | BindingFlags.Static) is not null} / " +
            $"{typeof(PasswordBox).GetField("PasswordCharProperty", BindingFlags.Public | BindingFlags.Static) is not null}"]);
        rows.Add(["PasswordChar (metadata default)",
            $"'{(char)PasswordBox.PasswordCharProperty.DefaultMetadata.DefaultValue}' (U+{(int)(char)PasswordBox.PasswordCharProperty.DefaultMetadata.DefaultValue:X4})"]);
        rows.Add(["MaxLength / SelectionOpacity / IsInactiveSelectionHighlightEnabled",
            $"{box.MaxLength} / {D(box.SelectionOpacity)} / {box.IsInactiveSelectionHighlightEnabled}"]);

        // 既定のスタイルが適用された後の値を読む。
        var host = new Grid();
        host.Children.Add(box);
        Layout(host, 200, 50);
        rows.Add(["PasswordChar with the default style (value source)",
            $"'{box.PasswordChar}' (U+{(int)box.PasswordChar:X4}, {DependencyPropertyHelper.GetValueSource(box, PasswordBox.PasswordCharProperty).BaseValueSource})"]);
        rows.Add(["CaretBrush / SelectionBrush (value source)",
            $"{WpfProbe.ValueAndSource(box, PasswordBox.CaretBrushProperty)} / {WpfProbe.ValueAndSource(box, PasswordBox.SelectionBrushProperty)}"]);

        // パスワードを保持している内部のフィールドを型で探す。
        object? container = typeof(PasswordBox)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(f => f.GetValue(box))
            .FirstOrDefault(v => v?.GetType().Name == "PasswordTextContainer");
        IEnumerable<string> storage = container?.GetType()
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => f.FieldType == typeof(SecureString) || f.FieldType == typeof(string) || f.FieldType == typeof(char[]))
            .Select(f => $"{f.Name}: {f.FieldType.Name}") ?? [];
        rows.Add(["internal text container / its text fields",
            $"{container?.GetType().Name ?? "not found"} / {string.Join(", ", storage)}"]);

        {
            var withPassword = new PasswordBox { Password = "secret" };
            SecureString first = withPassword.SecurePassword;
            SecureString second = withPassword.SecurePassword;
            rows.Add(["Password = \"secret\": Password", $"{withPassword.Password.GetType().Name} \"{withPassword.Password}\""]);
            rows.Add(["  SecurePassword", $"{first.GetType().Name}, Length {first.Length}, read-only {first.IsReadOnly()}"]);
            rows.Add(["  SecurePassword twice: same instance", ReferenceEquals(first, second).ToString()]);
        }

        {
            var fromXaml = SceneContext.LoadXaml<PasswordBox>("""<PasswordBox Password="PASSWORD" />""");
            rows.Add(["XAML Password=\"PASSWORD\" (the demo app's markup)", $"Password \"{fromXaml.Password}\""]);
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<string>>> BehaviorAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        {
            var box = new PasswordBox { Width = 200, MaxLength = 8 };
            await ShowAsync(box, async () =>
            {
                Type(box, "1234567890");
                await Capture.SettleAsync(Window.GetWindow(box)!);
                rows.Add(["MaxLength=8, typed \"1234567890\"", $"Password \"{box.Password}\""]);
            }, activate: true);
        }

        {
            var box = new PasswordBox { MaxLength = 8 };
            box.Password = "1234567890";
            rows.Add(["MaxLength=8, Password = \"1234567890\" from code", $"Password \"{box.Password}\""]);
        }

        {
            var box = new PasswordBox { Width = 200 };
            int changed = 0;
            box.PasswordChanged += (_, _) => changed++;
            await ShowAsync(box, async () =>
            {
                Type(box, "abc");
                await Capture.SettleAsync(Window.GetWindow(box)!);
                int typed = changed;
                box.Password = "xyz";
                int set = changed - typed;
                box.Clear();
                int cleared = changed - typed - set;
                rows.Add(["PasswordChanged count: typing \"abc\" / Password = \"xyz\" / Clear()", $"{typed} / {set} / {cleared}"]);
                rows.Add(["  Password after Clear()", $"\"{box.Password}\""]);
            }, activate: true);
        }

        {
            var box = new PasswordBox { Width = 200, Password = "secret" };
            await ShowAsync(box, async () =>
            {
                box.Focus();
                box.SelectAll();
                rows.Add(["all text selected: CanExecute of Copy / Cut / Paste",
                    $"{ApplicationCommands.Copy.CanExecute(null, box)} / {ApplicationCommands.Cut.CanExecute(null, box)} / " +
                    $"{ApplicationCommands.Paste.CanExecute(null, box)}"]);
                await Task.CompletedTask;
            }, activate: true);
        }

        {
            // デモアプリの IsSelectionActive 欄と同じく、値を表示するだけの PasswordBox と、フォーカスの移動先の TextBox を並べる。
            var box = new PasswordBox { Width = 200, Password = "Password" };
            var other = new TextBox { Width = 200 };
            var panel = new StackPanel();
            panel.Children.Add(box);
            panel.Children.Add(other);
            await ShowAsync(panel, async () =>
            {
                rows.Add(["IsSelectionActive: before focusing", box.IsSelectionActive.ToString()]);
                box.Focus();
                rows.Add(["  focused, nothing selected", box.IsSelectionActive.ToString()]);
                box.SelectAll();
                rows.Add(["  focused, all text selected", box.IsSelectionActive.ToString()]);
                other.Focus();
                rows.Add(["  focus moved to another control (selection kept)", box.IsSelectionActive.ToString()]);
                await Task.CompletedTask;
            }, activate: true);
        }

        return rows;
    }
}

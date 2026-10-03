using System.Reflection;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「PasswordBox」（apps/wpf-standard-control-demo/passwordbox.md と日本語版）の記述を実測する。
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
            [T("item", "項目"), T("value", "値")],
            Defaults(),
            "passwordbox-defaults.svg");

        await context.SaveTableAsync(
            "PasswordBox: input, events, commands and selection",
            [T("case", "条件"), T("measured", "計測値")],
            await BehaviorAsync(),
            "passwordbox-behavior.svg");
    }

    /// <summary>キーボードから 1 文字ずつ入力したのと同じ経路で、文字列を入力する（1 文字ごとに TextComposition を送る）。</summary>
    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    private static void Type(PasswordBox box, string text)
    {
        box.Focus();
        foreach (char c in text)
        {
            TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, box, c.ToString()));
        }
    }

    private static List<IReadOnlyList<Loc>> Defaults()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        var box = new PasswordBox();

        rows.Add([T("base class", "基底クラス"), typeof(PasswordBox).BaseType!.Name]);
        rows.Add([T("has PasswordProperty / PasswordCharProperty", "PasswordProperty / PasswordCharProperty があるか"),
            $"{typeof(PasswordBox).GetField("PasswordProperty", BindingFlags.Public | BindingFlags.Static) is not null} / " +
            $"{typeof(PasswordBox).GetField("PasswordCharProperty", BindingFlags.Public | BindingFlags.Static) is not null}"]);
        rows.Add([T("PasswordChar (metadata default)", "PasswordChar（メタデータの既定値）"),
            $"'{(char)PasswordBox.PasswordCharProperty.DefaultMetadata.DefaultValue}' (U+{(int)(char)PasswordBox.PasswordCharProperty.DefaultMetadata.DefaultValue:X4})"]);
        rows.Add(["MaxLength / SelectionOpacity / IsInactiveSelectionHighlightEnabled",
            $"{box.MaxLength} / {D(box.SelectionOpacity)} / {box.IsInactiveSelectionHighlightEnabled}"]);

        // 既定のスタイルが適用された後の値を読む。
        var host = new Grid();
        host.Children.Add(box);
        Layout(host, 200, 50);
        rows.Add([T("PasswordChar with the default style (value source)", "既定のスタイルでの PasswordChar（値の出どころ）"),
            $"'{box.PasswordChar}' (U+{(int)box.PasswordChar:X4}, {DependencyPropertyHelper.GetValueSource(box, PasswordBox.PasswordCharProperty).BaseValueSource})"]);
        rows.Add([T("CaretBrush / SelectionBrush (value source)", "CaretBrush / SelectionBrush（値の出どころ）"),
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
        rows.Add([T("internal text container / its text fields", "内部のテキストの入れ物 / その文字列のフィールド"),
            $"{container?.GetType().Name ?? "not found"} / {string.Join(", ", storage)}"]);

        {
            var withPassword = new PasswordBox { Password = "secret" };
            SecureString first = withPassword.SecurePassword;
            SecureString second = withPassword.SecurePassword;
            rows.Add(["Password = \"secret\": Password", $"{withPassword.Password.GetType().Name} \"{withPassword.Password}\""]);
            rows.Add(["  SecurePassword", T($"{first.GetType().Name}, Length {first.Length}, read-only {first.IsReadOnly()}", $"{first.GetType().Name}、Length {first.Length}、読み取り専用 {first.IsReadOnly()}")]);
            rows.Add([T("  SecurePassword twice: same instance", "  SecurePassword を 2 回読む: 同じインスタンスか"), ReferenceEquals(first, second).ToString()]);
        }

        {
            var fromXaml = SceneContext.LoadXaml<PasswordBox>("""<PasswordBox Password="PASSWORD" />""");
            rows.Add([T("XAML Password=\"PASSWORD\" (the demo app's markup)", "XAML の Password=\"PASSWORD\"（デモアプリのマークアップ）"), $"Password \"{fromXaml.Password}\""]);
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<Loc>>> BehaviorAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        {
            var box = new PasswordBox { Width = 200, MaxLength = 8 };
            await ShowAsync(box, async () =>
            {
                Type(box, "1234567890");
                await Capture.SettleAsync(Window.GetWindow(box)!);
                rows.Add([T("MaxLength=8, typed \"1234567890\"", "MaxLength=8、\"1234567890\" を入力"), $"Password \"{box.Password}\""]);
            }, activate: true);
        }

        {
            var box = new PasswordBox { MaxLength = 8 };
            box.Password = "1234567890";
            rows.Add([T("MaxLength=8, Password = \"1234567890\" from code", "MaxLength=8、コードから Password = \"1234567890\""), $"Password \"{box.Password}\""]);
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
                rows.Add([T("PasswordChanged count: typing \"abc\" one character at a time / Password = \"xyz\" / Clear()", "PasswordChanged の回数: \"abc\" を 1 文字ずつ入力 / Password = \"xyz\" / Clear()"), $"{typed} / {set} / {cleared}"]);
                rows.Add([T("  Password after Clear()", "  Clear() の後の Password"), $"\"{box.Password}\""]);
            }, activate: true);
        }

        {
            var box = new PasswordBox { Width = 200, Password = "secret" };
            await ShowAsync(box, async () =>
            {
                box.Focus();
                box.SelectAll();
                rows.Add([T("all text selected: CanExecute of Copy / Cut / Paste", "全選択: Copy / Cut / Paste の CanExecute"),
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
                rows.Add([T("IsSelectionActive: before focusing", "IsSelectionActive: フォーカスを移す前"), box.IsSelectionActive.ToString()]);
                box.Focus();
                rows.Add([T("  focused, nothing selected", "  フォーカスあり、選択なし"), box.IsSelectionActive.ToString()]);
                box.SelectAll();
                rows.Add([T("  focused, all text selected", "  フォーカスあり、全選択"), box.IsSelectionActive.ToString()]);
                other.Focus();
                rows.Add([T("  focus moved to another control (selection kept)", "  フォーカスを別のコントロールへ移す（選択は残る）"), box.IsSelectionActive.ToString()]);
                await Task.CompletedTask;
            }, activate: true);
        }

        return rows;
    }
}

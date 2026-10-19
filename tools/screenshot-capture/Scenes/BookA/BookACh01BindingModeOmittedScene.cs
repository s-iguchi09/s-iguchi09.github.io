using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Ch01.BindingModeOmitted;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// 本の付録サンプル（<c>samples/book-a</c>）第 1 章の検証。
///
/// MVVM で書き直した再現版（<see cref="ReproView"/>）と修正版（<see cref="FixedView"/>）を表示し、
/// 記事「WPF で Mode を書かないバインドが、ユーザー操作で黙って外れる仕組み」の計測
/// （<see cref="BindingModeOmittedScene"/>）と同じ操作を、実際のマウスとキーボードで行う。
/// 操作の後の表示・ビューモデルの値・バインドの有無・値の供給元と、ビューモデルのコマンド
/// （すべて閉じる・幅を戻す）を実行した後の表示を読み、記事の表の値と一致するかを確かめる。
/// 1 つでも食い違えば、レポートを書いたうえで例外にする。
/// </summary>
internal sealed class BookACh01BindingModeOmittedScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "付録サンプル第 1 章の再現版（Setter と ColumnDefinition に Mode を書かない）で、実際のマウスで展開ボタンをクリックすると、記事の表の 1 行目と同じく表示 True・ビューモデル False・バインドが外れて Local になり、ビューモデルの「すべて閉じる」の後も表示は True のままであること",
        "同じ再現版で、実際の → キーと見出しのダブルクリックでは、記事の表の 2・3 行目と同じくバインドが残って Style・式・現在値になり、ビューモデルは False のまま、「すべて閉じる」で表示が False に戻ること",
        "付録サンプル第 1 章の修正版（Mode=TwoWay）で、展開ボタン・→ キー・見出しのダブルクリックのどれでも、記事の表の 6〜8 行目と同じくビューモデルが True になり、バインドが残って Style・式になり、「すべて閉じる」で表示が False になること",
        "再現版で GridSplitter を 60 DIP ドラッグすると、記事の表の 15 行目と同じく表示 160・ビューモデル 100・バインドが外れて Local になり、「幅を戻す」の後も表示は 160 のままで、修正版では 16 行目と同じくビューモデルが 160 になって「幅を戻す」で 100 に戻ること",
        "上の操作の間に、バインドのエラーと警告が 1 件も出ないこと",
    ];

    public string Slug => "book-a-ch01-binding-mode-omitted";

    /// <summary>図は出さないが、出力先は images/ の下である必要がある（Program の約束）。</summary>
    public string ImageDirectory => Path.Combine("images", "book-a", "ch01");

    private enum Input
    {
        ExpanderButton,
        RightArrow,
        DoubleClick,
        SplitterDrag,
    }

    /// <summary>記事の表の 1 行（期待値）と、サンプルでの実測値。</summary>
    private sealed record Case(string Label, bool Fixed, Input Input, int ArticleRow, Outcome Expected);

    private sealed record Outcome(string DisplayedAndSource, string Binding, string ValueSource, string AfterCommand, int Errors);

    public async Task CaptureAsync(SceneContext context)
    {
        // 期待値は記事の表（_includes/tables/articles/wpf-binding-mode-omitted-detached/binding-mode-user-input.ja.md）の値。
        Case[] cases =
        [
            new("再現版、展開ボタンをクリック", false, Input.ExpanderButton, 1, new("True / False", "外れる", "Local", "True", 0)),
            new("再現版、ノードを選んで → キー", false, Input.RightArrow, 2, new("True / False", "残る", "Style、式、現在値", "False", 0)),
            new("再現版、見出しをダブルクリック", false, Input.DoubleClick, 3, new("True / False", "残る", "Style、式、現在値", "False", 0)),
            new("修正版、展開ボタンをクリック", true, Input.ExpanderButton, 6, new("True / True", "残る", "Style、式", "False", 0)),
            new("修正版、ノードを選んで → キー", true, Input.RightArrow, 7, new("True / True", "残る", "Style、式", "False", 0)),
            new("修正版、見出しをダブルクリック", true, Input.DoubleClick, 8, new("True / True", "残る", "Style、式", "False", 0)),
            new("再現版、GridSplitter を右へ 60 DIP ドラッグ", false, Input.SplitterDrag, 15, new("160 / 100", "外れる", "Local", "160", 0)),
            new("修正版、GridSplitter を右へ 60 DIP ドラッグ", true, Input.SplitterDrag, 16, new("160 / 160", "残る", "Local、式", "100", 0)),
        ];

        var results = new List<(Case Case, Outcome Actual)>();
        using (var errors = BindingErrorCounter.Start())
        {
            foreach (Case item in cases)
            {
                results.Add((item, await RunAsync(item, errors)));
            }
        }

        string report = WriteReport(results);
        Console.WriteLine(report);

        var mismatches = results.Where(r => r.Case.Expected != r.Actual).ToList();
        if (mismatches.Count > 0)
        {
            throw new InvalidOperationException(
                "付録サンプル第 1 章が記事の実測と一致しない: " +
                string.Join(" / ", mismatches.Select(m => $"{m.Case.Label}: 期待 {m.Case.Expected} 実測 {m.Actual}")));
        }
    }

    private static async Task<Outcome> RunAsync(Case item, BindingErrorCounter errors)
    {
        var viewModel = new FolderBrowserViewModel();
        UserControl view = item.Fixed ? new FixedView() : new ReproView();
        view.DataContext = viewModel;
        view.Width = 420;
        view.Height = 240;

        Outcome? outcome = null;
        await ShowAsync(view, async () =>
        {
            Window window = await FrontCenteredAsync(view);
            int before = errors.Count;

            if (item.Input == Input.SplitterDrag)
            {
                var column = (ColumnDefinition)view.FindName("navigationColumn");
                var grid = (Grid)view.FindName("layoutGrid");
                var splitter = (GridSplitter)view.FindName("splitter");
                using (RealMouse.Preserve())
                {
                    // 記事の計測と同じく、動かない Grid の座標で 15 DIP ずつ 60 DIP 動かす。
                    Point start = splitter.TranslatePoint(new Point(splitter.ActualWidth / 2, splitter.ActualHeight / 2), grid);
                    await RealMouse.MoveToAsync(grid, start);
                    await RealMouse.LeftDownAsync(window, splitter);
                    for (int dx = 15; dx <= 60; dx += 15)
                    {
                        await RealMouse.MoveToAsync(grid, new Point(start.X + dx, start.Y));
                    }

                    await RealMouse.LeftUpAsync(window);
                }

                outcome = await ReadAsync(
                    window, column, ColumnDefinition.WidthProperty,
                    () => viewModel.NavigationWidth,
                    () => viewModel.ResetWidthCommand.Execute(null),
                    () => errors.Count - before);
                return;
            }

            var tree = (TreeView)view.FindName("folderTree");
            var node = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0);
            using (RealMouse.Preserve())
            {
                switch (item.Input)
                {
                    case Input.ExpanderButton:
                        await RealMouse.ClickAsync(window, Descendants(node).OfType<ToggleButton>().First(b => b.TemplatedParent == node));
                        break;
                    case Input.RightArrow:
                        // ノードの選択とフォーカスは操作の準備で、記事の計測と同じくコードで行う。
                        node.IsSelected = true;
                        node.Focus();
                        await Capture.SettleAsync(window, 100);
                        before = errors.Count;
                        await RealKeyboard.PressAsync(window, 0x27); // VK_RIGHT
                        await Capture.SettleAsync(window, 100);
                        break;
                    case Input.DoubleClick:
                        FrameworkElement header = Descendants(node).OfType<ContentPresenter>().First(p => p.Name == "PART_Header");
                        await RealMouse.ClickAsync(window, header);
                        await RealMouse.LeftDownAsync(window, header);
                        await RealMouse.LeftUpAsync(window);
                        break;
                }
            }

            FolderViewModel documents = viewModel.Folders[0];
            outcome = await ReadAsync(
                window, node, TreeViewItem.IsExpandedProperty,
                () => documents.IsExpanded,
                () => viewModel.CollapseAllCommand.Execute(null),
                () => errors.Count - before);
        }, activate: true);

        return outcome ?? throw new InvalidOperationException("計測が実行されなかった: " + item.Label);
    }

    /// <summary>
    /// 操作の後の状態を読み、ビューモデルのコマンドを実行してから表示をもう一度読む。
    /// 記事の計測の「ソースに代入した後の表示」に当たるのが、ここではコマンドの実行後の表示である。
    /// バインドのエラーの数は、コマンドの実行と描画の後に読む（コマンドの実行中に出たエラーも数えるため）。
    /// </summary>
    private static async Task<Outcome> ReadAsync(
        Window window, DependencyObject target, DependencyProperty property,
        Func<object> readViewModel, Action runCommand, Func<int> readErrors)
    {
        string displayed = Describe(target.GetValue(property));
        string sourced = Describe(readViewModel());
        bool kept = BindingOperations.GetBindingExpressionBase(target, property) is not null;
        ValueSource valueSource = DependencyPropertyHelper.GetValueSource(target, property);
        string from = valueSource.BaseValueSource + (valueSource.IsExpression ? "、式" : "") + (valueSource.IsCurrent ? "、現在値" : "");

        runCommand();
        await Capture.SettleAsync(window, 100);
        string after = Describe(target.GetValue(property));

        return new Outcome($"{displayed} / {sourced}", kept ? "残る" : "外れる", from, after, readErrors());
    }

    private static string Describe(object? value) => value switch
    {
        GridLength length => length.ToString(),
        _ => WpfProbe.Describe(value),
    };

    /// <summary>
    /// 突き合わせの結果を <c>samples/book-a/verification/ch01.md</c> に書き出し、その内容を返す。
    /// </summary>
    private static string WriteReport(List<(Case Case, Outcome Actual)> results)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("<!-- tools/screenshot-capture が実行時に自動生成する。手で編集しない。 -->");
        builder.AppendLine("<!-- 再生成: dotnet run --project tools/screenshot-capture -c Release -- book-a-ch01-binding-mode-omitted -->");
        builder.AppendLine();
        builder.AppendLine("# 第 1 章 付録サンプルと記事の実測の突き合わせ");
        builder.AppendLine();
        builder.AppendLine($"環境: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} / {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        builder.AppendLine();
        builder.AppendLine("記事: [WPF で Mode を書かないバインドが、ユーザー操作で黙って外れる仕組み](https://s-iguchi09.github.io/ja/articles/wpf-binding-mode-omitted-detached/)。「記事の行」は記事の実測表の行番号。「コマンド後の表示」は、ビューモデルの「すべて閉じる」または「幅を戻す」を実行した後の表示で、記事の表の「ソースに代入した後の表示」に当たる。");
        builder.AppendLine();
        builder.AppendLine("| 操作 | 記事の行 | 表示 / ビューモデル | バインド | 値の供給元 | コマンド後の表示 | バインドエラー | 記事と一致 |");
        builder.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach ((Case item, Outcome actual) in results)
        {
            builder.AppendLine(
                $"| {item.Label} | {item.ArticleRow} | {actual.DisplayedAndSource} | {actual.Binding} | {actual.ValueSource} | {actual.AfterCommand} | {actual.Errors} | {(item.Expected == actual ? "一致" : "不一致（期待 " + item.Expected + "）")} |");
        }

        string text = builder.ToString();
        string directory = Path.Combine(FindRepositoryRoot(), "samples", "book-a", "verification");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "ch01.md"), text, new System.Text.UTF8Encoding(false));
        return text;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "_config.yml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("リポジトリルートが見つからない。");
    }
}

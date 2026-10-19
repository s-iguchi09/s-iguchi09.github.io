using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Ch03.DataGridCommitOnSave;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// 本の付録サンプル（<c>samples/book-a</c>）第 3 章の検証。
///
/// 再現版（<see cref="ReproView"/>）と、DataGrid に <see cref="PendingEdits"/>.Host を付けた修正版（<see cref="FixedView"/>）を表示し、
/// 記事「WPF の DataGrid で編集中の値が保存ボタンで ViewModel に届かない原因と CommitEdit の使い方」の計測
/// （<see cref="DataGridEditNotCommittedScene"/>）と同じく、1 行目のセルを編集中のまま実際のマウスとキーボードで保存する。
/// 保存の後のソースの値・ICollectionView.Refresh の結果・セルと行の編集状態を読み、記事の表の値と一致するかを確かめる。
///
/// 記事の計測は保存処理の中（確定を呼んだ直後）で値を読んでいたが、ここではサンプルに計測用のコードを入れないため、
/// 保存の操作が終わった後に読む。
/// </summary>
internal sealed class BookACh03DataGridCommitOnSaveScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "付録サンプル第 3 章の再現版で、1 行目の Name セルを編集中のまま実際のマウスで ToolBar の保存・Menu の保存をクリックし、実際のキーボードで Ctrl+S を押すと、ソースは入力前の \"alpha\" のままで、Refresh は InvalidOperationException、セルと行は編集中のままであること（記事の表の 1〜3 行目と同じ）",
        "同じ再現版で、ToolBar の外の保存ボタンをクリックすると、ソースが \"edited\" になり、Refresh が成功し、セルと行の編集が終わること（記事の表の 4 行目と同じ）",
        "付録サンプル第 3 章の修正版（DataGrid に PendingEdits.Host を付け、保存のコマンドで CommitEdit(DataGridEditingUnit.Row, true) を呼ぶ）で、ToolBar・Menu・Ctrl+S・ToolBar の外のボタンのどれで保存しても、ソースが \"edited\" になり、Refresh が成功し、セルと行の編集が終わること（記事の表の 9〜12 行目と同じ）",
        "修正版で、int の Quantity 列に \"abc\" を入力して ToolBar の保存をクリックすると確定できず、ソースは 1 のまま、Refresh は InvalidOperationException、セルと行は編集中のままで、5 に直してもう一度クリックするとソースが 5 になること。セルを編集していないときに保存しても、ソースは \"alpha\" のまま、Refresh が成功すること（記事の表の 16〜18 行目と同じ）",
    ];

    public string Slug => "book-a-ch03-datagrid-commit-on-save";

    public string ImageDirectory => Path.Combine("images", "book-a", "ch03");

    private enum SaveInput
    {
        ToolBar,
        Menu,
        CtrlS,
        Plain,
    }

    public async Task CaptureAsync(SceneContext context)
    {
        const string Kept = "\"alpha\"、InvalidOperationException、True / True";
        const string Committed = "\"edited\"、成功、False / False";
        var checks = new List<BookACheck>
        {
            new("再現版、編集中に ToolBar の保存をクリック", "1 行目", Kept, await SaveAsync(fixedView: false, SaveInput.ToolBar)),
            new("再現版、編集中に Menu の保存をクリック", "2 行目", Kept, await SaveAsync(fixedView: false, SaveInput.Menu)),
            new("再現版、編集中に Ctrl+S", "3 行目", Kept, await SaveAsync(fixedView: false, SaveInput.CtrlS)),
            new("再現版、編集中に ToolBar の外の保存をクリック", "4 行目", Committed, await SaveAsync(fixedView: false, SaveInput.Plain)),
            new("修正版、編集中に ToolBar の保存をクリック", "9 行目", Committed, await SaveAsync(fixedView: true, SaveInput.ToolBar)),
            new("修正版、編集中に Menu の保存をクリック", "10 行目", Committed, await SaveAsync(fixedView: true, SaveInput.Menu)),
            new("修正版、編集中に Ctrl+S", "11 行目", Committed, await SaveAsync(fixedView: true, SaveInput.CtrlS)),
            new("修正版、編集中に ToolBar の外の保存をクリック", "12 行目", Committed, await SaveAsync(fixedView: true, SaveInput.Plain)),
            new("修正版、Quantity に abc を入力して ToolBar の保存をクリック", "16 行目", "1、InvalidOperationException、True / True",
                await SaveAsync(fixedView: true, SaveInput.ToolBar, quantity: true)),
            new("修正版、Quantity に abc、5 に直してもう一度クリック", "17 行目", "5、成功、False / False",
                await SaveAsync(fixedView: true, SaveInput.ToolBar, quantity: true, retryWith: '5')),
            new("修正版、セルを編集せずに ToolBar の保存をクリック", "18 行目", "\"alpha\"、成功、False / False",
                await SaveAsync(fixedView: true, SaveInput.ToolBar, typeFirst: false)),
        };

        BookAReport.WriteAndAssert(
            "ch03.md", Slug, "第 3 章",
            "WPF の DataGrid で編集中の値が保存ボタンで ViewModel に届かない原因と CommitEdit の使い方",
            "wpf-datagrid-edit-not-committed-on-save",
            "「記事の該当箇所」は記事の表（保存の操作と保存処理の組み合わせ）の行。値は「ソースの値、Refresh の結果、保存後の編集中: セル / 行」の順。再現版は記事の「保存処理で呼ぶもの: なし」、修正版は「CommitEdit(DataGridEditingUnit.Row, true)」の行に当たる。",
            checks);
    }

    private static async Task<string> SaveAsync(bool fixedView, SaveInput input, bool quantity = false, bool typeFirst = true, char? retryWith = null)
    {
        var vm = new ItemsViewModel();
        UserControl view = fixedView ? new FixedView() : new ReproView();
        view.DataContext = vm;
        view.Width = 400;
        var grid = (DataGrid)view.FindName("grid");
        string result = "";
        await ShowAsync(view, async () =>
        {
            Window window = await FrontCenteredAsync(view);
            if (typeFirst)
            {
                await TypeInCellAsync(window, grid, quantity ? 1 : 0, quantity ? "abc" : "edited");
            }
            else
            {
                await FocusAsync(grid);
            }

            using (RealMouse.Preserve())
            {
                switch (input)
                {
                    case SaveInput.ToolBar:
                        await RealMouse.ClickAsync(window, (Button)view.FindName("toolBarSave"));
                        if (retryWith is char digit)
                        {
                            await Capture.SettleAsync(window, 100);
                            TypeDigit(digit);
                            await RealMouse.ClickAsync(window, (Button)view.FindName("toolBarSave"));
                        }

                        break;
                    case SaveInput.Menu:
                        await RealMouse.ClickAsync(window, (MenuItem)view.FindName("menuSave"));
                        break;
                    case SaveInput.CtrlS:
                        await RealKeyboard.PressAsync(window, 0x53, 0x11); // VK_S + VK_CONTROL
                        break;
                    case SaveInput.Plain:
                        await RealMouse.ClickAsync(window, (Button)view.FindName("plainSave"));
                        break;
                }
            }

            await Capture.SettleAsync(window, 100);
            ItemViewModel first = vm.Items[0];
            string value = quantity ? first.Quantity.ToString() : $"\"{first.Name}\"";
            string refresh;
            try
            {
                CollectionViewSource.GetDefaultView(vm.Items).Refresh();
                refresh = "成功";
            }
            catch (Exception ex)
            {
                refresh = ex.GetType().Name;
            }

            result = $"{value}、{refresh}、{YesNo(CellEditing(grid))} / {YesNo(RowEditing(grid))}";
        }, activate: true);
        return result;
    }

    /// <summary>1 行目の <paramref name="column"/> 列のセルを編集状態にし、<paramref name="letters"/> を打ち込む（記事の計測と同じ手順）。</summary>
    private static async Task TypeInCellAsync(Window window, DataGrid grid, int column, string letters)
    {
        await FocusAsync(grid);
        grid.CurrentCell = new DataGridCellInfo(grid.Items[0], grid.Columns[column]);
        if (!grid.BeginEdit())
        {
            throw new InvalidOperationException("セルを編集状態にできない。");
        }

        await Capture.SettleAsync(window, 100);
        if (Keyboard.FocusedElement is not TextBox box || !grid.IsAncestorOf(box))
        {
            throw new InvalidOperationException($"編集中のセルの TextBox にフォーカスが無い（{Keyboard.FocusedElement?.GetType().Name ?? "なし"}）。");
        }

        box.SelectAll();
        TypeInto(box, letters);
        await Capture.SettleAsync(window, 100);
    }

    /// <summary>フォーカスのある TextBox の文字をすべて選び、数字 1 文字に置き換える（記事の計測と同じく InputManager 経由）。</summary>
    private static void TypeDigit(char digit)
    {
        if (Keyboard.FocusedElement is not TextBox box)
        {
            throw new InvalidOperationException($"数字を打つ TextBox にフォーカスが無い（{Keyboard.FocusedElement?.GetType().Name ?? "なし"}）。");
        }

        box.SelectAll();
        var key = (Key)((int)Key.D0 + (digit - '0'));
        SendKey(key);
        TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, box, digit.ToString()));
        SendKey(key, down: false);
        if (box.Text != digit.ToString())
        {
            throw new InvalidOperationException($"数字が TextBox に届いていない（Text = \"{box.Text}\"）。");
        }
    }

    private static bool CellEditing(DataGrid grid) => Descendants(grid).OfType<DataGridCell>().Any(c => c.IsEditing);

    private static bool RowEditing(DataGrid grid) => Descendants(grid).OfType<DataGridRow>().Any(r => r.IsEditing);

    private static string YesNo(bool value) => value ? "True" : "False";
}

---
layout: article-ja
title: "WPF の DataGrid で編集中の値が保存ボタンで ViewModel に届かない原因と CommitEdit の使い方"
date: 2026-10-10
category: WPF
excerpt: "DataGrid のセルを編集中のままツールバーやメニューの保存を押すと、入力した値が ViewModel に届かない。実際のマウスとキーで測ると、CommitEdit() でも行は確定しなかった。原因と、保存の前に行を確定する実装を示す。"
image: /images/articles/wpf-datagrid-edit-not-committed-on-save/datagrid-toolbar-save-while-editing.png
---

## 概要

`DataGrid` のセルに値を入力し、そのままツールバーの「保存」を押すと、保存されるのは入力する前の値である。
画面のセルには入力した値が見えているため、利用者からは「保存したのに戻った」と報告される。
例外は出ず、出力ウィンドウにもバインドのエラーは出ない。

原因は、保存のコマンドが実行された時点で、`DataGrid` がまだ編集を確定していないことにある。
保存処理で確定を呼べば済むように見えるが、引数なしの `CommitEdit()` ではソースに値が届かない。
さらに、入力が不正な行は確定そのものが失敗する。

本記事では、セルを編集中のまま実際のマウスとキーボードで保存を押し、ソースの値と編集状態を実測した。
そのうえで、この 3 つの壁を順に越え、保存の前に行を確定する実装を示す。

---

## 前提・対象環境

- フレームワーク: WPF（.NET Framework 4.0 以降 / .NET Core 3.0 以降。`DataGrid` が標準に含まれる範囲）
- 対象: `DataGrid` の `DataGridTextColumn`、`ToolBar` と `Menu` に置いた保存のコマンド、`DataGrid.CommitEdit`
- アーキテクチャ: MVVM（ビューモデルは `INotifyPropertyChanged` を実装し、保存を `ICommand` で公開する）
- 検証環境: .NET 10 / Windows 11（既定のテーマ。Fluent テーマは測っていない）
- 計測方法: OS のマウスとキーボードの入力（`SendInput`）で保存の操作を各 1 回行い、保存のコマンドの実行時にキーボードフォーカス・`CommitEdit` の戻り値・ソースの値・`IEditableObject.EndEdit` の回数・`ICollectionView.Refresh` の結果・セルと行の `IsEditing` を読んだ。
  セルへの文字の入力は WPF の `InputManager` を通して送った。
  この計測は `tools/screenshot-capture` のシーンとして実装している。

---

## 問題

メニュー・ツールバー・ツールバーの外のボタンの 3 か所に、同じ保存のコマンドを置く。
`DataGrid` の列のバインドには、特別な設定を書いていない。

```xml
<DockPanel>
  <Menu DockPanel.Dock="Top">
    <MenuItem x:Name="menuSave" Header="Save" Command="{Binding SaveCommand}" />
  </Menu>
  <ToolBar DockPanel.Dock="Top">
    <Button x:Name="toolBarSave" Content="Save" Command="{Binding SaveCommand}" />
  </ToolBar>
  <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="4">
    <Button x:Name="plainSave" Content="Save" Command="{Binding SaveCommand}" Padding="12,2" />
    <TextBlock Text="{Binding LastSaved}" Margin="8,0" VerticalAlignment="Center" />
  </StackPanel>
  <DataGrid x:Name="grid" ItemsSource="{Binding Items}"
            AutoGenerateColumns="False" CanUserAddRows="False">
    <DataGrid.Columns>
      <DataGridTextColumn Header="Name" Binding="{Binding Name}" Width="*" />
      <DataGridTextColumn Header="Quantity" Binding="{Binding Quantity}" Width="90" />
    </DataGrid.Columns>
  </DataGrid>
</DockPanel>
```

`SaveCommand` は、1 行目の `Name` を `LastSaved` に書き出す。
1 行目の `Name` を `alpha` から `edited` に書き換え、セルを編集中のままツールバーの「Save」をクリックした結果が次の画面である。

<figure class="article-figure">
  <img src="/images/articles/wpf-datagrid-edit-not-committed-on-save/datagrid-toolbar-save-while-editing.png" alt="DataGrid の 1 行目の Name セルに edited と入力したまま編集中で、ツールバーの Save をクリックした後のウィンドウ。下部の表示は saved: Name = alpha で、保存されたのは入力前の値である。" width="402" height="207" loading="lazy">
  <figcaption>.NET 10 / Windows 11 で、実際のマウスでツールバーの Save をクリックした直後の画面。セルは編集中のまま <code>edited</code> を表示しているが、コマンドが読んだ値は <code>alpha</code> である。</figcaption>
</figure>

保存の操作と保存処理の組み合わせを変えて測った結果が次の表である。
アイテムは、注記の無い行では `IEditableObject` を実装している。

{% include tables/articles/wpf-datagrid-edit-not-committed-on-save/datagrid-save-while-editing.ja.md %}

.NET 10 / Windows 11 で実測。各行は新しいウィンドウで 1 回ずつ操作した。「ソースの値」「EndEdit」は保存処理で確定を呼んだ後の値で、「Refresh」はその後に <code>ICollectionView.Refresh</code> を呼んだ結果である。
{: .table-caption}

ツールバーの外のボタンでは入力した値が届き、ツールバー・メニュー・`Ctrl+S` では届かない。
以下、届かない側を順に直していく。

---

## 第一の壁: ツールバーとメニューではセルの編集が終わらない

`DataGrid` は、セルの編集を自分で終わらせる契機を持つ。
公式ドキュメントでは、同じ行の別のセルへ移るか Enter キーを押すとセルの編集が、別の行へ移るか Enter キーを押すと行の編集が確定すると説明されている（[DataGrid](https://learn.microsoft.com/dotnet/desktop/wpf/controls/datagrid#editing)）。
表のツールバーの外のボタンの行では、フォーカスがそのボタンへ移った時点でセルと行の両方の編集が終わり、ソースに `edited` が届いた。

ツールバーとメニューでも、クリックしたときにフォーカスはいったんボタンへ移っている（表の「DataGrid から出たフォーカスの移り先」）。
それでも、コマンドが実行された時点のフォーカスはセルの `TextBox` に戻っており、セルと行は編集中のままだった。
3 つのコントロールの違いは、`Focusable` ではなく、属するフォーカス スコープにある。

{% include tables/articles/wpf-datagrid-edit-not-committed-on-save/datagrid-save-controls-focus.ja.md %}

.NET 10 / Windows 11 で、記事の XAML を表示して <code>Focusable</code> と <code>FocusManager.GetFocusScope</code> を読んだ結果。
{: .table-caption}

`ToolBar` と `Menu` は既定でフォーカス スコープである（[FocusManager](https://learn.microsoft.com/dotnet/api/system.windows.input.focusmanager)）。
キーボードフォーカスがスコープの外へ出ても、元のスコープの中の要素は論理フォーカスを保ち、フォーカスが戻るとその要素がキーボードフォーカスを取り戻す（[フォーカスの概要](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/focus-overview#logical-focus)）。
今回の計測では、フォーカスが同じ `Window` のスコープの中の別の要素へ移ると `DataGrid` は編集を確定し、`ToolBar` や `Menu` のスコープへ一時的に移っただけでは確定しなかった。

`Ctrl+S` を `Window` の `KeyBinding` で受ける場合は、フォーカスは `DataGrid` から一度も出ない。
フォーカスの移動に確定を任せる限り、保存の操作の種類によって結果が変わる。
このため、保存処理の側で明示的に確定を呼ぶ必要がある。

---

## 第二の壁: CommitEdit() は行を確定しない

保存処理の先頭で `DataGrid.CommitEdit()` を呼ぶと、戻り値は `True` で、セルの編集も終わる。
ところが、表の `CommitEdit()` の行では、ソースの値は `alpha` のままで、`EndEdit` も呼ばれず、行は編集中のまま残った。
`IEditableObject` を実装していないアイテムでも結果は同じである。

これは公式ドキュメントの記述どおりの挙動である。
引数なしの `CommitEdit()` は、セルが編集中なら、セルの変更を「保留中の行」へ渡すだけで、行の変更は確定しない（[DataGrid.CommitEdit](https://learn.microsoft.com/dotnet/api/system.windows.controls.datagrid.commitedit)）。
ソースへ書き込まれるのは、行の編集が確定したときである。

行が編集中のまま残ると、値が届かないこと以外にも問題が起きる。
保存の後に一覧を並べ替えたり絞り込み直したりするため `ICollectionView.Refresh` を呼ぶと、`InvalidOperationException` になる。

行まで確定するには、単位に `DataGridEditingUnit.Row` を指定する。

```csharp
bool committed = grid.CommitEdit(DataGridEditingUnit.Row, true);
```

第 2 引数の `true` は、確定した後に編集モードを抜ける指定である。
表の `CommitEdit(DataGridEditingUnit.Row, true)` の行では、ソースに `edited` が届き、`EndEdit` が呼ばれ、セルと行の編集が終わり、`Refresh` も成功した。

---

## 第三の壁: 入力が不正な行は確定できない

`int` の `Quantity` 列に `abc` を入力したまま `CommitEdit(DataGridEditingUnit.Row, true)` を呼ぶと、戻り値は `False` になった。
ソースの `Quantity` は元の `1` のままで、セルと行は編集中のまま残る。

戻り値を見ずに保存を続けると、画面のセルには `abc` が見えたまま、元の値が保存される。
第一の壁と同じ食い違いが、確定を呼んでいても起きる。
戻り値が `False` のときは保存を中止し、入力の誤りを利用者に知らせる必要がある。

---

## 全体の実装

ビューモデルは `DataGrid` を知らないため、確定の処理をビューから渡す。
ビューモデルには、保存の前に呼ぶ処理を受け取るプロパティを置く。

```csharp
public sealed class ItemsViewModel : INotifyPropertyChanged
{
    public ItemsViewModel()
    {
        SaveCommand = new RelayCommand(Save);
    }

    public ObservableCollection<Item> Items { get; } = [];

    public ICommand SaveCommand { get; }

    /// <summary>保存の前に、ビューの編集中の値を確定する。確定できなければ false を返す。</summary>
    public Func<bool>? CommitPendingEdits { get; set; }

    private void Save()
    {
        if (CommitPendingEdits?.Invoke() == false)
        {
            StatusMessage = "入力に誤りがある行は保存できない。";
            return;
        }

        // ここで Items を保存する。
    }

    // StatusMessage と INotifyPropertyChanged の実装は省略する。
}
```

`RelayCommand` は、`Action` を実行する一般的な `ICommand` の実装を想定している。
`CommitPendingEdits` が `null` のとき（ビューが設定していないとき）は、確定せずに保存へ進む。

ビューのコードビハインドでは、行を確定する処理を渡す。

```csharp
public partial class ItemsWindow : Window
{
    public ItemsWindow(ItemsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CommitPendingEdits = () => grid.CommitEdit(DataGridEditingUnit.Row, true);
    }
}
```

セルを編集していない状態でこの処理を呼んでも、戻り値は `True` だった（表の最後の行）。
このため、保存のたびに無条件に呼んでよい。
確定はコマンドの中で行うため、ツールバー・メニュー・`Ctrl+S`・通常のボタンのどこから保存しても同じ結果になる。

---

## 注意点

- **`UpdateSourceTrigger=PropertyChanged` は代わりにならない。**
  列のバインドに書くと、確定しなくても入力のたびにソースが更新される（表の該当行）。
  しかし行は編集中のままで、`EndEdit` は呼ばれず、`Refresh` は `InvalidOperationException` になる。
  さらに、Esc キーで取り消したときに元の値へ戻るかどうかが、アイテムの実装で変わる。

{% include tables/articles/wpf-datagrid-edit-not-committed-on-save/datagrid-escape-after-typing.ja.md %}

.NET 10 / Windows 11 で、1 行目の Name に <code>edited</code> を入力した後、実際の Esc キーを 2 回押した結果。元の値は <code>alpha</code> である。
{: .table-caption}

- **`IEditableObject.EndEdit` は 1 回とは限らない。**
  今回の計測では、行を 1 回確定すると `EndEdit` が 2 回呼ばれた。
  `EndEdit` で保存や通知を行う場合は、複数回呼ばれても結果が変わらない形にする。
- **フォーカスの移動に頼った回避は保存の操作に依存する。**
  保存ボタンをツールバーの外に置けば確定は起きるが、`Ctrl+S` や、メニューから保存する経路では起きない。
- **`DataGrid` が複数あれば、それぞれを確定する。**
  `CommitEdit` は `DataGrid` のインスタンスのメソッドで、確定の対象はそのインスタンスの編集中のセルと行である。
  `CommitPendingEdits` では、画面上のすべての `DataGrid` の結果をまとめて返す。

---

## まとめ

`DataGrid` のセルを編集中のまま保存すると、ツールバー・メニュー・`Ctrl+S` では編集が確定せず、入力前の値が保存される。
ツールバーの外のボタンでは確定するが、保存の操作によって結果が変わる実装は避ける。

保存処理の先頭で `CommitEdit(DataGridEditingUnit.Row, true)` を呼び、戻り値が `False` なら保存を中止する形を推奨する。
引数なしの `CommitEdit()` はセルしか確定せず、ソースへ値が届かないため、この目的には使わない。
`UpdateSourceTrigger=PropertyChanged` はソースの値だけを先に届けるもので、行の編集は終わらないため、確定の代わりにはしない。

---

## 関連記事

- [WPF で TextBox の UpdateSource を View から呼び出すときの落とし穴と実装](/ja/articles/wpf-textbox-updatesource-from-view-pitfalls/)
- [WPF TextBox の UpdateSourceTrigger で入力がソースへ反映されるタイミングを制御する](/ja/articles/wpf-textbox-updatesourcetrigger-binding-timing/)
- [WPF で ICollectionView のフィルタが再評価されない原因と Refresh・ライブフィルタの使い分け](/ja/articles/wpf-collectionviewsource-filter-not-refreshing/)
- [WPF DataGrid でセル編集中と表示時でコントロールを切り替える方法](/ja/articles/wpf-datagrid-cell-editing-template/)
- [WPF で入力検証のエラーが表示されない原因と IDataErrorInfo / INotifyDataErrorInfo の使い分け](/ja/articles/wpf-validation-error-not-displayed/)
- [DataGrid（WPF 標準コントロールデモアプリ）](/ja/apps/wpf-standard-control-demo/datagrid.html)：F2・Esc・Enter での編集を実測したページ

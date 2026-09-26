---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/datagrid.html
title: "DataGrid"
badge: "List"
lead: "DataGrid はコレクションを行と列の表として表示するコントロールです。列の並べ替え、その場での編集、行の追加と削除を標準で備えています。"
description: "WPF の DataGrid の列の自動生成、並べ替え、編集、行の追加と削除、行の検証、仮想化、列の固定を .NET 10 で実測して解説します。見出しの 3 回目のクリックで並べ替えは解除されないことなど、誤解されやすい点もデモアプリで確かめます。"
---

## 自動で作られる列

**DataGrid** は `MultiSelector`・`Selector`・`ItemsControl` を継承しています。既定の `AutoGenerateColumns="True"` では、公開プロパティごとに 1 列を、プロパティを宣言した順に作ります。`Zeta`・`Alpha`・`Mid` の順に宣言したクラスでは、アルファベット順ではなくこの順に列ができました。セッターのないプロパティは読み取り専用の列になりました。

公開プロパティはすべて列になります。デモアプリの項目は `IDataErrorInfo` を実装しているため、自動で作られた列は `Name`・`Value` と、インターフェイスから来た読み取り専用の `Error` 列でした。見せたくないメンバーがある型では、`AutoGenerateColumns` を `False` にし、`DataGridTextColumn`・`DataGridCheckBoxColumn`・`DataGridComboBoxColumn` などで列を宣言します。編集中のセルには、それぞれ `TextBox`・`CheckBox`・`ComboBox` が入りました。

## 行の追加・削除と、読み取り専用の表

`CanUserAddRows` と `CanUserDeleteRows` は、最下部の空の行から行を追加できるか、選んだ行を Delete キーで削除できるかです。どちらも既定値は `True` です。新規行はソースによって変わりました。`List<T>` と `ObservableCollection<T>` では表示され、配列と引数なしのコンストラクターを持たない型では表示されませんでした。その場合、`CanUserAddRows` は例外を出さずに `False` になったので、ユーザーが行を追加できない場合はソースを確認します。3 行のうち 2 行目を選んで Delete キーを押すと、その行がソースのコレクションから削除されました。

`IsReadOnly` は表全体の編集を止めます。`IsReadOnly="True"` にすると、`CanUserAddRows` は `False` になりました。列の側から編集を許すことはできません。列に `IsReadOnly="False"` を明示すると、列の値は `False` のままでしたが、そのセルで `BeginEdit()` を呼ぶと `False` が返り、編集は始まりませんでした。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/datagrid/datagrid-columns-rows.svg" alt="DataGrid の列と行の計測結果の表。自動生成の列は宣言順でデモのデータでは Error 列も作られ、読み取り専用の表では IsReadOnly False の列も編集できず、新規行は List と ObservableCollection では表示されるが配列と引数なしのコンストラクターのない型では表示されず、行の検証には DataErrorValidationRule が必要で、Delete キーで選んだ行が削除される" width="1218" height="440" loading="lazy">
  <figcaption>列、読み取り専用の設定、検証、行の追加と削除。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 行の検証には規則が要る

`RowValidationErrorTemplate` は、行の検証に失敗したときの表示です。行が検証に失敗するのは規則が行を調べたときだけで、`RowValidationRules` は既定では空です。デモアプリのデータでは 3 行目が `IDataErrorInfo.Error` を返しますが、この行は編集した後も `Validation.HasError` になりませんでした。そのため、テンプレートは表示されません。`RowValidationRules` に `DataErrorValidationRule` を加えると、3 行目は検証エラーになりました。行にエラーを表示するなら、検証の規則を加えます。

## 編集と、編集中の並べ替え

フォーカスのある `Name` のセルで、キーボードでの編集を測りました。F2 キーで編集が始まり、セルに `TextBox` が入りました。"Changed" と入力して Esc キーを押すと、編集が終わり、ソースは変わりませんでした。もう一度 F2 キーを押して "Committed" と入力し、Enter キーを押すと、ソースに "Committed" が書き込まれました。

並べ替えは標準の機能です。列見出しをクリックすると昇順、もう一度で降順、3 回目で再び昇順になりました。3 回目で並べ替えが解除されるわけではありません。コードから解除する方法は[WPFのDataGridのソートを初期化する方法](/ja/articles/wpf-datagrid-sort-reset/)で扱っています。

セルの編集中に項目へ `SortDescription` を加えると、`InvalidOperationException` が発生しました。引数なしの `CommitEdit()` や `CancelEdit()` を呼んでも防げず、どちらの後でも並べ替えで例外が発生しました。`CommitEdit(DataGridEditingUnit.Row, true)` か `CancelEdit(DataGridEditingUnit.Row)` の後は並べ替えられました。コードから並べ替え・絞り込み・グループ化をする前に、このように行の編集を終えます。

グループ化はコレクションビューの機能で、DataGrid がグループを表示するのは `GroupStyle` があるときだけです。`GroupStyle` がないと、グループ化したビューでもグループの見出しは現れませんでした。

## 画面上の行：仮想化、列の固定、1 行おきの背景

`EnableRowVirtualization` と `EnableColumnVirtualization` は、表示範囲外の行・列を作らないかどうかです。既定値は行が `True`、列が `False` です。1,000 行では、`DataGridRow` は 11 個でした。既定では `VirtualizingPanel.IsVirtualizingWhenGrouping` が `False` なので、グループ化すると行の仮想化は止まり、`GroupStyle` を付けてグループ化すると 1,000 行すべてが作られました。これを `True` にすると、作られた行は 18 個でした。グループ化する表には設定します。

`FrozenColumnCount` は、横スクロールのときに動かさない、左端からの列の数です。既定値は 0 です。幅 200 の DataGrid に幅 100 の列を 4 つ並べて右へ 60 スクロールすると、1 列目は x = 7 から -53 へ動きました。`FrozenColumnCount="1"` では 1 列目は 7 のままで、2 列目だけが動きました。2 列に 3 を設定しても例外は出ず、2 に補正されました。

`AlternatingRowBackground` は 1 行おきの背景です。このプロパティだけを設定すれば足ります。`AlternationCount` は 2 になり、4 行が白と指定したブラシで交互になりました。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/datagrid/datagrid-behavior.svg" alt="DataGrid の挙動の表。F2・Esc・Enter で編集の開始・取り消し・確定ができ、列の種類ごとに編集用の要素が変わり、見出しの 3 回のクリックで昇順・降順・昇順になり、編集中の並べ替えは CommitEdit() や CancelEdit() の後でも InvalidOperationException になるが行の編集を終えた後はならず、AlternatingRowBackground だけで AlternationCount が 2 になり、GroupStyle 付きのグループ化では既定で 1,000 行すべて、IsVirtualizingWhenGrouping では 18 行が作られ、固定した 1 列目はスクロールしても動かない" width="1203" height="590" loading="lazy">
  <figcaption>編集、並べ替え、1 行おきの背景、仮想化、列の固定。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## そのほかの切り替えの既定値

- `SelectionMode` と `SelectionUnit` の既定値は `Extended` と `FullRow` です。
- 幅を指定していない列の既定の幅と、その上限・下限である `ColumnWidth`・`MaxColumnWidth`・`MinColumnWidth` は、`SizeToHeader`・`Infinity`・20 でした。
- `CanUserReorderColumns`・`CanUserResizeColumns`・`CanUserResizeRows`・`CanUserSortColumns` の既定値は 4 つとも `True` です。
- `RowDetailsTemplate` を行の下にいつ表示するかの `RowDetailsVisibilityMode` は `VisibleWhenSelected`、横スクロールのときに詳細を動かさないかどうかの `AreRowDetailsFrozen` は `False` が既定値です。
- `GridLinesVisibility` と `HeadersVisibility` の既定値は、どちらも `All` です。

## デモアプリで試す

![デモアプリの DataGrid のページ。左にコントロールの一覧、右に最初の節の AlternatingRowBackground](/images/wpf-standard-control-demo/datagrid.png){: .screenshot-img}

デモアプリの DataGrid のページには、ここまでのプロパティをすべて含む 20 のプロパティのまとまりごとに欄があります。データは `Name` と `Value` を持ち、`IDataErrorInfo` を実装した項目です。`FrozenColumnCount` のスライダーは、列が 2 つなのに 3 まで選べます。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`FrozenColumnCount` の欄（`DataGridUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。`Items` は `Name` と `Value` を持つ項目のコレクションです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Slider x:Name="PropertyFrozenColumnCount"
          IsSnapToTickEnabled="True" Maximum="3" Minimum="0" />

  <DataGrid x:Name="ResultFrozen" Height="150"
            AutoGenerateColumns="False"
            FrozenColumnCount="{Binding Value, ElementName=PropertyFrozenColumnCount}"
            ItemsSource="{Binding Items}">
    <DataGrid.Columns>
      <DataGridTextColumn Width="150" Binding="{Binding Name}" Header="Frozen" />
      <DataGridTextColumn Width="600" Binding="{Binding Value}" Header="Scrollable" />
    </DataGrid.Columns>
  </DataGrid>
</StackPanel>
```

## 関連するコントロールと記事

- [WPF DataGrid の並び替えを実装する方法](/ja/articles/wpf-datagrid-sorting/) — 並べ替えられる列と、その仕組みを扱います。
- [WPFのDataGridのソートを初期化する方法](/ja/articles/wpf-datagrid-sort-reset/) — コードから並べ替えを解除する方法を扱います。
- [WPF DataGrid でセル編集中と表示時でコントロールを切り替える方法](/ja/articles/wpf-datagrid-cell-editing-template/) — 表示用と編集用のセルのテンプレートを扱います。
- [ListView](/ja/apps/wpf-standard-control-demo/listview.html) — `GridView` で列を並べる、編集のない一覧です。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`DataGridDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/DataGridDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。列見出しのクリックは見出しのクリック処理（`OnClick`）を呼び、キー操作は表示したウィンドウへキーのイベントを送って再現しました。位置は計測した環境での値です。

[GitHub で DataGrid のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/DataGridUsage){: target="_blank" rel="noopener noreferrer"}

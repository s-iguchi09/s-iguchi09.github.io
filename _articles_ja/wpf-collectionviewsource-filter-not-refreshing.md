---
layout: article-ja
title: "WPF で ICollectionView のフィルタが再評価されない原因と Refresh・ライブフィルタの使い分け"
date: 2026-08-24
category: WPF
excerpt: "フィルタを設定した直後は正しいのに、項目のプロパティを変えても絞り込み結果が変わらない。ビューが再評価を行う契機を .NET 10 の実測で切り分け、Refresh とライブフィルタを比較する。"
image: /images/articles/wpf-collectionviewsource-filter-not-refreshing/collectionview-filter-refresh.png
---

## 概要

在庫一覧や検索ボックス付きの一覧など、条件で表示対象を絞り込む画面は `ICollectionView.Filter` で実装することが多い。
この構成では、フィルタを設定した直後の表示は正しい。
それにもかかわらず、以後に項目の値や検索条件を変えても表示は古いまま残る。

原因はフィルタの述語ではない。
`ICollectionView` はフィルタを保持するが、**フィルタを再評価する契機は限られており、既定では項目のプロパティ変更がその契機に含まれない**。

本記事では、どの操作がフィルタの再評価を起こし、どの操作が起こさないかを実測で切り分ける。
そのうえで、`ICollectionView.Refresh` による全件再評価、`ICollectionViewLiveShaping` によるライブフィルタ、フィルタ済みコレクションの自前構築という 3 方式を、再評価コスト・通知の粒度・選択状態への影響の観点から比較する。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF
- 検証環境: .NET 10 / Windows 11（本記事の実測値と図はこの環境で取得した）
- 追検証: フィルタ再評価の契機・`DeferRefresh` の挙動・`BindingListCollectionView` の制約・XAML の解決は .NET Framework 4.8 でも同一の結果を確認した
- 言語: C# 12 以降 / XAML（コード例はコレクション式を使う。既定の言語バージョンが C# 12 に満たない .NET 6・.NET 7 では `LangVersion` を引き上げるか、コレクション初期化子に書き換える）
- コード例は nullable 参照型有効を前提とする
- 対象の型: `System.ComponentModel.ICollectionView`、`System.ComponentModel.ICollectionViewLiveShaping`、`System.Windows.Data.CollectionViewSource`、`System.Windows.Data.ListCollectionView`
- アーキテクチャ: MVVM（コレクションを ViewModel が保持し、ビューを介して `ItemsControl` へ渡す構成）
- 名前空間: `System.Collections.ObjectModel`、`System.ComponentModel`、`System.Windows.Data`
- 項目の型は `INotifyPropertyChanged` を実装しているものとする

ライブフィルタの API（`ICollectionViewLiveShaping`、`ListCollectionView.IsLiveFiltering`、`CollectionViewSource.IsLiveFilteringRequested`）は .NET Framework 4.5 で追加されたものであり、それ以前のバージョンでは利用できない。
ランタイムの挙動は .NET Framework 版の WPF でも同一だが、本記事のコード例は .NET Framework 4.8 ではそのままではコンパイルできない。
理由は 3 つあり、それぞれ制約の種類が異なる。
`string.Contains(string, StringComparison)` は .NET Core 2.1 以降にのみ存在する API のため、`IndexOf(_keyword, StringComparison.OrdinalIgnoreCase) >= 0` に置き換える。
`init` アクセサが必要とする `System.Runtime.CompilerServices.IsExternalInit` は .NET Framework に無いため、自前で定義するか `set` に置き換える。
残りは言語バージョンの問題で、.NET Framework を対象とするプロジェクトの既定は C# 7.3 である。
nullable 参照型注釈は C# 8.0、代入先の型から型引数を省略する `new()`（ターゲットからの型指定）は C# 9.0、コレクション式は C# 12 を要求するため、`LangVersion` を 12 以降へ引き上げる。
ただし対象フレームワークに対応する言語バージョンより新しい `LangVersion` の指定は公式にはサポート外である。
本記事のコード例で使う機能に限れば .NET Framework 4.8 でも動作するが、これを避けるなら構文の側を上記のとおり書き換える。

---

## 問題

在庫がある商品だけを一覧に出す画面を想定する。
商品は在庫数 `Stock` を持ち、その値は補充や出荷で変化する。

```csharp
public sealed class Product : INotifyPropertyChanged
{
    private int _stock;

    public string Name { get; init; } = string.Empty;

    public int Stock
    {
        get => _stock;
        set
        {
            if (_stock == value)
            {
                return;
            }

            _stock = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Stock)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

`Stock` は変更通知を伴う。
一覧のセルに表示した在庫数は、この通知によって正しく更新される。

ViewModel は商品のコレクションと、そこから取得した既定のビューを保持する。
ビューには「在庫が 1 以上」というフィルタを設定する。

```csharp
public sealed class InventoryViewModel
{
    public InventoryViewModel()
    {
        Products =
        [
            new() { Name = "Bolt", Stock = 0 },
            new() { Name = "Nut", Stock = 5 },
            new() { Name = "Washer", Stock = 12 },
            new() { Name = "Screw", Stock = 3 },
        ];

        View = CollectionViewSource.GetDefaultView(Products);
        View.Filter = item => ((Product)item).Stock > 0;
    }

    public ObservableCollection<Product> Products { get; }

    public ICollectionView View { get; }
}
```

この時点の表示は期待どおりで、`Stock` が 0 の Bolt だけが除外される。
`Filter` プロパティへの代入自体がビューの再構築を起こすためである。

ここで Bolt に在庫を補充する。

```csharp
viewModel.Products[0].Stock = 8;
```

セルに表示された在庫数は 8 に変わる。
しかし Bolt は一覧に現れない。
逆に、在庫が 0 になった商品も一覧から消えない。

<figure class="article-figure">
  <img src="/images/articles/wpf-collectionviewsource-filter-not-refreshing/collectionview-filter-refresh.png" alt="同じフィルタを設定した 3 つの ListBox。左は Bolt の在庫を 8 にしても Bolt が表示されないまま、中央は Refresh 呼び出し後、右はライブフィルタ有効時で、いずれも Bolt が表示されている。" width="602" height="201" loading="lazy">
  <figcaption>Stock &gt; 0 のフィルタを設定した 3 つのビューに対し、Bolt の Stock を 0 から 8 へ変更した直後の表示。左は何もしない場合、中央は Refresh を呼んだ場合、右は IsLiveFiltering を有効にした場合。.NET 10 / Windows 11 で生成した。</figcaption>
</figure>

検索ボックスで絞り込む構成でも同じことが起きる。
述語が ViewModel のフィールドを参照している場合、そのフィールドを書き換えても表示は変わらない。

```csharp
private string _keyword = string.Empty;

// フィルタの述語は _keyword を読む。
View.Filter = item => ((Product)item).Name.Contains(_keyword, StringComparison.OrdinalIgnoreCase);

// キーワードを変えても一覧は変わらない。
_keyword = "Nut";
```

コレクションにも項目にも変化が無いため、症状は在庫の例よりもさらに分かりにくい。

---

## 原因・背景

ソースのコレクションが `ObservableCollection<T>` のように `INotifyCollectionChanged` を実装していれば、ビューはそれを購読し、項目の追加・削除の通知を受け取る。
このとき評価されるのは**通知が指す項目だけ**である。
1,000 件を保持するビューに 1 件追加したときのフィルタ述語の呼び出し回数を数えると 1 回であり、削除では 0 回になる。
既にビューが把握している項目を評価し直すことはない。

一方、項目の `PropertyChanged` は既定ではビューの再評価の契機にならない。
`Stock` を書き換えたときのフィルタ述語の呼び出し回数は 0 回で、ビューは `CollectionChanged` も発生させない。
バインディングは `PropertyChanged` を個別に購読しているため表示中のセルは更新されるが、**ビューの構成メンバーはその時点の判定結果のまま固定されている**。
この非対称性が「値は変わったのに一覧から出入りしない」という症状の正体である。

同じ理屈は並び替えにも及ぶ。
`SortDescriptions` を設定したビューで並び替えキーのプロパティを変更しても、行の位置は動かない。

フィルタの述語が項目以外の状態（検索キーワードを保持するフィールドなど）を読んでいる場合はさらに単純で、ソースのコレクションにも項目にも何の変化も起きていない。
ビューへ「条件が変わった」と伝える経路がそもそも存在しない。

「フィルタを設定した直後は正しい」という点がこの問題を分かりにくくしている。
公式ドキュメントが述べるとおり、`Filter` / `SortDescriptions` / `GroupDescriptions` への設定はそれ自体が再構築を起こす。
初回だけが暗黙に再評価されるため、以後も自動で追随するかのように見える。

---

## 解決方法

再評価の契機を明示的に与える。
方針は 2 つある。

- **全件を評価し直す** — `ICollectionView.Refresh` を呼ぶ。ビューは全項目にフィルタを掛け直し、`Reset` を 1 回通知する。フィルタが項目以外の状態に依存する場合はこれを使う。
- **変更された項目だけを評価し直す** — `ICollectionViewLiveShaping.IsLiveFiltering` を有効にし、`LiveFilteringProperties` にフィルタが読むプロパティ名を登録する。ビューは登録されたプロパティの変更通知を購読し、その項目だけを再判定して、ビューへの出入りが生じたときだけ `Add` / `Remove` を通知する。フィルタが項目のプロパティだけに依存する場合はこれを使う。

`ListCollectionView`（`ObservableCollection<T>` や `List<T>` を元にした既定のビューの実体）は `ICollectionViewLiveShaping` を実装しており、`CanChangeLiveFiltering` は `true` を返す。
両者は排他ではなく、項目のプロパティにはライブフィルタで追随させ、検索キーワードの変更時にだけ `Refresh` を呼ぶ、という併用が実務では扱いやすい。

---

## 実装例

フィルタが外部の状態を読む場合は、その状態を書き換えた側で `Refresh` を呼ぶ。
前掲の `InventoryViewModel` に `INotifyPropertyChanged` を実装したうえで、次の `Keyword` を追加する。
キーワードのセッターに再評価を集約すると、呼び忘れが起きにくくなる。

```csharp
public string Keyword
{
    get => _keyword;
    set
    {
        if (_keyword == value)
        {
            return;
        }

        _keyword = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Keyword)));

        // 述語が読む状態を変えたので、全件を評価し直す。
        View.Refresh();
    }
}
```

`Refresh` は述語を適用し直す。
呼び出し回数はフィルタ後のビューの件数ではなくソースのコレクションの件数と一致し、1 万件のコレクションでは 1 回の `Refresh` につき述語が 1 万回呼ばれる。
`UpdateSourceTrigger=PropertyChanged` の検索ボックスから 1 文字ごとに呼ぶ構成では、述語の実装コストがそのまま入力の引っかかりになる。

項目のプロパティに追随させる場合は、ビューを `ICollectionViewLiveShaping` として扱い、フィルタが読むプロパティ名を登録する。

```csharp
ICollectionView view = CollectionViewSource.GetDefaultView(Products);
view.Filter = item => ((Product)item).Stock > 0;

var liveShaping = (ICollectionViewLiveShaping)view;
liveShaping.IsLiveFiltering = true;
liveShaping.LiveFilteringProperties.Add(nameof(Product.Stock));
```

登録するのは**述語が読むプロパティ**であり、表示に使うプロパティではない。
`LiveFilteringProperties` が空のままだとライブフィルタは何も行わない。
なお `IsLiveFiltering` に `null` を代入すると `ArgumentNullException` になるため、無効化するときは `false` を代入する。

上のキャストは、ソースが `IList` を実装しない場合に `InvalidCastException` になる。
ソースの型が実行時にしか定まらない構成では、このキャストを `if (view is ICollectionViewLiveShaping liveShaping && liveShaping.CanChangeLiveFiltering)` に置き換え、判定が成立したときだけ設定する。
`CanChangeLiveFiltering` まで確認するのは、`DataView` や `BindingList<T>` を元にしたビューが `ICollectionViewLiveShaping` を実装していながらライブフィルタを切り替えられないためである。
型の判定だけで通すと、後述のとおり `IsLiveFiltering` の設定が `InvalidOperationException` になる。

XAML 側で組み立てる場合は `CollectionViewSource` の `IsLiveFilteringRequested` と `LiveFilteringProperties` を使う。
ビューを画面ごとに独立させたい場合もこの形になる。

```xml
<Window.Resources>
  <CollectionViewSource x:Key="InStockProducts"
                        Source="{Binding Products}"
                        Filter="OnProductFilter"
                        IsLiveFilteringRequested="True">
    <CollectionViewSource.LiveFilteringProperties>
      <sys:String>Stock</sys:String>
    </CollectionViewSource.LiveFilteringProperties>
  </CollectionViewSource>
</Window.Resources>

<ListBox ItemsSource="{Binding Source={StaticResource InStockProducts}}"
         DisplayMemberPath="Name" />
```

`sys` 接頭辞は `xmlns:sys="clr-namespace:System;assembly=mscorlib"` で宣言する（`assembly=System.Runtime` も .NET / .NET Framework の双方で解決できる）。
`Binding` の `Source` に `CollectionViewSource` を指定すると、`ItemsSource` には `CollectionViewSource` 自身ではなく `CollectionViewSource.View`（フィルタ適用後のビュー）が渡る。
`ItemsSource="{StaticResource InStockProducts}"` と書くと `CollectionViewSource` 自体を代入することになり、XAML の読み込み時に `XamlParseException` が発生する（`InnerException` は「`ItemsSource` の有効な値ではない」旨の `ArgumentException`）。

`Filter` は `FilterEventArgs.Accepted` を設定するイベントで、`ICollectionView.Filter` プロパティとは別の入口である。

```csharp
private void OnProductFilter(object sender, FilterEventArgs e)
{
    e.Accepted = ((Product)e.Item).Stock > 0;
}
```

`e.Accepted` の既定値は `true` であり、`false` を設定した項目だけがビューから除外される。
呼ばれる範囲は `ICollectionView.Filter` の述語と同じで、ビューを作り直すとき（`Filter` の設定時や `Refresh` 時）はソースの全項目に対して呼ばれるが、項目の追加やライブフィルタによる再判定では対象の項目に対してのみ呼ばれる。
`e.Item` は `object` であり、コレクションに異なる型の項目が混在する構成では、上のような無条件のキャストは `InvalidCastException` になる。

フィルタ・並び替え・グループ化をまとめて設定し直すときは `DeferRefresh` で囲む。
設定 1 つごとに再構築が走るのを、スコープを抜けた 1 回にまとめられる。

```csharp
using (View.DeferRefresh())
{
    View.Filter = item => ((Product)item).Stock > 0;
    View.SortDescriptions.Add(
        new SortDescription(nameof(Product.Stock), ListSortDirection.Descending));
}
```

上の 2 つの設定を `DeferRefresh` なしで行うと `Reset` は 2 回発生するが、囲むと 1 回になる。
ただしスコープ内でビューの内容やカレント位置を参照すると `InvalidOperationException` になるため、`using` の内側では設定だけを行う。

---

## 注意点

- **ライブフィルタの反映は同期ではない。** プロパティを書き換えた直後に同じメソッド内でビューを列挙すると、更新前の内容が返る。反映は `Dispatcher` のコールバックで行われるため、単体テストで確認する場合は `DispatcherFrame` などでメッセージポンプを回してから検証する。
- **登録していないプロパティの変更は無視される。** フィルタが `IsActive` を読むのに `LiveFilteringProperties` へ `Stock` だけを登録した構成では、`IsActive` を変えても表示は変わらない。フィルタの条件を変更したときは登録内容の見直しが必要である。
- **項目が `INotifyPropertyChanged` を実装していないとライブフィルタは機能しない。** 変更通知が無ければビューに再判定の契機が届かない。
- **既定のビューは、そのコレクションにバインドしたすべてのコントロールで共有される。** 同じ `ObservableCollection<T>` を 2 つの `ListBox` に渡した状態で `CollectionViewSource.GetDefaultView` にフィルタを設定すると、両方の表示が絞り込まれる。`ItemsControl.Items.Filter` への設定も同じ既定のビューへ書き込まれるため、片方の画面だけを絞り込む用途には使えない。画面ごとに独立させるには `CollectionViewSource` のインスタンスを別に用意する。
- **`Refresh` は `Reset` を通知するため、`ItemsControl` は項目コンテナを作り直す。** ビューに残り続ける項目のコンテナも再生成される。ライブフィルタが `Add` / `Remove` を通知するのはビューへの出入りが生じた項目についてだけで、ビューに残る項目のコンテナはそのまま再利用される。項目コンテナに描画コストの高いテンプレートを載せている場合、この差が体感に出る。
- **「`Refresh` で選択が失われる」は正確ではない。** 選択中の項目がフィルタを通り続ける限り、`Refresh` の前後で `SelectedItem`・`SelectedItems`・`CurrentItem` はいずれも保たれる（`ListBox` の `SelectionMode` が `Single` の場合と `Extended` の場合の双方で確認した）。選択が失われるのは、選択中の項目がフィルタから外れてビューから消えたときであり、これはライブフィルタでも同じである。この点は[仮想化環境での選択状態](/ja/articles/wpf-listbox-virtualization-selecteditems/)の問題とは原因が異なる。
- **`DataView` や `BindingList<T>` を元にしたビューでは `Filter` を使えない。** これらの既定のビューは `BindingListCollectionView` で、`CanFilter` はどちらも `false` を返し、`Filter` への代入は `NotSupportedException` になる。`CanChangeLiveFiltering` も `false` のため、`IsLiveFiltering` の設定は `InvalidOperationException` になる。
- **`CustomFilter` は `IBindingListView` を実装したコレクションにのみ使える。** `Filter` の代替は、文字列式を渡す `CustomFilter` である。`DataView` は `IBindingListView` を実装しているため `CanCustomFilter` が `true` になるのに対し、`BindingList<T>` は実装しておらず `CanCustomFilter` は `false` で、`CustomFilter` への代入も `NotSupportedException` になる。代入の前に `CanCustomFilter` を確認する。
- **`IList` を実装しないソースではライブフィルタを使えない。** LINQ の戻り値のような `IEnumerable` から得られる既定のビューは `CollectionView` を基底とする内部クラスで、`Filter` は使えるが `ICollectionViewLiveShaping` を実装しない。実行時の型は公開されていないため型名を指定したキャストはできないが、`ICollectionViewLiveShaping` に対する型検査自体は可能で、結果が `false` になる。
- **並び替えにも同じ設定が必要である。** ライブフィルタを有効にしても並び順は追随しない。`IsLiveSorting` と `LiveSortingProperties` を別途設定する。`DataGrid` の列ヘッダーによる並び替えも同じ `ICollectionView` の上で動くため、[並び替えの実装](/ja/articles/wpf-datagrid-sorting/)と組み合わせる場合は設定の対象が同一のビューであることに注意する。

---

## 代替案・比較

| 方法 | 再評価の契機 | 述語の呼び出し回数 | 通知 | 制約 |
|---|---|---|---|---|
| `Refresh` | 明示的な呼び出し | ソースの全項目（1 万件なら 1 万回） | `Reset` 1 回。項目コンテナは全て再生成 | 呼び忘れると古い表示のまま残る |
| `IsLiveFiltering` + `LiveFilteringProperties` | 登録したプロパティの変更通知 | 変更された項目のみ（10 件変更なら 10 回） | 出入りが生じた項目についてのみ `Add` / `Remove`。残る項目のコンテナは再利用 | 項目のプロパティ以外の条件には追随しない。反映は非同期 |
| フィルタ済みコレクションを自前で作る | 作り直しを呼んだとき | 作り直しの実装次第 | `Clear` と再追加で `Reset` | 残る項目を含めて選択が全て失われる。並び替え・グループ化を自前で持つ必要がある |

自前でフィルタ済みの `ObservableCollection<T>` を組み立てる方式は、`ICollectionView` の制約を受けない代わりに選択状態の扱いが最も悪い。
`Clear` の後に再追加する実装では、フィルタを通り続ける項目の選択まで失われる。
`ICollectionView` が使える構成でこの方式を選ぶ理由は乏しい。

---

## まとめ

`ICollectionView` は、フィルタを保持していても項目のプロパティ変更では再評価を行わない。
「表示が古い」と感じたときに疑うのは述語の条件ではなく、再評価の契機が与えられているかである。

- **フィルタが項目のプロパティだけを読む場合:**
`IsLiveFiltering` を有効にし、`LiveFilteringProperties` に述語が読むプロパティ名をすべて登録する。
再評価が変更された項目だけに限られ、項目コンテナも再利用されるため、既定の選択肢とする。
- **フィルタが検索キーワードなど項目以外の状態を読む場合:**
その状態を書き換える側で `Refresh` を呼ぶ。
ライブフィルタでは追随できない。
- **両方に依存する場合:**
併用する。
プロパティ変更はライブフィルタに任せ、外部条件を変えたときだけ `Refresh` を呼ぶ。
- **フィルタ・並び替え・グループ化を同時に変更する場合:**
`DeferRefresh` で囲んで再構築を 1 回にまとめる。
スコープ内でビューの内容を読まないこと。
- **`DataView` を扱う場合:**
`Filter` も `IsLiveFiltering` も設定できない。
`BindingListCollectionView.CustomFilter` に文字列式を渡す。
`BindingList<T>` はこの代替も使えないため、絞り込みが必要なら `ObservableCollection<T>` へ移す。
- **同じコレクションを複数の画面で別々に絞り込む場合:**
既定のビューは共有されるため使わない。
画面ごとに `CollectionViewSource` のインスタンスを用意する。

---

<!-- 関連記事 -->
- [WPF DataGrid の並び替えを実装する方法](/ja/articles/wpf-datagrid-sorting/)
- [WPFのDataGridのソートを初期化する方法](/ja/articles/wpf-datagrid-sort-reset/)
- [WPF で ObservableCollection をバックグラウンドスレッドから更新するとクロススレッド例外が発生する問題の解決方法](/ja/articles/wpf-observablecollection-cross-thread-update/)
- [WPF ListBox 仮想化環境での SelectedItems が消えたように見える問題とその解決法](/ja/articles/wpf-listbox-virtualization-selecteditems/)

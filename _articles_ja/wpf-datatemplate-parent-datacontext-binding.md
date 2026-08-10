---
layout: article-ja
title: "WPF の DataTemplate 内から親の DataContext にバインドできない原因と RelativeSource の使い分け"
date: 2026-08-06
category: WPF
excerpt: "DataTemplate 内でバインドしても親の ViewModel に届かないのは、DataContext がアイテムへ切り替わるためである。RelativeSource・ElementName・x:Reference・PlacementTarget の到達範囲を比較する。"
image: /images/articles/wpf-datatemplate-parent-datacontext-binding/datatemplate-parent-binding.png
---

## 概要

`ItemsControl` や `ListBox` の `DataTemplate` に置いたボタンへ、親の ViewModel が持つコマンドをバインドしても実行されないことがある。
バインド式の記述ミスが原因に見えるが、式は正しく評価されており、評価の起点となる `DataContext` が各アイテムへ切り替わっているために目的のメンバーへ届いていない。
本記事では、この現象の原因を `DataContext` の継承の仕組みから説明し、`RelativeSource` / `ElementName` / `x:Reference` / `PlacementTarget` という 4 つの到達手段について、`DataTemplate` 内と `ContextMenu` / `ToolTip` 内での可否を実測した結果をもとに選択基準を整理する。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF
- 言語: C# 9 以降 / XAML（コード例は target-typed new と nullable 参照型の有効化を前提とする。C# 8 以前では型を明示し `!` を外して読み替える）
- 対象機能: `ItemsControl` 系コントロールの `ItemTemplate` / `DataTemplate`、および `ContextMenu` / `ToolTip`
- アーキテクチャ: MVVM（コマンドや共通の表示情報を、各アイテムではなく親の ViewModel が持つ構成）
- 挙動の確認環境: .NET 10 / Windows 11

---

## 問題

一覧の各行に削除ボタンを置くため、`DataTemplate` の中でコマンドにバインドしたとする。

```xml
<ItemsControl ItemsSource="{Binding Items}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding Name}" />
                <Button Content="削除" Command="{Binding DeleteCommand}" />
            </StackPanel>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

`DeleteCommand` は `Items` の各要素ではなく、`ItemsControl` の `DataContext` に設定した親の ViewModel が持つ。
この状態でボタンを押しても何も起きない。
しかもボタンは無効表示にならず、通常どおり押せる見た目のまま無反応になるため、画面を見ただけでは異常と判別できない。

出力ウィンドウには次のバインディングエラーが記録される（実際の出力は 1 行だが、ここでは読みやすさのため折り返している）。

```text
System.Windows.Data Error: 40 : BindingExpression path error: 'DeleteCommand' property not found on
'object' ''Measurement' (HashCode=58682725)'. BindingExpression:Path=DeleteCommand;
DataItem='Measurement' (HashCode=58682725); target element is 'Button' (Name='');
target property is 'Command' (type 'ICommand')
```

`DataItem` が親の ViewModel ではなくアイテムの型（この例では `Measurement`）になっている点が、この問題を見分ける決め手である。
エラーメッセージ自体の読み方は [WPF バインディングエラーの読み方と出力ウィンドウを使った原因特定](/ja/articles/wpf-binding-error-debugging-output-window/) で扱っている。

---

## 原因・背景

`Binding` は `Source` / `RelativeSource` / `ElementName` のいずれも指定しない場合、バインド先要素の `DataContext` を起点として `Path` を解決する。
`DataContext` は要素ツリーを親から継承するため、通常は `Window` に設定した ViewModel がそのまま下位の要素まで届く。

`ItemsControl` はこの継承の流れを途中で差し替える。
`ItemsSource` の各要素に対してコンテナ（`ContentPresenter` や `ListBoxItem` など）を生成し、その `DataContext` に対応するデータ項目そのものを設定するためである。
コンテナ自身の `DataContext` がアイテムであるため、`ItemContainerStyle` の `Setter` に書いたバインドもアイテムを起点に解決される（[WPF TreeView で任意のノードをコードから選択・展開する方法と SelectedItem が読み取り専用である理由](/ja/articles/wpf-treeview-select-item-programmatically/)）。
`DataContext` は継承されるプロパティであるため、`DataTemplate` から展開された要素はコンテナの値をそのまま受け継ぐ。
結果として、テンプレート内の `{Binding DeleteCommand}` は「アイテムの `DeleteCommand`」を探し、存在しないため解決されない。

見落としやすいのは、**`Command` の解決に失敗してもボタンは無効化されない**点である。
`Command` 経由でボタンが無効表示になるのは、`Command` に設定された `ICommand` の `CanExecute` が `false` を返す場合である。
`Command` が `null` のままなら判定の対象が無く、`IsEnabled` は `true` を保つ。
これは `Button` に限らず、`MenuItem` など `ICommandSource` を実装するコントロールに共通である。
そのため見た目は正常なまま、クリックしても何も起きないという症状だけが残る。
コマンド自体は設定できているのに有効・無効が切り替わらない場合は別の原因であり、[WPF で RelayCommand の CanExecute がボタンの有効・無効に反映されない問題の解決方法](/ja/articles/wpf-relaycommand-canexecute-not-updating/) で扱っている。

素の `{Binding}` は `DataContext` をたどるだけであり、この切り替わりを越える手段を持たない。
越えるには「要素ツリーを親方向へさかのぼる」か「目的の要素を名前で直接指す」かのいずれかが必要になる。

どこで `DataContext` が切り替わり、どこで要素ツリーそのものが途切れるかを図にすると次のようになる。

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-datatemplate-parent-datacontext-binding/datacontext-scope-and-popup-boundary.svg" alt="ウィンドウの要素ツリーで ContentPresenter 以下の DataContext がアイテムに切り替わることと、Popup の中の ContextMenu がツリーから切り離され、RelativeSource と ElementName が境界で止まる一方 PlacementTarget は所有要素を指せることを示した図。" width="820" height="360" loading="lazy">
  <figcaption><code>DataContext</code> の切り替わり（青が親の ViewModel、赤がアイテム）と、<code>Popup</code> による要素ツリーの分断。左側では <code>ItemsControl</code> まで親方向へさかのぼれるが、右側の <code>Popup</code> 内からは経路自体が無く、<code>PlacementTarget</code> だけが所有要素を指せる。</figcaption>
</figure>

---

## 解決方法

`RelativeSource` の `FindAncestor` モードで、`DataContext` が切り替わる前の祖先要素までさかのぼり、その `DataContext` を経由して目的のメンバーへ到達する。

祖先の型には `ItemsControl` を指定するのを基本とする。
`Window` や `UserControl` を指定しても到達できるが、テンプレートを別の画面や `UserControl` へ移したときに祖先の構成が変わるため、テンプレートを使い回す前提では `ItemsControl` のほうが壊れにくい。

`Path` の先頭には `DataContext.` を明示する。
`RelativeSource` が返すのは祖先の**要素**であり、その `DataContext` は自動的には経由されない。

---

## 実装例

次の XAML は、同じ `DataTemplate` の中に「素のバインド」と「`RelativeSource` 経由のバインド」を並べたものである。
どちらも親の ViewModel だけが持つ `Unit` を表示しようとしており、違いは到達手段だけである。

```xml
<ItemsControl ItemsSource="{Binding Items}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding Name}" />
                <TextBlock Text="{Binding Value}" />

                <!-- アイテムは Unit を持たないため、常に空欄になる -->
                <TextBlock Text="{Binding Unit}" />

                <!-- ItemsControl までさかのぼり、その DataContext の Unit を表示する -->
                <TextBlock Text="{Binding DataContext.Unit,
                                  RelativeSource={RelativeSource AncestorType={x:Type ItemsControl}}}" />

                <Button Content="削除"
                        Command="{Binding DataContext.DeleteCommand,
                                  RelativeSource={RelativeSource AncestorType={x:Type ItemsControl}}}"
                        CommandParameter="{Binding}" />
            </StackPanel>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

`CommandParameter="{Binding}"` には素のバインドを使い、その要素の `DataContext`（＝そのアイテム）をそのまま渡す。
「どのメンバーを呼ぶか」は親の ViewModel から、「どのアイテムに対してか」はアイテム自身から取る、という役割分担になる。

対応する ViewModel は次のとおりである。
`Unit` と `DeleteCommand` はアイテムの型ではなく、この ViewModel 側に置く。

```csharp
public sealed class Measurement
{
    public Measurement(string name, int value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public int Value { get; }
}

public sealed class MeasurementListViewModel
{
    public MeasurementListViewModel()
    {
        DeleteCommand = new RelayCommand(item => Items.Remove((Measurement)item!));
    }

    public string Unit => "kg";

    public ObservableCollection<Measurement> Items { get; } = new()
    {
        new Measurement("A", 120),
        new Measurement("B", 80),
        new Measurement("C", 240),
    };

    public ICommand DeleteCommand { get; }
}
```

`Measurement` を `record` にしないのは、`ObservableCollection<T>.Remove` が値等価で最初に一致した要素を削除するためである。
`record` は値等価であり、同じ内容の行が複数あると、押した行ではなく先頭の行が消える。

`RelayCommand` は、`Action<object?>` を受け取るコンストラクタを持つ `ICommand` 実装に読み替えられる（引数の型が固定された実装、たとえば CommunityToolkit.Mvvm であれば `RelayCommand<Measurement>` が対応する）。
このインスタンスを `Window` の `DataContext` に設定すると、`ItemsControl` はそれを継承し、`RelativeSource` 経由のバインドはここへ到達する。

上記の XAML に、どちらの記述の結果かを示す見出しと、値を囲む枠を加えて表示すると、2 つのバインドの差が画面に現れる。

<figure class="article-figure">
  <img src="/images/articles/wpf-datatemplate-parent-datacontext-binding/datatemplate-parent-binding.png" alt="ItemsControl の 3 行それぞれで、素の Binding を使った左側の枠が空欄のまま、RelativeSource 経由の右側の枠に kg が表示されている。" width="598" height="209" loading="lazy">
  <figcaption>同じ <code>DataTemplate</code> 内に置いた 2 つのバインドの結果。左の枠は <code>{Binding Unit}</code> でアイテムを起点に解決するため空欄になり、右の枠は <code>ItemsControl</code> までさかのぼるため親の <code>ViewModel</code> の値が表示される。見出しの文字列と各値を囲む枠は、どちらの記述の結果かを示し空欄を判別しやすくするため図へ付加したものである。削除ボタンは 2 つのバインドの対比に関係しないため図では省いている（.NET 10 / Windows 11 で生成）。</figcaption>
</figure>

---

## 注意点

- **`ElementName` も `DataTemplate` の内側から解決できる。**
`DataTemplate` は独自の XAML ネームスコープを持つが、WPF の `ElementName` 解決は外側のネームスコープまで探索するため、`{Binding DataContext.DeleteCommand, ElementName=RootWindow}` のような記述は `DataTemplate` 内でも機能する（.NET 10 / Windows 11 で確認）。
「テンプレート内では `ElementName` が使えない」という説明は WPF には当てはまらない。
マークアップでの `ElementName` 解決にネームスコープの制約を課しているのは WinUI 側の `Binding.ElementName` であり、WPF のドキュメントに同じ制約の記述は無い。
ただし参照先の名前に依存するため、テンプレートを別の画面へ移すと壊れる。
- **`ContextMenu` / `ToolTip` の中からは `RelativeSource` も `ElementName` も外側へ届かない。**
`ContextMenu` と `ToolTip` はいずれも `Popup` の中に配置され、`Popup` は内容を画面上の独立したウィンドウへ描画する。
そのためポップアップ内の要素はアプリケーションのウィンドウの要素ツリーから切り離されており、祖先の探索も名前の解決もツリーを辿る以上この境界を越えられない。
`DataTemplate` の場合と異なり、外側へ続く経路そのものが存在しないという違いである。
出力ウィンドウには `Cannot find source for binding with reference 'RelativeSource FindAncestor, AncestorType='System.Windows.Window', AncestorLevel='1''` や `Cannot find source for binding with reference 'ElementName=RootWindow'` が記録される。
- **`ContextMenu` の `DataContext` は所有要素から継承される。**
`ContextMenu` を `DataTemplate` 内の要素に付けた場合、継承されるのはその要素の `DataContext`、すなわちアイテムである。
`ContextMenu` 内の素の `{Binding}` がアイテムに解決される点は `DataTemplate` 内と同じであり、親の ViewModel へは届かない。
- **`ContextMenu` は `ItemsControl` の派生クラスである。**
`ContextMenu` の内側で `AncestorType={x:Type ItemsControl}` を指定すると、外側の一覧ではなく `ContextMenu` 自身が最初の一致になる。
`ContextMenu` の内側では祖先の型指定そのものが期待どおりに働かない。
- **`DataContext` の継承も `PlacementTarget` の設定も、メニューが開く時点で成立する。**
`ContextMenu` を `FrameworkElement.ContextMenu` に割り当てた場合、`ContextMenuService` が開くときに `PlacementTarget` を所有要素へ設定する。
開く前は `PlacementTarget` も `DataContext` も `null` であり、`ContextMenuOpening` の段階でもまだ設定されていない。
`PlacementTarget` を経由するバインドは、開くまでの間は解決できずにバインディングエラーを 1 度出力する。
`PlacementTarget` は依存関係プロパティであるため、設定された時点でバインドは再評価され、以後は正しく解決される。
- **`x:Reference` はドキュメント上の制約を伴う。**
`x:Reference` は XAML 2009 の構文であり、公式ドキュメントは「WPF では XAML 2009 の機能をマークアップコンパイルされない XAML でのみ使用できる」と述べている。
実際には `.xaml` ページに書いた `{x:Reference}` が `DataTemplate` 内でも `ContextMenu` 内でも解決される（.NET 10 / Windows 11 で確認）が、ドキュメントが保証する範囲を外れるため、第一候補にはしない。
同じドキュメントも、大半の WPF アプリケーションでは `ElementName` バインディングを使うべきだと述べている。

---

## 代替案・比較

各手段が親の ViewModel へ到達できるかを、`DataTemplate` 内と `ContextMenu` / `ToolTip` 内で実測した結果は次のとおりである。

| 方法 | DataTemplate 内 | ContextMenu / ToolTip 内 | メリット | デメリット |
| --- | --- | --- | --- | --- |
| 素の `{Binding X}` | 到達不可 | 到達不可 | 記述が最も短い | `DataContext` がアイテムへ切り替わるため親へ届かない |
| `RelativeSource` `AncestorType` | 到達可 | 到達不可 | 名前に依存せず、テンプレートを使い回せる | 要素ツリーの構成に依存する。ポップアップの境界を越えられない |
| `ElementName` | 到達可 | 到達不可 | 記述が短く、祖先の型を考えずに済む | 参照先の名前に依存する。ポップアップの境界を越えられない |
| `x:Reference` | 到達可 | 到達可 | ポップアップの境界を越えられる | XAML 2009 の機能でドキュメント上の制約がある |
| `PlacementTarget` + `Tag` | 該当なし | 到達可 | ポップアップで確実に動く | `Tag` を占有する。開くまで `PlacementTarget` が `null` |

到達可否で選択肢を絞ったうえで、どの条件でどれを採るかは「まとめ」の選択基準に従う。

`ContextMenu` から親の ViewModel へ到達する場合は、所有要素の `Tag` に親の `DataContext` を退避し、`PlacementTarget` 経由で読み出す。

```xml
<Border Tag="{Binding DataContext,
              RelativeSource={RelativeSource AncestorType={x:Type ItemsControl}}}">
    <Border.ContextMenu>
        <ContextMenu>
            <MenuItem Header="削除"
                      Command="{Binding PlacementTarget.Tag.DeleteCommand,
                                RelativeSource={RelativeSource AncestorType={x:Type ContextMenu}}}"
                      CommandParameter="{Binding}" />
        </ContextMenu>
    </Border.ContextMenu>
</Border>
```

`Tag` を設定する側の `Border` は `DataTemplate` 内にあるため、`RelativeSource` で `ItemsControl` まで到達できる。
`MenuItem` 側は `ContextMenu` を祖先として辿り、`PlacementTarget`（＝この `Border`）の `Tag` を経由して親の ViewModel へ届く。
`CommandParameter="{Binding}"` は `ContextMenu` が所有要素から継承したアイテムに解決されるため、対象アイテムはこれで渡せる。

`ToolTip` でも同じ構造が使える。祖先の型を `ToolTip` に読み替える点だけが異なる。

```xml
<TextBlock Text="{Binding PlacementTarget.Tag.Unit,
                  RelativeSource={RelativeSource AncestorType={x:Type ToolTip}}}" />
```

`Tag` を使わずにポップアップの境界を越える場合は `x:Reference` を用いる。
`Source` に要素そのものを与えるため、要素ツリーも名前の解決経路も辿らない。

```xml
<TextBlock Text="{Binding Source={x:Reference RootWindow}, Path=DataContext.Unit}" />
```

---

## まとめ

`DataTemplate` の中で親の ViewModel へバインドできない原因は、バインド式の記述ではなく `DataContext` の起点がアイテムへ切り替わっていることである。
`Command` の場合はボタンが無効化されずに無反応となるため、症状ではなく出力ウィンドウの `DataItem` を見て判別する。

選択の基準は次のとおりである。

- **通常の `DataTemplate` 内:**
`RelativeSource={RelativeSource AncestorType={x:Type ItemsControl}}` を使う。
名前に依存せず、テンプレートを別の一覧へ流用しても壊れにくい。
- **同じ画面から動かさないテンプレート:**
`ElementName` でも到達できる。
祖先の型を意識せずに済むが、名前の変更に追随する必要がある。
- **`ContextMenu` / `ToolTip` の中:**
所有要素の `Tag` に親の `DataContext` を退避し、`PlacementTarget.Tag` 経由で読み出す。
`RelativeSource` と `ElementName` はポップアップの境界を越えられないため、ここでは選択肢にならない。
- **`Tag` を他の用途で使っている場合:**
`x:Reference` でもポップアップの境界を越えられる。
ただし XAML 2009 の機能であり、ドキュメントが保証する範囲を外れる。
`PlacementTarget` が使える場面ではそちらを優先する。

構成として最も安定するのは、アイテムに対する操作をアイテム自身の ViewModel が持つ形にすることである。
親のコマンドへ到達する記述が増えている場合は、そのコマンドをアイテムの ViewModel へ移せないかを先に検討する。

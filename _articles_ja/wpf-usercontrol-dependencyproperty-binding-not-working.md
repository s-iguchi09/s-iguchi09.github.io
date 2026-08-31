---
layout: article-ja
title: "WPF の UserControl に定義した DependencyProperty へ内部からバインドできない原因と DataContext の設計"
date: 2026-08-15
category: WPF
excerpt: "UserControl に依存関係プロパティを追加したのに、内部の {Binding Title} だけが空欄になる。DataContext の継承という原因を .NET 10 の実測で切り分け、RelativeSource・ElementName・内側ルートへの委譲を比較する。"
image: /images/articles/wpf-usercontrol-dependencyproperty-binding-not-working/usercontrol-dp-binding.png
---

## 概要

再利用する部品を `UserControl` として切り出し、外から値を受け取るために依存関係プロパティを追加する。
利用側の `Title="{Binding HeaderText}"` は正しく届いており、デバッガーで `Title` プロパティを見れば期待した文字列が入っている。
それにもかかわらず、コントロールの内部に書いた `{Binding Title}` だけが空欄のままになる。

原因は依存関係プロパティの登録方法ではない。
`{Binding}` の既定の起点が `DataContext` であること、そして `UserControl` 要素の `DataContext` が利用側から継承されることの 2 点が重なった結果である。
プロパティには値が届いているのに画面が空になるという非対称が、この症状の特定を難しくしている。

本記事では、値が届いていることと表示されないことが両立する理由を分解し、内部から自身の依存関係プロパティを参照する 3 つの書き方を比較する。
広く出回る `DataContext = this` という対処が、なぜ利用側の書き方によって効いたり効かなかったりするのかも併せて扱う。
本記事に「実測」と記した値は、すべて後述の環境で実際に動かして得た結果である。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF（実測はすべて .NET 10 / Windows 11 で取得）
- 言語: C# / XAML（掲載するコードは C# 7.0 以降で動作する構文のみを使う）
- 対象機能: `UserControl`、`DependencyProperty.Register`、`Binding` の `RelativeSource` / `ElementName`
- アーキテクチャ: MVVM（利用側のウィンドウに ViewModel を `DataContext` として設定する構成）
- その他制約: `UserControl` を XAML ファイルとコードビハインドの組で定義する構成を基準とする

掲載する XAML の `...` は、標準の `xmlns` 宣言など本題に関係しない属性の省略を示す。
そのまま貼り付けても解析できないため、実際のファイルでは通常の宣言に置き換える。

---

## 問題

見出しの文字列を外から受け取る `InfoCard` を作る。
コードビハインドで `Title` を依存関係プロパティとして登録する。

```csharp
public partial class InfoCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(InfoCard), new PropertyMetadata(string.Empty));

    public InfoCard() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
```

登録内容に誤りはなく、`Title` は外部から設定できる状態にある。
続けて、この値を表示する XAML を書く。

```xml
<UserControl x:Class="Sample.InfoCard" ...>
    <Border BorderBrush="Gray" BorderThickness="1" Padding="6">
        <TextBlock x:Name="TitleText" Text="{Binding Title}" />
    </Border>
</UserControl>
```

`TextBlock` に付けた `x:Name` は、後述するトレースの読み取りに使うためのものであり、表示自体には影響しない。

利用側では、ウィンドウの `DataContext` に設定した ViewModel の `HeaderText` を渡す。

```xml
<local:InfoCard Title="{Binding HeaderText}" />
```

この構成を動かすと、`InfoCard` の枠だけが描かれ、文字列は表示されない。
実測では `InfoCard.Title` は `HeaderText` の値を保持しており、内部の `TextBlock.Text` だけが空文字列であった。
`Title` の既定値を空文字列以外にしても表示は変わらない。
`TextBlock` が表示していたのは `Title` の既定値ではなく、解決に失敗したバインディングのターゲット側の既定値だからである。

出力ウィンドウには次のトレースが記録される。

```text
System.Windows.Data Error: 40 : BindingExpression path error: 'Title' property not found on
'object' ''PageViewModel' (HashCode=18705942)'. BindingExpression:Path=Title;
DataItem='PageViewModel' (HashCode=18705942); target element is 'TextBlock' (Name='TitleText');
target property is 'Text' (type 'String')
```

`DataItem` に現れているのが `InfoCard` ではなく利用側の ViewModel である点が、原因を直接示している。
バインディングは `InfoCard.Title` ではなく、`PageViewModel.Title` を探していた。

---

## 原因・背景

`{Binding Title}` のように `Path` だけを書いたバインディングは、ソースを指定していない。
ソースを省略したバインディングは、ターゲット要素の `DataContext` を起点として `Path` を解決する。

`DataContext` は要素ツリーを下方向へ継承される値である。
`InfoCard` を配置した時点で、`InfoCard` 要素の `DataContext` には利用側のウィンドウが持つ ViewModel が流れ込む。
`InfoCard` の内部要素はさらにそれを継承する。
実測でも、内部の `TextBlock` から見た `DataContext` は `InfoCard` ではなく `PageViewModel` であった。

一方、`Title` は `InfoCard` という**要素のプロパティ**であり、`DataContext` に載っているオブジェクトのプロパティではない。
依存関係プロパティとして登録しても、この関係は変わらない。
外から `Title="{Binding HeaderText}"` と書いたバインディングが成立するのは、そのバインディングのターゲットが `InfoCard` 要素で、ソースが利用側の `DataContext` だからである。
値が届くことと、内部から参照できることは別の経路の話である。

記法ごとの起点を整理すると次のようになる。

| 記法 | 値を探す起点 | 内部から自身の DP への到達可否 |
| --- | --- | --- |
| `{Binding Title}` | その要素の `DataContext`（継承値） | 到達しない |
| `{Binding Title, RelativeSource={RelativeSource AncestorType=...}}` | 要素の親チェーンをさかのぼった祖先要素 | 到達する |
| `{Binding Title, ElementName=Root}` | 同じ名前スコープ内の名前付き要素 | 到達する |
| `{Binding Title, Source=...}` | 明示したオブジェクト | 到達しない（マークアップ上で解決できる固定のオブジェクトに限られる） |

この症状が厄介なのは、失敗の現れ方が状況によって変わる点にある。

**利用側の `DataContext` に同名のプロパティがある場合、エラーは一切出ない。**
ViewModel 側にも `Title` という名前のプロパティが存在すると、内部の `{Binding Title}` はそちらへ解決される。
実測では、`InfoCard.Title` に `VM-TITLE` を渡しているにもかかわらず、内部の `TextBlock` には ViewModel 側の値である `VM-OWN-TITLE` が表示され、トレースは 1 行も出力されなかった。
値が出ている以上、バインディングの誤りとして疑われにくい。

**`DataContext` が `null` の場合も、エラーは一切出ない。**
`DataContext` を設定していない親の下に `InfoCard` を置いた実測では、`Title` に値を設定していても内部は空欄のままで、出力ウィンドウに追加されたトレースは 0 行であった。
バインディングはソースが未確定の状態で待機し、失敗として報告されない。
出力ウィンドウにエラーが出ないことは、バインディングが正しいことの証明にならない。
エラーが出た場合のメッセージの読み方は [WPF バインディングエラーの読み方と出力ウィンドウを使った原因特定](/ja/articles/wpf-binding-error-debugging-output-window/) で扱っている。

---

内部要素からの参照方法を変えて、届く値と `DataContext` の型を測った結果が次の図である。

<figure class="article-figure">
  <img src="/images/articles/wpf-usercontrol-dependencyproperty-binding-not-working/usercontrol-dp-scope.svg" alt="UserControl 内部の TextBlock からの参照方法別に、届いた文字列と DataContext の型を測った表。素の Binding と、内部要素に書いた RelativeSource Self は空のまま。RelativeSource AncestorType では Title の値が届く。DataContext はいずれも利用側の PageViewModel である。" width="705" height="170" loading="lazy">
  <figcaption>.NET 10 / Windows 11 で、<code>InfoCard</code>（<code>Title</code> 依存関係プロパティを持つ <code>UserControl</code>）の内部に置いた <code>TextBlock</code> から <code>Title</code> を参照した結果。利用側の <code>DataContext</code> には <code>PageViewModel</code> を設定している。</figcaption>
</figure>

**`DataContext` の列はどの行も `PageViewModel` である。** 内部要素から見た `DataContext` は `InfoCard` ではなく、利用側の ViewModel である。
素の `{Binding Title}` はこの `PageViewModel` に `Title` を探しにいくため、値が届かない。

2 行目にも注意する。`RelativeSource Self` は内部要素自身を指すため、`TextBlock` に `Title` を探すことになり、やはり届かない。
届くのは `AncestorType` で `UserControl` までさかのぼった 3 行目だけである。

---

## 3 つの参照方法

内部のバインディングに、`DataContext` 以外の起点を明示する。
選択肢は 3 つある。

1. **`RelativeSource` で祖先をたどる** — 要素の親チェーンを上方向に探索し、`UserControl` 自身を起点にする。
バインディングごとに指定する。
2. **`ElementName` で自身を名前で指す** — ルート要素に `x:Name` を付け、その名前を参照する。
バインディングごとに指定する。
3. **内側のルート要素へ `DataContext` を委譲する** — `UserControl` の直下に置いたパネルの `DataContext` だけを `UserControl` 自身へ切り替える。
以降は内部のすべてのバインディングを `{Binding Title}` のまま書ける。

いずれも `UserControl` 要素そのものの `DataContext` には手を触れない。
ここを書き換えると利用側からのバインディングが壊れる。
その詳細は「注意点」で扱う。

参照箇所が少ない場合や、内部で利用側の `DataContext` も併用したい場合は 1 を採る。
2 は 1 とほぼ等価であり、記述を短くしたい場合に選ぶ（両者の差は「代替案・比較」で扱う）。
内部で参照するプロパティが 3 つ以上あるコントロールでは 3 を採る。

まず 1 と 2 を、同じコントロールの中に並べて確認する。
ルート要素へ `x:Name="Root"` を付け、`Title` を 3 通りの書き方で表示する。

```xml
<UserControl x:Class="Sample.InfoCard" x:Name="Root" ...>
    <StackPanel>
        <TextBlock Text="{Binding Title}" />
        <TextBlock Text="{Binding Title, RelativeSource={RelativeSource AncestorType=UserControl}}" />
        <TextBlock Text="{Binding Title, ElementName=Root}" />
    </StackPanel>
</UserControl>
```

3 つとも同じコントロールの同じプロパティを指す意図で書いており、差は起点の指定方法だけである。
これを `Title="{Binding HeaderText}"` として配置すると、値が現れるのは下 2 つに限られる。

<figure class="article-figure">
  <img src="/images/articles/wpf-usercontrol-dependencyproperty-binding-not-working/usercontrol-dp-binding.png" alt="UserControl を 1 つ配置した画面。最上部に利用側のマークアップが 1 行あり、その下の枠の中に 3 組の記法ラベルと表示欄が縦に並ぶ。素の Binding Title を使った一番上の欄だけが空欄で、RelativeSource を使った欄と ElementName を使った欄には Report と表示されている。" width="546" height="278" loading="lazy">
  <figcaption>同じ <code>Title</code> を 3 通りの書き方で表示した結果。<code>InfoCard</code> の範囲を示す外枠、各欄の上の記法ラベル、最上部の利用側マークアップは、いずれも対応関係を示すために図へ付加したものである（.NET 10 / Windows 11 で生成）。</figcaption>
</figure>

`AncestorType=UserControl` は最も近い `UserControl` を探す。
そのため、バインディングを書いた要素が別の `UserControl` の内側に入っている構成では、意図した対象を外す。
これを避けるには、探索対象を自身の型で指定する。

```xml
<TextBlock Text="{Binding Title,
           RelativeSource={RelativeSource AncestorType={x:Type local:InfoCard}}}" />
```

型を指定すると探索はその型（およびその派生型）に一致する祖先で止まるため、内部の入れ子構成が変わっても対象は変わらない。
この書き方では `local` 名前空間の宣言（`xmlns:local="clr-namespace:Sample"`）が必要になる。

次に 3 の書き方を示す。
`UserControl` の直下に置いた `Grid` の `DataContext` だけを自身へ向ける。

```xml
<UserControl x:Class="Sample.InfoCard" x:Name="Root"
             xmlns:local="clr-namespace:Sample" ...>
    <Grid DataContext="{Binding RelativeSource={RelativeSource AncestorType={x:Type local:InfoCard}}}">
        <StackPanel>
            <TextBlock Text="{Binding Title}" />
            <TextBox Text="{Binding Title, UpdateSourceTrigger=PropertyChanged}" />
        </StackPanel>
    </Grid>
</UserControl>
```

`DataContext` を設定する対象が `UserControl` 要素ではなく、その子である点が重要である。
実測では、この構成で `Grid` 配下の `DataContext` が `InfoCard` になる一方、`InfoCard` 要素自身の `DataContext` は利用側の ViewModel のままであった。
外からのバインディングと内部のバインディングが、互いに干渉せずに成立する。

内部から値を書き戻すには、外からのバインディングが双方向で成立している必要がある。
この条件は、依存関係プロパティ側のメタデータで既定の転送方向を変えるか、利用側で `Mode=TwoWay` を指定するかのいずれかで満たせる。
`DependencyProperty.Register` に `PropertyMetadata` を渡した場合、外からのバインディングは既定で片方向になる。

```csharp
public static readonly DependencyProperty TitleProperty =
    DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(InfoCard),
        new FrameworkPropertyMetadata(
            string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
```

実測では、この指定を加えた `Title` に対して内部の `TextBox` を編集すると、値が ViewModel の `HeaderText` まで戻った。
上の `TextBox` のように内部から `Title` を更新する構成では、この指定を省略すると外側のバインディングが壊れる。
その詳細は次節で扱う。
利用側で `Mode=TwoWay` を明示しても同じ結果になるが、双方向が前提の入力用コントロールでは、利用側の書き忘れを防げる `BindsTwoWayByDefault` が扱いやすい。

---

## 選択の分岐点

3 つのどれを使うかは、内部で参照するプロパティの数と、利用側の `DataContext` も併用するかで決まる。

**参照箇所が 1、2 か所にとどまるなら `RelativeSource AncestorType`。**
バインディングごとに起点を書くため記述は増えるが、`DataContext` は利用側のまま残る。内部で利用側の `DataContext` も併せて参照したい場合は、この方法か次の `ElementName` を使う。

**ルート要素を名前で指すなら `ElementName`。**
記述は短くなるが、解決の仕組みは `RelativeSource` と異なる。`ElementName` は同じ XAML 名前スコープ内の名前を引き、`RelativeSource AncestorType` は要素ツリーを上へたどって型で探す。テンプレートの中のように名前スコープが分かれる位置では `ElementName` が解決しない。利用側の `DataContext` を参照するときは `Path=DataContext.HeaderText` のように書く。

**内部で参照するプロパティが 3 つ以上あるなら、内側のルート要素へ `DataContext` を委譲する。**
1 か所の設定で済み、以降は内部のすべてを `{Binding Title}` のまま書ける。`ContextMenu` の中からの参照が解決するのも、利用側からのバインディングを壊さない方法のうちではこれだけである。

いずれも `UserControl` 要素そのものの `DataContext` には手を触れない。ここを書き換えると利用側からのバインディングが壊れる。

---

## 方法別の比較

内部から自身の依存関係プロパティを参照する 4 つの方法を比較する。

| 方法 | 記述量 | 利用側からのバインディング | `ContextMenu` 内 | 適するケース |
| --- | --- | --- | --- | --- |
| `RelativeSource AncestorType` | バインディングごとに指定 | 影響なし | 解決しない | 参照箇所が少なく、内部で利用側の `DataContext` も併用する場合 |
| `ElementName` + ルートの `x:Name` | バインディングごとに指定 | 影響なし | 解決しない | 記述を短くしたい場合 |
| 内側ルートへ `DataContext` を委譲 | 1 箇所のみ | 影響なし | 解決する | 内部で参照するプロパティが 3 つ以上ある場合 |
| `DataContext = this` | 1 箇所のみ | **壊れる** | 解決する | 該当なし（採用しない） |

`RelativeSource` と `ElementName` は通常の構成では結果が等価であり、差が出るのは次の 2 つの構成に限られる。

1 つは、実装例で触れた「バインディングを書いた要素が別の `UserControl` の内側に入っている構成」である。
`ElementName` は名前で直接指すため、この影響を受けない。
実測でも、別の `UserControl` の内側に置いた要素から `AncestorType=UserControl` を評価すると、外側のコントロールではなく内側のコントロールが選ばれた。
`AncestorType={x:Type local:InfoCard}` と自身の型を指定すれば `RelativeSource` でも対象は安定するが、その型の派生型が祖先にある場合はやはり近い方が選ばれる。

もう 1 つは、テンプレートを別のコントロールへ再利用する構成である。
`ElementName` は使用側の名前スコープに `Root` が無い時点で解決できなくなるのに対し、`AncestorType` は祖先の型さえ一致すれば成立する。

内側ルートへの委譲は、内部の記述が `{Binding Title}` のまま済む点が最大の利点である。
一方で、内部から利用側の `DataContext` を参照するには一手間が要る。
`UserControl` 要素自身の `DataContext` は利用側の ViewModel のままであるため、`{Binding DataContext.HeaderText, RelativeSource={RelativeSource AncestorType={x:Type local:InfoCard}}}` と書けば到達でき、実測でも解決した。
ただし、この参照を必要とする設計は再利用可能な部品として成立していない可能性が高く、必要なデータは依存関係プロパティとして明示的に受け取る形へ変えるのが妥当である。

`DataContext = this` は、内部の記述量という点では委譲と同じ利点を持つが、利用側からのバインディングを壊す。
コントロールを自分の 1 画面でしか使わない段階では症状が出ないため、再利用を始めた時点で問題が表面化する。

---

## 注意点

コンストラクターで `DataContext = this` と書くと、内部の `{Binding Title}` は動くようになる。
しかし `UserControl` 要素の `DataContext` が自身に固定されるため、今度は利用側の `Title="{Binding HeaderText}"` が `InfoCard` の中に `HeaderText` を探して失敗する。
実測では `System.Windows.Data Error: 40` が記録され、`Title` は既定値のままであった。
`<UserControl DataContext="{Binding RelativeSource={RelativeSource Self}}">` と XAML で書いた場合も結果は同じである。

この破綻は、利用側の書き方によって現れたり隠れたりする。
リテラルを渡した `Title="Report"` はバインディングを介さないため、`DataContext` の内容に関係なく成立する。
同じコントロールでも、渡し方がリテラルかバインディングかで結果が分かれる。

<figure class="article-figure">
  <img src="/images/articles/wpf-usercontrol-dependencyproperty-binding-not-working/usercontrol-dp-datacontext-this.png" alt="DataContext = this を設定したコントロールを 2 つ並べた画面。各コントロールの上に利用側のマークアップが 1 行ずつ添えられ、リテラルで Title を渡した上の表示欄には Report と表示され、Binding で渡した下の表示欄は空欄になっている。" width="374" height="186" loading="lazy">
  <figcaption>コンストラクターで <code>DataContext = this</code> を設定した同一のコントロール。上下の差は利用側の渡し方だけである。各コントロールの上のマークアップは、その渡し方を示すために図へ付加したものである（.NET 10 / Windows 11 で生成）。</figcaption>
</figure>

残りの落とし穴を以下に挙げる。

- **利用側が `DataContext` を明示すると、`DataContext = this` は上書きされる。**
`<local:InfoCard DataContext="{Binding Detail}" ... />` のように利用側で `DataContext` を設定すると、コンストラクターでの代入より後に適用される。
その結果、外からのバインディングも内部の `{Binding Title}` も、差し替えられたオブジェクトを起点に解決されるようになる。
実測では、差し替え先に `Title` が無い場合は内部が空欄になり、`Title` がある場合はエラーを出さずに別のオブジェクトの値を表示した。
同じコントロールが配置場所によって別の壊れ方をするため、再現条件の切り分けが困難になる。
- **片方向のまま内部から値を更新すると、外側のバインディングが破棄される。**
`Title="{Binding HeaderText}"` が片方向で成立している状態で `Title` にローカル値を書き込むと、外側のバインディングが外れる。
実測では、コードから `this.Title = "..."` と代入した場合も、内部の `TextBox` に張った双方向バインディング経由で書き戻した場合も、`BindingOperations.GetBindingExpression` が `null` を返した。
以後、ViewModel 側の値を変更してもコントロールへは届かない。
内部から値を更新する構成では `BindsTwoWayByDefault`（または利用側の `Mode=TwoWay`）を指定する。
- **`SetCurrentValue` は `BindsTwoWayByDefault` の代わりにはならない。**
`SetCurrentValue` はバインディングを維持したまま実効値だけを変更する。
実測でも、代入と異なりバインディングは外れず、その後にソース側が変化すると値は上書きされた。
一方で、片方向のまま `SetCurrentValue` を使ってもソースへは書き戻らない。
表示上の値だけを一時的に変えるなら `SetCurrentValue`、利用側へ値を返すなら `BindsTwoWayByDefault` と、目的で使い分ける。
ローカル値と依存関係プロパティの値優先順位は [WPF で Style の Trigger・DataTrigger が効かない原因と依存関係プロパティの値優先順位](/ja/articles/wpf-style-trigger-not-working-local-value/) で扱っている。
- **`ContextMenu` の中からは、外側の `UserControl` を `RelativeSource` でも `ElementName` でも指せない。**
両者は失敗するが、理由は別である。
`RelativeSource` の祖先探索は親チェーンをたどるが、`ContextMenu` は要素ツリーの子ではなく `FrameworkElement.ContextMenu` プロパティとして付くため、チェーンが外側へつながらない。
`ElementName` は名前スコープから名前付き要素を探すが、`ContextMenu` は独自の名前スコープを持つため、`UserControl` 側に登録された `Root` が見えない。
実測では、`UserControl` 内の `Button` に付けた `ContextMenu` の中で `AncestorType=UserControl` と `ElementName=Root` の双方が `System.Windows.Data Error: 4` となり、`MenuItem.Header` は `null` のままであった。
一方、`DataContext` はこの親チェーンとは別の経路で配置元から継承されるため、前述の 3 の構成ではメニューを開いた状態で素の `{Binding Title}` が解決した。
この継承が成立するのはメニューが開いて配置元と結び付いた後であり、開く前や `ContextMenuOpening` の時点では成立しない。
`{Binding PlacementTarget.DataContext.Title, RelativeSource={RelativeSource AncestorType=ContextMenu}}` という書き方が回避策として挙げられることがあるが、これが指すのは配置元要素の `DataContext` である。
3 の構成では素の `{Binding Title}` で足りるため書く理由が無く、1 や 2 の構成では利用側の ViewModel を指してしまう。
この書き方が有効なのは、メニューから自身の依存関係プロパティではなく利用側の ViewModel へ到達したい場合に限られる。
- **インラインで宣言した `Popup` の中では、どちらも解決する。**
`Popup` の中身は `PopupRoot` という別の視覚ツリーに描かれる。
それでも `Popup` 自体は `UserControl` の XAML に子要素として書かれているため、`RelativeSource` がたどる親チェーンは途切れず、`ElementName` から見て `Root` も同じ名前スコープに属したままである。
実測でも、`UserControl` の中にインラインで書いた `Popup` の内側から `AncestorType=UserControl` と `ElementName=Root` の双方が解決した。
`ContextMenu` との差は、別の視覚ツリーに描かれるかどうかではなく、親チェーンと名前スコープが外側へつながっているかどうかである。
- **`DataTemplate` の中でも解決する。**
テンプレートは別の名前スコープを持つが、実測では、`UserControl` 内にインラインで書いた `DataTemplate` でも、`UserControl.Resources` にキー付きで置いた `DataTemplate` でも、`ElementName=Root` と `AncestorType=UserControl` の双方が解決した。
`ElementName` の解決先はテンプレートを定義したファイルではなく、テンプレートを使う側の名前スコープで決まる。
そのため、テンプレートを別ファイルへ切り出すこと自体は問題にならず、`Root` を持たない別のコントロールから同じテンプレートを再利用した時点で `ElementName` だけが破綻する。
`DataTemplate` から外側の `DataContext` を参照する方法は [WPF の DataTemplate 内から親の DataContext にバインドできない原因と RelativeSource の使い分け](/ja/articles/wpf-datatemplate-parent-datacontext-binding/) で扱っている。
- **CLR プロパティのラッパーに処理を書いても呼ばれない。**
XAML の解析とバインディングは、`Title` の setter ではなく `SetValue` を直接呼ぶ。
ラッパーを迂回することは公式ドキュメントに明記されている。
値の変化に応じた処理は、`PropertyMetadata` の `PropertyChangedCallback` に記述する。
- **`x:Name` の重複は問題にならない。**
`UserControl` のルートに付けた `x:Name="Root"` は、そのコントロールの名前スコープに閉じる。
実測でも、利用側に同じ名前の要素を置いた状態で双方が別の要素として解決し、エラーは出なかった。

---
---

## まとめ

内部の `{Binding}` が空欄になったときは、まず `DataContext` の中身を確認する。
値が届いているかどうかは、依存関係プロパティを直接読めば切り分けられる。

- **依存関係プロパティに値が入っていて表示だけが出ない場合** — 内部のバインディングが利用側の `DataContext` を見ている。
`RelativeSource` か `ElementName` で起点を指定する。
- **依存関係プロパティが既定値のままの場合** — 外からのバインディングが解決していない。
まず `UserControl` 自身の `DataContext` が書き換えられていないかを疑い、続いて利用側のパスと `DataContext` の設定を確認する。
- **エラーが出ないのに値が違う場合** — 利用側の `DataContext` に同名のプロパティが存在する。
この状態は出力ウィンドウに現れないため、`RelativeSource` を明示するまで気付けない。

分岐点は、内部で参照するプロパティの数にある。
3 つ以上なら内側のルート要素へ `DataContext` を委譲し、1、2 か所なら `RelativeSource` か `ElementName` を個別に指定する。
いずれの場合も `UserControl` 要素自身の `DataContext` には代入しない。
内部から値を書き戻す入力用のコントロールでは、`FrameworkPropertyMetadataOptions.BindsTwoWayByDefault` を併せて指定する。

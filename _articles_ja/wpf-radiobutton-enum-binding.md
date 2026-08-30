---
layout: article-ja
title: "WPF で RadioButton を enum にバインドすると初期選択が表示されない問題と GroupName の役割"
date: 2026-08-22
category: WPF
excerpt: "ViewModel が正しい列挙体の値を保持しているのにラジオボタンが未選択になるのは、GroupName を省いたことで別々の列挙体のボタンが 1 グループに統合されるためである。原因と解決策を実測で整理する。"
image: /images/articles/wpf-radiobutton-enum-binding/radiobutton-enum-groupname.png
---

## 概要

WPF で列挙体の値を選ばせる UI は、`RadioButton.IsChecked` をコンバーター経由で列挙体のプロパティにバインドして組むことが多い。
この構成では、ViewModel が正しい値を保持しているにもかかわらず、画面上では対応する `RadioButton` が選択されていない、という状態が発生する。
本記事では、この現象が `GroupName` の省略によるグループの統合に起因することを実測で示し、`GroupName` の指定方針と `ConvertBack` の戻り値の選び方、さらにコンバーター・ラッパープロパティ・添付ビヘイビア・選択コントロールへの置き換えという 4 方式の使い分けを整理する。

---

## 前提・対象環境

- フレームワーク: .NET 8 以降 / WPF（.NET Framework 版の WPF でも同じ挙動）
- 検証環境: .NET 10 / Windows 11（本記事の実測結果と図はこの環境で取得したものであり、グループ化の判定・`ConvertBack` の呼び出し・検証エラーの発生は .NET Framework 4.8 でも同一の結果を確認した）
- 言語: C# / XAML（コード例は nullable 参照型有効を前提とする）
- 対象コントロール: `System.Windows.Controls.RadioButton`、`System.Windows.Data.IValueConverter`
- アーキテクチャ: MVVM（選択状態を ViewModel の列挙体プロパティで保持する構成）
- 名前空間: `System`、`System.ComponentModel`、`System.Globalization`、`System.Windows.Data`
- XAML の `local` 接頭辞: 列挙体とコンバーターを定義した CLR 名前空間を指す（例: `xmlns:local="clr-namespace:PrintSettingsApp"`）

---

## 問題

印刷設定のダイアログを想定する。
印刷品質を表す `Quality` と、面付けを表す `PageLayout` の 2 つの列挙体があり、それぞれをラジオボタンで選ばせる。

```csharp
public enum Quality
{
    Draft,
    Standard,
    Fine,
}

public enum PageLayout
{
    Single,
    Dual,
}
```

いずれも通常の列挙体であり、`Flags` 属性や明示的な値の指定は伴わない。
この 2 つが同じ画面に同居することが、後述する問題の前提条件になる。

ViewModel は 2 つの列挙体プロパティを持ち、初期値をそれぞれ `Quality.Standard`、`PageLayout.Single` とする。
`Quality.Standard` は宣言順で 2 番目の値であり、初期選択が正しく反映されていれば `Draft` ではなく `Standard` が選択された状態で表示される。

```csharp
public sealed class PrintSettingsViewModel : INotifyPropertyChanged
{
    private Quality _quality = Quality.Standard;
    private PageLayout _pageLayout = PageLayout.Single;

    public Quality Quality
    {
        get => _quality;
        set
        {
            if (_quality == value) return;
            _quality = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Quality)));
        }
    }

    public PageLayout PageLayout
    {
        get => _pageLayout;
        set
        {
            if (_pageLayout == value) return;
            _pageLayout = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageLayout)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

変更通知は 2 つのプロパティとも実装済みであり、通知漏れは本記事が扱う問題の原因ではない。
この点は、以降で原因を切り分けるうえでの前提となる。

列挙体の値と `bool` を相互変換するコンバーターを用意し、`ConverterParameter` に対象の値を渡してバインドする。
コンバーターの実装は「実装例」で示す。
ここでは、一般に正しいとされる、選択解除時に `Binding.DoNothing` を返す実装を使っている。
以下は 5 つのラジオボタンを 1 つの `StackPanel` にまとめた、実務でよく用いられる書き方である。

```xml
<StackPanel>
    <StackPanel.Resources>
        <local:EnumToBooleanConverter x:Key="EnumToBoolean" />
    </StackPanel.Resources>

    <RadioButton Content="Draft"
                 IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Draft}}" />
    <RadioButton Content="Standard"
                 IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Standard}}" />
    <RadioButton Content="Fine"
                 IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Fine}}" />

    <RadioButton Content="Single"
                 IsChecked="{Binding PageLayout, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:PageLayout.Single}}" />
    <RadioButton Content="Dual"
                 IsChecked="{Binding PageLayout, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:PageLayout.Dual}}" />
</StackPanel>
```

この XAML は構文としては正しく、バインディングエラーも出ない。
それにもかかわらず、起動直後に `Single` は選択された状態で表示されるのに対し、`Quality` 側は 3 つとも未選択のまま表示される。
ViewModel の `Quality` は `Standard` を保持しており、画面と ViewModel が食い違う。

<figure class="article-figure">
  <img src="/images/articles/wpf-radiobutton-enum-binding/radiobutton-enum-groupname.png" alt="同じ ViewModel の値を表示する 2 つのラジオボタン群。GroupName を指定していない左側では Quality = Standard であるのに Draft・Standard・Fine のいずれも未選択で、GroupName を指定した右側では Standard が選択されている。" width="419" height="201" loading="lazy">
  <figcaption>左列は本節の XAML、右列は後述の「実装例」の XAML に対応する。左右とも ViewModel の値は <code>Quality = Standard</code> / <code>PageLayout = Single</code> で、異なるのは <code>GroupName</code> の指定だけである。<code>GroupName</code> を省いた左側では、後から選択状態になった <code>PageLayout</code> 側のラジオボタンが <code>Quality</code> 側の選択を解除している。上段のラベルと下段の 2 行は比較のために加えた表示で、本文の XAML には含まれない。.NET 10 / Windows 11 で取得。</figcaption>
</figure>

---

## 原因・背景

公式ドキュメントは、`RadioButton` のグループ化の方法として「親要素の内側に配置する」と「各 `RadioButton` に `GroupName` プロパティを設定する」の 2 通りを示している（[RadioButton クラス](https://learn.microsoft.com/dotnet/api/system.windows.controls.radiobutton)）。
WPF の `RadioButton.GroupName` の既定値は空文字列である。
`GroupName` が空文字列のときは名前によるグループ化が行われず、代わりに論理ツリー上の親（`FrameworkElement.Parent`）の単位でグループ化される。
論理親が同じラジオボタンは 1 つのグループになり、論理親が異なれば別のグループになる。
上記の 5 つのラジオボタンは同じ `StackPanel` を論理親に持つため、**バインド先のプロパティが別であっても 1 つのグループとして扱われる**。

グループ内のラジオボタンは相互排他になる。
初期化順は XAML の記述順であり、まず `Quality` 側の `Standard` が選択状態になり、続いて `PageLayout` 側の `Single` が選択状態になる。
`Single` が選択された時点で、同一グループとみなされている `Standard` がグループ機構によって解除される。

問題は、この解除がバインディングを通じてソース側へ伝わろうとする点にある。
`ToggleButton.IsChecked` は `bool?` 型の依存関係プロパティで、メタデータで既定の双方向バインディングが有効になっており、既定の `UpdateSourceTrigger` は `PropertyChanged` である。
そのため解除は即座にソース更新として扱われ、コンバーターの `ConvertBack` が `false` で呼び出される。
実測でも、`Single` の選択が確定した直後に `ConvertBack(value: false, parameter: Quality.Standard)` が 1 回呼ばれることを確認した。

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-radiobutton-enum-binding/radiobutton-group-convertback-path.svg" alt="GroupName を省いたときに解除がソースへ伝わる経路を示す 3 段構成の図。1 段目は、GroupName が空文字列のため 1 つの暗黙のグループができ、PageLayout.Single が選択されると Quality.Standard の IsChecked が true から false へ変わること、およびそのバインディングが TwoWay で UpdateSourceTrigger が PropertyChanged であること。2 段目は、その変化によって ConvertBack が value: false、parameter: Quality.Standard で呼ばれること。3 段目は、ConvertBack の戻り値 4 通りそれぞれの結果で、Binding.DoNothing と DependencyProperty.UnsetValue はいずれも Quality = Standard のまま IsChecked = false（後者には Validation.Errors が加わる）、parameter を返すと IsChecked = true に復元、例外を投げると NotImplementedException が UnhandledException となる。" width="880" height="466" loading="lazy">
  <figcaption>グループの誤りが画面の食い違いに変わるまでの経路と、<code>ConvertBack</code> の戻り値による結果の違い。いずれの戻り値でも根本原因である <code>GroupName</code> の欠落は解消されない。<code>parameter</code> を返す実装だけは初期選択の欠落が現れないが、それは読み戻しによって症状が隠れているだけである。各結果は .NET 10 / Windows 11 上で実際に確認した。</figcaption>
</figure>

以下では、本記事が前提としている `Binding.DoNothing` を返す実装の場合を追う。
残る 3 通りの戻り値は「注意点」で個別に扱う。

コンバーターが `Binding.DoNothing` を返す実装であれば、[Binding.DoNothing フィールド](https://learn.microsoft.com/dotnet/api/system.windows.data.binding.donothing)の定義どおり値はソースへ転送されず、`FallbackValue` も既定値も使われない。
ソースへの書き込みが行われないため、書き込み後の読み戻しも起きない。
ターゲット側は解除されたまま残る。
結果として ViewModel の `Quality` は `Standard` のまま保たれ、画面のラジオボタンだけが未選択の状態で取り残される。
これが冒頭で示した食い違いの原因である。

なお、**同じ列挙体プロパティにバインドしたラジオボタン同士では、この `ConvertBack(false)` は通常発生しない**。
選択を切り替えると、まず選択された側の `ConvertBack(true)` でソースが更新され、その変更が `Convert` を通じて他のボタンへ伝わって `false` になる。
グループ機構が解除しようとした時点では既に `false` であり、値が変化しないためソース更新も起きない。
検証環境でも、同一プロパティ内の切り替えでは `ConvertBack` は `true` で 1 回だけ呼ばれ、`false` では一度も呼ばれなかった。
`ConvertBack` が `false` で呼ばれるのは、**同じソースの同じプロパティにバインドしたボタン以外が、同じグループに混ざったとき**である。
別のプロパティにバインドしたボタンのほか、同じ名前のプロパティでもオブジェクトが異なるボタン、そもそもバインドしていないボタンがこれに当たる。
実行して確認したところ、一覧の各行が別の ViewModel の同名プロパティにバインドして全行に同じ `GroupName` を与えた構成でも、バインドしていないラジオボタンを 1 つ混ぜた構成でも `ConvertBack(false)` が発生した。

---

`GroupName` の有無だけを変えて、コンバーターの呼び出しとチェック状態を測った結果が次の図である。

<figure class="article-figure">
  <img src="/images/articles/wpf-radiobutton-enum-binding/radiobutton-grouping.svg" alt="GroupName の有無別に ConvertBack の呼び出し回数とチェック状態を測った表。GroupName の既定値は空文字列。GroupName を設定しない場合は ConvertBack が false で 1 回呼ばれ、チェックが Single だけになる。GroupName を設定すると呼び出しは 0 回で、Standard と Single の両方にチェックが残る。ソース側の値はどちらも Standard / Single のままである。" width="764" height="170" loading="lazy">
  <figcaption>.NET 10 / Windows 11 で、同じ <code>StackPanel</code> の下に 2 組（<code>Quality</code> と <code>Layout</code>）のラジオボタンを置いて測った結果。<code>GroupName</code> 属性の有無だけが 2 行の差である。</figcaption>
</figure>

**`GroupName` を設定しない行では、チェックが `Single` だけになっている。** 別のプロパティにバインドしているにもかかわらず `Standard` が解除されており、これが「初期選択が表示されない」症状の実体である。
このとき `ConvertBack` が `false` で 1 回呼ばれている。

注目すべきは、**ソース側の値はどちらの行も `Standard / Single` のまま無傷である**点である。
コンバーターが `false` に対して `Binding.DoNothing` を返しているため、ViewModel は壊れていない。
壊れているのは画面の表示だけであり、ViewModel をログに出しても原因にたどり着けない。

`GroupName` を設定した行では `ConvertBack` が 1 度も呼ばれず、両方のチェックが残る。

---

## 解決方法

根本原因は UI 側のグループ化にあるため、対処もグループ化で行う。

- **列挙体のプロパティごとに `GroupName` を指定する。**
  これが本質的な解決策である。
  同じ親に置いても、`GroupName` が異なるラジオボタンは別グループとして扱われる。
- **`ConvertBack` は選択解除時に `Binding.DoNothing` を返す。**
  グループを正しく分けていれば解除時の `ConvertBack` は呼ばれない。
  レイアウト変更で再びグループが混ざったときにソースを壊さないための防御になる。
- **`ConverterParameter` には `x:Static` で列挙体の値を渡す。**
  文字列を渡すと比較が成立しない（後述）。

グループ化は論理親の単位で行われるため、列挙体のプロパティごとに親のパネルを分けても症状は解消する。
実行して確認したところ、「問題」の 5 つを `Quality` 用と `PageLayout` 用の 2 つの `StackPanel` に分けるだけで、`GroupName` を書かずに初期選択が両方とも表示された。
ただしこの方法はレイアウトの構造に依存し、後の変更でパネルをまとめ直すと再発する。
`GroupBox` の `Header` と `Content`、`Grid` の別セルのように、画面上は離れていても論理親が同じになる配置では、グループが分かれない（「注意点」参照）。
グループの境界を意図として明示できる `GroupName` の指定を推奨する。

---

## 実装例

コンバーターは、`Convert` で列挙体の値とパラメーターの一致を `bool` に変換し、`ConvertBack` では選択された場合にのみパラメーターを返す。
`value` にはボックス化された `bool`、または `null` が渡る。
`value is true` のパターンマッチは、`null` とボックス化された `false` をまとめて弾く。

```csharp
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.Equals(parameter) == true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? parameter : Binding.DoNothing;
}
```

選択された側のバインディングで `ConvertBack` が `parameter`（対象の列挙体の値）を返し、ViewModel のプロパティが更新される。
解除された側で `ConvertBack` が呼ばれた場合は `Binding.DoNothing` を返すため、ソースは更新されない。
グループを正しく分けていればこの呼び出しは起きないが、戻り値を誤らないことが後述の落とし穴を防ぐ。

XAML では、列挙体のプロパティごとに異なる `GroupName` を与える。
「問題」で示した XAML との差分は `GroupName` 属性の追加だけである。

```xml
<StackPanel>
    <StackPanel.Resources>
        <local:EnumToBooleanConverter x:Key="EnumToBoolean" />
    </StackPanel.Resources>

    <RadioButton Content="Draft" GroupName="quality"
                 IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Draft}}" />
    <RadioButton Content="Standard" GroupName="quality"
                 IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Standard}}" />
    <RadioButton Content="Fine" GroupName="quality"
                 IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Fine}}" />

    <RadioButton Content="Single" GroupName="pageLayout"
                 IsChecked="{Binding PageLayout, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:PageLayout.Single}}" />
    <RadioButton Content="Dual" GroupName="pageLayout"
                 IsChecked="{Binding PageLayout, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:PageLayout.Dual}}" />
</StackPanel>
```

この状態で起動すると、`Standard` と `Single` の両方が初期選択として表示される。
`Draft` を選ぶと `Quality` だけが `Draft` へ変わり、`PageLayout` の選択は保たれる。

---

## 注意点

- **`GroupName` は親要素をまたいでグループ化する。**
  `GroupName` を設定したラジオボタンは、別々の `Border` や別のパネルに分かれていても同一グループになる。
  実測では、異なる親に配置した `GroupName="quality"` の 2 組が互いを解除した。
  そのため、1 つのグループに与える名前を、同じビジュアルツリーのルート内にある他のグループの名前と重複させないようにする（同じグループに属するボタンには同じ名前を与える）。
  命名規則をアプリケーション全体で統一する場合も、同じ列挙体の選択群が 1 つの画面に 2 セット出ないことが前提となる。
  なお、グループ化はビジュアルツリーのルートをまたがない。
  通常はウィンドウがルートになるが、`Popup` や `ContextMenu` の内側は別のルートになるため、この境界は同一ウィンドウ内にも生じる。
- **`ConverterParameter` を文字列で書かない。**
  `ConverterParameter=Draft` と書くと、`ConverterParameter` が `object` 型で変換先の型が確定しないため、値は文字列 `"Draft"` のまま渡る。
  `Convert` の比較が常に `false` となり、どのボタンも選択表示されない。
  一方でクリック時の `ConvertBack` は文字列を返し、WPF の既定の型変換で列挙体へ変換されるため、ソースの更新だけは成功する。
  実測では、ViewModel の値だけが変わり画面はどれも未選択のまま、という状態になった。
  `x:Static` で列挙体の値を渡すか、`Convert` 側で `Enum.Parse` して受けること。
- **`ConverterParameter` にはバインドできない。**
  `Binding` は `BindingBase` を経て `MarkupExtension` を継承しており、`DependencyObject` ではない。
  したがって `ConverterParameter` は依存関係プロパティではなく、`{Binding ...}` を入れ子にして動的な値を渡すことはできない。
  項目ごとに値を変えたい場合はコンバーター方式では対応できず、添付ビヘイビアや選択コントロールへの置き換えが必要になる。
- **`ConvertBack` を `NotImplementedException` のまま放置しない。**
  双方向バインディングでは `ConvertBack` が呼ばれ、データバインディングエンジンはコンバーターが投げた例外を捕捉しない。
  グループが混在した状態では解除時に `ConvertBack(false)` が発生するため、実測でもそのまま `NotImplementedException` でアプリケーションが停止した。
- **解除時に `DependencyProperty.UnsetValue` を返さない。**
  公式ドキュメントは、想定内の問題を `DependencyProperty.UnsetValue` の返却で扱うよう記し、その場合は `FallbackValue` があればそれが、なければ既定値が使われると述べている（[IValueConverter.ConvertBack メソッド](https://learn.microsoft.com/dotnet/api/system.windows.data.ivalueconverter.convertback)）。
  しかし検証環境では、`ConvertBack` からこの値を返してもソースは更新されず、`FallbackValue` も適用されないまま、`Value 'False' could not be converted.`（日本語環境では「値 'False' を変換できませんでした。」）という検証エラーがバインディングに設定され、`Validation.Errors` に残った。
  `Binding.DoNothing` を返した場合は、同じ条件でも検証エラーは発生しない。
  値を転送しないという意図だけを表すのは `Binding.DoNothing` である。
  検証エラーの読み方は[WPF バインディングエラーの読み方と出力ウィンドウを使った原因特定](/ja/articles/wpf-binding-error-debugging-output-window/)で扱っている。
- **解除時に `parameter` を返す実装は、グループの誤りを隠す。**
  `false` のときも `parameter` を返すと、解除された側のソースが同じ値で更新され、直後の読み戻しでターゲットが選択状態へ復元される。
  この復元はグループ機構による解除より後に効くため、.NET 10 で実行したところ、1 つのグループにまとめられているにもかかわらず `Quality` 側と `PageLayout` 側の両方が選択された状態で表示された。
  画面は意図どおりに見え、本記事が扱う「初期選択が表示されない」症状は現れない。
  ただし相互排他が読み戻しに打ち消されているだけであり、`GroupName` の欠落は残る。
  症状が現れないため欠落に気付きにくく、選択を切り替えるたびにソースへの書き込みと読み戻しが余分に発生する。
  解除時は `Binding.DoNothing` を返し、グループ化そのものを正すこと。
- **ラッパープロパティ方式でも `GroupName` は必要である。**
  列挙体の値ごとに `bool` プロパティを用意する方式でも、グループ化は UI 側の仕組みであるため、`GroupName` を省けば同じように別の列挙体のボタンが解除される。
  .NET 10 で実行したところ、解除された側のセッターが `false` で呼ばれて無視され、直後にソースが読み直されて選択が復元された。
  `parameter` を返すコンバーターと同じく画面は意図どおりに見えるが、無駄な往復が起きており、`GroupName` の欠落も残ったままである。
- **暗黙のグループ化の単位は論理ツリー上の親であり、画面上の区画でもビジュアルツリー上の親でもない。**
  `Grid` の別々のセルに置いたラジオボタンは、セルの指定が `Grid.Row` / `Grid.Column` という添付プロパティで行われ要素の階層を作らないため、論理親がいずれも同じ `Grid` になる。
  実際に動かすと 1 つのグループになった。
  判定が論理ツリーで行われるため、`GroupBox` の `Header` と `Content` のようにビジュアルツリー上は別々の `ContentPresenter` に属する配置でも、論理親が同じ `GroupBox` であれば 1 グループになる（検証環境で確認）。
  `ItemsControl` でも、`Items` にラジオボタンを直接並べた場合は論理親が `ItemsControl` 自身になり 1 グループになる。
  逆に、列挙体のプロパティごとに親のパネルを分けると論理親が別になるため、グループも分かれる。
- **テンプレートや単一子要素の内側では、論理親が変わる。**
  `ItemTemplate` を使う場合、テンプレートのルートに直接置いたラジオボタンは論理親を持たず、暗黙のグループ化が働かない。
  テンプレート内のパネルに並べた場合は論理親が項目ごとのパネルになるため、行内では 1 グループ、行どうしは別グループとなる。
  `Border` のように子を 1 つだけ持つ要素で個別に包んだ場合も論理親は別になり、グループ機構による相互排他は失われる。
  コンバーター方式では選択の排他がバインディング経由で成立するため表示は保たれるが、排他の根拠がグループから外れる。
  バインドしていないラジオボタンが混ざると排他が失われるため、選択群の境界は `GroupName` で表す。
- **項目ごとに独立した選択が必要な繰り返し表示では、`GroupName` を項目ごとに一意にする。**
  一覧の各行にラジオボタン群を出す構成で全行に同じ `GroupName` を与えると、`GroupName` が親をまたぐため全行が 1 グループに統合され、一覧全体で 1 つしか選択できなくなる。
  `GroupName` は `ConverterParameter` と異なり依存関係プロパティであるため、行の識別子をバインドして項目ごとに一意な名前を与えられる。

---

## 代替案・比較

| 方式 | メリット | デメリット | 適するケース |
|---|---|---|---|
| コンバーター + `ConverterParameter` | ViewModel に追加のプロパティが不要・選択肢が増えても XAML の追加だけで済む | `ConvertBack` の戻り値を誤ると挙動が壊れる・`ConverterParameter` をバインドできない | 選択肢が XAML に固定で並ぶ一般的な設定画面 |
| 列挙体の値ごとのラッパープロパティ | コンバーターが不要で XAML が単純・`ConvertBack` の考慮が要らない | 選択肢の数だけプロパティと変更通知が増える・列挙体の値を追加するたび ViewModel を直す | 選択肢が 2〜3 個で固定され、ViewModel を単純に保ちたい |
| 添付ビヘイビア（添付プロパティに列挙体の値を持たせる） | XAML から列挙体を直接指定でき、`ConvertBack` の落とし穴が無い・項目ごとに異なる値をバインドできる | 添付プロパティとイベント購読の実装が必要 | 同じパターンをアプリケーション全体で多用する |
| `ListBox` などの選択コントロールへ置き換え | `SelectedItem` / `SelectedValue` で完結し、グループ化の問題自体が起きない | ラジオボタンの見た目が要る場合は `ItemContainerStyle` の指定が必要 | 選択肢が動的、または数が多い |

ラッパープロパティ方式では、「問題」で示した `PrintSettingsViewModel` の `Quality` プロパティを次の形に置き換え、列挙体の値ごとに `bool` プロパティを追加する。
`_quality` フィールドと `PropertyChanged` の宣言は同じクラスのものをそのまま使う。
ラッパーのセッターは `true` のときだけ列挙体プロパティを更新し、解除時の `false` は無視する。
発火する通知が増えるため、`PropertyChanged?.Invoke` は `Raise` ヘルパーにまとめている。

```csharp
public Quality Quality
{
    get => _quality;
    set
    {
        if (_quality == value) return;
        _quality = value;
        Raise(nameof(Quality));
        Raise(nameof(IsDraft));
        Raise(nameof(IsStandard));
        Raise(nameof(IsFine));
    }
}

public bool IsDraft
{
    get => Quality == Quality.Draft;
    set { if (value) Quality = Quality.Draft; }
}

private void Raise(string propertyName)
    => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
```

`IsStandard` と `IsFine` も `IsDraft` と同じ形で定義する。
プロパティ名と列挙体名がどちらも `Quality` であるため、`Quality.Draft` は型のメンバーとして解決される。
発火漏れがあると、選択を切り替えても他のボタンの表示が古いまま残る。
列挙体の値を増やすたびにプロパティと発火の記述が増えるため、選択肢が少ない場合に向く。

添付ビヘイビア方式は、`RadioButton` に添付プロパティで対象の列挙体の値を持たせ、`Checked` イベントの発生時に ViewModel のプロパティへ書き戻す。
添付プロパティは依存関係プロパティであるため、`ConverterParameter` と異なり項目ごとに異なる値をバインドできる。

選択肢が動的に決まる場合は、`ListBox` の `ItemsSource` に列挙体の値を渡し、`SelectedItem` を ViewModel にバインドする方式が扱いやすい。
選択値の取得方法の使い分けは、[WPF ComboBox の ItemsSource バインドパターンと選択値の取得方法](/ja/articles/wpf-combobox-itemssource-patterns/)で整理している。

---

## まとめ

ラジオボタンの初期選択が画面に出ないのは、バインディングやコンバーターの不備ではなく、`GroupName` を省いたことで別々の列挙体のラジオボタンが 1 つのグループに統合されたことが原因である。
選択基準は次のとおりである。

- **列挙体を選ばせるラジオボタンには、プロパティごとに `GroupName` を指定する。**
  これを省くと、グループの分離をレイアウトの構造に委ねることになり、パネルをまとめ直した時点で画面と ViewModel が食い違う。
  1 つのグループに与える名前は、同じビジュアルツリーのルート内にある他のグループの名前と重複させない。
- **選択肢が XAML に固定で並ぶ場合:**
  コンバーター方式を選ぶ。
  `ConvertBack` は選択解除時に `Binding.DoNothing` を返し、`ConverterParameter` は `x:Static` で列挙体の値を渡す。
- **選択肢が 2〜3 個で ViewModel を単純に保ちたい場合:**
  ラッパープロパティ方式を選ぶ。
  関連プロパティの変更通知を漏らさないこと。
- **選択肢が動的、または項目ごとに異なる値を渡したい場合:**
  `ConverterParameter` がバインドできないため、添付ビヘイビアか選択コントロールへの置き換えを選ぶ。

双方向バインディングがソースを更新するタイミングは `UpdateSourceTrigger` で決まり、`IsChecked` の既定は `PropertyChanged` である。
この既定値の違いが入力の反映タイミングにどう影響するかは、[WPF TextBox の UpdateSourceTrigger で入力がソースへ反映されるタイミングを制御する](/ja/articles/wpf-textbox-updatesourcetrigger-binding-timing/)で扱っている。

---

<!-- 関連記事 -->
- [WPF ComboBox の ItemsSource バインドパターンと選択値の取得方法](/ja/articles/wpf-combobox-itemssource-patterns/)
- [WPF TextBox の UpdateSourceTrigger で入力がソースへ反映されるタイミングを制御する](/ja/articles/wpf-textbox-updatesourcetrigger-binding-timing/)
- [WPF バインディングエラーの読み方と出力ウィンドウを使った原因特定](/ja/articles/wpf-binding-error-debugging-output-window/)

---
layout: article-ja
title: "WPF で Style の Trigger・DataTrigger が効かない原因と依存関係プロパティの値優先順位"
date: 2026-08-03
category: WPF
excerpt: "Style のトリガーを書いても外観が変わらない原因の多くは、XAML の属性で直接指定したローカル値がスタイルトリガーより優先されることにある。値優先順位の仕組みと、Setter への移行・SetCurrentValue・ClearValue による解決方法を整理する。"
image: /images/articles/wpf-style-trigger-not-working-local-value/style-trigger-local-value.png
---

## 概要

WPF で `Style.Triggers` に定義した `Trigger` や `DataTrigger` が、条件を満たしているにもかかわらず外観へ反映されないことがある。
バインディングの誤りやトリガー条件の型不一致を疑いがちだが、トリガーは正しく作動しており、より優先順位の高い値に上書きされているだけ、というのがよくある原因である。
本記事では、依存関係プロパティの値優先順位を軸にこの現象の原因を説明し、ローカル値を持つコードの直し方と、状況別の選択基準を整理する。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF（値優先順位の規則は .NET Framework 4.x の WPF でも同じ）
- 言語: C#
- 対象機能: `Style.Triggers` に置く `Trigger` / `DataTrigger` / `MultiTrigger`
- 既定テーマ: Aero2（.NET 9 以降で選択できる Fluent テーマでは、後述する標準コントロールの色や既定テンプレートの構造が異なる）
- アーキテクチャ: MVVM・コードビハインドのいずれにも適用可能

---

## 問題

入力の検証状態に応じて枠の背景色を変えるため、`Style` に `DataTrigger` を定義したとする。

```xml
<Window.Resources>
    <Style x:Key="StatusBox" TargetType="Border">
        <Style.Triggers>
            <DataTrigger Binding="{Binding HasError}" Value="True">
                <Setter Property="Background" Value="#FFD4D4" />
            </DataTrigger>
        </Style.Triggers>
    </Style>
</Window.Resources>

<Border Style="{StaticResource StatusBox}" Background="White">
    <TextBlock Text="HasError = True" />
</Border>
```

`HasError` が `true` になっても、この `Border` の背景は `White` のまま変わらない。
バインディングは正しく解決されており、出力ウィンドウにバインディングエラーも現れない（バインディングエラーの読み方は [WPF バインディングエラーの読み方と出力ウィンドウを使った原因特定](/ja/articles/wpf-binding-error-debugging-output-window/) を参照）。
同じことは `Trigger Property="IsMouseOver"` のようなプロパティトリガーでも起きるため、原因はトリガーの種類ではない。

---

## 原因・背景

WPF の依存関係プロパティは、ローカル値・スタイル・テンプレート・継承など複数の入力元から値を与えられる。
どれを実効値とするかは**依存関係プロパティの値優先順位**で決まり、上位の入力元があれば下位の値は無視される。

優先順位は次のとおりである（上ほど優先度が高い）。

| 順位 | 値の出どころ | 具体例 |
| --- | --- | --- |
| 1 | プロパティシステムの強制（coercion） | `CoerceValueCallback` |
| 2 | 実行中のアニメーション、`Hold` 動作のアニメーション | `Storyboard` |
| 3 | **ローカル値** | XAML の属性・プロパティ要素、`SetValue`、要素に直接書いた `Binding` / `StaticResource` / `DynamicResource` |
| 4 | `TemplatedParent` のテンプレート由来の値 | `ControlTemplate` / `DataTemplate` が生成した要素 |
| 5 | 暗黙スタイル | `Style` プロパティにのみ適用される |
| 6 | **スタイルのトリガー** | `Style.Triggers` |
| 7 | テンプレートのトリガー | `ControlTemplate.Triggers` / `DataTemplate.Triggers` |
| 8 | スタイルの Setter | `Style` 直下の `Setter` |
| 9 | 既定（テーマ）スタイル | テーマスタイルのトリガー、次いで Setter |
| 10 | 継承 | `FontSize` などの継承プロパティ |
| 11 | 依存関係プロパティのメタデータ既定値 | `PropertyMetadata` の既定値 |

問題の本質はこの表の 3 と 6 の位置関係にある。
`Background="White"` のように XAML の属性で直接書いた値はローカル値（3）となり、スタイルのトリガー（6）より上位に位置する。
そのためトリガーの条件が成立して `Setter` が評価されても、実効値はローカル値のままとなり、画面上は何も変化しない。

見落としやすいのは、**要素に直接書いた `Binding` や `DynamicResource` もローカル値と同じ優先順位で扱われる**点である。
`Background="{Binding NormalBrush}"` と書いた場合も、値が遅延評価されるだけで優先順位はローカル値であり、スタイルのトリガーは勝てない。

一方、`Style` 直下の `Setter`（8）はトリガー（6）より下位である。
したがって既定値をローカル値ではなく `Setter` として与えれば、条件成立時にトリガーが優先されるようになる。

---

## 解決方法

対象要素からローカル値を取り除き、既定値を `Style` の `Setter` へ移す。
これで「既定値は Setter（8）、条件成立時はトリガー（6）」という上下関係が成立し、トリガーが期待どおり反映される。

トリガーが設定するプロパティと同じプロパティだけが対象である。
`Margin` や `Width` など、トリガーが触らないプロパティを要素側にローカル値として書くことは問題にならない。

---

## 実装例

次の XAML は、ローカル値を残した `Border` と、既定値を `Setter` へ移した `Border` を同じスタイルで並べたものである。
どちらも同一の `StatusBox` スタイルを参照しており、動作に関係する違いは `Background` をローカル値として持つかどうかだけである（下段の `Margin` は 2 つを縦に離して配置するためのもので、トリガーの挙動には関係しない）。

```xml
<Window.Resources>
    <Style x:Key="StatusBox" TargetType="Border">
        <Setter Property="Background" Value="White" />
        <Setter Property="BorderBrush" Value="#9AA4B2" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Padding" Value="18,6" />
        <Style.Triggers>
            <DataTrigger Binding="{Binding HasError}" Value="True">
                <Setter Property="Background" Value="#FFD4D4" />
            </DataTrigger>
        </Style.Triggers>
    </Style>
</Window.Resources>

<StackPanel>
    <!-- ローカル値が残っているため、トリガーの背景色が反映されない -->
    <Border Style="{StaticResource StatusBox}" Background="White">
        <TextBlock Text="HasError = True" />
    </Border>

    <!-- 既定値を Setter に移したため、トリガーの背景色が反映される -->
    <Border Style="{StaticResource StatusBox}" Margin="0,12,0,0">
        <TextBlock Text="HasError = True" />
    </Border>
</StackPanel>
```

トリガーの条件に使う `HasError` は、`DataContext` に設定した ViewModel のプロパティである。
実行中の変更をトリガーへ伝えるため、`INotifyPropertyChanged` を実装する。

```csharp
public sealed class ValidationViewModel : INotifyPropertyChanged
{
    private bool _hasError;

    public bool HasError
    {
        get => _hasError;
        set
        {
            if (_hasError == value)
            {
                return;
            }

            _hasError = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasError)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

この ViewModel を `Window` の `DataContext` に設定すると、`HasError` の変更が `DataTrigger` へ伝わる。
変更通知を実装しない単純なプロパティにすると、値を変えてもトリガーは再評価されない。

`HasError` を `true` にした状態で表示すると、両者の差がそのまま描画に現れる。

<figure class="article-figure">
  <img src="/images/articles/wpf-style-trigger-not-working-local-value/style-trigger-local-value.png" alt="同じスタイルを適用した 2 つの Border。Background をローカル値として指定した上段は白のまま、Setter に任せた下段は DataTrigger の淡い赤に変わっている。" width="415" height="139" loading="lazy">
  <figcaption><code>HasError</code> が <code>True</code> の状態で表示した結果。上段は <code>Background</code> をローカル値として持つためトリガーの色に変わらず、下段は既定値を <code>Setter</code> に移したためトリガーの色が反映されている。左側のラベルは 2 つの <code>Border</code> の記述の違いを示すために図へ付加したものである（.NET 10 / Windows 11 で生成）。</figcaption>
</figure>

コードビハインドから値を操作する場合も、代入方法によって優先順位が変わる。
次の 3 行は、いずれも `Background` を扱うが、値の格納先が異なる。

```csharp
// ローカル値になるため、以後この要素ではスタイルのトリガーが Background に効かなくなる
border.Background = Brushes.White;

// ローカル値を書き込まずに実効値だけを変える（トリガーが作動すればトリガーの値になる）
border.SetCurrentValue(Border.BackgroundProperty, Brushes.White);

// 既に設定されているローカル値を取り除き、Setter やトリガーの値へ戻す
border.ClearValue(Border.BackgroundProperty);
```

`SetCurrentValue` は優先順位の一覧に現れない特別な代入で、値の出どころを上書きせずに現在の値だけを変更する。
既存のバインディングやトリガーを壊さずに一時的な値を入れたい場合に適する。
ただしローカル値を作らないだけであり、既に設定されているローカル値を取り除く効果は無い。
対象プロパティにローカル値が残っている状態では実効値は変わらないため、先に `ClearValue` で取り除く必要がある。
`ClearValue` はローカル値のみを取り除くため、テーマスタイルなど他の入力元が残っていればその値が実効値となる。

---

## 注意点

- **`ClearValue` はバインディングや `DynamicResource` も解除する。**
リテラルのローカル値が無く、バインディングだけが設定されているプロパティに対して `ClearValue` を呼ぶと、そのバインディング自体が失われる。
既定値をバインディングで与えたい場合は、要素側ではなく `Setter` の `Value` に `Binding` を書く。
トリガーの条件をバインディングで指定する場合は、`DataTrigger` の `Binding`（`BindingBase` 型）を使う。
`Binding` を書けるのはこの条件のバインディングと値側の `Setter.Value` であり、比較値である `Trigger` / `DataTrigger` の `Value` には書けない。
- **ローカル値を代入するとバインディングが置き換わる。**
バインディングを設定したプロパティへ通常の代入を行うと、遅延評価されていた値ではなく代入したローカル値に完全に差し替わる。
その後に `ClearValue` を呼んでもバインディングは復元されない。
- **テーマスタイル（およびその `ControlTemplate`）のトリガーもローカル値に負ける。**
例として `Button` の `Foreground` をローカル値で指定すると、無効化時に文字をグレー表示にするトリガーが効かなくなる。
このトリガーは既定テーマの実装によって 7（テンプレートのトリガー）と 9（テーマスタイル）のどちらに置かれることもあるが、いずれもローカル値（3）より下位である点は変わらない。
標準コントロールの状態表現を壊していないか確認する。
- **ローカル値を除いてもトリガーが見た目に出ない場合は `ControlTemplate` を疑う。**
標準コントロールの既定テンプレートは、マウスオーバー時の背景などをテンプレート内に固定していることがある。
この場合はプロパティの値としてはトリガーが勝っていても描画に現れないため、テンプレートの差し替えが必要になる。
- **`ItemContainerStyle` の `Setter` に書いた片方向バインドやリテラル値も、コンテナへのローカル値に負ける。**
`ItemsControl` 系のコントロールでコンテナの状態をスタイル経由で与えている場合、コードから同じプロパティへ代入するとローカル値になり、以後スタイル側の値は反映されなくなる。
ただし `Setter` の `Binding` が `Mode=TwoWay` の場合はバインドが維持され、代入した値はソースへ書き戻される。
`TreeViewItem` の `IsSelected` / `IsExpanded` を例にした具体的な影響は [WPF TreeView で任意のノードをコードから選択・展開する方法と SelectedItem が読み取り専用である理由](/ja/articles/wpf-treeview-select-item-programmatically/) で扱っている。
- **`Style` プロパティ自体には同じ優先順位が適用されない。**
要素に直接書いた `Style` は明示スタイルとしてローカル値相当（3）、型に一致するリソースから適用される暗黙スタイルは 5 として扱われる。
どちらも無い場合は、既定（テーマ）スタイルが 9 相当で適用される。
明示スタイルを書いた要素に暗黙スタイルは適用されない。
- **リソースの評価タイミングとは別問題である。**
`StaticResource` を実行時に差し替えても反映されない現象は、優先順位ではなく評価タイミングに起因する（[WPF で StaticResource を変更しても画面が更新されない原因と解決方法](/ja/articles/wpf-staticresource-vs-dynamicresource/)）。
トリガーが効かない問題と混同しない。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| 既定値を `Style` の `Setter` へ移す | XAML だけで完結し、優先順位の逆転が起きない | 要素ごとに異なる既定値を持たせにくい | 通常はこれを第一候補とする |
| `SetCurrentValue` で値を変更する | ローカル値を作らないためトリガーが生き続ける | コードビハインドが必要 | 実行時に一時的な値を入れる場合 |
| `ClearValue` でローカル値を除去する | 既存の XAML を書き換えずに済む | バインディングも解除される。呼び出し時機の管理が要る | 実行時に付いたローカル値を取り除く場合 |
| `ControlTemplate` を差し替える | テンプレートが固定している外観まで制御できる | 記述量が多く、テーマの更新に追随しない | 既定テンプレートが外観を固定している場合 |
| トリガーの `EnterActions` でアニメーションを開始する | 優先順位 2 のためローカル値があっても上書きできる | 停止・巻き戻しの管理が必要になる。`{StaticResource}` などで共有した Freeze されていないブラシの `Color` をアニメーションすると、同じブラシを参照する他の要素にも影響する | 状態変化に遷移演出を伴わせる場合 |

---

## まとめ

`Style` のトリガーが効かない原因としてよくあるのは、トリガーの記述ミスではなく、対象プロパティにローカル値が設定されていることである。
ローカル値は優先順位 3、スタイルのトリガーは 6、スタイルの `Setter` は 8 であり、この順序を把握していれば現象は説明できる。

選択の基準は次のとおりである。

- **XAML で既定値を与えている場合:**
要素の属性からその指定を外し、`Style` の `Setter` へ移す。
もっとも副作用が少なく、第一に検討すべき方法である。
- **コードビハインドで実行時に値を変える場合:**
通常の代入ではなく `SetCurrentValue` を使う。
値の出どころを上書きしないため、後からトリガーが作動しても正しく反映される。
既にローカル値があるプロパティには効かないため、その場合は先に `ClearValue` で取り除く。
- **既にローカル値が設定されてしまっている場合:**
`ClearValue` で取り除く。
ただしバインディングごと解除される点を踏まえ、既定値が必要なら `Setter` 側で与える。

まず対象プロパティにローカル値が無いことを確認し、既定値はすべて `Setter` に置く設計にしておくことで、トリガーが効かない問題は設計段階で回避できる。

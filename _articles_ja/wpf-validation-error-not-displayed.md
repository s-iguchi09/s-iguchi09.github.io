---
layout: article-ja
title: "WPF で入力検証のエラーが表示されない原因と IDataErrorInfo / INotifyDataErrorInfo の使い分け"
date: 2026-08-13
category: WPF
excerpt: "検証コードは動いているのにエラーが画面に出ない。ValidatesOnDataErrors の既定値、AdornerLayer の不在、ソース更新のタイミングといった原因を .NET 10 の実測で切り分け、検証方式を比較する。"
image: /images/articles/wpf-validation-error-not-displayed/validation-error-display.png
---

## 概要

必須チェックを書いたのに `TextBox` が赤くならない。
症状はさらに 2 つに分かれる。
検証コードにブレークポイントを置いても停止しない場合と、検証は動いていて `Validation.GetHasError` が `true` を返しているのに画面が変化しない場合である。

いずれも検証ロジックの誤りではなく、WPF の入力検証が「エラーを発生させる経路」「エラーを保持する場所」「エラーを描画する場所」の 3 つに分かれていることに起因する。
どれか 1 つが欠けても、残りは正常に動いたまま画面だけが無反応になる。

本記事では、エラーが表示されない原因をこの 3 段階に分解して切り分け、`ValidationRule` / `IDataErrorInfo` / `INotifyDataErrorInfo` および例外・型変換による検証の使い分けを整理する。
3 段階がすべて成立していても、既定の `ErrorTemplate` の仕様によりメッセージだけが出ないという症状も併せて扱う。
記載した挙動・既定値は、いずれも .NET 10 / Windows 11 で実際に動かして確認した結果である。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF（挙動の確認環境は .NET 10 / Windows 11 の日本語環境）
- 言語: C# 12 以降 / XAML（コード例はコレクション式を使う。`net6.0` など既定の言語バージョンが C# 11 以前になる対象では、`[]` を `new()` に、スプレッド要素の `[.. messages]` を `new List<string>(messages)` に読み替える）
- 対象機能: `Binding` の検証（`Validation` 添付プロパティ、`ValidationRule` 派生クラス、`IDataErrorInfo`、`INotifyDataErrorInfo`、`ValidatesOnExceptions`）
- アーキテクチャ: MVVM（ViewModel が検証結果を保持する構成）
- その他制約: 既定の `ErrorTemplate` を使う構成を基準とする。カスタムテンプレートは実装例で扱う

---

## 問題

同じ「名前が未入力」という状態に対して、バインディングの書き方と ViewModel が実装するインターフェイスだけを変えた 3 つの `TextBox` を縦に並べる。
検証ロジックはいずれも「空文字なら必須エラーを 1 件返す」で同一である。

```xml
<StackPanel>
    <!-- IDataErrorInfo を実装した ViewModel をバインドする -->
    <TextBox x:Name="Plain"
             Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />

    <!-- 同じ ViewModel に ValidatesOnDataErrors を付けてバインドする -->
    <TextBox x:Name="WithFlag"
             Text="{Binding Name, UpdateSourceTrigger=PropertyChanged,
                    ValidatesOnDataErrors=True}" />

    <!-- INotifyDataErrorInfo を実装した ViewModel をバインドする -->
    <TextBox x:Name="Notify"
             Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />
</StackPanel>
```

比較のため、`DataContext` は `TextBox` ごとに個別に割り当てる。

```csharp
Plain.DataContext = new DataErrorAccount();     // IDataErrorInfo を実装
WithFlag.DataContext = new DataErrorAccount();  // 同上
Notify.DataContext = new NotifyErrorAccount();  // INotifyDataErrorInfo を実装
```

`DataErrorAccount` と `NotifyErrorAccount` は、同じ必須チェックをそれぞれ `IDataErrorInfo` と `INotifyDataErrorInfo` で返すだけの ViewModel である（定義は割愛する）。
この 3 つを同時に表示すると、既定のエラー表示（赤枠）が出るのは下 2 つだけである。

<figure class="article-figure">
  <img src="/images/articles/wpf-validation-error-not-displayed/validation-error-display.png" alt="3 つの TextBox を縦に並べた画面。IDataErrorInfo に ValidatesOnDataErrors を付けていない一番上の TextBox だけ枠が通常色のままで、ValidatesOnDataErrors=True を付けたものと INotifyDataErrorInfo を実装したものは赤い枠で囲まれている。" width="474" height="224" loading="lazy">
  <figcaption>同じ「名前が未入力」という状態に対する既定のエラー表示。バインディングの書き方と ViewModel が実装するインターフェイスだけが異なる。各 <code>TextBox</code> の上のラベルは、対応するバインディングを示すために図へ追加したものである（.NET 10 / Windows 11 で生成）。</figcaption>
</figure>

一番上の `TextBox` では `Validation.GetHasError` が `false` のままであり、`Validation.Errors` は空である。
`IDataErrorInfo` のインデクサーにブレークポイントを置いても停止しない。

赤枠が出た 2 つにも問題が残る。
`Validation.Errors` にはメッセージが入っているのに、画面にはメッセージが一切出ない。

さらに、上記のいずれの構成でも枠すら出なくなる場合がある。
`Window` の `ControlTemplate` を差し替えたアプリケーションで、`Validation.GetHasError` は `true`、`Validation.Errors` にも要素があるにもかかわらず、画面が完全に無反応になるという症状である。

---

## 3 つの段階と、止まる場所

WPF の入力検証は、次の 3 段階が独立して成立して初めて画面に現れる。

1. **発生** — バインディングに関連付けられた検証ルールが実行され、`ValidationError` が作られる。
2. **保持** — `ValidationError` がバインディングのターゲット要素の `Validation.Errors` へ追加され、`Validation.HasError` が `true` になる。
3. **描画** — `Validation.ErrorTemplate` が、その要素のアドーナーレイヤー上に描かれる。

段階 2 はバインディングエンジンが自動的に行うため、アプリケーション側の記述で欠けるのは段階 1 と段階 3 である。
以下では、症状ごとにどちらで止まっているかを切り分ける。

### 段階 1: バインディングにルールが関連付いていない

段階 1 で止まるケースである。
`IDataErrorInfo` は、実装しただけでは検証に参加しない。
バインディングに `DataErrorValidationRule` が追加されて初めて `this[string columnName]` が呼ばれる。
このルールを追加する簡易記法が `ValidatesOnDataErrors` であり、その既定値は `false` である。

方式ごとの有効化条件と、実測した既定の挙動は次のとおりである。

| 検証方式 | エラーに記録されるルール | 有効化に必要な指定 | 既定 | 初期表示時点でエラーが現れるか |
| --- | --- | --- | --- | --- |
| 自作 `ValidationRule` | 自作クラス | `Binding.ValidationRules` へ追加 | 追加しなければ動かない | されない（既定。`ValidatesOnTargetUpdated="True"` で可） |
| `IDataErrorInfo` | `DataErrorValidationRule` | `ValidatesOnDataErrors="True"` | `False` | される |
| `INotifyDataErrorInfo` | `NotifyDataErrorValidationRule` | `ValidatesOnNotifyDataErrors`（既定で有効） | `True` | される |
| 型変換の失敗 | WPF 内部の変換用ルール | 指定不要 | 常に有効 | — |
| setter が投げた例外 | `ExceptionValidationRule` | `ValidatesOnExceptions="True"` | `False` | — |

`ValidatesOnNotifyDataErrors` だけが既定で `true` である。
`INotifyDataErrorInfo` を実装した ViewModel が、バインディングに何も書かなくても赤枠を出すのはこのためである。

`INotifyDataErrorInfo` は、エラーの作られ方も他の方式と異なる。
`NotifyDataErrorValidationRule.Validate` は入力値によらず常に有効を返す。
実測でも、`null` や空文字を渡した場合を含めて `IsValid` は `true` であった。
エラーの実体は ViewModel の `GetErrors` が返す内容であり、バインディングエンジンがそれを読み取り、`ErrorsChanged` の通知に追随して `Validation.Errors` を更新する。
このルールは、そうして記録された `ValidationError` の `RuleInError` に現れる標識として働く。

この読み取りには順序がある。
バインディングエンジンはまず `HasErrors` を参照し、`true` のときにだけ `GetErrors` を呼ぶ。
実測では、`GetErrors` がメッセージを返す実装であっても、`HasErrors` が `false` を返す場合は `GetErrors` が一度も呼ばれず、エラーは表示されなかった。
`HasErrors` が `false` のときは既存の通知エラーが解除され、`ErrorsChanged` が発生するたびにこの判定が繰り返される。
`HasErrors` をプロパティごとの検証結果と切り離して実装すると、この不一致だけで「検証結果は保持しているのに表示されない」状態になる。

初期表示時点の挙動には方式差がある。
`DataErrorValidationRule` と `NotifyDataErrorValidationRule` は、ユーザーが 1 文字も入力していない段階でエラーを報告した。
一方、自作の `ValidationRule` は、バインディングを張った直後には呼ばれず、`Validate` が実行されるのは最初のソース更新のときであった。
必須項目を空のまま起動しても自作ルールが反応しないのは、この差による。

差を生むのは `ValidationRule.ValidatesOnTargetUpdated` である。
このプロパティは、ターゲット側が更新されたとき、すなわちバインディングの確立時やソース側の値が変化したときにもルールを実行するかどうかを決める。
実測では、組み込みの `DataErrorValidationRule` と `NotifyDataErrorValidationRule` はいずれも `true` を返し、自作の `ValidationRule` の既定は `false` であった。
自作ルールにも起動時から検証させるには、`ValidatesOnTargetUpdated="True"` を指定する。
指定した自作ルールは、実測でも起動直後に `Validate` が呼ばれ、以後はソース側の値が変わるたびに評価された。

この段階を成立させるには、使う方式に応じた有効化を行う。
`IDataErrorInfo` を使う構成では、バインディングに `ValidatesOnDataErrors="True"` を指定する。
`INotifyDataErrorInfo` は既定で有効であり、1 プロパティに複数のメッセージを持たせられ、非同期に完了する検証（サーバー照会など）もあとから反映できる。複数のメッセージや非同期の検証が要るならこちらを選ぶ。

### 段階 3: アドーナーレイヤーが無い

段階 3 で止まるケースである。
`Validation.ErrorTemplate` は、対象要素そのものを書き換えるのではなく、アドーナーレイヤーに重ねて描かれる。
このレイヤーの代表的な供給源が `AdornerDecorator` であり、`Window` の既定の `ControlTemplate` はこれを含んでいる。

`Window` のテンプレートを独自のものへ差し替え、`AdornerDecorator` を書き忘れると描画先が消える。
実測では、`ContentPresenter` だけを置いたテンプレートで `AdornerLayer.GetAdornerLayer` が `null` を返し、`Validation.HasError` が `true` でも画面には何も出なかった。
同じテンプレートに `AdornerDecorator` を 1 つ足すと、レイヤーが取得できてアドーナーが 1 つ付いた。

| `Window.ControlTemplate` | `Validation.HasError` | `AdornerLayer.GetAdornerLayer` | 画面上の赤枠 |
| --- | --- | --- | --- |
| 既定 | `true` | 取得できる | 出る |
| 差し替え・`AdornerDecorator` 無し | `true` | `null` | 出ない |
| 差し替え・`AdornerDecorator` 有り | `true` | 取得できる | 出る |

エラー自体は正しく発生・保持されているため、ログや `HasError` を見ている限り異常が見つからない。
この非対称性が、原因の特定を難しくしている。

レイヤーを提供するのは `Window` のテンプレートに限らない。
`AdornerDecorator` を視覚ツリーの途中に置けば、その配下に新しいレイヤーが作られる。
`AdornerDecorator` を含まないテンプレートへ差し替えた `Window` の上に、次の内容を置く。

```xml
<StackPanel>
    <TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />

    <AdornerDecorator HorizontalAlignment="Left">
        <TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />
    </AdornerDecorator>
</StackPanel>
```

2 つの `TextBox` は同じ ViewModel の同じプロパティにバインドしており、検証エラーの状態も等しい。
違いは `AdornerDecorator` に包まれているかどうかだけである。

<figure class="article-figure">
  <img src="/images/articles/wpf-validation-error-not-displayed/adorner-layer-required.png" alt="2 つの TextBox を縦に並べた画面。AdornerDecorator で包んでいない上の TextBox は枠が通常色のままで、AdornerDecorator で包んだ下の TextBox だけが赤い枠で囲まれている。" width="355" height="167" loading="lazy">
  <figcaption><code>AdornerDecorator</code> で包んだ下側だけに赤枠が描かれている。撮影前に双方の <code>Validation.HasError</code> が <code>true</code> であること、および上側では <code>AdornerLayer.GetAdornerLayer</code> が <code>null</code> を返すことを確認しており、差はエラーの有無ではなく描画先の有無による（.NET 10 / Windows 11 で生成）。</figcaption>
</figure>

レイヤーを提供するのは `AdornerDecorator` だけではない。
`ScrollViewer` の内部にある `ScrollContentPresenter` もレイヤーを持つ。
実測では、`AdornerDecorator` を含まないテンプレートの `Window` でも、`ScrollViewer` の中に置いた `TextBox` には赤枠が描かれた。
同じ画面でも `ScrollViewer` の内側と外側で結果が分かれるため、この症状は「一部の入力欄だけ赤枠が出ない」という形で現れることがある。
テンプレートを差し替えていながら再現しない場合は、対象が `ScrollViewer` の内側にないかを確認する。

この段階を成立させるには、`Window` のテンプレートを差し替えている場合に `AdornerDecorator` を含める。

### 段階 1 の手前: ソースがまだ更新されていない

段階 1 へ到達するタイミングの問題である。
先述のとおり、組み込みの 2 つのルールは `ValidatesOnTargetUpdated` が `true` であるため、バインディングの確立時やソース側の値の変化でも評価される。
一方、ユーザーが入力した内容が検証されるのは、ターゲットからソースへ値が転送されたときに限られる。
`TextBox.Text` の `UpdateSourceTrigger` の既定は `LostFocus` であるため、入力しただけではソースが更新されず、入力内容の検証も走らない。
実測では、有効な初期値を持つ `TextBox` の内容を空にしても、フォーカスを移すまでは `Validation.HasError` が `false` のままであった。

これは `IDataErrorInfo` でも、本記事の実装例のように setter で検証する `INotifyDataErrorInfo` でも同じである。
後者は ViewModel の `ErrorsChanged` に追随するが、この構成でそのイベントを起こすのはプロパティの setter であり、setter を呼ぶのがソース更新だからである。
`UpdateSourceTrigger=Explicit` の場合はさらに顕著で、同じ構成では `UpdateSource` を呼ぶまで検証結果が変化しなかった。
逆に、`ErrorsChanged` をソース更新とは独立に発生させる構成、たとえば非同期の照会が完了した時点で通知する実装は、この制約を受けない。

---

どの段階で止まっているかは、`Validation.HasError`・`Validation.Errors` の件数・アドーナーの数を分けて読めば判別できる。
常にエラーを返すソースへバインドした `TextBox` で測った結果が次の図である。

<figure class="article-figure">
  <img src="/images/articles/wpf-validation-error-not-displayed/validation-stages.svg" alt="検証の構成別に HasError・Errors の件数・アドーナーの数を測った表。IDataErrorInfo だけでは HasError が False で Errors も 0。ValidatesOnDataErrors を有効にすると True・1・1 になる。INotifyDataErrorInfo は既定で True・1・1。ValidationRules も True・1・1。ErrorTemplate を null にすると True・1 のままアドーナーだけが 0 になる。" width="615" height="230" loading="lazy">
  <figcaption>.NET 10 / Windows 11 で、常にエラーを返すソースへバインドした <code>TextBox</code> を測った結果。<code>adorners</code> は <code>AdornerLayer.GetAdorners</code> が返した数である。</figcaption>
</figure>

**1 行目は `Errors` が 0 である。** `IDataErrorInfo` を実装しても、`ValidatesOnDataErrors` を有効にしなければ段階 1 に到達しない。
2 行目で有効にすると 1 件になり、アドーナーも 1 つ描かれる。

`INotifyDataErrorInfo` は 3 行目のとおり、対応する `ValidatesOnNotifyDataErrors` が既定で有効なため、実装しただけで検証に参加する。
2 つのインターフェイスで既定値が異なる点が、この問題を分かりにくくしている。

最終行が段階 3 だけで止まった状態である。`HasError` は `True`、`Errors` は 1 件のまま、アドーナーだけが 0 になっている。
**エラーは保持されているのに描かれない。** 「値は不正なのに赤枠が出ない」という症状は、この行に当たる。

この段階の手前を解消するには、入力の途中で結果を出すなら、対象の `TextBox.Text` バインディングに `UpdateSourceTrigger=PropertyChanged` を指定する。

---

## 3 段階を満たす実装

3 つを成立させたうえで、メッセージを表示する `ErrorTemplate` を用意する。
既定の `ErrorTemplate` は赤枠だけであり、`Validation.Errors` の内容は画面に出ないためである。

まず、検証結果を保持する ViewModel の基底クラスを用意する。
`INotifyDataErrorInfo` はプロパティ名ごとのメッセージ集合を返す設計であるため、辞書で保持すると実装が単純になる。

```csharp
// ImplicitUsings を無効にしている場合は System と System.Collections.Generic も必要になる。
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public abstract class ValidatableBase : INotifyPropertyChanged, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> errors = [];

    public bool HasErrors => errors.Count > 0;

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName is not null && errors.TryGetValue(propertyName, out List<string>? list)
            ? list
            : Array.Empty<string>();

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        Validate(propertyName!);
    }

    protected void SetErrors(string propertyName, IReadOnlyList<string> messages)
    {
        if (messages.Count == 0)
        {
            errors.Remove(propertyName);
        }
        else
        {
            errors[propertyName] = [.. messages];
        }

        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasErrors)));
    }

    protected abstract void Validate(string propertyName);
}
```

`ErrorsChanged` はエラーが増えたときだけでなく、消えたときにも発生させる必要がある。
発生させないと赤枠が残り続ける。
`HasErrors` の変更通知を併せて発生させているのは、保存ボタンの活性状態を `HasErrors` へバインドする構成を想定したものである。
`ICommand` の `CanExecute` に連動させる場合は、通知だけでは再評価されないため `CommandManager.InvalidateRequerySuggested` などの再評価の契機が別途必要になる（[WPF で RelayCommand の CanExecute がボタンの有効・無効に反映されない問題の解決方法](/ja/articles/wpf-relaycommand-canexecute-not-updating/)）。

`SetErrors` に渡すプロパティ名は、バインディングのパスと完全に一致させる。
不一致の場合の挙動は「注意点」で扱う。

派生クラスでは、対象プロパティの検証だけを書く。

```csharp
public sealed class AccountViewModel : ValidatableBase
{
    private string name = string.Empty;

    public AccountViewModel() => Validate(nameof(Name));

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    protected override void Validate(string propertyName)
    {
        if (propertyName != nameof(Name))
        {
            return;
        }

        List<string> messages = [];
        if (string.IsNullOrWhiteSpace(Name))
        {
            messages.Add("名前は必須である。");
        }
        else if (Name.Length > 20)
        {
            messages.Add("名前は 20 文字以内である。");
        }

        SetErrors(nameof(Name), messages);
    }
}
```

コンストラクターで `Validate` を呼んでいるのは、`HasErrors` を初期状態から正しくするためである。
これにより、起動直後から保存操作を抑止できる。

XAML 側では、メッセージを描く `ErrorTemplate` を定義して `Style` から適用する。
`AdornedElementPlaceholder` が元のコントロールの位置を示し、その前後に任意の装飾を置ける。

```xml
<StackPanel Margin="16">
    <StackPanel.Resources>
        <ControlTemplate x:Key="FieldErrorTemplate">
            <StackPanel>
                <Border BorderBrush="#D13438" BorderThickness="1">
                    <AdornedElementPlaceholder x:Name="Adorned" />
                </Border>
                <TextBlock Margin="2,2,0,0" FontSize="11" Foreground="#D13438"
                           Text="{Binding ElementName=Adorned,
                                  Path=AdornedElement.(Validation.Errors)/ErrorContent}" />
            </StackPanel>
        </ControlTemplate>

        <Style TargetType="TextBox">
            <Setter Property="Validation.ErrorTemplate" Value="{StaticResource FieldErrorTemplate}" />
            <Setter Property="Margin" Value="0,0,0,22" />
        </Style>
    </StackPanel.Resources>

    <TextBox Width="240" HorizontalAlignment="Left"
             Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />
</StackPanel>
```

`AdornedElement.(Validation.Errors)` の丸括弧は添付プロパティを指すための記法であり、続く `/ErrorContent` はコレクションの現在の項目、すなわち既定では先頭のエラーを指す。
この XAML に `AccountViewModel` を `DataContext` として与えて実行したところ、起動直後に `名前は必須である。`、21 文字以上を入力した状態では `名前は 20 文字以内である。` が `TextBox` の下に描かれ、有効な値を入力するとアドーナーごと消えた。

`Style` に指定した下方向の余白は、メッセージの表示領域を確保するためのものである。
アドーナーはレイアウトに関与しないため、この余白が無いとメッセージが直下の要素へ重なる。

---

## 注意点

- **既定の `ErrorTemplate` はメッセージを表示しない。**
既定のテンプレートはアドーナーレイヤー上の赤い枠だけを描く。
実測でも、エラー状態の `TextBox` の `ToolTip` は `null` のままであった。
メッセージを出すには、`ErrorTemplate` を差し替えるか、`Validation.HasError` を条件とする `Style` の `Trigger` で `ToolTip` を設定する。
- **アドーナーはレイアウトを押し広げない。**
エラー表示の有無で親パネルの `ActualHeight` を測ったところ、どちらも同じ値であった。
メッセージを縦に伸ばす `ErrorTemplate` を使う場合は、あらかじめ余白を確保しないと下の要素に重なる。
- **`(Validation.Errors)[0].ErrorContent` はエラー解消時にバインディングエラーを出す。**
インデクサーで先頭要素を指す書き方は、エラーが解消してコレクションが空になった瞬間に評価され、`System.Windows.Data Error: 17` が出力ウィンドウへ記録された。
表示自体は正しく消えるため見落としやすい。
現在の項目を指す `/ErrorContent` に書き換えると、同じ表示のままトレースが出なくなる。
出力ウィンドウのメッセージの読み方は [WPF バインディングエラーの読み方と出力ウィンドウを使った原因特定](/ja/articles/wpf-binding-error-debugging-output-window/) で扱っている。
- **`Mode=OneWay` ではユーザーの入力が検証されず、一度出た赤枠が消えない。**
`OneWay` にはターゲットからソースへの転送が無いため、入力内容は検証対象にならない。
ただし「検証が一切走らない」わけではない。
前述の `ValidatesOnTargetUpdated` により、実測では `OneWay` でもバインディングの確立時とソース側のプロパティ更新時に評価され、無効な値であれば赤枠が出た。
問題は、この赤枠がユーザーの操作では消えないことである。
実測でも、`OneWay` の `TextBox` へ有効な文字列を入力しても `Validation.HasError` は `true` のままであった。
表示専用のつもりで `OneWay` にした入力欄が、恒久的にエラー表示のまま残ることになる。
- **`OneWay` バインディングのターゲットへコードから代入するとバインディングが外れる。**
実測では、`TextBox.Text` へコードから直接代入すると `BindingOperations.GetBinding` が `null` を返し、赤枠も消えた。
`OneTime` でも同じである。
ユーザーの入力や `SetCurrentValue` による設定ではバインディングは維持されるため、コードからの代入だけがこの挙動になる。
- **`ErrorsChanged` のプロパティ名がバインディングのパスと一致しないと表示されない。**
`Name` にバインドしている状態で、誤って `Namee` を指定して `ErrorsChanged` を発生させたところ、`HasErrors` が `true` であるにもかかわらず `Validation.HasError` は `false` のままであった。
`nameof` を使い、文字列リテラルを避けることで防げる。
- **非同期に確定した検証結果は UI スレッドで反映する。**
バインディングエンジンは `ErrorsChanged` を購読して `Validation.Errors` を更新するため、この通知は UI スレッドで発生させる必要がある。
バックグラウンドの処理で結果が確定する構成では、`errors` の書き換えと 2 つの通知を含む `SetErrors` の呼び出し全体を `Dispatcher` 経由で UI スレッドへ移す。
- **`Validation.Error` 添付イベントは既定では発生しない。**
エラーを画面表示以外の経路（ログ・集計・画面遷移の抑止など）で拾う場合、`Binding.NotifyOnValidationError` を `True` にする必要がある。
既定は `False` であり、指定しなければハンドラーは一度も呼ばれない。
- **setter が投げた例外は既定で握りつぶされる。**
プロパティの setter が `ArgumentOutOfRangeException` を投げる ViewModel に不正な値を入力したところ、`ValidatesOnExceptions` を指定していない構成では `Validation.HasError` が `false` のままで、値も更新されなかった。
`ValidatesOnExceptions="True"` を指定すると `ExceptionValidationRule` が例外を捕捉し、`Exception.Message` がそのままエラー内容になった。
`ArgumentException` 系の例外はメッセージ末尾に `(Parameter 'value')` のような引数名が付くため、画面に出す文言としてはそのまま使えない場合がある。
- **型変換の失敗だけは指定なしで表示される。**
`int` 型のプロパティへバインドした `TextBox` に `abc` と入力すると、`ValidatesOnExceptions` も検証ルールも無い状態でエラーが表示された。
バインディングエンジンが変換失敗を検証エラーとして扱うためである。
この場合のメッセージはフレームワークが生成したもの（日本語環境では `値 'abc' を変換できませんでした。`）であり、業務的な文言に差し替えるには自作の `ValidationRule` か `IValueConverter` を用いる。
- **自作 `ValidationRule` が受け取るのは変換前の値である。**
`ValidationStep` の既定は `RawProposedValue` であり、`int` 型のプロパティへバインドしていても `Validate` に渡るのは `string` であった。
数値として検証するには、メソッド内で自分で解析するか、`ValidationStep="ConvertedProposedValue"` を指定する。
- **`DataGrid` のセルは、列の種類によって扱いが分かれる。**
編集用のコントロールをフレームワークが実行時に生成する `DataGridTextColumn` などでは、単純なコントロールと同じ形で `Validation.ErrorTemplate` を適用できず、セル専用のエラーテンプレートも無いと公式ドキュメントに明記されている。この場合は `DataGridBoundColumn.EditingElementStyle`（セル単位）と `DataGrid.RowValidationErrorTemplate`（行単位）で表現する。
一方 `DataGridTemplateColumn` では `CellEditingTemplate` に自分で書いたコントロールへ `Validation.ErrorTemplate` を直接指定でき、実測でも編集中のセル内にメッセージが描かれた。`EditingElementStyle` は `DataGridBoundColumn` のメンバーであり、この列には存在しない。
セルの表示・編集でコントロールを切り替える構成は [WPF DataGrid でセル編集中と表示時でコントロールを切り替える方法](/ja/articles/wpf-datagrid-cell-editing-template/) で扱っている。
- **検証が走るタイミングは `UpdateSourceTrigger` に従う。**
入力中に結果を出すか、フォーカスが外れてから出すかは、表示の好みではなく更新タイミングの設計そのものである。
各値の違いは [WPF TextBox の UpdateSourceTrigger で入力がソースへ反映されるタイミングを制御する](/ja/articles/wpf-textbox-updatesourcetrigger-binding-timing/) で扱っている。
`Explicit` を選んだ場合に View 側から更新を指示する実装は [WPF で TextBox の UpdateSource を View から呼び出すときの落とし穴と実装](/ja/articles/wpf-textbox-updatesource-from-view-pitfalls/) で扱っている。

---

## 代替案・比較

エラーを発生させる 4 つの方式を比較する。

| 方式 | 有効化 | 1 プロパティに複数メッセージ | 非同期・遅延した検証 | 適するケース |
| --- | --- | --- | --- | --- |
| 自作 `ValidationRule` | `ValidationRules` へ追加 | 不可（`Validation.Errors` に入るのは 1 件） | 不可 | 入力書式の検証を View 側で完結させる場合 |
| `IDataErrorInfo` | `ValidatesOnDataErrors="True"` | 不可（`string` 1 件） | 不可 | 既存資産が `IDataErrorInfo` に依存している場合 |
| `INotifyDataErrorInfo` | 既定で有効 | 可能 | 可能（`ErrorsChanged` で後から通知） | ViewModel が検証を担う新規実装 |
| `ValidatesOnExceptions` | `ValidatesOnExceptions="True"` | 不可 | 不可 | ドメインモデルが setter で不変条件を守っている場合 |

自作 `ValidationRule` を複数登録しても、`Validation.Errors` に複数件は入らない。
先に失敗したルールがあると以降は評価されないためである。
1 件のエラーに複数の文言を持たせることは可能で、`ValidationResult.ErrorContent` は `object` であるためコレクションを渡せる。
ただしその場合、表示側も `ItemsControl` などコレクションを描けるテンプレートに変える必要がある。
本記事の実装例のように `ErrorContent` を `TextBlock.Text` へバインドしたままでは、型名が表示される。

`INotifyDataErrorInfo` が既定で有効な点は利点であると同時に、意図しない検証が混入する経路にもなる。
ViewModel の基底クラスが `INotifyDataErrorInfo` を実装していると、個々のバインディングに何も書かなくても検証が有効になる。
特定のバインディングだけ検証を外すには `ValidatesOnNotifyDataErrors="False"` を明示する。

複数の方式を同時に有効にすることも可能である。
`IDataErrorInfo` と `INotifyDataErrorInfo` を併用した実測では、`Validation.Errors` に 2 件が積み上がった。
ただし、同じ構成に `RawProposedValue` 段階の自作ルールを足して失敗させると、`DataErrorValidationRule` のエラーは現れなくなった。
`UpdatedValue` 段階はターゲットからソースへの検証経路の後段にあり、前段が失敗した時点で打ち切られるためである。
`NotifyDataErrorValidationRule` も同じ `UpdatedValue` 段階にあるが、そのエラーは ViewModel が保持する状態から `ErrorsChanged` 経由で供給されるため、この経路の打ち切りとは独立に維持された。
先頭のエラーだけを表示する `ErrorTemplate` では、どのメッセージが出るかがこの評価順に左右されるため、表示要件が単純でない限り方式は 1 つに絞るのが扱いやすい。

---

## まとめ

エラーが表示されないという症状は、まず `Validation.GetHasError` の値で切り分ける。

- **`false` の場合** — 検証ルールがバインディングに関連付いていないか、入力した内容がまだソースへ転送されていない。
`IDataErrorInfo` なら `ValidatesOnDataErrors="True"` を、自作ルールなら `ValidationRules` への追加を確認する。
入力中に反応させるには `UpdateSourceTrigger=PropertyChanged` を指定する。
- **`true` なのに何も出ない場合** — 描画先が無い。
`Window` のテンプレートを差し替えているなら `AdornerDecorator` を含める。
- **赤枠は出るがメッセージが出ない場合** — 既定の `ErrorTemplate` の仕様である。
`AdornedElementPlaceholder` を含むカスタムテンプレートを用意し、`(Validation.Errors)/ErrorContent` を表示する。

方式の選択は次を基準とする。
ViewModel が検証を担う新規実装では `INotifyDataErrorInfo` を採用する。
入力書式のチェックを View 側で完結させたい場合に限り自作の `ValidationRule` を用い、ドメインモデルが setter で不変条件を守っている場合は `ValidatesOnExceptions` を併用する。
既存資産が `IDataErrorInfo` に依存している場合は、`ValidatesOnDataErrors="True"` の指定漏れが無いかを最初に確認する。

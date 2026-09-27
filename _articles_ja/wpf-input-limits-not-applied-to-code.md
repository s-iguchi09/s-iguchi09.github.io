---
layout: article-ja
title: "WPF の MaxLength などの入力制限が、コードやバインドから入る値に効かない理由"
date: 2026-09-27
category: WPF
excerpt: "MaxLength・CharacterCasing・DisplayDateStart・IsSnapToTickEnabled などの入力制限は、コードやバインドから入る値を止めない。3 つの経路を .NET 10 で実測し、値の範囲をビューモデルで守る実装を示す。"
image: /images/articles/wpf-input-limits-not-applied-to-code/input-limits-by-path.svg
---

## 概要

データベースの列が 50 文字なら `TextBox` に `MaxLength="50"` を、受付期間が決まっているなら `DatePicker` に `DisplayDateStart` と `DisplayDateEnd` を書く。
画面の入力制限は、値の範囲を守る手段として広く使われている。
ところが、保存済みのデータを読み込んだときや、ファイルから取り込んだ値をバインドで流し込んだときには、これらの制限を超えた値がそのまま画面に表示され、保存まで進む。

本記事では、代表的な入力制限 7 つを「利用者の操作」「コードからの代入」「バインドした値」の 3 つの経路で試し、どの経路で効くのかを表にまとめる。
そのうえで、制限を超えた値を確実に止めるために、検証と丸めをビューモデルに置く実装を示す。
図の値は、いずれも .NET 10 / Windows 11 で実際に動かして確認した結果である。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF
- 検証環境: .NET 10 / Windows 11
- 言語: C# 10 以降 / XAML（コード例は nullable 参照型と暗黙の using の有効化を前提とする）
- 対象コントロール: `TextBox` / `PasswordBox` / `DatePicker` / `Slider` / `TabControl` と `TabItem`
- アーキテクチャ: 入力値をビューモデルに双方向でバインドする MVVM の構成

---

## 問題

顧客情報の編集画面を例にとる。
画面には次の入力制限を書いてある。

```xml
<TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}"
         MaxLength="50" />
<DatePicker SelectedDate="{Binding DeliveryDate}"
            DisplayDateStart="{Binding FirstDeliveryDate, Mode=OneWay}"
            DisplayDateEnd="{Binding LastDeliveryDate, Mode=OneWay}" />
<Slider Value="{Binding Volume, Mode=TwoWay}"
        Minimum="0" Maximum="100"
        IsSnapToTickEnabled="True" TickFrequency="10" />
```

名前は 50 文字まで、配送日は期間内、音量は 0〜100 の 10 刻み、という意図である。
`DisplayDateStart` と `DisplayDateEnd` は既定で双方向にバインドされ、受付期間を読み取り専用のプロパティで持つと、既定のバインドは、`DataContext` を設定した時点か、ウィンドウを表示した時点の遅いほうで `InvalidOperationException` になる（3 つ目の図、注意点の節）。
そのため、ビューモデルから渡すだけの受付期間には `Mode=OneWay` を指定している。
キーボードやカレンダーで操作している限り、この画面はおおむね意図どおりに動く。
51 文字目は入らず、期間外の日付はカレンダーで選べず、スライダーは 10 刻みで動く。

問題が起きるのは、値が利用者の操作以外の経路から入ったときである。
たとえば次のような場面がある。

- 以前の版で 80 文字まで許していた頃の顧客データを読み込む
- CSV から取り込んだ配送日を `DeliveryDate` に設定する
- 設定ファイルに保存してあった音量 `23.4` を復元する

いずれの値も画面にそのまま表示され、利用者が保存を押せば、制限を超えたまま保存処理へ渡る。

---

## よく紹介される対処

入力できる文字数を制限したいという質問には、`MaxLength` を設定するよう答えるのが定番である。
Microsoft Learn の [`TextBox.MaxLength` のリファレンス](https://learn.microsoft.com/dotnet/api/system.windows.controls.textbox.maxlength)も、郵便番号や電話番号の長さの制限に加えて、データベースの対応する列の最大長を超えないようにする用途を挙げている。
ほかのコントロールにも、リファレンスが利用者の操作を制限すると説明しているプロパティがある。
`DatePicker.DisplayDateStart` のリファレンスが参照している [`Calendar.DisplayDateStart`](https://learn.microsoft.com/dotnet/api/system.windows.controls.calendar.displaydatestart) は範囲外の日付へのスクロールと選択を制限し、[`Slider.IsSnapToTickEnabled`](https://learn.microsoft.com/dotnet/api/system.windows.controls.slider.issnaptotickenabled) はつまみを最も近い目盛りへ動かす。
本記事では、値の範囲を守るつもりで書かれやすい設定として、次の 7 つを取り上げる。

| 守りたいこと | 対応する設定 |
|---|---|
| 文字数の上限 | `TextBox.MaxLength` |
| 大文字への統一 | `TextBox.CharacterCasing="Upper"` |
| パスワードの文字数の上限 | `PasswordBox.MaxLength` |
| 日付の範囲 | `DatePicker.DisplayDateStart` / `DisplayDateEnd` |
| 決まった刻みの値 | `Slider.IsSnapToTickEnabled` と `TickFrequency` |
| 値の上限 | `Slider.Maximum` |
| 選べないタブ | `TabItem.IsEnabled="False"` |

これらは利用者の入力を制限する手段としては正しい。
問題は、これを「プロパティの値の制約」だと受け取り、コードやバインドから入る値まで守られると期待することにある。

---

## なぜ効かないのか

3 つの経路で、それぞれの制限を実際に試した。
利用者の操作は、対象にキーボードフォーカスを移したうえで、WPF の入力の仕組みである `InputManager` を通してキーを、`TextCompositionManager` を通して文字を送った。
カレンダーは範囲外の日付ボタンが有効かどうかを読み、タブの選択は支援技術が使う UI オートメーションの `Select` を呼んで試した。
コードからの代入とバインドした値は、いずれもウィンドウに表示したコントロールに対して設定した。

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-input-limits-not-applied-to-code/input-limits-by-path.svg" alt="入力制限を 3 つの経路で試した表。MaxLength が 5 の TextBox は、abcdefgh を入力すると abcde になるが、コードから設定しても、バインドしても abcdefgh のまま。CharacterCasing が Upper の TextBox は、hello を入力すると HELLO になるが、コードとバインドでは hello のまま。MaxLength が 8 の PasswordBox は、10 文字を入力すると 8 文字になるが、コードから Password に設定すると 10 文字のままで、PasswordBox にはバインドできる PasswordProperty が無い。表示範囲を 4 月 10 日から 20 日にした DatePicker では、カレンダーの 4 月 5 日のボタンは無効だが、テキスト欄に 4/5/2026 を入力すると 2026-04-05 が選ばれ、コードとバインドでも 2026-04-05 になる。その後の DisplayDateStart は、どの経路でも 2026-04-05 に下がり、コードから設定した後のカレンダーでは範囲外の 4 月 7 日のボタンが有効になる。10 刻みの目盛りに合わせる Slider は、50 から右矢印キーで 60 になるが、コードとバインドの 23.4 は 23.4 のまま。Maximum が 100 の Slider は、End キーで 100、コードの 150 も 100 になり、その後 Maximum を 200 にすると 150 に戻る。バインドした 150 は Slider が 100 でソースは 150 のまま。IsEnabled が False の 2 番目の TabItem は、UI オートメーションの Select で ElementNotEnabledException になり選択は 0 のまま、コードの SelectedIndex とバインドでは 1 が選ばれて Page 2 が表示される。" width="1266" height="320" loading="lazy">
  <figcaption>.NET 10 / Windows 11 での実測結果。利用者の操作は InputManager と TextCompositionManager で送ったキーと文字で、カレンダーは日付ボタンの有効・無効を読み、タブは UI オートメーションの Select を呼んだ。DatePicker は en-US の表示で試した。DisplayDateStart afterwards の行は、上の行で範囲外の日付が入った後の状態で、04-07 のボタンはコードから設定した後だけを確かめた。キー操作では上限を超える値を作れないため、Slider の Maximum の行の End キーは上限で止まることだけを示す。この行のバインドは Mode=TwoWay を指定した。</figcaption>
</figure>

**コードとバインドから入る値を画面上で範囲に収めたのは、`Slider` の `Maximum` だけだった。**
コードからの代入は、7 つのうち 6 つで制限をすり抜けた。
バインドした値は、バインドできない `PasswordBox` を除く 6 つのうち、5 つで画面の値が制限をすり抜けた。
ソースの値は、6 つすべてで制限を超えたまま残った。

`MaxLength`・`CharacterCasing`・`PasswordBox.MaxLength` については、これは仕様である。
いずれも、プロパティの値ではなく、利用者が入力した文字に掛かる制限として定義されている。
`MaxLength` のリファレンスは、手動で入力できる最大文字数と定義したうえで、プログラムから加えた文字には影響しないと明記している。
[`CharacterCasing` のリファレンス](https://learn.microsoft.com/dotnet/api/system.windows.controls.textbox.charactercasing)にも同じ注記があり、[`PasswordBox.MaxLength` のリファレンス](https://learn.microsoft.com/dotnet/api/system.windows.controls.passwordbox.maxlength)も、コードから `Password` を操作した場合には効かないと書いている。

`Slider` の目盛り合わせは、リファレンスの説明からは経路による違いを読み取れない。
実測では、矢印キーの操作は目盛りに合ったが、コードとバインドから設定した `23.4` は `23.4` のまま残った。

`DatePicker` は、利用者の操作の中でも一部しか制限しない。
範囲外の日付ボタンは無効になるが、テキスト欄に範囲外の日付を入力すると、そのまま `SelectedDate` になった。
さらに、範囲外の日付がどの経路で入った場合も、`DisplayDateStart` はその日付まで下がった。
コードから `2026-04-05` を設定した後にカレンダーを開くと、範囲外の 4 月 7 日のボタンも有効になっていた。
`Calendar.DisplayDateStart` のリファレンスにも、`SelectedDate` を `DisplayDateStart` より前にすると `DisplayDateStart` が同じ値になると書かれている。
実測したのは開始日の側だけだが、[`Calendar.DisplayDateEnd` のリファレンス](https://learn.microsoft.com/dotnet/api/system.windows.controls.calendar.displaydateend)にも、終了日より後の日付について同じ記述がある。
**範囲外の値を読み込んだ画面では、カレンダーさえ範囲を守らない。**

`TabItem` の `IsEnabled="False"` は、UI オートメーションからの選択を `ElementNotEnabledException` で拒む。
しかし `SelectedIndex` を設定すると、無効なタブがそのまま選ばれ、その内容（`SelectedContent`）が `Page 2` になった。
実際のマウスのクリックでも無効なタブを選べないことは、[TabItem のデモページ](/ja/apps/wpf-standard-control-demo/tabitem.html)で実測している。

---

## 効く条件

表の中で、コードとバインドから入る値を画面上で範囲に収めたのは `Slider` の `Maximum` だけである。
これは入力処理ではなく、`Value` そのものを範囲に収める補正（coerce）として働くためである。
表のとおり、コードから `150` を設定すると `100` になり、その後 `Maximum` を `200` に広げると `150` に戻った。
設定した値を覚えたまま、実際の値だけを範囲に収めていることが分かる。
この補正の仕組みは、[依存関係プロパティのコールバックと検証](https://learn.microsoft.com/dotnet/desktop/wpf/properties/dependency-property-callbacks-and-validation)で説明されている。

ただし実測では、補正した値はバインドしたソースへ書き戻されなかった。
`Mode=TwoWay` でもソースは `150` のまま、画面の `Slider` は `100` を表示していた。
**画面とビューモデルの値が食い違ったまま、ビューモデルの `150` が保存処理へ渡る**ため、これも値を守る手段にはならない。

コントロールの入力制限に値の保証を任せてよいのは、次の条件をどちらも満たすときだけである。

- 値を書き込むのが、そのコントロールを操作する利用者だけである（読み込み・取り込み・既定値の設定など、コードから値を入れる処理が無い）
- `DatePicker` のように、制限が操作の一部にしか掛からないコントロールではない

業務アプリで、この条件を満たす入力欄は少ない。
保存済みの値を編集する画面は、それだけで 1 つ目の条件を満たさない。
そのため、**値の範囲はビューモデルで検証し、コントロールの制限は入力を助ける役割にとどめる**のが確実である。

---

## 実装例

### 文字数をビューモデルで検証する

名前の長さを `INotifyDataErrorInfo` で検証する。
`Name` に値が入るたびに長さを確かめ、超えていればエラーを記録して `ErrorsChanged` を発生させる。
コードから入った値もバインドから入った値も、必ずこの setter を通る。

```csharp
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public sealed class CustomerViewModel : INotifyPropertyChanged, INotifyDataErrorInfo
{
    public const int MaxNameLength = 50;

    private string _name = "";
    private readonly List<string> _nameErrors = new();

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
            _nameErrors.Clear();
            if (value.Length > MaxNameLength)
            {
                _nameErrors.Add($"Name must be at most {MaxNameLength} characters.");
            }

            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Name)));
            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public bool HasErrors => _nameErrors.Count > 0;

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName == nameof(Name) ? _nameErrors : Array.Empty<string>();

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

長すぎる値を切り詰めずにエラーとして残すのは、読み込んだデータを黙って書き換えないためである。
どこを直すかは利用者が決め、保存は `HasErrors` が `false` のときだけ許す。
`HasErrors` の変化も `PropertyChanged` で通知しているため、保存ボタンの `IsEnabled` を `HasErrors` から決めるバインドなら、ボタンも追従する（2 つ目の図の 3 行目）。
WPF には bool を反転する標準のコンバーターが無いため、次のようなコンバーターを用意する。

```csharp
using System.Globalization;
using System.Windows.Data;

public sealed class InvertBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(bool)value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(bool)value;
}
```

保存ボタンの `IsEnabled` は、このコンバーターを通して `HasErrors` にバインドする。
`local` は、コンバーターを置いた名前空間を指す XAML の接頭辞である。

```xml
<Window.Resources>
    <local:InvertBooleanConverter x:Key="InvertBooleanConverter" />
</Window.Resources>

<Button Content="Save"
        IsEnabled="{Binding HasErrors, Converter={StaticResource InvertBooleanConverter}}" />
```

`HasErrors` が `true` の間はボタンが無効になり、長さを直して `false` になると有効に戻る。

XAML の `MaxLength` は残す。
キーボードからは 50 文字を超えて入力できなくなり、利用者の入力の段階で上限を超えることが無くなる。
値は `CustomerViewModel.MaxNameLength` と同じにしておく。

```xml
<TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}"
         MaxLength="50" />
```

[`ValidatesOnNotifyDataErrors`](https://learn.microsoft.com/dotnet/api/system.windows.data.binding.validatesonnotifydataerrors) の既定値は `true` である。
`INotifyDataErrorInfo` を実装したソースへのバインドは、指定しなくても検証エラーを `TextBox` の `Validation.HasError` に反映する。

### 範囲と刻みを setter で丸める

音量のように、範囲外の値を拒むより丸めて受け取るほうが自然な値もある。
その場合は setter で範囲に収め、刻みに合わせる。
`NaN` は範囲に収められないため、受け取らずに今の値を残す。

```csharp
using System.ComponentModel;

public sealed class VolumeViewModel : INotifyPropertyChanged
{
    private double _volume;

    public double Volume
    {
        get => _volume;
        set
        {
            if (double.IsNaN(value))
            {
                return;
            }

            double clamped = Math.Clamp(value, 0, 100);
            _volume = Math.Round(clamped / 10, MidpointRounding.AwayFromZero) * 10;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Volume)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

丸めた値で `PropertyChanged` を発生させるので、`Slider` の表示も丸めた後の値にそろう。
XAML は「問題」の節の `Slider` のままでよい。

### 両方の経路で確かめる

この 2 つのビューモデルを、コードからの代入と利用者の操作の両方で試した。
図の検証では、名前の上限を 5 文字に変えている。
`Slider` は、ビューモデルの規則だけを確かめるために、範囲を 0〜200 に広げて目盛り合わせを外し、`SmallChange` を 5 にした。
`Slider` 自身は値を丸めないので、ソースと画面の値が丸まっていれば、丸めたのはビューモデルである。
`Slider` がキー操作でソースへ送った値を見るため、計測用に、setter が受け取った値を記録する処理を足した（実装例には含めていない）。

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-input-limits-not-applied-to-code/input-limits-viewmodel.svg" alt="ビューモデルで検証した結果の表。長さを INotifyDataErrorInfo で 5 文字までに検証するビューモデルに、コードから abcdefgh を設定すると、HasErrors も TextBox の Validation.HasError も True になる。MaxLength が 5 の TextBox に abcdefgh を入力すると abcde になり、HasErrors は False。abcdefgh を読み込んだ後は HasErrors が True で、末尾に x を打っても abcdefgh のまま入らず、Backspace で 1 文字消しても HasErrors は True のまま。IsEnabled を HasErrors の反転にバインドした保存ボタンは、コードから abcdefgh を設定すると False、abcde にすると True になり、読み込んだ abcdefgh を Backspace 3 回で abcde にすると False から True になる。0 から 100 に収めるビューモデルは、コードの 150 でソースも Slider も 100、End キーで Slider が送った 200 も両方 100。10 刻みに丸めるビューモデルは、コードの 23.4 でソースも Slider も 20、50 から右矢印キーで Slider が送った 55 も両方 60。" width="1254" height="230" loading="lazy">
  <figcaption>.NET 10 / Windows 11 での実測結果。名前の行は、上限を 5 文字にしたビューモデルと MaxLength 5 の TextBox を UpdateSourceTrigger=PropertyChanged でバインドし、ValidatesOnNotifyDataErrors は指定していない。保存ボタンの行は、IsEnabled を bool を反転するコンバーター経由で HasErrors にバインドした。Slider は Minimum 0・Maximum 200・目盛り合わせ無し・SmallChange 5 で、Volume に TwoWay でバインドした。sent は、setter が丸める前に受け取った値を計測用に記録したもので、記事の実装例には無い処理である。</figcaption>
</figure>

**丸めは、コードから入った値にも、`Slider` の操作から届いた値にも掛かった。**
`150` と `23.4` はそれぞれ `100` と `20` に、End キーで届いた `200` と右矢印キーで届いた `55` は `100` と `60` になり、いずれもソースと `Slider` の値が一致した。
1 つ目の図で `Slider` の `Maximum` だけに頼ったときの、ソース `150`・画面 `100` という食い違いは起きていない。
長すぎる名前は、コードから入ると `HasErrors` が `True` になり、`TextBox` の `Validation.HasError` も `True` になった。
保存ボタンは、コードからの値でも利用者の編集でも、`HasErrors` の変化に追従した。

---

## 注意点

- **上限内の値から入力する限り、検証エラーは出ない。**
`MaxLength` を残すと、入力は上限で止まり、ビューモデルに長すぎる値は届かない。
2 つ目の図の 1 行目のとおり、`HasErrors` は `False` のままであり、エラーの表示で上限を知らせることはできない。
上限は画面の文言で示しておく。
- **上限を超えた値を読み込んだ後は、文字を打ち足せない。**
2 つ目の図の 2 行目のとおり、上限を超えた状態の `TextBox` では末尾に文字を打っても入らず、1 文字消しただけではエラーが残る。
上限まで削らないと保存できないことを、エラーの文言で伝える。
- **`PasswordBox.Password` はバインドできない。**
`PasswordBox` にはバインドの対象になる `PasswordProperty` が無い。
コードから設定した `Password` には `MaxLength` が効かず、1 つ目の図では 10 文字のまま残った。
長さの確認は、`Password` を読み出すコードの側で行う。
- **`DatePicker` の範囲は、ビューモデルで検証する。**
テキスト欄から範囲外の日付が入り、一度入るとカレンダーの範囲も広がる。
`SelectedDate` のバインド先で範囲を検証する。
書き方は文字数の検証と同じで、比べる対象が日付の範囲に変わるだけである。
- **無効なタブは、コードからなら選べる。**
`SelectedIndex` をビューモデルから設定する画面では、設定する前に、そのタブを選べる状態かどうかをビューモデル自身が判定する。
- **`Slider` の補正は、ソースを書き換えない。**
画面に出ている値と保存される値が一致するとは限らない。
範囲はビューモデルで丸めるか、検証する。
- **受付期間を読み取り専用で持つなら、`Mode=OneWay` を指定する。**
`DisplayDateStart` と `DisplayDateEnd` の既定のバインドは双方向である。
読み取り専用のプロパティへは、`Source` を指定した場合は `SetBinding` の時点で `InvalidOperationException` になった。
`DataContext` 経由では、表示中の `DatePicker` なら `DataContext` を設定した時点で、表示前に設定した場合はウィンドウを表示する処理の中で、同じ例外になった。
表示前に親の要素へ `DataContext` を設定して継承させた場合も、表示する処理の中で例外になった。
設定できるプロパティなら、範囲外の日付で `DisplayDateStart` が下がってもソースは書き換わらず、`OneWay` と同じ結果だった。

受付期間のバインドを試した結果を次に示す。
既定の双方向と `OneWay` は `DisplayDateStart` で試した。
読み取り専用のプロパティは、`DisplayDateStart` と `DisplayDateEnd` のそれぞれで、`Source` の指定・表示中の `DataContext` の設定・表示前の `DataContext` の設定・表示前の親の要素への `DataContext` の設定の 4 通りを試した。

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-input-limits-not-applied-to-code/input-limits-displaydate-binding.svg" alt="DisplayDateStart と DisplayDateEnd をビューモデルにバインドした結果の表。2026-04-10 のソースに DisplayDateStart を既定の TwoWay でバインドした場合も、Mode=OneWay の場合も、コードから SelectedDate を 2026-04-05 にすると DisplayDateStart は 2026-04-05 に下がり、ソースは 2026-04-10 のまま、バインドは残り、ソースを 04-12 にすると DisplayDateStart は 2026-04-12 になる。読み取り専用のプロパティへの既定のバインドは、DisplayDateStart と DisplayDateEnd のどちらも、Source を指定すると SetBinding で、表示中の DatePicker に DataContext を設定するとその時点で、表示前に DataContext を設定すると、DatePicker 自身に設定しても親の要素に設定して継承させても、設定の時点では例外にならずウィンドウを表示した時点で、読み取り専用のプロパティを理由とする InvalidOperationException になる。" width="1305" height="380" loading="lazy">
  <figcaption>.NET 10 / Windows 11 での実測結果。上の 2 行は、コードから SelectedDate を 2026-04-05 にした後の値である。source set to 04-12 の値は、SelectedDate を null に戻してからソースを 2026-04-12 にした後の DisplayDateStart である。read-only property は、例外のメッセージが読み取り専用のプロパティを理由に挙げていたことを示す。parent's DataContext の行は、DatePicker を置いた親の要素に DataContext を設定して継承させた。それ以外の行は DatePicker 自身に設定した。</figcaption>
</figure>

---

## まとめ

`MaxLength`・`CharacterCasing`・`DisplayDateStart`・`IsSnapToTickEnabled`・`TabItem.IsEnabled` は、いずれも利用者の操作を制限する仕組みであり、プロパティの値そのものを制約しない。
コードからの代入とバインドした値はこれらをすり抜け、`DatePicker` はテキスト欄からの入力さえ止めない。
表の中で唯一、コードからの値を画面上で範囲に収めた `Slider.Maximum` の補正も、バインドしたソースへは書き戻されなかった。

守るべき値の範囲は、次のように置き分ける。

- **値の保証:**
ビューモデルで検証する（`INotifyDataErrorInfo`）か、setter で丸める。
コード・バインド・利用者の操作のどの経路から来た値にも効く。
- **入力の補助:**
`MaxLength` などのコントロールの制限は残し、利用者が範囲外の値を打ち込めないようにする。
上限の値は、ビューモデルの定数と同じにしておく。
- **例外的にコントロールだけでよい場合:**
値を書き込むのがそのコントロールの利用者だけで、コードから値を入れる処理が無い入力欄に限る。

---

<!-- 関連記事 -->
- [WPF で入力検証のエラーが表示されない原因と IDataErrorInfo / INotifyDataErrorInfo の使い分け](/ja/articles/wpf-validation-error-not-displayed/)
- [TextBox（WPF 標準コントロールデモアプリ）](/ja/apps/wpf-standard-control-demo/textbox.html)：`MaxLength` と `CharacterCasing` が入力にだけ働くことを実測したページ
- [PasswordBox（WPF 標準コントロールデモアプリ）](/ja/apps/wpf-standard-control-demo/passwordbox.html)：`Password` がバインドできないことと、`MaxLength` が入力にだけ働くことを実測したページ
- [DatePicker（WPF 標準コントロールデモアプリ）](/ja/apps/wpf-standard-control-demo/datepicker.html)：表示範囲の外の日付が、入力でもコードからでも受け付けられることを実測したページ
- [Slider（WPF 標準コントロールデモアプリ）](/ja/apps/wpf-standard-control-demo/slider.html)：目盛りへの吸着と、範囲の補正を実測したページ
- [TabItem（WPF 標準コントロールデモアプリ）](/ja/apps/wpf-standard-control-demo/tabitem.html)：無効なタブがコードからは選べることを実測したページ

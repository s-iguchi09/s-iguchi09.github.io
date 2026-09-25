---
layout: article-ja
title: "DatePicker の表示形式をカスタマイズする方法"
date: 2026-04-15
category: WPF
excerpt: "WPF の DatePicker の日付を yyyy/MM/dd などの固定形式で表示する方法。DatePickerTextBox のスタイル、XAML での区切り文字のエスケープ、コードビハインドで Text を書き換えても効かない理由、コンバーターの使いどころを実測で示す。"
image: /images/articles/wpf-datepicker-custom-format/datepicker-default-vs-custom-format.png
---

## 概要

WPF の `DatePicker` は、自身にも親要素にも `Language`(XAML では `xml:lang`)が指定されていない場合、選択された日付をスレッドの `CurrentCulture`(既定ではシステムの地域設定)の形式で表示する(例: en-US では `4/15/2026`)。
この挙動は、マシンの地域設定に依存せず固定のレイアウトで日付を見せたい場合に不都合となる。
ログ向けの `yyyy/MM/dd` やレポート向けの `dd MMM yyyy` などが典型例である。
本記事では、コントロールが常にアプリの要求する形式で日付を表示するようカスタマイズする方法を、XAML スタイル・コードビハインド・バリューコンバーターの 3 通りで比較する。コードビハインドで `Text` を書き換える方法は、実測では表示を変えられなかった。

## 前提・対象環境

- フレームワーク / 言語: .NET 6 以降 / C# 10
- 対象コントロール: WPF `DatePicker`(`System.Windows.Controls`)
- アーキテクチャ: コードビハインド・MVVM のいずれにも適用可能
- 検証環境: .NET 10 / Windows 11（日本語環境）

以下の手法は、既定の `DatePicker` コントロールテンプレートが視覚ツリーに `DatePickerTextBox` を含むことに依存する。
テンプレートを全面的に差し替えた `DatePicker` ではこの要素が公開されない場合があり、その際はカスタムテンプレート内で整形する必要がある。コードビハインドで `Text` を書き換えても、後述のとおり表示は変わらない。
後述するコンバーターが整形するのは、`DatePicker` 本体ではなく併設の表示である。

`DatePicker` に実際に表示される文字列は、設定を変えて読み出せば確かめられる。

<figure class="article-figure">
  <img src="/images/articles/wpf-datepicker-custom-format/datepicker-format-matrix.svg" alt="DatePicker の設定ごとに、SelectedDateFormat の実効値とその出どころ、表示される文字列を測った表。何も設定しない場合は既定スタイル由来の Short になる。Short と既定はいずれも 2026/07/17、Long は 2026年7月17日、テキスト部分を書き換えた場合は 2026/07/17 (金) になる。" width="642" height="200" loading="lazy">
  <figcaption>.NET 10 / 日本語環境の Windows 11 で、<code>2026-07-17</code> を選択した <code>DatePicker</code> のテキスト部分を読み取った結果。2 列目の括弧内は、その値がどこから来たかを <code>DependencyPropertyHelper.GetValueSource</code> で読んだものである。</figcaption>
</figure>

**`SelectedDateFormat` は `Short` と `Long` の 2 つしか持たない。** どちらも任意の書式を指定する手段を持たない。
これが、以降で述べる回避方法が必要になる理由である。

何も設定しなければ `Short` になるが、これは依存関係プロパティのメタデータに登録された既定値ではない。
メタデータの既定値は `Long` であり、`Short` は既定スタイルが設定している。表の 2 列目に出ている `DefaultStyle` がその出どころである。

最終行は、テンプレート内のテキスト部分を直接書き換えた場合である。曜日を含む書式のように、2 つの既定形式では表せない表示もこの方法なら作れる。

本記事の図は、上記の環境で `DatePicker` の設定を変えながら、テキスト部分に表示される文字列を読み取って得たものである。
この環境で確認しているのは次の点である。

- `SelectedDateFormat` は `Short` と `Long` の 2 つしか持たず、任意の書式にはできない。
- `SelectedDateFormat` の既定値は `Short` であり、これは依存関係プロパティのメタデータではなく既定スタイルに由来する。
- テンプレート内のテキスト部分を書き換えれば、任意の書式にできる。

---

## XAML スタイルによる方法

コントロールテンプレート内の `DatePickerTextBox` をスタイル経由で対象にし、`Text` プロパティに `StringFormat` を指定する。

```xml
<DatePicker x:Name="datePicker" SelectedDate="{Binding SelectedDate}">
  <DatePicker.Resources>
    <Style TargetType="DatePickerTextBox">
      <Setter Property="Text"
              Value="{Binding SelectedDate,
                              RelativeSource={RelativeSource AncestorType=DatePicker},
                              StringFormat='yyyy\\/MM\\/dd'}" />
    </Style>
  </DatePicker.Resources>
</DatePicker>
```

エスケープしない `/` は日付区切りのプレースホルダーであり、バインドのカルチャによって別の文字に置き換えられるため、リテラルとして描画させるにはエスケープが要る。
ただし、マークアップ拡張の中では `\` 自体が XAML のエスケープ文字として扱われる。`\/` とだけ書くと XAML のパーサーに `\` が消費されて書式は `yyyy/MM/dd` になり、`de-DE` では `2026.04.15` と表示される。上の例のように `\\/` と重ねると、書式に `\/` が残る。
シングルクォートで囲む場合も、`StringFormat=yyyy\'/\'MM\'/\'dd` のようにクォート自体をエスケープすれば同じ効果が得られる。エスケープしない `yyyy'/'MM'/'dd` では、XAML の読み込みに失敗した。4 通りの結果は、次の図の後の表に示す。

既定表示のカルチャは、`Language`(XAML では `xml:lang`)の値がどこから来ているかで決まる。
自身にも親要素にも指定がなく、値の出どころが既定値(`Default`)のままであれば、スレッドの `CurrentCulture` に従う。このとき `Language` の既定値である `en-US` は使われない。
自身に指定した場合(`Local`)も、`Window` などの親要素の指定を継承した場合(`Inherited`)も、`CurrentCulture` に関係なくその言語の形式になる。

<figure class="article-figure">
  <img src="/images/articles/wpf-datepicker-custom-format/datepicker-culture-matrix.svg" alt="DatePicker 自身への xml:lang の指定、親要素への指定、CurrentCulture を独立に変えて、既定表示を測った表。どこにも指定しない場合は CurrentCulture に従い、ja-JP では 2026/04/15、en-US では 4/15/2026 になる。自身に指定した場合は CurrentCulture に関係なく、en-US では 4/15/2026、de-DE では 15.04.2026 になる。親要素に de-DE を指定した場合も、継承した言語に従い 15.04.2026 になる。" width="725" height="320" loading="lazy">
  <figcaption><code>SelectedDate</code> を 2026-04-15 とした <code>DatePicker</code> の既定表示（.NET 10 / Windows 11 で実測）。どこにも <code>xml:lang</code> を指定しない行では <code>Language</code> の値の出どころが <code>Default</code> であり、表示は <code>CurrentCulture</code> に従う。自身に指定した行（<code>Local</code>）と、親要素の指定を継承した行（<code>Inherited</code>）では、<code>CurrentCulture</code> を変えても表示が変わらない。</figcaption>
</figure>

適用前後を並べると次のようになる。
実行マシンの地域設定に左右されないよう、両方に `xml:lang="en-US"` を指定している。

```xml
<!-- 既定の表示 -->
<DatePicker xml:lang="en-US" SelectedDate="2026-04-15" Width="190" />

<!-- 上のスタイルを適用したもの -->
<DatePicker xml:lang="en-US" SelectedDate="2026-04-15" Width="190">
  <DatePicker.Resources>
    <Style TargetType="DatePickerTextBox">
      <Setter Property="Text"
              Value="{Binding SelectedDate,
                              RelativeSource={RelativeSource AncestorType=DatePicker},
                              StringFormat='yyyy\\/MM\\/dd'}" />
    </Style>
  </DatePicker.Resources>
</DatePicker>
```

<figure class="article-figure">
  <img src="/images/articles/wpf-datepicker-custom-format/datepicker-default-vs-custom-format.png" alt="同じ日付を選択した 2 つの DatePicker。既定のものは 4/15/2026 と表示され、StringFormat を指定したものは 2026/04/15 と表示されている。" width="501" height="146" loading="lazy">
  <figcaption>同じ <code>SelectedDate</code> を与えた 2 つの <code>DatePicker</code>。書式の差が出るよう、どちらにも <code>xml:lang="en-US"</code> を指定している。上は既定の表示で、この設定に従って <code>4/15/2026</code> になる。下は本節のスタイルを適用したもので、区切り文字と年月日の並びが指定どおりに固定される。</figcaption>
</figure>

ただし、区切り文字のエスケープで固定できるのは区切り文字と並び順であって、暦そのものではない。区切りが保たれても、`th-TH`・`ar-SA`・`fa-IR` はそれぞれの暦の年と月で表示される。

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-datepicker-custom-format/datepicker-stringformat-escape.svg" alt="XAML の Binding の中の StringFormat の 4 通りの書き方を比べた表。'yyyy\/MM\/dd' は解析後に yyyy/MM/dd になり、en-US と ja-JP では 2026/04/15 だが de-DE では 2026.04.15 になり、ar-SA では見えない方向記号が入る。'yyyy\\/MM\\/dd' と yyyy\'/\'MM\'/\'dd は 6 つの文化圏すべてでスラッシュが残るが、th-TH・ar-SA・fa-IR は自分の暦のままになる。エスケープしない yyyy'/'MM'/'dd は XamlParseException で読み込めない。" width="1159" height="200" loading="lazy">
  <figcaption>.NET 10 / Windows 11 で、本節のスタイルをそれぞれの <code>StringFormat</code> と <code>xml:lang</code> で読み込み、<code>SelectedDate</code> を 2026-04-15 にして測った結果。ASCII 以外の文字は符号位置で示している。</figcaption>
</figure>

暦まで含めて固定したい場合は、バインドに `ConverterCulture` を指定してカルチャ自体を固定する。  

## コードビハインドによる方法

`SelectedDateChanged` イベントを購読し、整形した文字列を `DatePicker.Text` に代入する方法がよく紹介される。この方法では表示は変わらない。ハンドラーの後で `DatePicker` が `SelectedDate` から自分の書式で文字列を書き直すため、既定の書式に戻った。

<figure class="article-figure">
  <img src="/images/articles/wpf-datepicker-custom-format/datepicker-codebehind.svg" alt="xml:lang が en-US の DatePicker で、SelectedDateChanged の中で Text を書き換えた結果の表。ハンドラーは 2 回呼ばれ、Text も表示も、ハンドラーが設定した yyyy/MM/dd ではなく 4/15/2026 だった。" width="626" height="110" loading="lazy">
  <figcaption>.NET 10 / Windows 11 で、<code>SelectedDate</code> を 2026-04-15 にしたあと、ハンドラーで <code>Text</code> を不変カルチャの <code>yyyy/MM/dd</code> に書き換えて測った結果。</figcaption>
</figure>

コードから書式を指定したい場合は、XAML による方法と同じ `DatePickerTextBox` のスタイルとバインドを設定するか、本体の表示はそのままにして、次節のコンバーターで併設表示を整形する。

## コンバーターによる併設表示

コンバーターは `DatePicker` 本体の表示を変えられない。
本体のテキストはコントロールテンプレートが `SelectedDate` から生成するものであり、バインド値から作られるわけではない。
加えて `SelectedDate` は `DateTime?` であるため、文字列を返すコンバーターを直接バインドしても適用されない。
コンバーターが適するのは、同じ日付を選んだ形式で表示する併設のラベルやステータスバーなど、いわゆるコンパニオン表示である。

```csharp
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

public class DateFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is DateTime d ? d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DateTime.TryParseExact(value as string, "yyyy/MM/dd",
               CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : DependencyProperty.UnsetValue;
}
```

同じソースにコンバーター経由で `TextBlock` をバインドする。

```xml
<TextBlock Text="{Binding SelectedDate, ElementName=datePicker,
                  Converter={StaticResource DateFormatConverter}}" />
```

コンバーターは形式文字列を 1 箇所に集約するため、1 回の修正で再利用しているすべての併設表示に反映される。

## よく使う書式指定文字列

`ToString` や `StringFormat` に渡す書式は、標準の .NET カスタム日時書式指定子に従う。

| 指定子             | 出力例             | 補足                                     |
| ------------------ | ------------------ | ---------------------------------------- |
| `yyyy/MM/dd`       | `2026/04/15`       | ゼロ埋め。`/` はカルチャに従う            |
| `dd MMM yyyy`      | `15 Apr 2026`      | `MMM` はカルチャに依存する               |
| `yyyy年MM月dd日`   | `2026年04月15日`   | 日本語向け。年月日はリテラル             |
| `yyyy/MM/dd HH:mm` | `2026/04/15 09:30` | 日付と時刻を 1 つの文字列に結合          |

ここで `/` と `:` はリテラルではなく、それぞれ日付区切り・時刻区切りのプレースホルダーであり、書式化に使うカルチャの区切り文字に置き換えられる(区切りが `.` のカルチャでは `2026.04.15` となる)。
そのカルチャは、`ToString` ではスレッドの `CurrentCulture` である。
バインドの `StringFormat` では、`ConverterCulture` を指定すればそのカルチャ、指定しなければ対象要素の `Language` になる。
後者は、`Language` が既定値のままなら OS の地域設定ではなく `en-US` になる([Binding.StringFormat の記事](/ja/articles/wpf-binding-stringformat-number-currency-date/)で実測している)。
`年` `月` `日` や `-`、`,` などはリテラルとしてそのまま保持される。
マシンの地域設定に関わらず固定レイアウトを保つには、上記のコンバーターのように `ToString` へ `CultureInfo.InvariantCulture` を渡すか、`yyyy'/'MM'/'dd` のように区切りをエスケープする。

## 注意点

- XAML の `StringFormat` 方式が変えるのは表示テキストだけである。基となる `SelectedDate` の値は変わらないため、日付を直接参照するバインドには影響しない。
- コンバーターでは、`is DateTime` の型の判定が日付未選択の場合を扱っている。`null` は判定を通らないため整形されない。そのうえで `string.Empty` を返すと、何も表示されない。

## まとめ

| 方法                  | メリット                      | デメリット                          |
| --------------------- | ----------------------------- | ----------------------------------- |
| Style + StringFormat  | 宣言的・コード不要            | StringFormat の制約がある           |
| バリューコンバーター  | MVVM フレンドリー・再利用可能 | 本体ではなく併設表示の整形に用いる  |

最適な方法はプロジェクトのアーキテクチャに依存する。
`DatePicker` 本体の表示を変えるには Style + StringFormat を使う。`SelectedDateChanged` で `Text` を書き換えても表示は変わらなかった。コンバーターは、同じ日付を表示する併設表示が複数ある場合に適する。

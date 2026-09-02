---
layout: article-ja
title: "DatePicker の表示形式をカスタマイズする方法"
date: 2026-04-15
category: WPF
excerpt: "XAML とコードビハインドの両面から、WPF DatePicker の日付表示形式を変更する方法をまとめる。"
image: /images/articles/wpf-datepicker-custom-format/datepicker-default-vs-custom-format.png
---

## 概要

WPF の `DatePicker` は、選択された日付をシステムのロケール形式(例: en-US では `4/15/2026`)で表示する。
この挙動は、マシンの地域設定に依存せず固定のレイアウトで日付を見せたい場合に不都合となる。
ログ向けの `yyyy/MM/dd` やレポート向けの `dd MMM yyyy` などが典型例である。
本記事では、コントロールが常にアプリの要求する形式で日付を表示するようカスタマイズする方法を、XAML スタイル・コードビハインド・バリューコンバーターの 3 通りで比較する。

## 前提・対象環境

- フレームワーク / 言語: .NET 6 以降 / C# 10
- 対象コントロール: WPF `DatePicker`(`System.Windows.Controls`)
- アーキテクチャ: コードビハインド・MVVM のいずれにも適用可能
- 検証環境: .NET 10 / Windows 11（日本語環境）

以下の手法は、既定の `DatePicker` コントロールテンプレートが視覚ツリーに `DatePickerTextBox` を含むことに依存する。
テンプレートを全面的に差し替えた `DatePicker` ではこの要素が公開されない場合があり、その際はコードビハインド方式か、カスタムテンプレート内での整形に頼ることになる。
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
                              StringFormat='yyyy\/MM\/dd'}" />
    </Style>
  </DatePicker.Resources>
</DatePicker>
```

区切り文字は `\/` とエスケープしてリテラルとして描画させている。
エスケープしない `/` は日付区切りのプレースホルダーであり、バインドのカルチャによって別の文字に置き換えられ、固定レイアウトが崩れる。
なお、シングルクォートで囲む `'/'`(例: `yyyy'/'MM'/'dd`)でも同じ効果が得られ、記事の後半ではこの記法を用いている。

適用前後を並べると次のようになる。
既定表示はターゲット要素の `Language`(XAML では `xml:lang`)に従うため、実行マシンのロケールに左右されないよう両方に `en-US` を指定している。

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
                              StringFormat='yyyy\/MM\/dd'}" />
    </Style>
  </DatePicker.Resources>
</DatePicker>
```

<figure class="article-figure">
  <img src="/images/articles/wpf-datepicker-custom-format/datepicker-default-vs-custom-format.png" alt="同じ日付を選択した 2 つの DatePicker。既定のものは 4/15/2026 と表示され、StringFormat を指定したものは 2026/04/15 と表示されている。" width="486" height="146" loading="lazy">
  <figcaption>同じ <code>SelectedDate</code> を与えた 2 つの <code>DatePicker</code>。書式の差が出るよう、どちらにも <code>xml:lang="en-US"</code> を指定している。上は既定の表示で、この設定に従って <code>4/15/2026</code> になる。下は本節のスタイルを適用したもので、区切り文字と年月日の並びが指定どおりに固定される。</figcaption>
</figure>

ただし、`\/` のエスケープで固定できるのは区切り文字と並び順であって、暦そのものではない。  
同じ `yyyy\/MM\/dd` をカルチャだけ変えて評価すると次のようになる（.NET 10 での実測値）。  

| `xml:lang` | 表示 |
|---|---|
| `en-US` / `ja-JP` / `de-DE` | `2026/04/15` |
| `th-TH`(仏暦) | `2569/04/15` |
| `ar-SA`(ヒジュラ暦) | `1447/10/27` |
| `fa-IR`(ペルシャ暦) | `1405/01/26` |

暦まで含めて固定したい場合は、バインドに `ConverterCulture` を指定してカルチャ自体を固定する。  

## コードビハインドによる方法

コードビハインドでは、`SelectedDateChanged` イベントを購読してテキストを手動で整形する。

```csharp
using System.Globalization;
using System.Windows.Controls;

private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
{
    if (datePicker.SelectedDate.HasValue)
    {
        datePicker.Text = datePicker.SelectedDate.Value
            .ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
    }
    else
    {
        datePicker.Text = string.Empty;
    }
}
```

`CultureInfo.InvariantCulture` を渡すことで、マシンの地域設定に関わらず区切り文字を固定できる。
これを省いた `ToString("yyyy/MM/dd")` は現在のカルチャを使うため、`/` がそのカルチャの日付区切りに従う。

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

ここで `/` と `:` はリテラルではなく、それぞれ日付区切り・時刻区切りのプレースホルダーであり、ランタイムが現在のカルチャの区切り文字に置き換える(区切りが `.` のカルチャでは `2026.04.15` となる)。
`年` `月` `日` や `-`、`,` などはリテラルとしてそのまま保持される。
マシンの地域設定に関わらず固定レイアウトを保つには、上記のコンバーターのように `ToString` へ `CultureInfo.InvariantCulture` を渡すか、`yyyy'/'MM'/'dd` のように区切りをエスケープする。

## 注意点

- XAML の `StringFormat` 方式が変えるのは表示テキストだけである。基となる `SelectedDate` の値は変わらないため、日付を直接参照するバインドには影響しない。
- `SelectedDateChanged` 方式は `Text` プロパティを手動で上書きする。ユーザーがボックスに入力し直したときも双方向の解析が成立する必要があるため、パーサーが往復変換できない形式は避ける。
- 日付未選択のとき `string.Empty` を返すコンバーターにしておくと、`NullReferenceException` を防げる。

## まとめ

| 方法                  | メリット                      | デメリット                          |
| --------------------- | ----------------------------- | ----------------------------------- |
| Style + StringFormat  | 宣言的・コード不要            | StringFormat の制約がある           |
| SelectedDateChanged   | シンプル・明示的              | コードビハインドに依存              |
| バリューコンバーター  | MVVM フレンドリー・再利用可能 | 本体ではなく併設表示の整形に用いる  |

最適な方法はプロジェクトのアーキテクチャに依存する。
`DatePicker` 本体の表示を変えるには Style + StringFormat かコードビハインドが必要であり、コンバーターは同じ日付を表示する併設表示が複数ある場合に適する。

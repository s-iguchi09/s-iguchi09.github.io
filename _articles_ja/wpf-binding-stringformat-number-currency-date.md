---
layout: article-ja
title: "WPF Binding.StringFormat で数値・通貨・日付を書式化する方法と制約"
date: 2026-07-17
category: WPF
excerpt: "コンバーターを書かずに Binding.StringFormat で数値・通貨・日付を書式化する手法を整理し、カルチャ依存や ContentControl での制約までまとめる。"
---

## 概要

WPF のデータバインディングでは、`double` や `decimal`、`DateTime` を画面へ表示する際に既定の `ToString()` の結果がそのまま出力される。
このため、価格を「1234.5」ではなく「¥1,235」、日付を「2026/07/17 0:00:00」ではなく「2026年7月17日」と表示したい場合、何らかの整形処理が必要になる。
`Binding.StringFormat` は `IValueConverter` を実装せずに、XAML の 1 行で表示専用の書式を指定できる仕組みである。
本記事では、数値・通貨・日付それぞれの書式化手法を実装例とともに示し、カルチャ依存や `ContentControl` での適用制約といった落とし穴を整理する。

---

## 前提・対象環境

- フレームワーク／言語: .NET 8 / C# 12(.NET Framework 3.5 SP1 以降でも `StringFormat` は利用可能)
- 対象コントロール・機能: `TextBlock` / `TextBox` / `Label` / `Button` などのバインディング
- アーキテクチャ: MVVM(ViewModel の数値・日付プロパティを View へ表示)
- 前提知識: `System.String.Format` の複合書式指定文字列と標準／カスタム書式指定子

`Binding.StringFormat` に指定する書式は、`string.Format` に渡すものと同じ書式指定文字列である。
したがって、`C`(通貨)・`N`(数値)・`P`(パーセント)といった標準書式指定子や、`#,0.##` などのカスタム書式指定子がそのまま使える。

---

## 基本構文と 2 つの指定方法

`StringFormat` には「書式指定子のみ」と「複合書式指定文字列」の 2 通りの書き方がある。
値だけを整形する場合は、標準書式指定子を単独で指定できる。
この場合、バインドされた 1 つの値全体にその書式が適用される。

```xml
<!-- 書式指定子のみ: 値全体を通貨として整形する -->
<TextBlock Text="{Binding Price, StringFormat=C}" />
```

固定文字列を前後に付ける場合は、`{0}` をプレースホルダーとする複合書式指定文字列を使う。
`{0:C}` の `C` はプレースホルダー内の書式指定子であり、`string.Format("価格: {0:C}", price)` と同じ意味になる。

```xml
<!-- 複合書式指定文字列: 固定文字列とプレースホルダーを組み合わせる -->
<TextBlock Text="{Binding Price, StringFormat='価格: {0:C}'}" />
```

複合書式指定文字列を単独の `Binding` で使う場合、指定できるプレースホルダーは `{0}` のみである。
複数の値を 1 つの文字列へ組み込む場合は、後述の `MultiBinding` を使う。

---

## 数値の書式化

数値には、桁区切りや小数点以下の桁数を制御する標準書式指定子とカスタム書式指定子が使える。
`N2` は桁区切りありで小数点以下 2 桁、`F0` は小数点以下 0 桁の固定小数点、`P1` はパーセント表記で小数点以下 1 桁を表す。

```xml
<!-- N2: 1234.5 -> 1,234.50 -->
<TextBlock Text="{Binding Quantity, StringFormat=N2}" />

<!-- カスタム書式 #,0.##: 末尾の不要な 0 を省く。カンマを含むため単一引用符で囲む -->
<TextBlock Text="{Binding Ratio, StringFormat='#,0.##'}" />

<!-- P1: 0.153 -> 15.3% -->
<TextBlock Text="{Binding Rate, StringFormat=P1}" />
```

`P`(パーセント)は元の値を 100 倍して表示する点に注意する。
`0.15` を「15%」と表示するため、ViewModel 側では 0〜1 の比率を保持する。
すでに「15」という値を保持している場合は、`P` ではなくカスタム書式で末尾に `%` を付ける。

なお、`#,0.##` のようにカンマを含む書式指定子は、`{Binding ...}` のショートハンド構文ではパラメーターの区切り記号(カンマ)と衝突する。
このため、書式全体を単一引用符で囲み、カンマをリテラルとして扱わせる必要がある。

---

## 通貨の書式化

通貨は標準書式指定子 `C` で表す。
`C` は現在のカルチャの通貨記号・桁区切り・小数点以下桁数を自動的に適用する。
`C0` のように桁数を付けると、小数点以下の桁数を明示できる。

```xml
<!-- C: カルチャの通貨記号を付与する -->
<TextBlock Text="{Binding Price, StringFormat=C}" />

<!-- C0: 小数点以下を表示しない(円などの整数通貨向け) -->
<TextBlock Text="{Binding Price, StringFormat=C0}" />
```

ここで重要な制約がある。
`C` が使う通貨記号は OS のロケールではなく、**バインディングが評価するカルチャ**に従う。
WPF の既定ではこのカルチャが `en-US` になるため、日本語環境でも通貨記号が `¥` ではなく `$` になることがある。
この挙動と対処は後述の「カルチャ依存の制約」で扱う。

---

## 日付・時刻の書式化

`DateTime` には標準日付書式指定子とカスタム書式指定子が使える。
`d` は短い日付、`D` は長い日付、`t` は短い時刻を表す。
特定の並びで表示する場合は、`yyyy/MM/dd` のようなカスタム書式で桁を明示する。

```xml
<!-- d: カルチャに応じた短い日付 -->
<TextBlock Text="{Binding OrderDate, StringFormat=d}" />

<!-- カスタム書式: 固定の並びで表示する -->
<TextBlock Text="{Binding OrderDate, StringFormat='yyyy/MM/dd (ddd)'}" />

<!-- 時刻を含む複合書式 -->
<TextBlock Text="{Binding OrderDate, StringFormat='{}{0:yyyy/MM/dd HH:mm}'}" />
```

カスタム書式では、`MM`(月)と `mm`(分)、`HH`(24 時間制)と `hh`(12 時間制)の違いに注意する。
また、カスタム書式内の `/` は日付区切り、`:` は時刻区切りのプレースホルダーであり、リテラル文字ではない。
これらはカルチャの `DateSeparator` / `TimeSeparator` に置換されるため、区切り記号がカルチャによって `-` や `.` に変わる場合がある。
桁の並びはカスタム書式指定子で固定できるが、区切り記号まで固定するには `\/` のようにエスケープするか、`'/'` のように単一引用符で囲んでリテラル化する。
`ddd`(曜日の省略名)や `dddd`(曜日の完全名)の表記もカルチャに依存するため、日本語で曜日を出したい場合はカルチャ指定が必要になる。
`DatePicker` そのものの表示形式を変える方法は、[DatePicker の表示形式をカスタマイズする方法](/ja/articles/wpf-datepicker-custom-format/)で扱っている。

---

## MultiBinding による複数値の書式化

複数のプロパティを 1 つの文字列に組み込むには、`MultiBinding` の `StringFormat` を使う。
`StringFormat` は `MultiBinding` 自体に設定した場合にのみ有効で、子 `Binding` に設定した `StringFormat` は無視される。
プレースホルダーの数は子 `Binding` の数を超えてはならない。

```xml
<TextBlock>
  <TextBlock.Text>
    <MultiBinding StringFormat="{}{0:C} / {1:yyyy年M月d日}">
      <Binding Path="Price" />
      <Binding Path="OrderDate" />
    </MultiBinding>
  </TextBlock.Text>
</TextBlock>
```

`MultiBinding` を使うと、単独の `Binding` では 1 つに限られていたプレースホルダーを `{0}` `{1}` … と複数並べられる。
それぞれの子 `Binding` の値が、対応する番号のプレースホルダーへ順に割り当てられる。

---

## カルチャ依存の制約

`Binding.StringFormat` の最大の落とし穴は、書式化に使うカルチャが OS の地域設定ではないことである。
バインディングは `Binding.ConverterCulture` を使い、これが未設定(既定 `null`)の場合はバインディング先要素の `Language` プロパティを参照する。
XAML では `Language` の既定値が `en-US` であるため、日本語環境でも通貨が `$`、日付が `M/d/yyyy` 形式になる。
対処は主に次の 2 つである。

1 つ目は、個別のバインディングに `ConverterCulture` を指定する方法である。
表示単位でカルチャを固定したい場合に適する。

```xml
<!-- このバインディングだけ日本語カルチャで書式化する -->
<TextBlock Text="{Binding Price, StringFormat=C, ConverterCulture=ja-JP}" />
```

2 つ目は、アプリ全体の既定カルチャを合わせる方法である。
起動時に `FrameworkElement` の `Language` メタデータを現在のカルチャで上書きすると、以降のすべてのバインディングへ反映される。

```csharp
// App 起動時に一度だけ実行する
FrameworkElement.LanguageProperty.OverrideMetadata(
    typeof(FrameworkElement),
    new FrameworkPropertyMetadata(
        XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
```

アプリ全体を一貫したカルチャで表示するなら 2 つ目が適する。
一部の値だけ別カルチャで表示する必要がある場合は、1 つ目の `ConverterCulture` を併用する。

---

## ContentControl での制約

`Label` や `Button` などの `ContentControl` 派生コントロールでは、`Content` プロパティの型が `object` である。
`Binding.StringFormat` は表示先プロパティが `string` 型のときにのみ機能するため、`Content` へバインドしても `StringFormat` は無視される。
これらのコントロールでは、代わりに `ContentStringFormat` プロパティを使う。

```xml
<!-- Label: StringFormat ではなく ContentStringFormat を使う -->
<Label Content="{Binding Price}" ContentStringFormat="C" />

<!-- TextBlock.Text は string 型なので StringFormat がそのまま効く -->
<TextBlock Text="{Binding Price, StringFormat=C}" />
```

`ContentStringFormat` は書式指定子・複合書式指定文字列のいずれも受け付ける。
ただし `ContentTemplate` または `ContentTemplateSelector` を設定している場合、`ContentStringFormat` は無視される。
`TextBlock.Text` や `TextBox.Text` は `string` 型のため、これらでは `StringFormat` がそのまま適用できる。

---

## 波括弧のエスケープ

書式指定文字列が `{` で始まると、XAML パーサーがマークアップ拡張の開始と誤認する。
これを避けるため、文字列の先頭に空の波括弧 `{}` を付けてエスケープする。
この `{}` は、書式全体を単一引用符で囲んでも省略できない。
エスケープの要否は「書式が `{` で始まるかどうか」だけで決まり、引用符の有無は影響しない。

```xml
<!-- 先頭が { のため {} でエスケープする -->
<TextBlock Text="{Binding OrderDate, StringFormat={}{0:yyyy/MM/dd}}" />

<!-- 単一引用符で囲む場合も、先頭が { なら {} は必要 -->
<TextBlock Text="{Binding OrderDate, StringFormat='{}{0:yyyy/MM/dd}'}" />
```

固定文字列が先頭にある `StringFormat='価格: {0:C}'` のようなケースでは、先頭が `{` ではないため `{}` は不要である。
単一引用符は、カンマや空白などマークアップ拡張の区切り文字を書式へ含めるための仕組みであり、先頭 `{` のエスケープとは役割が異なる。
書式が `{` で始まり、かつカンマや空白を含む場合は、`{}` と単一引用符の両方を併用する。

---

## 注意点

- `StringFormat` は表示先プロパティが `string` 型のときにのみ機能する。`ContentControl` の `Content`(object 型)には `ContentStringFormat` を使う。
- 双方向バインディングでも `StringFormat` はソース→ターゲット方向にのみ適用される。ユーザー入力(ターゲット→ソース)の解析には影響しない。
- `Converter` と `StringFormat` を併用した場合、先に `Converter` が適用され、その結果へ `StringFormat` が適用される。
- 書式化に使うカルチャは既定で `en-US` であり、OS の地域設定ではない。通貨記号・日付並びが想定と異なる場合はカルチャを疑う。
- 単独の `Binding` の複合書式で使えるプレースホルダーは `{0}` のみである。複数値は `MultiBinding` を使う。

---

## まとめ

`Binding.StringFormat` は、コンバーターを書かずに数値・通貨・日付を表示用に整形できる最も軽量な手段である。
表示先が `string` 型で、値を 1 つ整形するだけなら第一候補になる。
一方、`Label` や `Button` では `ContentStringFormat`、複数値の結合では `MultiBinding.StringFormat`、値そのものの変換や複雑な条件分岐が必要なら `IValueConverter` を選ぶ。
用途ごとの選択基準を次の表にまとめる。

| 方法 | 適用対象 | 複数値 | 適するケース |
|---|---|---|---|
| `Binding.StringFormat` | `string` 型プロパティ(`TextBlock.Text` など) | 不可(`{0}` のみ) | 単一の数値・通貨・日付を表示用に整形する |
| `ContentStringFormat` | `ContentControl.Content`(`Label` / `Button`) | 不可 | `Content` にバインドした値を書式化する |
| `MultiBinding.StringFormat` | `string` 型プロパティ | 可(`{0}` `{1}` …) | 複数プロパティを 1 つの文字列に組み込む |
| `IValueConverter` | 任意の型 | コンバーター次第 | 条件分岐・型変換・双方向の解析を伴う |

いずれの方法でも、書式化に使うカルチャが既定で `en-US` である点は共通の注意事項である。
通貨・日付を地域に合わせて表示する場合は、`ConverterCulture` またはアプリ全体の `Language` メタデータ上書きでカルチャを明示する。

---

<!-- 関連記事 -->
<!-- - [WPF DatePicker の表示形式をカスタマイズする方法](/ja/articles/wpf-datepicker-custom-format/) -->

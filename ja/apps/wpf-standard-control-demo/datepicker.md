---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/datepicker.html
title: "DatePicker"
badge: "Inputs"
lead: "DatePicker は、日付を入力するテキスト欄と、カレンダーのポップアップを開くボタンを組み合わせたコントロールです。選んだ日付は null 許容の <code>DateTime</code> です。"
description: "WPF の DatePicker を .NET 10 で実測して解説します。DisplayDateStart / End は範囲外の日付の入力を止めないこと、不正な入力は元の日付に戻ること、IsTodayHighlighted などの既定値がスタイル由来であることを確かめます。"
---

## 値は null 許容の日付：DateTime? にバインドする

**DatePicker** の値は `SelectedDate` で、型は `DateTime?` です。日付を選んでいないときは `null` になり、既定で TwoWay にバインドされます。テキスト欄はこの値を文字列で表示し、日付を入力して Enter キーを押すと値が設定されます。テキスト欄を空にして Enter キーを押すと `null` になりました。ユーザーが選ぶまで空にしておく期日などは、`DateTime?` のプロパティにバインドします。`DateTime` のプロパティにバインドした状態で `null` にすると、バインドエラーになりました。ソースは元の日付のままで、DatePicker の `Validation.HasError` が `True` になりました。

`DisplayDate` はカレンダーに表示する月です。新しく作った DatePicker では今日の日付でした。`SelectedDate` を 2026-04-15 にすると、`DisplayDate` も同じ日に移りました。その後で `DisplayDate` を変えても `SelectedDate` は変わりませんでした。

カレンダーのポップアップは `Calendar` コントロールです。`IsDropDownOpen` はポップアップが開いているかどうかを表し、既定で TwoWay にバインドされます。ウィンドウを表示する前に `True` にしても例外は出ず、ウィンドウが表示された時点でポップアップは開いていました。

## 範囲が制限するのはカレンダーだけ

`DisplayDateStart` と `DisplayDateEnd` は、カレンダーで選べる最初と最後の日付です。2026-04-10〜2026-04-20 の範囲では、ポップアップの 04-05 の日付ボタンは無効になりました。範囲が効くのはここだけです。コードから `SelectedDate` を 04-05 にしても、`Text` に "4/5/2026" を設定しても、テキスト欄に "4/5/2026" と入力して Enter キーを押しても、日付は 04-05 になりました。例外も `DateValidationError` も発生しませんでした。範囲の検証は ViewModel で行います。

選択中の日付より後の `DisplayDateStart` も効きません。04-10 を選んだ状態で `DisplayDateStart = 04-15` とすると、04-10 に引き戻されました。そのため、開始日と終了日の 2 つの DatePicker で、終了日側の `DisplayDateStart` を開始日にバインドしても、終了日側にすでにそれより前の日付が入っていると効きません。2 つの日付の前後は ViewModel で確かめます。

選ばせたくない日は `BlackoutDates` に入れます。範囲と違い、BlackoutDates は守られます。コードから該当日を `SelectedDate` に設定すると `ArgumentOutOfRangeException` が発生して元の日付のままになり、ポップアップではその日が選択不可（IsBlackedOut）として示されました。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/datepicker/datepicker-range-input.svg" alt="2026 年 4 月 10 日から 20 日の範囲を設定した DatePicker の結果を示す表。コード・Text・入力と Enter で与えた範囲外の日付は受け付けられ、解析できない入力は DateValidationError を発生させて元の日付に戻り、範囲外の日付ボタンは無効になり、BlackoutDates の日付をコードから設定すると ArgumentOutOfRangeException が発生し、選択中の日付より後の DisplayDateStart は引き戻される" width="1226" height="560" loading="lazy">
  <figcaption><code>xml:lang="en-US"</code> での範囲、BlackoutDates、入力の結果。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## Text の設定と、テキスト欄への入力の違い

ここでの日付の解析と表示には `xml:lang="en-US"` を指定しました。`Text` は日付の文字列です。`SelectedDate` と違い、既定では TwoWay にバインドされません。設定するとすぐに DatePicker のカルチャで解析され、"4/15/2026"・"2026-04-15"・"April 15, 2026" はいずれも 2026-04-15 になり、`Text` は "4/15/2026" に変わりました。解析できない文字列（en-US での "15/4/2026"）を設定すると、`SelectedDate` は `null` になりました。

テキスト欄に入力した場合は動きが異なり、"abc" と入力して Enter キーを押すと `DateValidationError` が発生し、日付も文字列も元のまま戻りました。入力が受け付けられなかったことを伝えるには、`DateValidationError` を処理します。処理しないと、テキスト欄は何も知らせずに元の日付へ戻ります。

## スタイルとカルチャで決まる既定値

既定値のうち 2 つは既定のスタイルから与えられ、プロパティのメタデータと食い違います。`IsTodayHighlighted` はメタデータでは `False` ですが、既定のスタイルが `True` を設定するため、無効にしない限り今日には印が付きます。`False` にすると、日付ボタンの表示状態が `Today` から `RegularDay` に変わり、今日の印が透明になりました。ボタンは有効なままで、今日の日付は選べます。

`SelectedDateFormat` の値は `Short` と `Long` の 2 つだけです。メタデータでは `Long` で、実際の既定値は既定のスタイルが設定する `Short` です。カルチャごとの表示と、これ以外の形式で表示する方法は、[DatePicker の表示形式をカスタマイズする方法](/ja/articles/wpf-datepicker-custom-format/)で実測しています。`SelectedDate` が保持するのは文字列ではなく `DateTime` なので、記事では DatePicker のテキスト部分の側で形式を変えています。

カレンダーの左端の列の曜日である `FirstDayOfWeek` は、ポップアップの `Calendar` にも同じ値が渡りました。新しく作った DatePicker は、スレッドの現在のカルチャから値を取ります。en-US と ja-JP では `Sunday`、de-DE と fr-FR では `Monday` でした。ユーザーのカルチャと違う曜日から始めたいときだけ、明示的に設定します。

## デモアプリで試す

![デモアプリの DatePicker のページ。左にコントロールの一覧、右に最初の節の DisplayDate](/images/wpf-standard-control-demo/datepicker.png){: .screenshot-img}

デモアプリの DatePicker のページには、`SelectedDate`、`DisplayDate`、`DisplayDateStart` と `DisplayDateEnd`、`FirstDayOfWeek`、`IsDropDownOpen`、`IsTodayHighlighted`、`SelectedDateFormat`、`Text` の欄があります。`DisplayDate` の欄では、別の DatePicker で選んだ日付（今日の 1 か月後）を `DisplayDate` にバインドしています。範囲の欄は直前の月曜日から次の金曜日までで、`IsDropDownOpen` の欄のチェックボックスはポップアップを開閉するだけでなく、ポップアップの状態にも追従します。`Text` の欄では、テキストボックスから OneWay でバインドしています。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`DisplayDate Range (Start/End)` の欄（`DatePickerUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。`DateTimeAdvancedConverter` はデモアプリのコンバーターで、今日から直前の月曜日と次の金曜日を求めます。`converters` はデモアプリ独自のコンバーターの接頭辞です。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:sys="clr-namespace:System;assembly=mscorlib"
            xmlns:converters="clr-namespace:WPFStandardControlDemoApp.Common.Converters">
  <DatePicker x:Name="DisplayDateStartSourceDatePicker"
              SelectedDate="{Binding Source={x:Static sys:DateTime.Now},
                                     Converter={converters:DateTimeAdvancedConverter Operation=Previous, TargetDay=Monday},
                                     Mode=OneTime}" />
  <DatePicker x:Name="DisplayDateEndSourceDatePicker"
              SelectedDate="{Binding Source={x:Static sys:DateTime.Now},
                                     Converter={converters:DateTimeAdvancedConverter Operation=Next, TargetDay=Friday},
                                     Mode=OneTime}" />

  <DatePicker x:Name="DisplayDateRangeDatePicker"
              DisplayDateEnd="{Binding SelectedDate, ElementName=DisplayDateEndSourceDatePicker}"
              DisplayDateStart="{Binding SelectedDate, ElementName=DisplayDateStartSourceDatePicker}"
              SelectedDate="{x:Static sys:DateTime.Now}" />
</StackPanel>
```

## 関連するコントロールと記事

- [DatePicker の表示形式をカスタマイズする方法](/ja/articles/wpf-datepicker-custom-format/) — カルチャごとの `SelectedDateFormat` の表示の実測と、独自の形式で表示する方法を扱います。
- [TextBox](/ja/apps/wpf-standard-control-demo/textbox.html) — DatePicker のテキスト部分は TextBox の派生の `DatePickerTextBox` です。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`DatePickerDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/DatePickerDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。入力は、DatePicker のテキスト部分に文字列を設定して Enter キーのイベントを送って再現し、カレンダーはポップアップを開いて日付ボタンの状態を読みました。

[GitHub で DatePicker のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/DatePickerUsage){: target="_blank" rel="noopener noreferrer"}

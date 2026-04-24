---
layout: article-ja
title: "DatePicker の表示形式をカスタマイズする方法"
date: 2026-04-15
category: WPF
excerpt: "XAML とコードビハインドの両面から、WPF DatePicker の日付表示形式を変更する方法をまとめます。"
---

## 概要

WPF の `DatePicker` は、選択された日付をシステムのロケール形式（例: `2026/04/15`）で表示します。アプリの要件に合わせて `yyyy年MM月dd日` や `MM/dd/yyyy` などの任意フォーマットに変更したい場合は、いくつかのアプローチがあります。本記事では XAML スタイル、コードビハインド、バリュー コンバーターの 3 つの方法を比較します。

## XAML スタイルによる方法

`DatePickerTextBox` の `Text` プロパティに `StringFormat` を指定するスタイルを `DatePicker.Resources` に追加します。

```xml
<DatePicker x:Name="datePicker" SelectedDate="{Binding SelectedDate}">
  <DatePicker.Resources>
    <Style TargetType="DatePickerTextBox">
      <Setter Property="Text"
              Value="{Binding SelectedDate,
                              RelativeSource={RelativeSource AncestorType=DatePicker},
                              StringFormat='yyyy/MM/dd'}" />
    </Style>
  </DatePicker.Resources>
</DatePicker>
```

シンプルで宣言的な方法ですが、フォーマット文字列の表現力に限定されます。

## コードビハインドによる方法

`SelectedDateChanged` イベントを購読し、テキストを手動で設定します。

```csharp
private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
{
    if (datePicker.SelectedDate.HasValue)
    {
        datePicker.Text = datePicker.SelectedDate.Value.ToString("yyyy年MM月dd日");
    }
}
```

記述が直感的ですが、コードビハインドへの依存が生まれます。

## バリュー コンバーターによる方法

MVVM パターンを採用しているプロジェクトでは、コンバーターが最も再利用性が高いアプローチです。

```csharp
public class DateFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is DateTime d ? d.ToString("yyyy/MM/dd") : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DateTime.TryParse(value as string, out var d) ? d : DependencyProperty.UnsetValue;
}
```

XAML では以下のようにバインドします。

```xml
<DatePicker SelectedDate="{Binding SelectedDate,
                           Converter={StaticResource DateFormatConverter}}" />
```

## 方法の比較

| 方法 | メリット | デメリット |
|---|---|---|
| Style + StringFormat | 宣言的・コード不要 | StringFormat の制約がある |
| SelectedDateChanged | シンプル・わかりやすい | コードビハインドに依存 |
| バリュー コンバーター | MVVM フレンドリー・再利用可能 | クラス定義が必要 |

新規 MVVM プロジェクトではコンバーター方式を選ぶと、複数箇所への適用や単体テストがしやすくなります。

## まとめ

`DatePicker` の日付表示形式を変更するには、スタイル・コードビハインド・コンバーターの 3 通りの方法があります。プロジェクトのアーキテクチャと必要な再利用性に応じて最適な方法を選択してください。

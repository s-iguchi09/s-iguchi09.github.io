---
layout: article-ja
title: "WPF Fluent テーマの TextBox でクリアボタンを非表示にする方法"
date: 2026-07-19
category: WPF
excerpt: "Fluent テーマの TextBox がフォーカス時に表示するクリアボタンを、入力挙動を変えずに非表示化する。名前付きパーツを操作する方法と AcceptsReturn を使う方法を .NET 10・9 対応で整理する。"
---

## 概要

Fluent テーマを適用した WPF の `TextBox` は、編集可能で単一行の入力欄にテキストがある状態でフォーカスが入ると、テキスト右端にクリアボタン(×)を表示する。
この既定挙動は入力補助として有効だが、独自のクリア操作を別に備えた検索欄やフィルター欄では不要であり、同じ役割のボタンが二重に並んで見える。
本記事では、テンプレートの入力挙動を実質的に変えずにこのクリアボタンだけを非表示にする方法を、`.NET 10` を主対象として整理する。
アプローチは 2 つある。
1 つはテンプレート内のボタン要素(名前付きパーツ)を直接非表示にする方法、もう 1 つは `AcceptsReturn` プロパティの非表示トリガーを利用する方法である。
`.NET 9` ではパーツ名が異なるため、その差分と対応コードも併記する。

---

## 前提・対象環境

- フレームワーク／言語: `.NET 10` を主対象(`.NET 9` の差分も記載) / C# 13
- 対象コントロール: WPF `TextBox`(Fluent テーマ適用時)
- テーマ: `PresentationFramework.Fluent`(`ThemeMode` または `Fluent.xaml` のマージ)
- アーキテクチャ: MVVM / コードビハインドのいずれにも適用可能

---

## 問題

Fluent テーマの `TextBox` は、キーボードフォーカスが入るとテンプレート内のクリアボタンを自動表示する。
標準テーマ(Aero2)には存在しない要素のため、テーマを切り替えた後に意図せず現れる。
既に「×」ボタンやコマンドで入力値をクリアする UI を用意している画面では、同一機能のボタンが重複し、レイアウトの一貫性が損なわれる。

---

## 原因・背景

このクリアボタンは、Fluent テーマの `TextBox` コントロールテンプレートに名前付きパーツとして定義されている。
パーツ名はバージョンで異なり、`.NET 10` では `DeleteButton`、`.NET 9` では `ClearButton` である(`.NET 9` から `.NET 10` への更新でボタン要素が改名された)。
`.NET 10` のテンプレートでは、このボタンの既定の `Visibility` が `Collapsed` で、`IsKeyboardFocusWithin` が `true` のときだけ表示するトリガーを持つ。
このためフォーカスが外れているときは、既定値のまま非表示となる。
加えて、`Text` が空・`IsReadOnly` のとき、および `AcceptsReturn=True`・`TextWrapping` が `Wrap` または `WrapWithOverflow` のときに非表示とするトリガーが定義されている。
一方 `.NET 9` のテンプレート(パーツ名 `ClearButton`)には `AcceptsReturn`・`TextWrapping` のトリガーは無く、フォーカスが外れているときは `IsKeyboardFocusWithin` が `false` のトリガーで非表示にする。
このため、クリアボタンだけを消す公開プロパティは用意されておらず、パーツを直接操作するか、上記トリガーの非表示条件を満たすかのいずれかを取る。

---

## 解決方法

前述のとおり、アプローチは 2 系統ある。

- **方法 1: 名前付きパーツを直接非表示にする。**
  パーツ(`.NET 10` は `DeleteButton`、`.NET 9` は `ClearButton`)の `Visibility` にローカル値で `Collapsed` を設定する。
  WPF の依存関係プロパティは値の優先順位が定義されており、ローカル値はスタイルやテンプレートのトリガーより優先される。
  このためフォーカスが入ってトリガーが `Visible` を設定しようとしても、ローカル値の `Collapsed` が勝ち、非表示が維持される。
  メリットは表示を直接制御できることであり、トリガー条件やプロパティ値に依存せず確実に消せる。
  一方でテンプレートの内部パーツ名に依存する。

- **方法 2: `AcceptsReturn` プロパティの非表示トリガーを利用する。**
  `AcceptsReturn=True` を設定してテンプレートの非表示トリガー(`.NET 10` で追加)を成立させ、クリアボタンを消す。
  メリットは公開プロパティに依存するため、将来テンプレートのパーツ名が変わっても影響を受けにくいことである。
  一方で単一行の `TextBox` が複数行入力に変わる副作用があり、`.NET 9` ではこのトリガーが無いため効かない。

いずれもパーツ取得には `Template.FindName` を用いる。
一連の操作を添付プロパティにまとめることで、XAML から属性 1 つを付けるだけで宣言的に適用できる。

---

## 実装例

### 方法 1: 名前付きパーツを非表示にする(`.NET 10`・`.NET 9` 両対応)

添付プロパティ `HideClearButton` を定義し、`True` が設定されたらクリアボタンのパーツを `Collapsed` にする。
パーツ名はバージョンで異なるため、`DeleteButton`(`.NET 10`)と `ClearButton`(`.NET 9`)の両方を順に探索してフォールバックする。
プロパティ変更時点ではテンプレート未適用の場合があるため、未読み込みなら `Loaded` を待ってから処理する。
`Loaded` の購読は `WeakEventManager` による弱参照とし、ハンドラーが `TextBox` の生存を延ばさないようにする。

```csharp
using System.Windows;
using System.Windows.Controls;

public static partial class TextBoxHelper
{
    // .NET 10 は "DeleteButton"、.NET 9 は "ClearButton"。実行環境に応じてフォールバックする。
    private static readonly string[] ClearButtonPartNames = ["DeleteButton", "ClearButton"];

    public static bool GetHideClearButton(DependencyObject obj) =>
        (bool)obj.GetValue(HideClearButtonProperty);

    public static void SetHideClearButton(DependencyObject obj, bool value) =>
        obj.SetValue(HideClearButtonProperty, value);

    public static readonly DependencyProperty HideClearButtonProperty =
        DependencyProperty.RegisterAttached(
            "HideClearButton",
            typeof(bool),
            typeof(TextBoxHelper),
            new FrameworkPropertyMetadata(false, OnHideClearButtonChanged));

    private static void OnHideClearButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox || !(bool)e.NewValue)
        {
            return;
        }

        if (textBox.IsLoaded)
        {
            HideClearButtonPart(textBox);
        }
        else
        {
            // 多重登録防止のため一度解除してから登録する。
            WeakEventManager<FrameworkElement, RoutedEventArgs>.RemoveHandler(textBox, nameof(FrameworkElement.Loaded), OnLoaded);
            WeakEventManager<FrameworkElement, RoutedEventArgs>.AddHandler(textBox, nameof(FrameworkElement.Loaded), OnLoaded);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            WeakEventManager<FrameworkElement, RoutedEventArgs>.RemoveHandler(textBox, nameof(FrameworkElement.Loaded), OnLoaded);
            HideClearButtonPart(textBox);
        }
    }

    private static void HideClearButtonPart(TextBox textBox)
    {
        textBox.ApplyTemplate();

        foreach (string partName in ClearButtonPartNames)
        {
            if (textBox.Template?.FindName(partName, textBox) is UIElement clearButton)
            {
                clearButton.Visibility = Visibility.Collapsed;
            }
        }
    }
}
```

`ApplyTemplate` でテンプレートの適用を強制してからパーツを取得している。
`Visibility` をローカル値で設定しているため、フォーカス変化でトリガーが再評価されても非表示が保たれる。
`AcceptsReturn` を変更しないので、Enter キーやペーストの挙動には一切影響しない。

XAML 側では、対象の `TextBox` に添付プロパティを付与するだけでよい。

```xml
<TextBox xmlns:helper="clr-namespace:MyApp.Helpers"
         helper:TextBoxHelper.HideClearButton="True"
         Text="{Binding Keyword, UpdateSourceTrigger=PropertyChanged}" />
```

`xmlns:helper` は添付プロパティ `TextBoxHelper` を定義したクラスの名前空間(`clr-namespace`)を指す。
実際に定義した名前空間に合わせて置き換える。

### 方法 2: `AcceptsReturn` の非表示トリガーを利用する(`.NET 10` 以降)

パーツ名に依存しない方法として、`AcceptsReturn=True` を設定してテンプレートの非表示トリガーを成立させる。
このままでは単一行の `TextBox` が複数行入力に変わるため、Enter による改行入力を抑止し、貼り付け時の改行を除去して単一行の挙動を保つ。
以下は、これらをまとめて有効化する添付プロパティ `SingleLineHideClear` の実装である。

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

public static partial class TextBoxHelper
{
    public static bool GetSingleLineHideClear(DependencyObject obj) =>
        (bool)obj.GetValue(SingleLineHideClearProperty);

    public static void SetSingleLineHideClear(DependencyObject obj, bool value) =>
        obj.SetValue(SingleLineHideClearProperty, value);

    public static readonly DependencyProperty SingleLineHideClearProperty =
        DependencyProperty.RegisterAttached(
            "SingleLineHideClear",
            typeof(bool),
            typeof(TextBoxHelper),
            new FrameworkPropertyMetadata(false, OnSingleLineHideClearChanged));

    private static void OnSingleLineHideClearChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            // AcceptsReturn を SetCurrentValue で設定し、.NET 10 の非表示トリガーを成立させる。
            textBox.SetCurrentValue(TextBox.AcceptsReturnProperty, true);
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            DataObject.AddPastingHandler(textBox, OnPasting);
        }
        else
        {
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
            DataObject.RemovePastingHandler(textBox, OnPasting);
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Enter による改行入力を抑止し、単一行の見た目を保つ。
        if (e.Key is Key.Enter)
        {
            e.Handled = true;
        }
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            return;
        }

        string text = (string)e.SourceDataObject.GetData(DataFormats.UnicodeText);
        if (text.Contains('\n') || text.Contains('\r'))
        {
            // 貼り付け文字列の改行を空白へ置換してから貼り付ける。
            string singleLine = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
            DataObject data = new();
            data.SetData(DataFormats.UnicodeText, singleLine);
            e.DataObject = data;
        }
    }
}
```

`AcceptsReturn=True` の非表示トリガーはフォーカスの表示トリガーより後に宣言されており、両方が同時に成立した場合は後に宣言されたトリガーが優先されるため、フォーカス中でもクリアボタンは表示されない。
Enter の抑止と貼り付け改行の除去により、見た目と入力は単一行のまま保たれる。
`AcceptsReturn` は `SetCurrentValue` で設定しているため、`AcceptsReturn` にバインディングやスタイルが指定されていても、それらをローカル値で上書きしない。
このトリガーは `.NET 10` で追加されたものであり、`.NET 9` では非表示にならない点に注意する。

XAML 側では、対象の `TextBox` に `SingleLineHideClear` を付与する。

```xml
<TextBox xmlns:helper="clr-namespace:MyApp.Helpers"
         helper:TextBoxHelper.SingleLineHideClear="True"
         Text="{Binding Keyword, UpdateSourceTrigger=PropertyChanged}" />
```

方法 1 と同じく、`xmlns:helper` は `TextBoxHelper` を定義したクラスの名前空間を指す。

---

## 注意点

- 方法 1 はテンプレートの内部パーツ名に依存する。実際に `.NET 9`(`ClearButton`)から `.NET 10`(`DeleteButton`)でパーツ名が変わっており、将来の更新で再び構造が変わればパーツが見つからずクリアボタンが再表示される(例外は発生せず、非表示化が効かないだけのグレースフルな劣化となる)。
- 方法 2 の非表示トリガーは `.NET 10` で追加されたものであり、`.NET 9` では `AcceptsReturn=True` を設定してもクリアボタンは消えない。`.NET 9` を対象に含める場合は方法 1 を用いる。
- 方法 2 は `AcceptsReturn=True` により内部的には複数行入力となる。Enter の抑止や貼り付け改行の除去で単一行を保つが、IME や複数行貼り付けの扱いはアプリの要件に応じて追加検証する。
- コントロールテンプレートを全面差し替えした `TextBox` にはクリアボタンのパーツが存在しない場合がある。その際は差し替えたテンプレート内でボタン要素自体を除く。
- 上記実装は `True` 設定時に非表示化するのみで、動的に再表示へ戻す処理は含まない。実行時にオン・オフを切り替える要件があれば、`Visibility` やハンドラー登録を元に戻す分岐を追加する。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
|---|---|---|---|
| 名前付きパーツを Collapse(方法 1) | 表示を直接制御でき確実、`.NET 9`・`.NET 10` 両対応が容易、入力挙動へ副作用がない | 内部パーツ名に依存する(バージョンで改名の実績あり) | 単一行のまま確実に非表示にしたい通常のケース |
| `AcceptsReturn` トリガー利用(方法 2) | 公開プロパティ依存でパーツ名の変更に強い | 複数行化の副作用の打ち消しが必要、`.NET 9` では効かない | パーツ名依存を避けたい `.NET 10` 以降のケース |
| コントロールテンプレート全面差し替え | 構造を完全に制御できる | 記述量が多く保守コストが高い | テーマを大きくカスタムする場合 |

---

## まとめ

Fluent テーマの `TextBox` のクリアボタンを消すには、対象パーツ(`.NET 10` は `DeleteButton`、`.NET 9` は `ClearButton`)にローカル値で `Visibility=Collapsed` を設定する方法 1 が、入力挙動を保ったまま確実で、両バージョンにも対応しやすい。
パーツ名の変更に左右されにくくしたい場合は、`AcceptsReturn` の非表示トリガーを使う方法 2 を選ぶが、これは `.NET 10` 以降に限られ、複数行化の副作用を打ち消す必要がある。
単一行の検索欄・フィルター欄など大半のケースでは方法 1 を既定とし、パーツ名依存を避けたい `.NET 10` 以降の設計でのみ方法 2 を検討し、テーマ全体を再設計するならテンプレート差し替えを選ぶのが妥当である。

---

## 関連記事

- [WPF で Fluent デザインを追加ライブラリなしで適用する方法](/ja/articles/wpf-fluent-design-with-systemcolors/)

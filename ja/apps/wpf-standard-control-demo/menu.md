---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/menu.html
title: "Menu"
badge: "Menu"
lead: "Menu は、見出しを並べたバーで、見出しごとにコマンドの一覧を開きます。各項目は MenuItem で、項目自身がサブメニューを持てます。"
description: "WPF の Menu を .NET 10 で実測して解説。項目の Role、実際のクリックでのチェックと StaysOpenOnClick、Alt・F10・アクセスキーでメインメニューに入るか、InputGestureText、コマンドの項目の IsEnabled を確かめます。"
---

## 概要

**Menu** は `MenuItem` を持ち、各 MenuItem の `Role` は、置かれた位置と子の有無で決まります。デモアプリの Role の欄の 4 項目は、`TopLevelHeader`・`SubmenuHeader`・`SubmenuItem`・`TopLevelItem` でした。Role は子に応じて変わり、`TopLevelItem` に子を追加すると `TopLevelHeader` になりました。

サブメニューの項目の `IsEnabled` は、メニューが閉じている間は最新ではありません。TextBox にフォーカスがあり何も選択していないとき、`Copy` の項目の `IsEnabled` は、コマンドの `CanExecute` が `False` を返すのに `True` でした。実際のクリックでメニューを開くと、項目は無効になりました。

デモアプリには、`IsMainMenu`、`IsCheckable` と `IsChecked`・`StaysOpenOnClick`、`Role`、`Command`、`Icon` と `InputGestureText` の欄と、`IsHighlighted`・`IsPressed`・`IsSubmenuOpen`・`IsSuspendingPopupAnimation` を表示する欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。

## 画面キャプチャ

![menu demo screen](/images/wpf-standard-control-demo/menu.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `IsMainMenu (Menu)` | `bool` | <kbd>Alt</kbd> を押したときにメニューへ入るかどうかで、既定値は `True` です。TextBox にフォーカスがある状態で実際に <kbd>Alt</kbd> を押すと、`True` では最初の見出しが強調されてフォーカスが移り、`False` では何も起きませんでした。ドキュメントは `IsMainMenu` を <kbd>Alt</kbd> と <kbd>F10</kbd> の通知を受けるかどうかと説明しています。日本語入力を使う計測したマシンでは、TextBox にフォーカスがある状態で実際に <kbd>F10</kbd> を押すと、どちらでも何も起きませんでした。`True` で、Button にフォーカスがある状態の実際の <kbd>F10</kbd> と、WPF の入力管理を通して TextBox に送った <kbd>F10</kbd> では、どちらもメニューに入りました。違いの原因として、<kbd>F10</kbd> を自分でも使う日本語入力が考えられますが、入力方式をオフにして測ってはいないので、確かめていません。アクセスキーはどちらでも働き、<kbd>Alt</kbd>+<kbd>M</kbd> は `False` でもデモアプリの「Main Menu(\_M)」を開きました。 |
| `IsCheckable` | `bool` | クリックで `IsChecked` を切り替えるかどうかで、既定値は `False` です。実際にクリックすると項目にチェックが付き、もう一度クリックすると外れました。`IsCheckable` でなければ、クリックしてもチェックは付きませんでした。チェックできる項目どうしは排他ではなく、同じサブメニューの 2 項目をクリックすると両方にチェックが付きました。 |
| `IsChecked` | `bool` | チェックマークを表示するかどうかです。デモアプリはチェックボックスを TwoWay でバインドしており、チェックボックスはクリックに追従し、コードでチェックボックスにチェックを付けると項目にもチェックが付きました。 |
| `StaysOpenOnClick` | `bool` | 項目をクリックした後もサブメニューを開いたままにするかどうかで、既定値は `False` です。実際にクリックすると、`False` ではサブメニューが閉じ、`True` では開いたままでした。デモアプリは `IsCheckable` と `StaysOpenOnClick` の両方を `True` で始めます。 |
| `Role` | `MenuItemRole (ReadOnly)` | 項目の種類で、バーの見出しかサブメニューの項目か、子があるかどうかで決まります（上を参照）。 |
| `Command` | `ICommand` | 項目が実行するコマンドです。デモアプリは `ApplicationCommands.Copy` を使っています。実際のクリックで開くと、TextBox で何も選択していなければ無効、全体を選択していれば有効、Button にフォーカスがあれば無効でした。メニューを開いている間、キーボードフォーカスは MenuItem にありましたが、項目は TextBox の選択に従いました。 |
| `Icon / InputGestureText` | `object / string` | 左に出す画像と、右に出すショートカットの文字です。`InputGestureText` は文字だけで、デモアプリの「Ctrl+O」は表示されましたが、実際に <kbd>Ctrl</kbd>+<kbd>O</kbd> を押しても項目はクリックされませんでした。キーの組み合わせを持つコマンドでは自動で入り、`Copy` の項目は設定しなくても `Ctrl+C` でした。 |
| `IsHighlighted / IsPressed / IsSubmenuOpen / IsSuspendingPopupAnimation` | `bool (IsSubmenuOpen 以外は ReadOnly)` | 見出しの状態で、デモアプリの最後の欄に表示されます。実際のマウスを重ねると `IsHighlighted` が `True` になりました。ボタンを押すと `IsPressed` が `True` になってサブメニューが開き、離すと `IsPressed` だけが戻りました。`IsSuspendingPopupAnimation` は、サブメニューが開くと `True` になりました。見出しが 2 つあるとき、1 つ目を開いたまま 2 つ目にマウスを重ねると、1 つ目が閉じて 2 つ目が開きました。 |

## XAML 使用例

デモアプリ（`MenuUsageControl.xaml`）の `IsCheckable` の欄から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CheckBox x:Name="IsCheckableCheckBox" Content="IsCheckable" IsChecked="True" />
  <CheckBox x:Name="StaysOpenOnClickCheckBox" Content="StaysOpenOnClick" IsChecked="True" />

  <Menu IsMainMenu="False">
    <MenuItem Header="Options">
      <MenuItem x:Name="InteractiveMenuItem"
                Header="Interactive Item"
                IsCheckable="{Binding IsChecked, ElementName=IsCheckableCheckBox}"
                StaysOpenOnClick="{Binding IsChecked, ElementName=StaysOpenOnClickCheckBox}" />
    </MenuItem>
  </Menu>

  <CheckBox x:Name="IsCheckedSyncCheckBox"
            Content="IsChecked"
            IsChecked="{Binding IsChecked, ElementName=InteractiveMenuItem, Mode=TwoWay}" />
</StackPanel>
```

## 主な使用例

- **アプリのメニューバー** — ウィンドウの上端の File・Edit・View で、<kbd>Alt</kbd> で入れます。
- **オン・オフの設定** — 「折り返し」「ステータスバーを表示」のような、チェックできる項目です。
- **標準のコマンド** — フォーカスのあるテキストボックスの選択に応じて有効になる Copy の項目です。

## ヒントとベストプラクティス

- **サブメニューの項目の `IsEnabled` を読まず、コマンドの `CanExecute` を呼ぶ** — メニューが閉じている間、項目は更新されません。
- **ショートカットは `KeyBinding` で結ぶ** — `InputGestureText` は文字を出すだけです。キーの組み合わせを持つコマンドなら、文字は自動で入ります。
- **排他の選択は自分で作る** — チェックできる項目は互いに独立しているので、ほかの項目のチェックをコードで外すか、値のバインドで表します。
- **メニューバー以外のメニューには `IsMainMenu="False"` を設定する** — 既定値は `True` で、<kbd>Alt</kbd> でそのメニューに入ります。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`MenuDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/MenuDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使い、デモアプリの各欄と同じ形のメニューで試しました。クリックとマウスの重ね合わせは実際のマウスで行い、<kbd>Alt</kbd>・<kbd>F10</kbd>・ショートカットは実際のキー入力として押しました。<kbd>F10</kbd> は切り分けのため、WPF の入力管理を通しても送りました。計測したマシンのクリップボードを書き換えないよう、`Copy` の項目はクリックせず、有効かどうかだけを読みました。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/menu/menu-behavior.svg" alt="Menu の計測結果の表。IsMainMenu の既定値は True、Role の欄の 4 項目はそれぞれの Role で、子を持った TopLevelItem は TopLevelHeader になり、実際のクリックはチェックできる項目にチェックを付けて StaysOpenOnClick でなければサブメニューを閉じ、チェックできる 2 項目は両方チェックされ、Alt はメインメニューにだけ入り、TextBox にフォーカスがあるときの実際の F10 はどちらにも入らないが Button にフォーカスがあるときや入力管理を通したときはメインメニューに入り、Alt+M はどちらも開き、Ctrl+O の文字は出るが Ctrl+O ではクリックされず、Copy の項目には Ctrl+C が入り、Copy の項目は閉じている間は有効と読め、開くと TextBox の選択に従い、マウスの重ね合わせ・押下・解放で見出しの状態が変わる" width="1242" height="770" loading="lazy">
  <figcaption>Role、チェックできる項目、メインメニュー、ショートカット、コマンド、見出しの状態。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール

- [ToolBar](/ja/apps/wpf-standard-control-demo/toolbar.html) — よく使うコマンドのボタンで、ふつうはメニューバーの下に置きます。
- [Popup](/ja/apps/wpf-standard-control-demo/popup.html) — サブメニューが開くのと同じ種類の要素です。
- [CheckBox](/ja/apps/wpf-standard-control-demo/checkbox.html) — メニューではなく画面上に置く、オン・オフの設定です。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で Menu のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/MenuUsage){: target="_blank" rel="noopener noreferrer"}

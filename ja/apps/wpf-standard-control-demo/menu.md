---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/menu.html
title: "Menu"
badge: "Menu"
lead: "Menu は、見出しを並べたバーで、見出しごとにコマンドの一覧を開きます。各項目は MenuItem で、項目自身がサブメニューを持てます。"
description: "WPF の Menu を .NET 10 で実測して解説。項目の Role、実際のクリックでのチェックと StaysOpenOnClick、Alt・F10・アクセスキーでメインメニューに入るか、InputGestureText、コマンドの項目の IsEnabled を確かめます。"
---

## どのキーでメニューに入るか

**Menu** の `IsMainMenu` は、<kbd>Alt</kbd> を押したときにメニューへ入るかどうかで、既定値は `True` です。TextBox にフォーカスがある状態で実際に <kbd>Alt</kbd> を押すと、`True` では最初の見出しが強調されてフォーカスが移り、`False` では何も起きませんでした。既定値が `True` なので、メニューバー以外のメニューには `IsMainMenu="False"` を設定します。

ドキュメントは `IsMainMenu` を <kbd>Alt</kbd> と <kbd>F10</kbd> の通知を受けるかどうかと説明しています。日本語入力を使う計測したマシンでは、TextBox にフォーカスがある状態で実際に <kbd>F10</kbd> を押すと、どちらでも何も起きませんでした。`True` で、Button にフォーカスがある状態の実際の <kbd>F10</kbd> と、WPF の入力管理を通して TextBox に送った <kbd>F10</kbd> では、どちらもメニューに入りました。違いの原因として、<kbd>F10</kbd> を自分でも使う日本語入力が考えられますが、入力方式をオフにして測ってはいないので、確かめていません。

アクセスキーはどちらでも働き、<kbd>Alt</kbd>+<kbd>M</kbd> は `False` でもデモアプリの「Main Menu(\_M)」を開きました。

## 項目の Role と見出しの状態

Menu は `MenuItem` を持ち、各 MenuItem の読み取り専用の `Role` は、置かれた位置と子の有無で決まります。デモアプリの Role の欄の 4 項目は、`TopLevelHeader`・`SubmenuHeader`・`SubmenuItem`・`TopLevelItem` でした。Role は子に応じて変わり、`TopLevelItem` に子を追加すると `TopLevelHeader` になりました。

見出しの状態は `IsHighlighted`・`IsPressed`・`IsSubmenuOpen`・`IsSuspendingPopupAnimation` で表され、`IsSubmenuOpen` 以外は読み取り専用です。実際のマウスを重ねると `IsHighlighted` が `True` になりました。ボタンを押すと `IsPressed` が `True` になってサブメニューが開き、離すと `IsPressed` だけが戻りました。`IsSuspendingPopupAnimation` は、サブメニューが開くと `True` になりました。見出しが 2 つあるとき、1 つ目を開いたまま 2 つ目にマウスを重ねると、1 つ目が閉じて 2 つ目が開きました。

## チェックできる項目と、開いたままのサブメニュー

`IsCheckable` は、クリックで `IsChecked` を切り替えるかどうかで、既定値は `False` です。実際にクリックすると項目にチェックが付き、もう一度クリックすると外れました。`IsCheckable` でなければ、クリックしてもチェックは付きませんでした。`IsChecked` はチェックボックスと TwoWay でバインドでき、チェックボックスはクリックに追従し、コードでチェックボックスにチェックを付けると項目にもチェックが付きました。

チェックできる項目どうしは排他ではなく、同じサブメニューの 2 項目をクリックすると両方にチェックが付きました。排他の選択にするには、ほかの項目のチェックをコードで外すか、値のバインドで表します。

`StaysOpenOnClick` は、項目をクリックした後もサブメニューを開いたままにするかどうかで、既定値は `False` です。実際にクリックすると、`False` ではサブメニューが閉じ、`True` では開いたままでした。

## コマンド、IsEnabled、ショートカットの文字

`Command` に `ApplicationCommands.Copy` を設定し、実際のクリックでメニューを開くと、TextBox で何も選択していなければ無効、全体を選択していれば有効、Button にフォーカスがあれば無効でした。メニューを開いている間、キーボードフォーカスは MenuItem にありましたが、項目は TextBox の選択に従いました。

サブメニューの項目の `IsEnabled` は、メニューが閉じている間は最新ではありません。TextBox にフォーカスがあり何も選択していないとき、`Copy` の項目の `IsEnabled` は、コマンドの `CanExecute` が `False` を返すのに `True` でした。実際のクリックでメニューを開くと、項目は無効になりました。項目の `IsEnabled` を読まず、コマンドの `CanExecute` を呼びます。

`Icon` は左に出す画像、`InputGestureText` は右に出すショートカットの文字です。`InputGestureText` は文字だけで、「Ctrl+O」は表示されましたが、実際に <kbd>Ctrl</kbd>+<kbd>O</kbd> を押しても項目はクリックされませんでした。キーの組み合わせを持つコマンドでは自動で入り、`Copy` の項目は設定しなくても `Ctrl+C` でした。ウィンドウに `KeyBinding` を付けると、キーの組み合わせを持たないコマンドが実際の <kbd>Ctrl</kbd>+<kbd>O</kbd> で実行されましたが、項目の `InputGestureText` は空のままでした。ショートカットは `KeyBinding` で結び、`InputGestureText` も設定します。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/menu/menu-behavior.svg" alt="Menu の計測結果の表。IsMainMenu の既定値は True、Role の欄の 4 項目はそれぞれの Role で、子を持った TopLevelItem は TopLevelHeader になり、実際のクリックはチェックできる項目にチェックを付けて StaysOpenOnClick でなければサブメニューを閉じ、チェックできる 2 項目は両方チェックされ、Alt はメインメニューにだけ入り、TextBox にフォーカスがあるときの実際の F10 はどちらにも入らないが Button にフォーカスがあるときや入力管理を通したときはメインメニューに入り、Alt+M はどちらも開き、Ctrl+O の文字は出るが Ctrl+O ではクリックされず、Copy の項目には Ctrl+C が入り、Copy の項目は閉じている間は有効と読め、開くと TextBox の選択に従い、マウスの重ね合わせ・押下・解放で見出しの状態が変わる。ウィンドウの KeyBinding では実際の Ctrl+O でコマンドが実行されるが項目の InputGestureText は空のまま" width="1242" height="800" loading="lazy">
  <figcaption>Role、チェックできる項目、メインメニュー、ショートカット、コマンド、見出しの状態。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの Menu のページ。左にコントロールの一覧、右に最初の節の IsMainMenu](/images/wpf-standard-control-demo/menu.png){: .screenshot-img}

デモアプリの Menu のページには、`IsMainMenu`、`IsCheckable` と `IsChecked`・`StaysOpenOnClick`、`Role`、`Command`、`Icon` と `InputGestureText` の欄と、`IsHighlighted`・`IsPressed`・`IsSubmenuOpen`・`IsSuspendingPopupAnimation` を表示する最後の欄があります。`IsCheckable` の欄は `IsCheckable` と `StaysOpenOnClick` の両方を `True` で始め、チェックボックスを `IsChecked` に TwoWay でバインドしています。`Command` の欄は `ApplicationCommands.Copy` を使い、`InputGestureText` の欄は「Ctrl+O」を表示します。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`IsCheckable` の欄（`MenuUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。

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

## 関連するコントロールと記事

- [ToolBar](/ja/apps/wpf-standard-control-demo/toolbar.html) — よく使うコマンドのボタンで、ふつうはメニューバーの下に置きます。
- [Popup](/ja/apps/wpf-standard-control-demo/popup.html) — サブメニューが開くのと同じ種類の要素です。
- [CheckBox](/ja/apps/wpf-standard-control-demo/checkbox.html) — メニューではなく画面上に置く、オン・オフの設定です。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`MenuDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/MenuDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使い、デモアプリの各欄と同じ形のメニューで試しました。クリックとマウスの重ね合わせは実際のマウスで行い、<kbd>Alt</kbd>・<kbd>F10</kbd>・ショートカットは実際のキー入力として押しました。<kbd>F10</kbd> は切り分けのため、WPF の入力管理を通しても送りました。計測したマシンのクリップボードを書き換えないよう、`Copy` の項目はクリックせず、有効かどうかだけを読みました。

[GitHub で Menu のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/MenuUsage){: target="_blank" rel="noopener noreferrer"}

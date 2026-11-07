---
layout: article-ja
title: "WPF の KeyBinding が反応しない原因をキーボードフォーカスの位置から切り分ける"
date: 2026-11-07
category: WPF
excerpt: "KeyBinding は、キーボードフォーカスのある要素から Window へ向かう経路に置いたものしか実行されない。フォーカスが無い・別ウィンドウにある、置いた要素の外にある、コントロールがキーを先に使う、の 3 通りを実際のキー入力で切り分ける。"
image: /images/articles/wpf-keybinding-not-firing-focus/keybinding-focus-position.svg
---

## 概要

`Ctrl+S` で保存する `KeyBinding` を書いたのに、画面のある場所では効き、別の場所では効かないことがある。
このとき、`Command` のバインドより先に、キーボードフォーカスの位置を疑うべき場合がある。

`KeyBinding` が実行されるのは、キーボードフォーカスのある要素から `Window` へ向かう経路に置いたものだけである。
本記事では、効かない場合を「フォーカスがどこにも無い・別ウィンドウにある」「フォーカスが `KeyBinding` を置いた要素の外にある」「フォーカスのあるコントロールがそのキーを自分で使う」の 3 通りに分け、実際のキー入力で確かめた結果から切り分ける。
あわせて、フォーカスが経路の上にあっても実行されない場合の見分け方と、`UserControl` に `Focusable="True"` を付けても中の `TextBlock` をクリックしただけではフォーカスが移らないことも示す。

---

## 前提・対象環境

- フレームワーク: .NET Framework 4.0 以降・.NET Core 3.0 以降の WPF（`KeyBinding` の `Command` をバインドでき、`Keyboard.ClearFocus` を持つ範囲）
- 検証環境: .NET 10.0.13 / Windows 11（10.0.26200）
- アーキテクチャ: MVVM（`KeyBinding` の `Command` をビューモデルの `ICommand` にバインドする）
- 計測方法: 計測用のウィンドウを前面に出し、OS のキー入力（`SendInput`）で実際にキーを押した。マウスのクリックも OS の入力で行った

---

## 問題

次の画面で、`Window` には `Ctrl+S`・`Ctrl+Z`・`Enter` を、編集パネルの `UserControl` には `Ctrl+D` を割り当てた。

```xml
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Editor" SizeToContent="WidthAndHeight" ResizeMode="NoResize">
  <Window.InputBindings>
    <KeyBinding Key="S" Modifiers="Control" Command="{Binding SaveCommand}" />
    <KeyBinding Key="Z" Modifiers="Control" Command="{Binding UndoCommand}" />
    <KeyBinding Key="Enter" Command="{Binding SubmitCommand}" />
  </Window.InputBindings>
  <StackPanel Margin="8" Width="240">
    <TextBox x:Name="titleBox" Text="{Binding Title}" />
    <Button x:Name="okButton" Content="OK" Command="{Binding OkCommand}" Margin="0,4" />
    <UserControl x:Name="panel" Background="Transparent">
      <UserControl.InputBindings>
        <KeyBinding Key="D" Modifiers="Control" Command="{Binding DeleteCommand}" />
      </UserControl.InputBindings>
      <StackPanel>
        <TextBlock x:Name="panelLabel" Text="Panel" Padding="0,8" />
        <TextBox x:Name="panelBox" />
      </StackPanel>
    </UserControl>
  </StackPanel>
</Window>
```

`DataContext` は `Window` に設定したビューモデル 1 つで、各コマンドは実行されたことを記録するだけである。
コマンドはいずれも `CanExecute` が常に `true` を返すので、表の結果は `CanExecute` には左右されていない。

この画面では、次の症状が起きる。

- `Ctrl+D` が、上の `titleBox` に入力している間は効かない
- `Ctrl+Z` が、`titleBox` に入力している間は呼ばれない
- `titleBox` を複数行入力（`AcceptsReturn="True"`）にすると、`Enter` では改行が入るだけになる

---

## 症状から絞り込めること

キーボードのイベントは、キーボードフォーカスのある要素で発生する。
`KeyDown` などのバブルイベントはそこから親へ向かって伝わり、`PreviewKeyDown` などのトンネルイベントは逆にルートから下へ伝わる（Microsoft Learn の [Input Overview](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/input-overview)）。
表 1 の実測では、`KeyBinding` もこの経路の上にあるものだけが実行された。
このため、フォーカスの位置に起因する原因は次の 3 つに絞れる。

| 症状 | 疑う原因 |
|---|---|
| 画面のどこに入力しても効かない時間帯がある | フォーカスがどこにも無い、または別のウィンドウにある |
| 画面の一部に入力している間だけ効かない。`KeyBinding` は `UserControl` やパネルに置いている | フォーカスが `KeyBinding` を置いた要素の外にある |
| 特定のコントロールに入力している間だけ効かない。そのコントロールはキーを使う種類（`TextBox` など） | フォーカスのあるコントロールがキーを先に使っている |

`Key` 属性で書いた `KeyBinding` は、修飾キーが無くても検証環境では実行された（注意点の表 4）。

---

## 切り分けの手順

出発点は、キーを押す直前の `Keyboard.FocusedElement` である。
次の順に判定する。

1. `Keyboard.FocusedElement` が `null` なら、フォーカスがどこにも無い。
2. `null` ではないが、それを `DependencyObject` として `Window.GetWindow` に渡した結果が、`KeyBinding` を置いたウィンドウではないなら、フォーカスは別のウィンドウにある。
3. 同じウィンドウの中なら、`KeyBinding` を置いた要素の `IsKeyboardFocusWithin` を見る。`false` なら、フォーカスがその要素の外にある。
4. `true` なら、`KeyBinding` は経路の上にある。それでも実行されないときは、押したキーと修飾キーが `KeyBinding` の `Key`・`Modifiers` と一致していないか、`Command` のバインドが解決されていないか、経路の上のほかの要素がキーを先に処理している。1 つ目は、`Command` が解決されていても実行されない（表 1 の、`Ctrl+S` の `KeyBinding` に `Ctrl+Shift+S` を押した行）。2 つ目は、その `KeyBinding` の `Command`（`InputBindings` の要素の `Command`）が `null` かどうかで見分けられる（表 1 の、別ウィンドウに `DataContext` を設定しない行）。3 つ目には、フォーカスのあるコントロール自身、より内側の要素に置いた同じキーの `KeyBinding`、`Window` の `PreviewKeyDown` で `Handled` にするハンドラーが当たる（表 2）。経路の途中の要素で `Handled` にする場合は測っていない。`CanExecute` が `false` の場合は本記事では測っていない。

表 1・表 2 の「切り分けの手順の判定」の列は、この 1〜4 をそのままコードにして、キーを押す直前に求めたものである。
判定が 1〜3 の行ではどれもコマンドが実行されなかった。
判定が 4 で何も実行されなかった行は、手順 4 の 1 つ目・2 つ目に当たる行（`Ctrl+Shift+S` を押した行と、`DataContext` を設定しなかった別ウィンドウの行）を除き、どれもフォーカスが `TextBox` にあった。

フォーカスはキーを押すまでの操作で移っていくので、移るたびに記録しておくと、キーを押したときの位置を後から読める。
確かめる間だけ、次のデバッグ用のコードを `App` の静的コンストラクター（`static App()`）に一時的に入れる。
`using System.Windows;` と `using System.Windows.Input;` が必要である。

```csharp
static App()
{
    // フォーカスを得た要素を書く。イベントは親へも伝わるので、得た要素自身のときだけ書く。
    EventManager.RegisterClassHandler(
        typeof(UIElement),
        Keyboard.GotKeyboardFocusEvent,
        new KeyboardFocusChangedEventHandler((sender, e) =>
        {
            if (sender == e.OriginalSource)
            {
                System.Diagnostics.Debug.WriteLine($"focus: {e.NewFocus}");
            }
        }),
        handledEventsToo: true);

    // どこにもフォーカスが無くなったときは、得る要素が無いので、失った側で書く。
    EventManager.RegisterClassHandler(
        typeof(UIElement),
        Keyboard.LostKeyboardFocusEvent,
        new KeyboardFocusChangedEventHandler((sender, e) =>
        {
            if (sender == e.OriginalSource && e.NewFocus is null)
            {
                System.Diagnostics.Debug.WriteLine("focus: null");
            }
        }),
        handledEventsToo: true);
}
```

フォーカスが `UIElement` にある限り、最後に出た行が、キーを押したときのフォーカスである。
`Keyboard.ClearFocus()` の後は `focus: null` が、その後に `Keyboard.Focus(window)` を呼ぶと `focus: System.Windows.Window` が最後に出た。
何も入力していない `TextBox` へ移したときは `focus: System.Windows.Controls.TextBox` で始まる行が出た。
計測では素の `Window` を使ったので `System.Windows.Window` と出たが、アプリでは `MainWindow` などの派生クラスの名前になる。
書き出すのは `UIElement` の要素だけである。
フォーカスが `Hyperlink` などの `ContentElement` に移ったときは行が出ないので、最後の行がフォーカスと一致するのは、フォーカスが `UIElement` にある場合に限る。
`RegisterClassHandler` で登録したハンドラーは外せず、アプリ全体に効くので、切り分けが済んだらコードごと消す。

---

## 原因別の対処

### フォーカスがどこにも無い・別ウィンドウにある

`Keyboard.ClearFocus()` を呼んだ後は、`Keyboard.FocusedElement` が `null` になり、`Window` の `Ctrl+S` は実行されなかった。
このとき `Window` の `PreviewKeyDown` も呼ばれなかった。
キーのイベントが `Window` まで届いていない。
Owner を指定した別ウィンドウの `TextBox` に入力している間も、元の `Window` の `PreviewKeyDown` は呼ばれず、`Ctrl+S` は実行されなかった。

{% include tables/articles/wpf-keybinding-not-firing-focus/keybinding-focus-position.ja.md %}

表 1: キーボードフォーカスの位置と、実行されたビューモデルのコマンド。「KeyBinding の置き場所」に差分を書いた行のほかは、問題の節の XAML である。切り分けの手順の判定は、Ctrl+S・Ctrl+Shift+S の行は `Window` の Ctrl+S（別ウィンドウにも置いた行は別ウィンドウ）、Ctrl+D の行は `UserControl`（`Window` に置いた行は `Window`）の `KeyBinding` を基準に求めた。キーを押す前の操作の後に、OS のキー入力で 1 回押した。クリックも OS のマウス入力で行った。.NET 10.0.13 / Windows 11。
{: .table-caption}

WPF のコードが `Keyboard.ClearFocus()` を呼んでフォーカスが `null` になったときは、`Keyboard.Focus(window)` で `Window` へ移すことを試みる。
元の要素か移す先の要素が拒むと、`Keyboard.Focus` を呼んでもフォーカスは要求どおりに移らず、`Keyboard.Focus` はその時点でフォーカスを持つ要素を返す（Microsoft Learn の [Focus Overview](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/focus-overview)）。
このため、戻り値が `window` であることで移ったことを確かめる。
表 1 の計測では、`Keyboard.ClearFocus()` の後に `Keyboard.Focus(window)` を呼ぶと、戻り値は `Window` で、フォーカスが `Window` に戻り、`Ctrl+S` が実行された。
後で述べる、ホストした Win32 のウィンドウにフォーカスがあって `null` になっている場合は、その状態が意図したものなので、同じように `Window` へ移してよいとは限らない。
この場合は本記事では測っていない。
表示して `Activate` しただけでどこにもフォーカスを移していない場合も、フォーカスは `Window` 自体にあり、`Ctrl+S` は実行された。
フォーカスのある `Button` を `IsEnabled = false` にした場合と、フォーカスのある `TextBox` を `Collapsed` にした場合も、フォーカスは `Window` に移り、`Ctrl+S` は実行された。
表 1 で試した操作の中で、フォーカスが `null` になったのは `Keyboard.ClearFocus()` を呼んだときだけである。
ただし Microsoft Learn の [Keyboard.ClearFocus](https://learn.microsoft.com/dotnet/api/system.windows.input.keyboard.clearfocus) によれば、`HwndHost` などでホストした Win32 のウィンドウにフォーカスがあるときにメニューを閉じると、WPF 自身が `ClearFocus` を呼び、WPF の要素はどれもフォーカスを持たなくなる。

別ウィンドウでも同じショートカットを効かせるには、そのウィンドウにも同じ `KeyBinding` を置く。
表 1 の「Window と Owner 付きの別ウィンドウ」の行では、別ウィンドウにも `Ctrl+S` を置き、`DataContext` に同じビューモデルを設定すると、そちらの `TextBox` に入力している間に `SaveCommand` が実行された。
別ウィンドウに `DataContext` を設定しなかった行では、判定は「4: 経路の上」でも `SaveCommand` は実行されなかった。
キーを押す直前に読むと、別ウィンドウの `KeyBinding` の `Command` は `null` だった。
Owner を指定しても、元の `Window` の `DataContext` は別ウィンドウの `{Binding SaveCommand}` に届かない。
別ウィンドウの `DataContext` にも、同じコマンドを持つビューモデルを設定する。

### フォーカスが KeyBinding を置いた要素の外にある

`UserControl` に置いた `Ctrl+D` は、`UserControl` の中の `panelBox` に入力している間は実行され、外の `titleBox` に入力している間は実行されなかった。
表 1 の、置き場所が「UserControl」で、操作が「Keyboard.Focus(panelBox)」と「Keyboard.Focus(titleBox)」の Ctrl+D の行がその結果である。
外の `titleBox` に入力している間も `Window` の `PreviewKeyDown` は呼ばれており、キーは `Window` まで届いている。
届いていても、経路から外れた `UserControl` の `KeyBinding` は実行されない。

画面全体で効かせたいショートカットは、`Window` の `InputBindings` に置くのが適切である。
表 1 の「Window（UserControl からは外す）」の行では、`Ctrl+D` を `UserControl` から外して `Window` に置くと、外の `titleBox` に入力していても実行された。

`UserControl` に `Focusable="True"` を付けても、この症状は直らなかった。
`titleBox` にフォーカスを置いたまま `UserControl` の中の `TextBlock` をクリックすると、`Focusable` が `False` でも `True` でも、フォーカスは `titleBox` に残った。
表 1 で試した操作の中では、フォーカスが `UserControl` に移ったのは、`Focusable="True"` を付けたうえでコードから `Keyboard.Focus(panel)` を呼んだときだけである。
`Focusable` は、要素がフォーカスを受け取れるかを決めるプロパティである（Microsoft Learn の [UIElement.Focusable](https://learn.microsoft.com/dotnet/api/system.windows.uielement.focusable)）。

### フォーカスのあるコントロールがキーを使う

`titleBox` に入力している間は、`Window` の `Ctrl+Z` は実行されず、`TextBox` の元に戻す操作が働いた。
何も入力しておらず元に戻すものが無い状態でも、`Window` の `Ctrl+Z` は実行されなかった。
同じように、`AcceptsReturn="True"` の `TextBox` では `Enter` で改行が入り、`Button` では `Enter` で `Button` のコマンドが実行された。
どちらの場合も `Window` の `Enter` は実行されなかった。
判定はどの行も「4: 経路の上」であり、`KeyBinding` の置き場所の問題ではない。

{% include tables/articles/wpf-keybinding-not-firing-focus/keybinding-key-consumed.ja.md %}

表 2: フォーカスのあるコントロールが自分で使うキー。「実行されたビューモデルのコマンド」の列は、ビューモデルが記録したコマンドだけを示し、`TextBox` 自身の元に戻す操作は含まない。どの行も `Window` の `Ctrl+Z`（`UndoCommand`）と `Enter` は残したままで、置き場所の列には、足した `KeyBinding`・ハンドラーと、変えた属性を書いた。切り分けの手順の判定は `Window` の `KeyBinding` を基準に求めた（titleBox.InputBindings の行は `TextBox` の `KeyBinding`）。`"abc"` の入力は InputManager を通して送り、キーは OS のキー入力で 1 回押した。.NET 10.0.13 / Windows 11。
{: .table-caption}

コントロールが使うキーを横取りするには、そのコントロールの `InputBindings` に同じキーの `KeyBinding` を置く。
表 2 の「Window と titleBox.InputBindings（TextUndoCommand）」の行では、`Window` の `Ctrl+Z` を残したまま、`TextBox` に置いた `KeyBinding` で別のコマンド（`TextUndoCommand`）を実行させた。
実行されたのは `TextUndoCommand` だけで、`Window` の `UndoCommand` は実行されず、`TextBox` の文字列は `"abc"` のまま残った。
`TextBox` 自身に置いた `KeyBinding` が、`TextBox` の元に戻す操作と `Window` の `KeyBinding` より先に処理されたことになる。
ただし、横取りすると `Ctrl+Z` では `TextBox` の元に戻す操作が働かなくなる。
横取りが適するのは、ビューモデルが持つ編集履歴で元に戻すなど、`TextBox` の操作をアプリの操作で置き換えたい場合である。
そうでなければ、コマンドは `TextBox` と重ならない別のキーに割り当てるほうが、利用者の期待とずれない。

---

## 実装例

次の例は、画面全体のショートカットを `Window` にまとめ、`Ctrl+Z` をビューモデルの編集履歴で置き換える画面である。
`Ctrl+Z` は `TextBox` の操作と重なるので、`TextBox` に置く。
ビュー（XAML）はバインドだけで動き、コードビハインドには `InitializeComponent` 以外を書かない。
コードビハインドと結び付ける `x:Class` と、`DataContext` にビューモデルを設定する箇所は省略した（計測では XAML を読み込んだ後に `DataContext` を設定した）。

```xml
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Editor" SizeToContent="WidthAndHeight" ResizeMode="NoResize">
  <Window.InputBindings>
    <KeyBinding Key="S" Modifiers="Control" Command="{Binding SaveCommand}" />
    <!-- UserControl に置くと、UserControl の外に入力している間は効かない -->
    <KeyBinding Key="D" Modifiers="Control" Command="{Binding DeleteCommand}" />
    <KeyBinding Key="Enter" Command="{Binding SubmitCommand}" />
  </Window.InputBindings>
  <StackPanel Margin="8" Width="240">
    <TextBox x:Name="titleBox" Text="{Binding Title}">
      <TextBox.InputBindings>
        <!-- TextBox の元に戻す操作より先に処理される -->
        <KeyBinding Key="Z" Modifiers="Control" Command="{Binding UndoCommand}" />
      </TextBox.InputBindings>
    </TextBox>
    <Button x:Name="okButton" Content="OK" Command="{Binding OkCommand}" Margin="0,4" />
    <UserControl x:Name="panel">
      <StackPanel>
        <TextBlock Text="Panel" Padding="0,8" />
        <TextBox x:Name="panelBox" />
      </StackPanel>
    </UserControl>
  </StackPanel>
</Window>
```

この XAML そのもので各キーを押した結果が表 3 である。

{% include tables/articles/wpf-keybinding-not-firing-focus/keybinding-implementation.ja.md %}

表 3: 実装例の XAML で押したキーと、実行されたビューモデルのコマンド。`"abc"` の入力は InputManager を通して送り、キーは OS のキー入力で 1 回押した。.NET 10.0.13 / Windows 11。
{: .table-caption}

`Ctrl+S` と `Ctrl+D` は `Window` に置いたので、`UserControl` の中の `panelBox` に入力しているときも外の `titleBox` に入力しているときも実行された。
`Ctrl+Z` は、`TextBox` に置いた `KeyBinding` の `UndoCommand` が実行され、入力した `"abc"` は残った。
`Enter` は、`AcceptsReturn` を付けていない `titleBox` では `SubmitCommand` が実行され、`Button` では `Button` のコマンドが実行された。
`AcceptsReturn="True"` の `TextBox` でも `Window` の `Enter` は実行されない（表 2）。
`Enter` を確定の操作に使う画面では、`Enter` を使うコントロールにフォーカスがある間は確定されないことを前提に設計する。

`Window.PreviewKeyDown` でキーを先に受け取り、`e.Handled = true` にしてもコマンドは実行でき、表 2 の結果も `TextBox.InputBindings` と同じだった。
`PreviewKeyDown` は `Window` から下へ伝わるので、`TextBox` より先に処理される。
ただしコードビハインドに処理を書くことになるので、本記事では `InputBindings` の置き場所で解決する方法を採る。

---

## 注意点

- 表 1〜3 は、`CanExecute` が常に `true` のコマンドで測った。`CanExecute` が `false` のときの扱いは測っていない。
- `UserControl` の `KeyBinding` が `UserControl` の中でだけ効くのは、部品の中に閉じたショートカットとしては正しい動きである。編集パネルの中だけで意味を持つキーなら、`UserControl` に残す。
- 修飾キーの無い `KeyBinding` は、書き方で結果が分かれた（表 4）。`Key` 属性だけで書いた `Key="A"`・`Key="F2"`・`Key="Enter"` は実行された。一方、`Gesture="A"` は XAML の読み込みで例外になり、コードの `new KeyGesture(Key.A)` と `new KeyBinding(command, Key.A, ModifierKeys.None)` は `NotSupportedException` になった。Microsoft Learn の [KeyBinding](https://learn.microsoft.com/dotnet/api/system.windows.input.keybinding) には、ファンクションキーとテンキー以外は修飾キーが必要で、修飾キーの無いものは実行されないとあるが、`Key` 属性の書き方はこれに当たらなかった。同じページには、XAML でもコードでも無効なジェスチャーの作成を防ぐ検証は無いともあるが、`Gesture` 属性とコンストラクターでは作成の時点で例外になった。
- 文字キーを修飾キーなしで割り当てると、文字を入力するコントロールの入力と衝突しうる。その組み合わせは本記事では測っていない。
- 計測は OS のキー入力で行った。テストなどで `RaiseEvent` によりキーのイベントを要素へ直接発生させる場合は、経路の起点が発生させた要素になる（Microsoft Learn の [Routed events overview](https://learn.microsoft.com/dotnet/desktop/wpf/events/routed-events-overview)）。その場合の `KeyBinding` の扱いは本記事では測っていない。

{% include tables/articles/wpf-keybinding-not-firing-focus/keybinding-no-modifier.ja.md %}

表 4: 修飾キーの無い `KeyBinding` の書き方と結果。XAML の行は、`Window` に置き、`Keyboard.FocusedElement` が `Window` 自体であることを確かめてから、OS のキー入力で 1 回押した。`Gesture="A"` の行は XAML を読み込んだ時点の結果で、キーは押していない。コードの行は、`KeyGesture`・`KeyBinding` を作った時点の結果である。.NET 10.0.13 / Windows 11。
{: .table-caption}

---

## まとめ

`KeyBinding` は、キーボードフォーカスのある要素から `Window` へ向かう経路に置いたものだけが実行される。
効かないときは、まず `Keyboard.FocusedElement` を確かめて、フォーカスが無いのか、別ウィンドウなのか、`KeyBinding` を置いた要素の外なのかを切り分ける。
フォーカスが経路の上にあるなら、押したキーが `Key`・`Modifiers` と一致しているか、`KeyBinding` の `Command` が `null` でないか（バインドが解決されているか）、経路の上のほかの要素がキーを先に処理していないかを確かめる。

- WPF のコードが `Keyboard.ClearFocus()` を呼んでフォーカスが `null` になったときは、`Keyboard.Focus(window)` で `Window` へ移すことを試み、戻り値が `window` であることで移ったことを確かめる。ホストした Win32 のウィンドウに入力中のときは、この方法の対象外である。別ウィンドウでも効かせたいショートカットは、そのウィンドウにも置き、`DataContext` も設定する。
- 画面全体で効かせたいショートカットは `Window` の `InputBindings` に置く。`UserControl` に `Focusable="True"` を付けても、中の `TextBlock` をクリックしただけではフォーカスは移らなかった。
- `TextBox` が使うキーをアプリの操作で置き換えるときは、その `TextBox` の `InputBindings` に置く。置き換える理由が無ければ、`TextBox` と重ならないキーに割り当てる。

---

## 関連記事

- [WPF の DataGrid で編集中の値が保存ボタンで ViewModel に届かない原因と CommitEdit の使い方](/ja/articles/wpf-datagrid-edit-not-committed-on-save/)：`Ctrl+S` の `KeyBinding` で保存したときのフォーカスの位置を扱う
- [WPF で RelayCommand の CanExecute がボタンの有効・無効に反映されない問題の解決方法](/ja/articles/wpf-relaycommand-canexecute-not-updating/)
- [WPF の UserControl に定義した DependencyProperty へ内部からバインドできない原因と DataContext の設計](/ja/articles/wpf-usercontrol-dependencyproperty-binding-not-working/)

---
layout: article-ja
title: "WPF の PasswordBox.Password をバインドできない理由と、MVVM でビューモデルへ渡す 2 つの方法"
date: 2026-11-13
category: WPF
image: /images/articles/wpf-passwordbox-binding-mvvm/passwordbox-plaintext-in-memory.svg
excerpt: "PasswordBox.Password は依存関係プロパティでなく、XAML でバインドすると XamlParseException になる。添付プロパティと CommandParameter の 2 つの渡し方を、ボタンの有効化とメモリに残る平文で実測比較する。"
---

## 概要

MVVM でログイン画面を作るとき、`TextBox` と同じように `PasswordBox` の `Password` をビューモデルの `string` にバインドする書き方が最初に試される。
しかし `Password="{Binding Password}"` と書いた XAML は、読み込むと例外になる。
回避策の添付プロパティも、書き方によっては入力がビューモデルに届かなかったり、入力した文字の順序が逆になったりする。

本記事では、バインドできない原因を例外と型の情報から確かめたうえで、ビューモデルへパスワードを渡す 2 つの方法（添付プロパティで `string` と同期する / `CommandParameter` で `PasswordBox` を渡す）を実装する。
2 つの方法は、ボタンの有効・無効を入力に追従させられるか、パスワードの平文がプロセスのメモリに残るかで結果が分かれる。
後者はプロセスのメモリを実際に走査して確かめた。

---

## 前提・対象環境

- フレームワーク: WPF。コード例は C# 9 以降が要る（`is not` パターンは C# 9、`using` 宣言は C# 8）。動作は下記の検証環境でだけ測った
- アーキテクチャ: MVVM。ビューモデルの基底クラス `ObservableObject` と `RelayCommand` / `RelayCommand<T>` は自前の最小実装で、`CanExecuteChanged` はビューモデルが `RaiseCanExecuteChanged` を呼んだときだけ発生する（`CommandManager.RequerySuggested` にはつながない）。`RelayCommand<T>` は、パラメーターが `T` でなければ `CanExecute` が `false` を返し、`Execute` も何もしない。ジェネリックでない `RelayCommand` を自前のイベントで実装する例と、`RequerySuggested` につなぐ例は[関連記事](/ja/articles/wpf-relaycommand-canexecute-not-updating/)にある
- 検証環境: .NET 10 / Windows 11（64 ビットのプロセス）
- 入力の再現: キーボードからの入力と同じく、1 文字ずつ `TextComposition` を送って `PasswordBox` に入力した

---

## 問題

次の XAML は、ビューモデルの `Password` プロパティを `PasswordBox` に表示し、入力をビューモデルへ戻すつもりで書いたものである。

```xml
<PasswordBox Password="{Binding Password}" />
```

この XAML を読み込むと `XamlParseException` になる。
メッセージは、`PasswordBox` の `Password` プロパティには `Binding` を設定できず、`Binding` は `DependencyObject` の `DependencyProperty` にしか設定できない、というものだった。

{% include tables/articles/wpf-passwordbox-binding-mvvm/passwordbox-binding-error.ja.md %}

`Password` にバインドを設定したときの例外と、`PasswordBox` の型の性質。.NET 10 / Windows 11 で実測。XAML は `XamlReader.Parse` で読み込み、メッセージは UI カルチャを ja-JP にして 1 行目を読んだ。
{: .table-caption}

---

## 原因・背景

`PasswordBox` には `PasswordProperty` も `SecurePasswordProperty` も無く、`Password` と `SecurePassword` は依存関係プロパティではない（表の 1 行目の例外と 3 行目）。
`PasswordBox` は `INotifyPropertyChanged` も実装していない。
そのため `Password` はバインドのターゲットにならない。
バインドのソースにした場合も、入力による値の変化は伝わらなかった（後述の CommandParameter の表）。
入力の変化を知るために用意されているのは `PasswordChanged` イベントである（[PasswordBox](https://learn.microsoft.com/dotnet/api/system.windows.controls.passwordbox) の Examples）。

依存関係プロパティにしなかった理由は、公式ドキュメントには書かれていない。
ドキュメントが述べているのは、`Password` を読むと平文がメモリに出ること、それを避けるには `SecurePassword` で `SecureString` として受け取ること、の 2 点である（[PasswordBox.Password](https://learn.microsoft.com/dotnet/api/system.windows.controls.passwordbox.password)）。
内部でも、パスワードは `string` ではなく `SecureString` のフィールドに保持されている（[PasswordBox のデモページ](/ja/apps/wpf-standard-control-demo/passwordbox.html)で、同じ環境で確かめている）。

`PasswordBox` は `sealed` なので（表の 2 行目）、派生クラスで依存関係プロパティを足すこともできない。
ビューモデルへ渡すには、`PasswordBox` の外側から仕組みを足すことになる。
本記事では、その手段として添付プロパティと `CommandParameter` の 2 つを比べる。

---

## 2 つの受け渡し方

### 方法 1: 添付プロパティで string のプロパティと同期する

`PasswordBox` に添付プロパティ `BoundPassword` を付け、これをビューモデルの `string` にバインドする。
入力されたら `PasswordChanged` で `BoundPassword` を書き換え、ビューモデルが値を変えたら `Password` を書き換える。

この方向の実装は、書き方によって次の 2 つの不具合が出た。

{% include tables/articles/wpf-passwordbox-binding-mvvm/passwordbox-attached-property.ja.md %}

添付プロパティの書き方と、入力の結果。.NET 10 / Windows 11 で実測。「abc」は 1 文字ずつ入力した。
{: .table-caption}

1 つ目は、`PasswordChanged` の購読を `BoundPassword` の変更コールバックの中で行う書き方である。
変更コールバックは、プロパティの有効値が変わったときに呼ばれる（[PropertyChangedCallback](https://learn.microsoft.com/dotnet/api/system.windows.propertychangedcallback)）。
ビューモデルの初期値が空文字列だと、添付プロパティの既定値（空文字列）と同じなので有効値が変わらず、変更コールバックが呼ばれない。
購読されないまま入力が進み、ビューモデルの `Password` は空のままだった。

2 つ目は、変更コールバックで値を比べずに `Password` を設定し直す書き方である。
入力のたびに `PasswordChanged` で `BoundPassword` が書き換わり、その変更コールバックが同じ値を `Password` に設定し直す。
コードから同じ値を `Password` に設定し直すと、その直後の入力は先頭に入る（表の「添付プロパティなし」の 2 行。設定しなければ `"abc"`、「ab」の後に設定し直してから「c」を入力すると `"cab"`）。
これが 1 文字ごとに起きるため、「abc」と入力した結果は `"cba"` になった。

記事の実装では、購読を専用の `Attach` プロパティに分け、`Password` と値が同じなら設定し直さない。

```csharp
using System.Windows;
using System.Windows.Controls;

public static class PasswordBoxAssist
{
    public static readonly DependencyProperty AttachProperty =
        DependencyProperty.RegisterAttached("Attach", typeof(bool), typeof(PasswordBoxAssist),
            new PropertyMetadata(false, OnAttachChanged));

    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordBoxAssist),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    public static bool GetAttach(DependencyObject d) => (bool)d.GetValue(AttachProperty);
    public static void SetAttach(DependencyObject d, bool value) => d.SetValue(AttachProperty, value);
    public static string GetBoundPassword(DependencyObject d) => (string)d.GetValue(BoundPasswordProperty);
    public static void SetBoundPassword(DependencyObject d, string value) => d.SetValue(BoundPasswordProperty, value);

    private static void OnAttachChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box) return;
        box.PasswordChanged -= OnPasswordChanged;
        if ((bool)e.NewValue) box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box) return;
        string value = (string?)e.NewValue ?? string.Empty;

        // 入力で書き戻した値と同じなら設定し直さない。Password を設定すると、次の入力が先頭に入る。
        if (box.Password != value) box.Password = value;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        SetBoundPassword(box, box.Password);
    }
}
```

`Attach` は値が `false` から `true` に変わったときに変更コールバックが呼ばれるため、ビューモデルの初期値に関係なく購読される。
`BoundPassword` は `BindsTwoWayByDefault` にしてあるので、XAML で `Mode=TwoWay` を書かなくても入力がビューモデルへ戻る。

ビューモデルは、パスワードを `string` のプロパティとして持つ。
パスワードが空でなければボタンを押せるよう、値が変わるたびに `RaiseCanExecuteChanged` を呼ぶ。

```csharp
public sealed class LoginViewModel : ObservableObject
{
    private string _password = string.Empty;

    public LoginViewModel() => SignInCommand = new RelayCommand(SignIn, () => Password.Length > 0);

    public RelayCommand SignInCommand { get; }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value)) SignInCommand.RaiseCanExecuteChanged();
        }
    }

    private void SignIn()
    {
        // 認証の処理。終わったら空にする。
        Password = string.Empty;
    }
}
```

XAML では、`Attach` と `BoundPassword` の両方を付ける。
`local` は `PasswordBoxAssist` を定義した名前空間である。

```xml
<StackPanel>
  <PasswordBox local:PasswordBoxAssist.Attach="True"
               local:PasswordBoxAssist.BoundPassword="{Binding Password}" />
  <Button Content="サインイン" Command="{Binding SignInCommand}" />
</StackPanel>
```

この実装では「abc」がそのまま `"abc"` としてビューモデルに届き、ビューモデルが `Password = ""` にすると `PasswordBox` も空になった（表の「記事の添付プロパティ」の行と、続く 2 行）。
ボタンは入力の前は無効で、入力すると有効になった。

### 方法 2: CommandParameter で PasswordBox を渡し、実行時に SecurePassword を読む

ビューモデルにパスワードを持たせず、ボタンを押したときだけ受け取る方法である。
ここでも、`SecurePassword` や `Password` をバインドする書き方は期待どおりに動かなかった。

{% include tables/articles/wpf-passwordbox-binding-mvvm/passwordbox-command-parameter.ja.md %}

`CommandParameter` の書き方と、コマンドが受け取った値。.NET 10 / Windows 11 で実測。「abc」は 1 文字ずつ入力した。
{: .table-caption}

`CommandParameter` に `{Binding SecurePassword, ElementName=PasswordInput}` や `{Binding Password, ElementName=PasswordInput}` を書くと、「abc」と入力してから押しても、コマンドに渡る値の長さは 0 だった。
入力の前の値（空）がそのまま渡っている。
`PasswordBox` には `PasswordChanged` イベントがあるが、バインドは入力の後に値を読み直さなかった。

`PasswordBox` そのものを渡し、コマンドを実行するときに `SecurePassword` を読めば、入力した長さ（3）になった。

```xml
<StackPanel>
  <PasswordBox x:Name="PasswordInput" />
  <Button Content="サインイン"
          Command="{Binding SignInCommand}"
          CommandParameter="{Binding ElementName=PasswordInput}" />
</StackPanel>
```

ビューモデルは `PasswordBox` を受け取るコマンドを公開し、実行の中だけ `SecureString` を扱う。
`SecurePassword` は読むたびに新しいコピーを返すので（デモページで確認）、`using` で破棄する。

```csharp
using System.Security;
using System.Windows.Controls;

public sealed class SignInViewModel
{
    public SignInViewModel() => SignInCommand = new RelayCommand<PasswordBox>(SignIn);

    public RelayCommand<PasswordBox> SignInCommand { get; }

    private void SignIn(PasswordBox box)
    {
        using SecureString password = box.SecurePassword;

        // 認証の処理に password を渡す。
    }
}
```

この方法では、ビューモデルは入力が変わったことを知らない。
`CanExecute` で `SecurePassword.Length > 0` を見る書き方にしても、ボタンは入力の前も後も無効のままだった（表の最後の行）。
入力しても `CanExecuteChanged` を発生させるコードが無いため、ボタンの状態は入力の前のまま変わらない。
`CommandManager.RequerySuggested` につなぐ実装のコマンドでは、`TextBox` へのキー入力で問い直されることを[関連記事](/ja/articles/wpf-relaycommand-canexecute-not-updating/)で実測している。
`RequerySuggested` につなぐコマンドに `PasswordBox` を組み合わせた場合は、本記事の結果（追従しない）と異なる可能性があるが、本記事では測っていない。

---

## 判断の軸

- **ボタンの有効・無効を入力に追従させる必要があるか。** 「パスワードが空なら押せない」を実現するには、ビューモデルが入力のたびに変更を知る必要がある。
- **ビューモデルから値を消す必要があるか。** 認証に失敗したときに入力欄を空にする、といった操作である。
- **パスワードの平文をプロセスのメモリに残してよいか。** `string` は不変で、使い終わってもコードから消せない（[SecureString](https://learn.microsoft.com/dotnet/api/system.security.securestring) の String versus SecureString）。
- **ビューモデルが WPF のコントロールの型に依存してよいか。** 方法 2 のビューモデルは `PasswordBox` を引数に取るため、単体テストでも `PasswordBox` を作ることになる。

---

## 軸ごとの結論

平文がメモリに残るかは、プロセスの読み取り可能なコミット済みメモリをすべて読み、パスワードの UTF-16 の並びを探して確かめた。
パスワードは条件ごとに乱数で作り直し、探す側のコードがパスワードの並びを作らないよう、マスクした形でだけ保持して 1 文字ずつ照合した。

{% include tables/articles/wpf-passwordbox-binding-mvvm/passwordbox-plaintext-in-memory.ja.md %}

各操作の後に、12 文字のパスワードの UTF-16 の並びがプロセスのメモリに見つかったか。.NET 10 / Windows 11（64 ビット）で実測。条件ごとに乱数のパスワードを作り直し、1 文字ずつ `TextComposition` で入力した。12 文字届いたことは各行で確かめた（ビューモデルを持つ行は操作の前にビューモデルの string の長さで、ほかの行は走査の後に `SecurePassword` の長さで）。走査したのは `VirtualQuery` でコミット済みかつ読み取り可能な領域（アクセス不可とガードページを除く）で、2 バイト境界に揃った連続した並びだけを探した。読み込みに失敗した範囲は飛ばした。ANSI や分割された断片は探していない。
{: .table-caption}

`PasswordBox` に入力しただけ、`SecurePassword` を読んだだけでは、並びは見つからなかった。
`SecureString` を `Marshal.SecureStringToGlobalAllocUnicode` で非マネージドのメモリへ展開すると、その直後は見つかり、`ZeroFreeGlobalAllocUnicode` で消した後には見つからなかった。
展開した直後に見つかったことは、この走査が非マネージドのメモリも読めていることを示している。

一方、`Password` を 1 回読むと、参照を捨てた後でも並びが見つかった。
方法 1 でビューモデルの `string` に同期した場合も見つかった。
同期の後に `PasswordBox.Clear()` で空にして（ビューモデルも空文字列になる）、`GC.Collect` を実行した後でも見つかった。

| 軸 | 方法 1: 添付プロパティで string と同期 | 方法 2: CommandParameter で PasswordBox を渡す |
|---|---|---|
| ボタンの有効・無効 | 入力に追従した（添付プロパティの表の最後の行） | 本記事の `RelayCommand` では追従しなかった（CommandParameter の表の最後の行） |
| ビューモデルから値を消す | `Password = ""` で `PasswordBox` も空になった | ビューモデルは値を持たない |
| 平文の並びがメモリに残るか | 同期した時点で残り、空にして GC を実行した後も残っていた | `SecurePassword` を読むだけなら見つからなかった |
| ビューモデルが依存する型 | `string` だけ | `PasswordBox`（WPF） |

本記事の `RelayCommand` で入力に応じた有効化を行いたい場合や、ビューモデルからの消去が要る場合は、方法 1 を選ぶ。
その代わり、入力した時点から平文の `string` がプロセスに残ることを受け入れることになる。

平文を `string` にする回数を減らしたいなら、方法 2 を選ぶ。
ただし本記事の `RelayCommand` では、`CanExecute` で入力を見ず、空のパスワードは実行の中で弾く設計になる。

---

## 注意点

- **方法 2 の利点は、`string` に直すまでしか続かない。** 認証に使う API が `string` を受け取るなら、そこへ渡すために `string` を作ることになり、メモリの表の「`Password` を 1 回読んだ」の行と同じく平文の並びができる。`SecureString` を受け取る API（`NetworkCredential` の `SecureString` を取るコンストラクターなど）でも、受け取った先で `string` に直されないかは API ごとに確かめる必要がある。
- **`SecureString` は新規の開発で使うことを推奨されていない。** ドキュメントは、.NET（Core）での新規開発や .NET Framework からの移行では `SecureString` を使わないよう勧め、パスワードはプロセスの外に保存した資格情報へのハンドルで扱うことを代替に挙げている。Windows 以外では中身が暗号化されないことと、プロセスのダンプやスワップファイルからは読まれうることも明記されている（[SecureString](https://learn.microsoft.com/dotnet/api/system.security.securestring) の Remarks。冒頭の Important と How secure is SecureString?）。方法 2 は、メモリ上に平文の `string` ができる機会を減らすだけである。
- **本記事のメモリの計測は、「完全な並びが残るか」だけを見ている。** 見つからなかったことが、断片やほかの符号化でも残っていないことを示すわけではない。
- **方法 1 の比較（`box.Password != value`）も `Password` を読む。** 同期するたびに `string` が作られる。方法 1 は、平文の `string` を作ることを前提にした方法である。
- **`CommandParameter` に `SecurePassword` や `Password` をバインドしない。** どちらも、入力の前の値がそのままコマンドに渡る。

---

## まとめ

`PasswordBox.Password` は依存関係プロパティではないため、XAML でバインドすると `XamlParseException` になる。
`PasswordBox` は `sealed` なので外側から仕組みを足すことになり、本記事ではその手段として添付プロパティと `CommandParameter` を比べた。

`CanExecuteChanged` をビューモデルから発生させるコマンドでボタンの有効・無効を入力に追従させる画面や、ビューモデルから入力欄を空にしたい画面では、方法 1 の添付プロパティを使う。
購読は専用の `Attach` プロパティで行い、`Password` は値が違うときだけ設定し直す。
平文の `string` を作る回数を減らしたい画面では、方法 2 で `PasswordBox` を渡し、実行の中で `SecurePassword` を読んで破棄する。
どちらを選んでも、認証の API が `string` を要求する時点で平文はメモリに出るので、メモリ上の秘匿を強く求める要件ではパスワードをプロセスの外の資格情報で扱う設計を検討する。

---

## 関連記事

- [WPF の UserControl に定義した DependencyProperty へ内部からバインドできない原因と DataContext の設計](/ja/articles/wpf-usercontrol-dependencyproperty-binding-not-working/)
- [WPF で RelayCommand の CanExecute がボタンの有効・無効に反映されない問題の解決方法](/ja/articles/wpf-relaycommand-canexecute-not-updating/)
- [WPF バインディングエラーの読み方と出力ウィンドウを使った原因特定](/ja/articles/wpf-binding-error-debugging-output-window/)

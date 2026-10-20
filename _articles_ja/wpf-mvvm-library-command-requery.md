---
layout: article-ja
title: "WPF で CommunityToolkit.Mvvm と Prism のコマンドに InvalidateRequerySuggested が効かない理由"
seo_title: "WPF で Toolkit と Prism のコマンドに InvalidateRequerySuggested は効かない"
date: 2026-10-21
category: WPF
excerpt: "Toolkit の RelayCommand と Prism の DelegateCommand は、InvalidateRequerySuggested を呼んでもボタンが有効にならない。理由を参照アセンブリとボタンの実測で示し、CanExecute を反映させる書き方を示す。"
image: /images/articles/wpf-mvvm-library-command-requery/library-command-requery.png
---

## 概要

自作の `RelayCommand` を CommunityToolkit.Mvvm（以下 Toolkit）や Prism のコマンドに置き換えると、入力に合わせて有効になっていた保存ボタンが、無効のまま変わらなくなることがある。
`CanExecute` が反映されないときの対処として紹介される `CommandManager.InvalidateRequerySuggested()` を呼んでも、ボタンは変わらない。

本記事では、Toolkit の `RelayCommand` と Prism の `DelegateCommand` が `CommandManager` の再問い合わせに乗っていないことを、ボタンの `IsEnabled` と、ライブラリが参照するアセンブリの実測で示す。
そのうえで、それぞれのライブラリで `CanExecuteChanged` を発生させる書き方を、同じ計測で確かめた結果とともに整理する。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF
- ライブラリ: CommunityToolkit.Mvvm 8.4.0 / Prism.Core 8.1.97（パッケージ名は Prism.Core、アセンブリ名は Prism。8.1.97 は MIT ライセンスで提供された最後の版で、9 以降はライセンスが変わった）
- 検証環境: .NET 10 / Windows 11
- 追検証: .NET Framework 4.8 向けにビルドし、4.8.1（4.8.9363）/ Windows 11 で実行（動作対象の範囲の外だが、.NET Framework 4.8 のまま保守されているアプリケーションも読者に含まれるため確かめた）
- アーキテクチャ: MVVM（ビューはバインドだけで動かし、コードビハインドを持たない）

追検証では、同じ `CanExecute` の条件を別のビューで再現した MVVM のサンプルを使った。
確かめたのは、Toolkit と Prism の「setter は `SetProperty` だけ」の 2 行（入力前・名前を入れた後・`InvalidateRequerySuggested` の後）と、`[NotifyCanExecuteChangedFor]`・`ObservesProperty` の 2 行（入力前・名前を入れた後）で、いずれも .NET 10 と同じ結果だった。
setter で通知を呼ぶ 2 行、自作のコマンドの 2 行、フォーカスを移した後の列、参照するアセンブリの表と図は、.NET Framework では測っていない。

---

## 問題

名前を入力すると保存できるようになる画面を、Toolkit で書く。
`CanExecute` は「名前が空でない」である。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed class CustomerViewModel : ObservableObject
{
    private string _name = "";

    public CustomerViewModel()
    {
        SaveCommand = new RelayCommand(Save, () => !string.IsNullOrEmpty(Name));
    }

    public RelayCommand SaveCommand { get; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private void Save()
    {
        // 保存する
    }
}
```

`SetProperty` は値が変わったときに `PropertyChanged` を発生させる。
ビューは次のとおりで、名前の `TextBox` は入力のたびにソースを更新する。
右端の 2 つ目の `TextBox` は、後の計測でキーボードフォーカスの移動先に使う。

```xml
<StackPanel Orientation="Horizontal">
  <TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" Width="150" />
  <Button Content="Save" Command="{Binding SaveCommand}" Padding="14,4" Margin="12,0" />
  <TextBox Width="80" />
</StackPanel>
```

名前を入力しても、保存ボタンは無効のまま変わらない。
Prism の `DelegateCommand` で同じ形に書いても（基底クラスは `BindableBase`、setter は `SetProperty` だけ）、結果は同じである。

---

## よく紹介される対処

`CanExecute` の結果がボタンに反映されないときの対処として、`CommandManager.InvalidateRequerySuggested()` を呼ぶ方法が紹介されている。
本サイトでも、[WPF で入力検証のエラーが表示されない原因と IDataErrorInfo / INotifyDataErrorInfo の使い分け](/ja/articles/wpf-validation-error-not-displayed/)で、`CanExecute` を検証結果に連動させる場合の再評価の契機としてこのメソッドを挙げた。
[公式のリファレンス](https://learn.microsoft.com/ja-jp/dotnet/api/system.windows.input.commandmanager.invalidaterequerysuggested)は、このメソッドを `CommandManager` に `RequerySuggested` イベントを発生させるものと説明している。

```csharp
// 条件（Name）が変わった後に呼ぶ
CommandManager.InvalidateRequerySuggested();
```

Toolkit の `RelayCommand` と Prism の `DelegateCommand` には、この対処は効かない。

<figure class="article-figure">
  <img src="/images/articles/wpf-mvvm-library-command-requery/library-command-requery.png" alt="入力欄・ボタン・入力欄を 1 組とした 3 組の画面。Toolkit の RelayCommand と Prism の DelegateCommand は、名前を入力して InvalidateRequerySuggested を呼んだ後もボタンが無効のまま。CanExecuteChanged を CommandManager.RequerySuggested へ転送した自作のコマンドだけが有効になっている。" width="513" height="267" loading="lazy">
  <figcaption>.NET 10 / Windows 11、CommunityToolkit.Mvvm 8.4.0 / Prism.Core 8.1.97 で、3 つのビューモデルに同じ名前（taro）を WPF の入力処理（<code>InputManager</code>）を通して入力し、<code>CommandManager.InvalidateRequerySuggested()</code> を呼んだ後の画面。撮影の前に、キャレットを写さないためキーボードフォーカスを外している。上の 2 つは setter で <code>SetProperty</code> だけを呼ぶ書き方、下は <code>CanExecuteChanged</code> を <code>CommandManager.RequerySuggested</code> へ転送する自作のコマンドである。</figcaption>
</figure>

---

## なぜ効かないのか

ボタンはコマンドの `CanExecuteChanged` にハンドラーを登録し、このイベントが発生したときに `CanExecute` を呼び直す。
`InvalidateRequerySuggested` が通知する先は、`CommandManager.RequerySuggested` に登録されたハンドラーだけである。
次の自作のコマンドのように、`CanExecuteChanged` の登録を `RequerySuggested` へ転送していれば、ボタンのハンドラーは `RequerySuggested` に登録され、通知が届く。

```csharp
public event EventHandler? CanExecuteChanged
{
    add => CommandManager.RequerySuggested += value;
    remove => CommandManager.RequerySuggested -= value;
}
```

Toolkit と Prism.Core のコマンドは、`CanExecuteChanged` を自分のイベントとして持ち、`RequerySuggested` へは転送しない。
そもそも転送できない。
両ライブラリのアセンブリは、`CommandManager` を含む `PresentationCore` を参照していないためである。
実行時に読み込まれたアセンブリの参照を読んだ結果が次の表である。

{% include tables/articles/wpf-mvvm-library-command-requery/library-command-references.ja.md %}

.NET 10 / Windows 11 で、各型を含むアセンブリの `GetReferencedAssemblies()` と `TargetFrameworkAttribute` を読んだ結果。どちらも WPF のアセンブリ（`PresentationCore`・`PresentationFramework`・`WindowsBase`）を参照していない。
{: .table-caption}

ボタンの `IsEnabled` も、この構造どおりに振る舞う。
名前を入れた後、`InvalidateRequerySuggested` を呼んだ後、キーボードフォーカスを 2 つ目の `TextBox` へ移した後の値を、書き方ごとに測った。

{% include tables/articles/wpf-mvvm-library-command-requery/library-command-requery-matrix.ja.md %}

.NET 10 / Windows 11、CommunityToolkit.Mvvm 8.4.0 / Prism.Core 8.1.97 で、名前（taro）を WPF の入力処理（`InputManager`）を通して入力し（2 行目だけはビューモデルの `Name` へコードで代入し）、続けて `CommandManager.InvalidateRequerySuggested()` を呼び、最後にキーボードフォーカスを 2 つ目の `TextBox` へ移して、各段階の後に保存ボタンの `IsEnabled` を読んだ。`CanExecute` はどの行も「名前が空でない」である。
{: .table-caption}

**setter で `SetProperty` だけを呼ぶ書き方は、Toolkit でも Prism でも、どの段階でも `False` のままである。**
`InvalidateRequerySuggested` を呼んでも、フォーカスを移しても変わらない。

対照の自作のコマンドは、`InvalidateRequerySuggested` の通知を受け取る。
名前をコードで代入した 2 行目は、代入しただけでは `False` のままで、`InvalidateRequerySuggested` を呼んだ後に `True` になった。
名前をキーで入力した 1 行目は、何も呼ばなくても入力した時点で `True` になる。
WPF はキー入力を受けて `RequerySuggested` を発生させるためで、[WPF で RelayCommand の CanExecute がボタンの有効・無効に反映されない問題の解決方法](/ja/articles/wpf-relaycommand-canexecute-not-updating/)でも同じ結果を測っている。

自作のコマンドから Toolkit や Prism へ移してボタンが動かなくなるのは、この入力による再問い合わせだけで `CanExecute` を反映していた画面である。

---

## 効く条件

`InvalidateRequerySuggested` が効くのは、`CanExecuteChanged` の登録を `RequerySuggested` へ転送するコマンドだけである（前節の表の 2 行目）。
Toolkit と Prism のコマンドはこれに当たらないため、コマンド自身の `CanExecuteChanged` を発生させる。
前節の表のとおり、次の 4 つの書き方は、いずれも名前を入れた時点で `True` になった。

| ライブラリ | 書き方 | 通知の経路 |
| --- | --- | --- |
| CommunityToolkit.Mvvm | setter で `SaveCommand.NotifyCanExecuteChanged()` を呼ぶ | 手で書いた setter のコード |
| CommunityToolkit.Mvvm | `[ObservableProperty]` に `[NotifyCanExecuteChangedFor(nameof(SaveCommand))]` を付ける | ソース生成された setter |
| Prism | setter で `SaveCommand.RaiseCanExecuteChanged()` を呼ぶ | 手で書いた setter のコード |
| Prism | `new DelegateCommand(...).ObservesProperty(() => Name)` | コマンドがビューモデルの `PropertyChanged` を購読する |

どれも、`CanExecute` が依存するプロパティの変化をコマンドへ知らせる経路を持つ点が共通する。
`InvalidateRequerySuggested` は、その経路の代わりにならない。

---

## 実装例

Toolkit では、ソース生成の属性で書くと、依存するプロパティとコマンドの関係が宣言に残る。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed partial class CustomerViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = "";

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        // 保存する
    }

    private bool CanSave() => !string.IsNullOrEmpty(Name);
}
```

`[RelayCommand]` は `Save` から `SaveCommand` プロパティを生成し、`[NotifyCanExecuteChangedFor]` は生成された `Name` の setter に `SaveCommand.NotifyCanExecuteChanged()` の呼び出しを加える。
クラスは `partial` にする必要がある。
ビューの XAML は「問題」の節のものをそのまま使う。

Prism では、コマンドを作るときに `ObservesProperty` で依存するプロパティを登録する。

```csharp
using Prism.Commands;
using Prism.Mvvm;

public sealed class CustomerViewModel : BindableBase
{
    private string _name = "";

    public CustomerViewModel()
    {
        SaveCommand = new DelegateCommand(Save, () => !string.IsNullOrEmpty(Name))
            .ObservesProperty(() => Name);
    }

    public DelegateCommand SaveCommand { get; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private void Save()
    {
        // 保存する
    }
}
```

`ObservesProperty` は、ビューモデルの `PropertyChanged` で `Name` の変化を受け取り、`CanExecuteChanged` を発生させる。
setter に通知の呼び出しを書かなくてよいので、`SetProperty` だけの setter をそのまま残せる。

---

## 注意点

- **依存するプロパティごとに書く。**
  `[NotifyCanExecuteChangedFor]` は付けたプロパティの setter から、`ObservesProperty` は登録したプロパティの変更通知から、コマンドに知らせる。
  `CanExecute` が複数のプロパティを読むなら、そのすべてに書く。
  本記事の計測は、依存するプロパティが 1 つの場合だけである。
- **ほかの経路で変わる状態は、変化を受け取った箇所で発生させる。**
  通信の完了やサービスの状態のように、上の 2 つの仕組みが見ていない経路で変わる値に `CanExecute` が依存するなら、変化を受け取った箇所で `NotifyCanExecuteChanged()` または `RaiseCanExecuteChanged()` を呼ぶ。
  本記事では、UI スレッドから呼ぶ場合だけを測った。
- **移行の手引きでは気付きにくい。**
  Microsoft の [MvvmLight からの移行の手引き](https://learn.microsoft.com/ja-jp/dotnet/communitytoolkit/mvvm/migratingfrommvvmlight)は、Toolkit の `RelayCommand` を同等の機能を持つ型として紹介し、`GalaSoft.MvvmLight.CommandWpf` を含む `using` の差し替えと、`RaiseCanExecuteChanged()` を `NotifyCanExecuteChanged()` に置き換えることを挙げている。
  `CommandManager` との関係には触れていない。
  元のコードが入力による再問い合わせに頼っていて `RaiseCanExecuteChanged()` を呼んでいなければ、置き換える呼び出しも無いため、移行後はどこからも通知されない。
  なお、MvvmLight のコマンドそのものは本記事では測っていない。
- **Toolkit と Prism のコマンドに対しては、`InvalidateRequerySuggested` に効果がない。**
  呼ぶと、`RequerySuggested` に登録されたほかのコマンドソースが `CanExecute` を問い合わせ直す。
  Toolkit と Prism のコマンドだけの画面では外してよい。

---

## まとめ

Toolkit の `RelayCommand` と Prism の `DelegateCommand` は、`CanExecuteChanged` を `CommandManager.RequerySuggested` に転送せず、そもそも WPF のアセンブリを参照していない。
そのため setter が `SetProperty` だけなら、`CommandManager.InvalidateRequerySuggested()` を呼んでも、キー入力やフォーカスの移動があっても、ボタンの有効・無効は変わらない。

ボタンを更新するには、そのコマンド自身の `CanExecuteChanged` を発生させる。
Toolkit では `[NotifyCanExecuteChangedFor]` を、Prism では `ObservesProperty` を使うと、依存するプロパティとコマンドの関係が宣言として残り、setter に呼び出しを書き忘れることがない。
それらが見ていない経路で変わる状態に依存する場合だけ、変化を受け取った箇所で `NotifyCanExecuteChanged()` または `RaiseCanExecuteChanged()` を呼ぶ。

---

## 関連記事

- [WPF で RelayCommand の CanExecute がボタンの有効・無効に反映されない問題の解決方法](/ja/articles/wpf-relaycommand-canexecute-not-updating/)
- [WPF で入力検証のエラーが表示されない原因と IDataErrorInfo / INotifyDataErrorInfo の使い分け](/ja/articles/wpf-validation-error-not-displayed/)

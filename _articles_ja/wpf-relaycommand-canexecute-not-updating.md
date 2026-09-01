---
layout: article-ja
title: "WPF で RelayCommand の CanExecute がボタンの有効・無効に反映されない問題の解決方法"
date: 2026-07-23
category: WPF
excerpt: "自作 RelayCommand の CanExecute を変えてもボタンが更新されないのは CanExecuteChanged が発火されないため。CommandManager.RequerySuggested への委譲と手動発火の使い分けを整理する。"
image: /images/articles/wpf-relaycommand-canexecute-not-updating/relaycommand-canexecute-button-state.png
---

## 概要

WPF の MVVM では、ボタンを ViewModel の `ICommand` にバインドし、`CanExecute` の結果でボタンの有効・無効を切り替える。
ところが、`CanExecute` が参照する条件（入力の有無など）を変えてもボタンの状態が変わらない、という不具合が頻発する。
本記事では、この現象が `ICommand.CanExecuteChanged` イベントの発火漏れに起因することを説明し、`CommandManager.RequerySuggested` へ委譲する方式と、自前で `CanExecuteChanged` を発火する方式の実装・使い分けを整理する。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF（.NET Framework 4.5 以降でも同様）
- 言語: C# / XAML（コード例は nullable 参照型有効を前提とする。C# 7 以前では nullable 注釈を外して読む）
- 対象機能: `System.Windows.Input.ICommand` を実装した自作 `RelayCommand`、`Button.Command` バインド
- アーキテクチャ: MVVM（コマンドロジックを ViewModel に置く構成）
- 名前空間: `System`、`System.Windows.Input`
- 検証環境: .NET 10 / Windows 11

本記事の図は、上記の環境で `CanExecute` の戻り値を切り替えながら実際にボタンを表示し、`Button.IsEnabled` を読み出して得たものである。
戻り値を変えただけでは `IsEnabled` が変わらないこと、`InvalidateRequerySuggested` で更新されるのは `RequerySuggested` に委譲した実装だけであること、自前で `CanExecuteChanged` を発火した場合に更新されるのはその実装だけであること、`Command` が未設定のボタンは有効のままであることを、この環境で確認している。

---

## 問題

`Button.Command` に ViewModel のコマンドをバインドし、`CanExecute` に入力状態を反映する構成を考える。
以下は、名前の入力有無で保存ボタンの有効・無効を切り替える意図のコードである。

```csharp
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public RelayCommand(Action execute, Func<bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute();

    public void Execute(object? parameter) => _execute();

    // 誰も発火しないため、ボタンの状態は初回評価のまま固定される
    public event EventHandler? CanExecuteChanged;
}
```

`CanExecute` は初回バインド時に評価される。本来はその後 `CanExecuteChanged` の発火に応じて再評価されるが、この実装では一度も発火していないため、`Name` を入力しても保存ボタンは無効のまま変わらない。
ボタンが `CanExecute` を再評価する契機が無いことが原因である。

<figure class="article-figure">
  <img src="/images/articles/wpf-relaycommand-canexecute-not-updating/relaycommand-canexecute-button-state.png" alt="同じ文字列を入力した 2 組の入力欄とボタン。CanExecuteChanged を発火しない実装ではボタンが無効のまま、CommandManager.RequerySuggested へ委譲した実装ではボタンが有効になっている。" width="382" height="179" loading="lazy">
  <figcaption>どちらも同じ条件（<code>Name</code> が空でなければ実行可能）で、同じ文字列を入力した状態。上は <code>CanExecuteChanged</code> を一度も発火しない実装で、入力してもボタンは無効のままである。下は <code>CommandManager.RequerySuggested</code> へ委譲した実装で、再問い合わせが走りボタンが有効になる。</figcaption>
</figure>

---

## 原因・背景

コマンドソース（`Button` など）は、`ICommand.CanExecuteChanged` イベントを購読し、これが発火したときにだけ `CanExecute` を呼び直して自身の有効・無効を更新する。
公式ドキュメントも「コマンドソースは通常 `CanExecuteChanged` を購読し、発火時に `CanExecute` を呼んで、実行不可なら自身を無効化する」と記述している。
したがって `CanExecuteChanged` を発火しない限り、`CanExecute` の戻り値がいくら変化してもボタンには反映されない。
なお、この判定が働くのは `Command` に `ICommand` が設定されている場合に限られる。
バインドが解決できず `Command` が `null` のままなら判定対象が無く、ボタンは有効表示のまま無反応になる（[WPF の DataTemplate 内から親の DataContext にバインドできない原因と RelativeSource の使い分け](/ja/articles/wpf-datatemplate-parent-datacontext-binding/)）。

WPF 標準の `RoutedCommand` がこの問題を表面化させにくいのは、その `CanExecuteChanged` が `CommandManager.RequerySuggested` に委譲されているためである。
`CommandManager` は、キーボードフォーカスの移動などコマンドの実行可否に影響し得る操作を検知すると `RequerySuggested` を発火し、バインドされた各コマンドに再評価を促す。
一方、自作の `RelayCommand` はこの仕組みに乗っていないため、`CanExecuteChanged` を自分で発火する責務がある。
さらに、`CommandManager` が検知するのはフォーカス変更などの UI 操作に限られ、ViewModel のプロパティ変更のような UI 非依存の条件変化は検知しない点にも注意が必要である。

---

どの発火方法がどちらの実装に効くかは、実際にボタンを表示して `IsEnabled` を読めば確かめられる。
`CanExecute` が `false` を返す状態で表示し、`true` を返すよう条件を変えてから、何も呼ばない場合・`CommandManager.InvalidateRequerySuggested()` を呼ぶ場合・コマンド自身の `RaiseCanExecuteChanged()` を呼ぶ場合を測った結果が次の図である。

<figure class="article-figure">
  <img src="/images/articles/wpf-relaycommand-canexecute-not-updating/relaycommand-requery.svg" alt="実装と発火方法の組み合わせごとに Button.IsEnabled を測った表。何も呼ばない場合はどちらの実装も False のまま。InvalidateRequerySuggested で True になるのは RequerySuggested に委譲した実装だけ。RaiseCanExecuteChanged で True になるのは自前イベントの実装だけ。Command 未設定のボタンは最初から True。" width="548" height="290" loading="lazy">
  <figcaption>.NET 10 / Windows 11 で、<code>CanExecute</code> の戻り値を <code>false</code> から <code>true</code> に変えた前後の <code>Button.IsEnabled</code> を測った結果。<code>before</code> は条件を変える直前、<code>after</code> は変えて発火操作を行った直後の値である。</figcaption>
</figure>

**何も呼ばなければ、どちらの実装でも `IsEnabled` は `False` のままである。** `CanExecute` の戻り値が変わっただけでは反映されない。

そのうえで、**発火方法と実装は対応していなければ効かない。**
`InvalidateRequerySuggested()` が効くのは `CanExecuteChanged` を `CommandManager.RequerySuggested` へ委譲した実装だけであり、自前のイベントを持つ実装には届かない。
逆に `RaiseCanExecuteChanged()` が効くのは自前のイベントを持つ実装だけである。
どちらの方式を採るかで、条件が変わったときに呼ぶべきものが変わる。

最終行は対照である。`Command` が未設定のボタンは判定の対象が無いため、最初から有効のまま変化しない。

---

## 2 つの発火方式

`CanExecuteChanged` を発火する方式は 2 つある。

- **`CommandManager.RequerySuggested` へ委譲する** — `CanExecuteChanged` の購読を `CommandManager.RequerySuggested` に転送する。UI 操作に伴う再評価に自動で相乗りでき、実装も少ない。UI 非依存の条件は `CommandManager.InvalidateRequerySuggested()` で明示的に再評価を促す。
- **自前で `CanExecuteChanged` を発火する** — 独自のイベントを保持し、条件が変わった時点で明示的に発火する。再評価の対象が当該コマンドに限られ、発火の契機を完全に制御できる。

前者は「WPF の再評価サイクルに相乗りする」方式、後者は「必要なときだけ自分で再評価させる」方式である。

### CommandManager.RequerySuggested へ委譲する

`CanExecuteChanged` の `add` / `remove` を `CommandManager.RequerySuggested` へ転送する。
これにより、フォーカス移動などの UI 操作のたびに `CommandManager` が再評価を促し、ボタンの状態が追従する。

```csharp
public event EventHandler? CanExecuteChanged
{
    add    => CommandManager.RequerySuggested += value;
    remove => CommandManager.RequerySuggested -= value;
}
```

UI 操作を伴わない条件変化（タイマー・非同期処理の完了など）では、次のように明示的に再評価を促す。
`InvalidateRequerySuggested` は `RequerySuggested` を発火し、これに接続されたコマンドソース（標準の `RoutedCommand` や委譲方式の `RelayCommand` を購読するボタン等）に `CanExecute` の再問い合わせを促す。

```csharp
// 条件が変わったが UI 操作が伴わない場合に呼ぶ
CommandManager.InvalidateRequerySuggested();
```

この呼び出しは即座に評価するのではなく、`RequerySuggested` を発火して接続中のコマンドソースに `CanExecute` の再問い合わせを促す。
そのため、後述のとおり `RequerySuggested` に接続された各コマンドソースを再評価させるコストを伴う。

### 自前で CanExecuteChanged を発火する

冒頭の `RelayCommand` の `CanExecuteChanged` を独自イベントに変え、再評価が必要な時点で発火するメソッドを追加する。

```csharp
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public RelayCommand(Action execute, Func<bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute();
    public void Execute(object? parameter) => _execute();

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

ViewModel 側では `SaveCommand` を初期化し、`CanExecute` が参照するプロパティ（`Name`）を更新した時点で `RaiseCanExecuteChanged` を呼ぶ。
以下は保存ボタンの有効条件（`Name` の入力有無）が変わるたびに再評価させる、コンパイル可能な最小構成である。

```csharp
public class SaveViewModel
{
    public RelayCommand SaveCommand { get; }

    public SaveViewModel()
    {
        // Name が空でなければ実行可能
        SaveCommand = new RelayCommand(Save, () => !string.IsNullOrEmpty(Name));
    }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            // 入力状態が変わったので保存コマンドを再評価させる
            SaveCommand.RaiseCanExecuteChanged();
        }
    }

    private void Save() { /* 保存処理を実装する */ }
}
```

この方式では、再評価されるのは `SaveCommand` だけであり、発火のタイミングも明確である。
`CommunityToolkit.Mvvm` の `RelayCommand` はこの方式を採用しており、`NotifyCanExecuteChanged()` メソッドや `[NotifyCanExecuteChangedFor]` 属性で同等の発火を行う。

---

## 選択の分岐点

どちらを使うかは、実行可否を決める条件がどこにあるかで決まる。

**実行可否が UI 操作（フォーカス移動・選択の変化）に連動する場合は委譲方式。**
`CommandManager` はこれらの操作を契機に再評価を促すため、発火を書かなくても追従する。記述が最も少ない。

**実行可否が ViewModel のプロパティで決まる場合は自前発火。**
プロパティが変わった時点で対象のコマンドだけを再評価でき、契機が読み手に見える。`CommunityToolkit.Mvvm` などのフレームワークもこの方式を採る。

**委譲方式のまま UI 非依存の条件変化を反映したい場合は、その時点で `CommandManager.InvalidateRequerySuggested()` を呼ぶ。**
非同期処理の完了など、UI 操作を伴わない契機がこれにあたる。ただし `RequerySuggested` に接続されたコマンドソースをまとめて再評価するため、頻度が高いと応答性に響く。

---

## 方式別の比較

| 方式 | メリット | デメリット | 適するケース |
|---|---|---|---|
| `RequerySuggested` へ委譲 | 実装が少なく UI 操作に自動追従 | `RequerySuggested` 接続分を広く再評価・発火契機が不透明・弱参照の考慮が要る | 実行可否が主に UI 操作（フォーカス・選択）に連動する |
| 自前で `CanExecuteChanged` を発火 | 対象コマンドのみ再評価・契機が明確 | 条件変化ごとに発火の記述が必要 | ViewModel のプロパティ変化で可否が決まる |
| `InvalidateRequerySuggested` を都度呼ぶ | 委譲方式のまま任意契機で再評価できる | `RequerySuggested` 接続分の再評価コスト・呼び忘れ | 委譲方式で UI 非依存の条件変化を反映したい |

---

## 注意点

- **`RequerySuggested` は弱参照でハンドラを保持する:** `CommandManager.RequerySuggested` は登録されたハンドラを弱参照で保持する。委譲方式では、コマンドソース（`Button` など）が生存している間はハンドラが保たれる仕組みが WPF 側に用意されているため、通常は問題にならない。一方、自分で `RequerySuggested` へハンドラを登録する場合は、そのハンドラが到達可能なまま保たれるよう寿命を管理する必要がある。ローカル変数やラムダのまま登録すると、回収された時点で再評価が止まる。
- **`InvalidateRequerySuggested` は UI スレッドで呼ぶ:** この API が促す `CommandManager` の再評価は UI スレッド側で処理され、対象のコマンドソース（UI 要素）も UI スレッドに属する。そのため呼び出しも UI スレッドを前提とし、バックグラウンドスレッドで状態を変えた場合は、`Dispatcher` で UI スレッドへ移してから呼ぶ。
- **自前発火も UI スレッドで行う:** `RaiseCanExecuteChanged` の発火はボタン側のハンドラ（UI 要素の更新）を同期的に呼び出す。別スレッドから発火すると UI 要素へ別スレッドで触れることになるため、`Dispatcher` 経由で UI スレッドに寄せる。
- **`CanExecute` は軽量に保つ:** `InvalidateRequerySuggested` は `RequerySuggested` に接続されたコマンドソースに `CanExecute` を問い直させる。重い処理を書くと、頻繁な再評価が UI の応答性を損なう。
- **`CanExecute` を空実装のまま放置しない:** 冒頭のように `CanExecuteChanged` を宣言だけして発火しない実装は、コンパイルは通るが状態が固定される典型的な原因である。

---

## まとめ

ボタンの有効・無効が更新されないのは、`CanExecute` の結果ではなく `CanExecuteChanged` の発火漏れが原因である。

分岐点は、実行可否を決める条件が UI 操作にあるか ViewModel にあるかにある。
UI 操作なら `CommandManager.RequerySuggested` へ委譲し、ViewModel のプロパティなら自前で発火する。
いずれの発火も UI スレッドで行い、`CanExecute` を軽量に保つことが、応答性を損なわない前提となる。

---

<!-- 関連記事 -->
- [WPF で ObservableCollection をバックグラウンドスレッドから更新するとクロススレッド例外が発生する問題の解決方法](/ja/articles/wpf-observablecollection-cross-thread-update/)

---
layout: article-ja
title: "WPF で RelayCommand の CanExecute がボタンの有効・無効に反映されない問題の解決方法"
date: 2026-07-23
category: WPF
excerpt: "自作 RelayCommand の CanExecute を変えてもボタンが更新されないのは CanExecuteChanged が発火されないため。CommandManager.RequerySuggested への委譲と手動発火の使い分けを整理する。"
---

## 概要

WPF の MVVM では、ボタンを ViewModel の `ICommand` にバインドし、`CanExecute` の結果でボタンの有効・無効を切り替える。
ところが、`CanExecute` が参照する条件（入力の有無など）を変えてもボタンの状態が変わらない、という不具合が頻発する。
本記事では、この現象が `ICommand.CanExecuteChanged` イベントの発火漏れに起因することを説明し、`CommandManager.RequerySuggested` へ委譲する方式と、自前で `CanExecuteChanged` を発火する方式の実装・使い分けを整理する。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF（.NET Framework 4.5 以降でも同様）
- 言語: C# / XAML（コード例は nullable 有効・target-typed new を前提とする。C# 8 以前では明示型で記述する）
- 対象機能: `System.Windows.Input.ICommand` を実装した自作 `RelayCommand`、`Button.Command` バインド
- アーキテクチャ: MVVM（コマンドロジックを ViewModel に置く構成）
- 名前空間: `System`、`System.Windows.Input`

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

`CanExecute` は起動直後に一度は評価されるが、その後に `Name` を入力しても保存ボタンは無効のまま変わらない。
`CanExecuteChanged` を一度も発火していないため、ボタンが `CanExecute` を再評価する契機が無いことが原因である。

---

## 原因・背景

コマンドソース（`Button` など）は、`ICommand.CanExecuteChanged` イベントを購読し、これが発火したときにだけ `CanExecute` を呼び直して自身の有効・無効を更新する。
公式ドキュメントも「コマンドソースは通常 `CanExecuteChanged` を購読し、発火時に `CanExecute` を呼んで、実行不可なら自身を無効化する」と記述している。
したがって `CanExecuteChanged` を発火しない限り、`CanExecute` の戻り値がいくら変化してもボタンには反映されない。

WPF 標準の `RoutedCommand` がこの問題を表面化させにくいのは、その `CanExecuteChanged` が `CommandManager.RequerySuggested` に委譲されているためである。
`CommandManager` は、キーボードフォーカスの移動などコマンドの実行可否に影響し得る操作を検知すると `RequerySuggested` を発火し、バインドされた各コマンドに再評価を促す。
一方、自作の `RelayCommand` はこの仕組みに乗っていないため、`CanExecuteChanged` を自分で発火する責務がある。
さらに、`CommandManager` が検知するのはフォーカス変更などの UI 操作に限られ、ViewModel のプロパティ変更のような UI 非依存の条件変化は検知しない点にも注意が必要である。

---

## 解決方法

`CanExecuteChanged` を発火する方式は 2 つある。

- **`CommandManager.RequerySuggested` へ委譲する** — `CanExecuteChanged` の購読を `CommandManager.RequerySuggested` に転送する。UI 操作に伴う再評価に自動で相乗りでき、実装も少ない。UI 非依存の条件は `CommandManager.InvalidateRequerySuggested()` で明示的に再評価を促す。
- **自前で `CanExecuteChanged` を発火する** — 独自のイベントを保持し、条件が変わった時点で明示的に発火する。再評価の対象が当該コマンドに限られ、発火の契機を完全に制御できる。

前者は「WPF の再評価サイクルに相乗りする」方式、後者は「必要なときだけ自分で再評価させる」方式である。

---

## 実装例

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
`InvalidateRequerySuggested` はバインド中の全コマンドを一括で再評価させる。

```csharp
// 条件が変わったが UI 操作が伴わない場合に呼ぶ
CommandManager.InvalidateRequerySuggested();
```

### 自前で CanExecuteChanged を発火する

独自イベントとして `CanExecuteChanged` を保持し、再評価が必要な時点で発火するメソッドを用意する。

```csharp
public event EventHandler? CanExecuteChanged;

public void RaiseCanExecuteChanged()
    => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
```

ViewModel 側では、`CanExecute` が参照するプロパティを更新した時点で `RaiseCanExecuteChanged` を呼ぶ。
以下は保存ボタンの有効条件（`Name` の入力有無）が変わるたびに再評価させる例である。

```csharp
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
```

この方式では、再評価されるのは `SaveCommand` だけであり、発火のタイミングも明確である。
`CommunityToolkit.Mvvm` の `RelayCommand` はこの方式を採用しており、`NotifyCanExecuteChanged()` メソッドや `[NotifyCanExecuteChangedFor]` 属性で同等の発火を行う。

---

## 注意点

- **`RequerySuggested` は弱参照でハンドラを保持する:** `CommandManager.RequerySuggested` は登録されたハンドラを弱参照で保持する。委譲方式ではコマンド側がハンドラの強参照を持たないため、コマンド自体が生存していればよく通常は問題にならないが、ハンドラを直接 `RequerySuggested` に登録する独自実装では、強参照を別途保持しないとハンドラが回収されて再評価が止まる。
- **`InvalidateRequerySuggested` は UI スレッドで呼ぶ:** この API は `CommandManager` に再評価を促すもので、UI スレッド上での呼び出しを前提とする。バックグラウンドスレッドから状態を変えた場合は、`Dispatcher` で UI スレッドへ移してから呼ぶ。
- **自前発火も UI スレッドで行う:** `RaiseCanExecuteChanged` の発火はボタン側のハンドラ（UI 要素の更新）を同期的に呼び出す。別スレッドから発火すると UI 要素へ別スレッドで触れることになるため、`Dispatcher` 経由で UI スレッドに寄せる。
- **委譲方式は全コマンドを再評価する:** `InvalidateRequerySuggested` はバインド中の全コマンドの `CanExecute` を呼び直す。`CanExecute` に重い処理を書くと、頻繁な再評価が UI の応答性を損なう。`CanExecute` は軽量に保つ。
- **`CanExecute` を空実装のまま放置しない:** 冒頭のように `CanExecuteChanged` を宣言だけして発火しない実装は、コンパイルは通るが状態が固定される典型的な原因である。

---

## 代替案・比較

| 方式 | メリット | デメリット | 適するケース |
|---|---|---|---|
| `RequerySuggested` へ委譲 | 実装が少なく UI 操作に自動追従 | 全コマンド一括再評価・発火契機が不透明・弱参照の考慮が要る | 実行可否が主に UI 操作（フォーカス・選択）に連動する |
| 自前で `CanExecuteChanged` を発火 | 対象コマンドのみ再評価・契機が明確 | 条件変化ごとに発火の記述が必要 | ViewModel のプロパティ変化で可否が決まる |
| `InvalidateRequerySuggested` を都度呼ぶ | 委譲方式のまま任意契機で再評価できる | 全コマンド再評価のコスト・呼び忘れ | 委譲方式で UI 非依存の条件変化を反映したい |

---

## まとめ

ボタンの有効・無効が更新されないのは、`CanExecute` の結果ではなく `CanExecuteChanged` の発火漏れが原因である。
選択基準は次のとおりである。

- **実行可否がフォーカスや選択など UI 操作に連動する場合:** `CommandManager.RequerySuggested` へ委譲する。記述が最も少なく、UI 操作に自動で追従する。
- **実行可否が ViewModel のプロパティで決まる場合:** 自前で `CanExecuteChanged` を発火する。対象コマンドだけを、条件が変わった時点で再評価できる。既存のフレームワーク（`CommunityToolkit.Mvvm` など）を使う場合もこの方式に相当する。
- **委譲方式のまま非同期完了などを反映したい場合:** その時点で `CommandManager.InvalidateRequerySuggested()` を呼ぶ。

いずれの発火も UI スレッドで行い、`CanExecute` は軽量に保つことが、応答性を損なわない前提となる。

---

<!-- 関連記事 -->
- [WPF で ObservableCollection をバックグラウンドスレッドから更新するとクロススレッド例外が発生する問題の解決方法](/ja/articles/wpf-observablecollection-cross-thread-update/)

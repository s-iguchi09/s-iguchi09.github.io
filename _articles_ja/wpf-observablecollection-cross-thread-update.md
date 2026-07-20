---
layout: article-ja
title: "WPF で ObservableCollection をバックグラウンドスレッドから更新するとクロススレッド例外が発生する問題の解決方法"
date: 2026-07-20
category: WPF
excerpt: "バインド中の ObservableCollection を別スレッドから変更すると NotSupportedException が発生する。原因のスレッドアフィニティと、EnableCollectionSynchronization・Dispatcher による解決策を整理する。"
---

## 概要

WPF で `ItemsControl` にバインドした `ObservableCollection<T>` を、UI スレッド以外のスレッドから変更すると、`NotSupportedException` が発生する。
メッセージは「この種類の `CollectionView` では、`Dispatcher` スレッドと異なるスレッドからの `SourceCollection` への変更はサポートされていません」といった内容になる（文言は .NET のバージョンやロケールで多少異なる）。
本記事では、この例外がコレクション自体ではなく `CollectionView` のスレッドアフィニティに起因することを説明し、`BindingOperations.EnableCollectionSynchronization` と `Dispatcher` を使った解決方法、および両者の使い分けの基準を整理する。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF（.NET Framework 4.5 以降でも同様）
- 言語: C# / XAML（本文のコード例は target-typed new（`= new();`、C# 9 以降）を用いる。C# 8 以前では `= new ObservableCollection<string>();` のように明示型で記述する）
- 対象コントロール・機能: `ObservableCollection<T>`、`ItemsControl`（`ListBox`・`DataGrid`・`ListView` 等を含む）、`CollectionView`
- アーキテクチャ: MVVM・コードビハインドのいずれにも適用可能
- 前提: バックグラウンドスレッド（`Task.Run` やワーカースレッド）でコレクションを更新する構成

---

## 問題

バインド済みのコレクションを、バックグラウンドスレッドから直接変更すると例外が送出される。
以下は、`Task.Run` で開始した処理の中から `ObservableCollection<T>` に要素を追加する例である。

```csharp
public ObservableCollection<string> Items { get; } = new();

private async Task LoadAsync()
{
    await Task.Run(() =>
    {
        foreach (var line in ReadHugeFile())
        {
            // UI スレッド以外からの Add で NotSupportedException が発生する
            Items.Add(line);
        }
    });
}
```

`Items` が `ItemsControl.ItemsSource` にバインドされている場合、`Add` の呼び出しは `CollectionChanged` 通知を通じて `CollectionView` に伝わる。
この通知が UI スレッド以外から届くため、`CollectionView` が例外を送出する。

---

## 原因・背景

原因は `ObservableCollection<T>` そのものではなく、WPF がコレクションを表示する際に経由する `CollectionView` にある。
公式ドキュメントは、`ItemsControl` と `CollectionView` の両方が「`ItemsControl` を生成したスレッドへのアフィニティ（親和性）を持ち、異なるスレッドからの使用は禁止され、例外が送出される」と明記している。
この制約は事実上、バインド対象のコレクションにも及ぶ。

WPF のオブジェクトの多くは `DispatcherObject` から派生し、生成元スレッド（通常は UI スレッド）へのスレッドアフィニティを持つ。
`CollectionView` も `DispatcherObject` から派生し、既定ではバインド対象コレクションが別スレッドから変更されることを許可しない。
そのため、`CollectionChanged` 通知が UI スレッド以外から届くと、`CollectionView` はクロススレッドの変更を許可しないものとして `NotSupportedException` を送出する。
問題の本質は「コレクションを別スレッドで触ったこと」ではなく、「別スレッドからの変更通知を UI スレッド専有の `CollectionView` が受け取れないこと」にある。

---

## 解決方法

アプローチは 2 つある。

- **`Dispatcher` で UI スレッドへマーシャリングする** — コレクションの変更操作自体を UI スレッド上で実行する。実装が単純で、既存コードにも適用しやすい。
- **`BindingOperations.EnableCollectionSynchronization` を使う** — アプリ側でロックを用意し、そのロックを WPF に登録することで、バックグラウンドスレッドからの直接変更を許可する。大量更新でも UI スレッドを占有しにくい。

前者は「変更を UI スレッドへ寄せる」方法、後者は「別スレッドでの変更を WPF に安全に取り込ませる」方法である。

---

## 実装例

### Dispatcher で UI スレッドへマーシャリングする

コレクションの変更を `Dispatcher.Invoke`（または `InvokeAsync`）で UI スレッドへ移す。
`Application.Current.Dispatcher` を用いれば、ViewModel からも UI スレッドの `Dispatcher` を取得できる。

```csharp
private async Task LoadAsync()
{
    var dispatcher = Application.Current.Dispatcher;
    await Task.Run(() =>
    {
        foreach (var line in ReadHugeFile())
        {
            // Add を UI スレッド上で実行するため例外は起きない
            dispatcher.Invoke(() => Items.Add(line));
        }
    });
}
```

変更操作が UI スレッドで実行されるため、`CollectionView` のアフィニティ違反は発生しない。
ただし要素ごとに `Invoke` すると UI スレッドへの往復が多発するため、まとめて追加できる場合は 1 回の `Invoke` 内で複数要素を処理するのが適切である。

### EnableCollectionSynchronization でロックを共有する

ロックオブジェクトを用意し、UI スレッド上で `EnableCollectionSynchronization` を呼んで WPF に登録する。
以降はアプリ側の変更も、その同じロックで保護する。

```csharp
private readonly object _lock = new();
public ObservableCollection<string> Items { get; } = new();

public ViewModel()
{
    // 呼び出しは UI スレッドで、かつコレクションを別スレッドで使う前に行う
    BindingOperations.EnableCollectionSynchronization(Items, _lock);
}

private async Task LoadAsync()
{
    await Task.Run(() =>
    {
        foreach (var line in ReadHugeFile())
        {
            lock (_lock)
            {
                Items.Add(line);
            }
        }
    });
}
```

`EnableCollectionSynchronization` を呼ぶと、`CollectionView` は登録されたロックを使ってコレクションへアクセスし、UI スレッド用の「シャドウコピー」を保持する。
変更通知は到着順にキューされ、UI スレッドが処理可能なときに反映される。
これによりバックグラウンドスレッドから直接 `Add` できる。
公式ドキュメントの要件どおり、呼び出しは UI スレッドで、かつコレクションを別スレッドで使う前（またはコントロールへ結び付ける前）に行う必要がある。

---

## 注意点

- **アプリ側の全アクセスを同じロックで保護する:** `EnableCollectionSynchronization` に渡したロックは、WPF だけでなくアプリのすべての読み書きで使わなければならない。片方でもロックを外すと、`CollectionView` のアクセスと競合し得る。
- **変更と通知の原子性:** 変更（`Add` 等）とその `CollectionChanged` 通知が原子的である必要がある。`ObservableCollection<T>` は、すべての変更を同じ同期で保護していればこれを保証する。
- **登録のタイミングと解除:** `EnableCollectionSynchronization` と `DisableCollectionSynchronization` はいずれも UI スレッドで呼ぶ。複数の UI スレッドで同じコレクションを使う場合は、各 UI スレッドで個別に登録する。
- **`Dispatcher.Invoke` の多用は UI を圧迫する:** 要素単位の同期 `Invoke` を大量に回すと UI スレッドが飽和し、応答性が落ちる。件数が多い場合は `EnableCollectionSynchronization` か、UI スレッド側でのバッチ追加を検討する。
- **UI 要素自体は依然 UI スレッド専有:** この解決策が緩めるのはバインド対象コレクションへのアクセスのみである。`DependencyObject`（コントロール等）を別スレッドから直接操作することは引き続き不可である。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| `Dispatcher.Invoke` / `InvokeAsync` | 追加設定が不要で単純。既存コードに適用しやすい | 要素ごとの往復で UI スレッドを圧迫しやすい | 更新頻度・件数が少ない。散発的な追加・削除 |
| `EnableCollectionSynchronization`（単純ロック） | バックグラウンドから直接変更でき、UI を占有しにくい | ロックの一貫運用が必要。設計がやや複雑 | 大量・高頻度の更新を別スレッドで行う |
| `EnableCollectionSynchronization`（コールバック版） | セマフォ等、ロック以外の同期機構を使える | 実装が最も複雑 | 独自の同期機構が既にある構成 |
| UI スレッドでまとめて反映 | スレッド問題を回避できる | バックグラウンドの利点が薄れる | 収集後に一括反映できる処理 |

---

## まとめ

バインド中の `ObservableCollection<T>` を別スレッドから変更したときの例外は、コレクションではなく `CollectionView` のスレッドアフィニティに起因する。
解決策の選択基準は次のとおりである。

- **更新が散発的で件数が少ない場合:** `Dispatcher.Invoke` / `InvokeAsync` で変更を UI スレッドへ寄せる。追加設定が不要で最も単純である。
- **別スレッドで大量・高頻度に更新する場合:** `EnableCollectionSynchronization` でロックを共有し、バックグラウンドからの直接変更を許可する。UI スレッドの圧迫を避けられる。
- **セマフォなど独自の同期機構がある場合:** コールバック版のオーバーロードを使う。

いずれの場合も、UI 要素そのものは UI スレッド専有のままである点を踏まえ、緩めるのはコレクションアクセスに限る、という前提で設計するのが要点である。

---

<!-- 関連記事 -->
- [WPF ComboBox の ItemsSource バインドパターンと選択値の取得方法](/ja/articles/wpf-combobox-itemssource-patterns/)
- [WPF ListBox 仮想化環境での SelectedItems が消えたように見える問題とその解決法](/ja/articles/wpf-listbox-virtualization-selecteditems/)

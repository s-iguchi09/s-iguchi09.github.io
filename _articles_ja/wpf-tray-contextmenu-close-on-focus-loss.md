---
layout: article-ja
title: "WPF タスクトレイの ContextMenu がフォーカス移動で閉じない問題の解消方法"
date: 2026-06-07
category: WPF
excerpt: "TreePaste で発生したタスクトレイから表示した ContextMenu が閉じない問題に対し、StaysOpen=False と SetForegroundWindow の併用で解消する方法を整理します。"
---

## 概要

本記事では、TreePaste の開発時に発生した「タスクトレイアイコンの右クリックで表示した `ContextMenu` が、フォーカス移動後も閉じない」問題を扱う。  
解消には `ContextMenu.StaysOpen = false` の設定に加えて、表示直前に `Win32Api.SetForegroundWindow()` を呼び出し、メニュー表示の起点をフォアグラウンド状態へ揃える構成を採用する。  

---

## 前提・対象環境

- フレームワーク／言語: .NET 10 / C# 14
- 対象 UI: WPF + `System.Windows.Forms.NotifyIcon`
- アーキテクチャ: コードビハインド
- 対象プロジェクト: TreePaste

---

## 問題

タスクトレイアイコンを右クリックして `ContextMenu` を表示すると、メニュー項目を選択しない限り表示が残り続ける挙動が発生した。  
本来は他ウィンドウへフォーカスが移った時点でメニューが閉じることが期待されるが、実際には閉じず、操作体験を阻害していた。  

---

## 原因・背景

WPF 側の `ContextMenu` は、`StaysOpen` の設定と表示時のフォアグラウンド状態に依存して閉じ方が変わる。  
タスクトレイ起点の右クリックは通常の Window 配下の右クリックと異なり、アクティブウィンドウ遷移の文脈がずれる場合がある。  
その状態で `ContextMenu` を開くと、フォーカス喪失を契機としたクローズ判定が期待どおりに働かないことがある。  

---

## 解決方法

採用した解決策は次の 2 点である。  

- `ContextMenu.StaysOpen = false` を明示する。
- `ContextMenu` を開く直前に `Win32Api.SetForegroundWindow()` を呼び、メニュー表示時のウィンドウ状態をフォアグラウンドへ合わせる。

この 2 点を併用すると、右クリックで開いたメニューがフォーカス移動時に閉じる挙動へ安定する。  

---

## 実装例

最初に、タスクトレイ右クリック時の処理で `SetForegroundWindow` を呼び出してから `ContextMenu` を開く。  
以下は TreePaste の `MainWindow.xaml.cs` で使用している実装である。  

```csharp
_notifyIcon.MouseClick += (_, e) =>
{
    if (e.Button == System.Windows.Forms.MouseButtons.Right)
    {
        Dispatcher.Invoke(() =>
        {
            var helper = new WindowInteropHelper(this);
            Win32Api.SetForegroundWindow(helper.Handle);
            _trayContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            _trayContextMenu.IsOpen = true;
        });
    }
};
```

この順序により、メニューを開く直前のウィンドウ状態を明示的に整えられる。  
`IsOpen = true` だけを先に実行する構成より、フォーカス遷移時の挙動が安定しやすい。  

次に、`ContextMenu` 側で `StaysOpen = false` を明示する。  
以下は TreePaste の `CreateTrayContextMenu()` で返却している構成である。  

```csharp
return new System.Windows.Controls.ContextMenu
{
    Items = { showItem, githubItem, separator, exitItem },
    StaysOpen = false
};
```

`StaysOpen = false` は「外側をクリックした際に閉じる」ための基本設定である。  
タスクトレイ起点の表示では、この設定単独では不十分な場合があり、前段の `SetForegroundWindow` との組み合わせが有効である。  

`SetForegroundWindow` は `Win32Api` で次のように定義して使用する。  

```csharp
[DllImport("user32.dll")]
public static extern bool SetForegroundWindow(IntPtr hWnd);
```

Win32 API の P/Invoke 定義を追加しておくことで、WPF アプリ側から表示制御の補助が可能になる。  

---

## 注意点

- `SetForegroundWindow` は OS のフォアグラウンド制御制約を受けるため、常に完全な制御を保証するものではない。
- `NotifyIcon` と WPF `ContextMenu` を混在させる実装では、UI スレッド上でメニュー操作を行うため `Dispatcher.Invoke` を維持する。
- `StaysOpen = false` を設定しても、表示元の状態が不整合なままでは期待どおりに閉じないケースがある。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| `StaysOpen = false` のみ | 実装が最小で簡潔 | タスクトレイ起点では閉じない事例が残る | 通常のウィンドウ内右クリック中心のメニュー |
| `SetForegroundWindow` のみ | 表示時のアクティブ状態を補正できる | `ContextMenu` 設定不足時の閉じ漏れを防ぎ切れない | 既存メニュー設定を維持したい場合 |
| 2 つを併用（採用） | 表示時とクローズ条件の双方を補完できる | Win32 API の依存が増える | タスクトレイメニューを安定動作させたい場合 |

---

## まとめ

TreePaste で発生したタスクトレイ `ContextMenu` の閉じ残りは、`StaysOpen = false` と `SetForegroundWindow()` の併用で解消できる。  
この構成により、右クリックで表示したメニューがフォーカス移動時に閉じる標準的な操作感へ揃えられる。  
タスクトレイ起点のメニューを WPF で扱う場合は、メニュー設定だけでなく、表示直前のフォアグラウンド制御を合わせて設計することが有効である。  

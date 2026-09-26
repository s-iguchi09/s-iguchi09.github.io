---
layout: article-ja
title: "WPF タスクトレイの ContextMenu がフォーカス移動で閉じない問題の解消方法"
date: 2026-06-07
category: WPF
excerpt: "TreePaste の開発で発生した、タスクトレイから表示した WPF の ContextMenu がフォーカス移動後も閉じない問題について、StaysOpen=False と表示直前の SetForegroundWindow の併用で解消する方法と、その原因を整理する。"
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
- 検証環境: .NET 10 / Windows 11

### 本記事の根拠について

本記事の中心的な主張、すなわち「タスクトレイ起点で開いた `ContextMenu` が `StaysOpen = false` だけでは閉じず、`SetForegroundWindow` の併用で閉じるようになる」は、**TreePaste の実際の開発で遭遇し、この構成に変更して解消した事象**に基づく。

この挙動は自動テストで再現しにくい。
トレイアイコンへの実際のマウス入力と、別プロセス間のフォアグラウンド遷移が同時に必要になるためである。
実際、後述のフォアグラウンド制限により、直前にユーザー入力を受けていないプロセスからは前面化そのものができない。

そのうえで、次の点は上記の環境で実際に動かして確認した。

- `ContextMenu.StaysOpen` の既定値は、依存関係プロパティのメタデータでも、`new` した直後の値でも `true` だった。[公式リファレンス](https://learn.microsoft.com/dotnet/api/system.windows.controls.contextmenu.staysopen)は既定値を `false` としており、実装と食い違っている。
- 自分のウィンドウが既に前面にあるときに `SetForegroundWindow` を呼ぶと `true` が返り、前面のままだった。
- 別のプロセスのウィンドウが前面にある状態で、このプロセスから自分のウィンドウへ `SetForegroundWindow` を呼ぶと `false` が返り、前面は切り替わらなかった。

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-tray-contextmenu-close-on-focus-loss/tray-contextmenu-facts.svg" alt="ContextMenu.StaysOpen と SetForegroundWindow を測った表。ContextMenu.StaysOpen のメタデータの既定値と new した直後の値はどちらも True、Popup.StaysOpen の既定値も True。自分のウィンドウが既に前面のときの SetForegroundWindow は True を返し、前面のまま。別のプロセスが前面のときは False を返し、前面は切り替わらない。" width="810" height="230" loading="lazy">
  <figcaption>.NET 10 / Windows 11 で実測。別のプロセスが前面の行は、補助の PowerShell のプロセスに小さなフォームを出させて前面を渡し、そのあとで呼んだ結果である。</figcaption>
</figure>

`ContextMenu` が閉じるかどうかそのものは、上記の理由から自動化して確認できていない。

---

## 問題

タスクトレイアイコンを右クリックして `ContextMenu` を表示すると、メニュー項目を選択しない限り表示が残り続ける挙動が発生した。  
本来は他ウィンドウへフォーカスが移った時点でメニューが閉じることが期待されるが、実際には閉じず、操作体験を阻害していた。  

---

## 原因・背景

`StaysOpen` の既定値は `true` であり、そのままでは `ContextMenu` は外側の操作で閉じない（前掲の表）。  
さらに、タスクトレイ起点の右クリックでは、メニューを開いた時点で自アプリが前面にないことがある。TreePaste では、`StaysOpen = false` だけではこの状態でメニューが閉じないことがあり、開く前に自ウィンドウを前面へ移すと閉じるようになった。  
前面にないことがどのように閉じる判定へ影響するかという内部の仕組みは、自動化して確かめられていない。  

---

## 解決方法

採用した解決策は次の 2 点である。  

- `ContextMenu.StaysOpen = false` を明示する。
- `ContextMenu` を開く直前に `Win32Api.SetForegroundWindow()` を呼び、メニュー表示時のウィンドウ状態をフォアグラウンドへ合わせる。

この 2 点を併用すると、右クリックで開いたメニューがフォーカス移動時に閉じる挙動へ安定する。  

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-tray-contextmenu-close-on-focus-loss/tray-contextmenu-foreground.svg" alt="タスクトレイの右クリックからメニュー表示までの流れを比較した図。SetForegroundWindow を呼ばない場合はフォアグラウンドが別アプリのままでメニューが閉じず、呼ぶ場合はメニューを開く前に自ウィンドウをフォアグラウンドへ移す。" width="880" height="356" loading="lazy">
  <figcaption>右クリックからメニュー表示までの経路の違い。上段は <code>StaysOpen</code> だけを設定した場合で、フォアグラウンドが別アプリのままメニューが開く。TreePaste では、この経路でメニューが閉じないことがあった。下段は表示前に <code>SetForegroundWindow</code> を呼ぶ経路である。Windows にはフォアグラウンド化の制限があり、この API は常に成功するとは限らない（上の P/Invoke 宣言では失敗時に <code>false</code> が返る）。トレイアイコンのクリック直後は、フォアグラウンド化が許可される条件の 1 つを満たすが、それでも Windows が拒否して <code>false</code> を返すことはある。TreePaste ではこの構成で切り替わることを確認しているが、成功を前提にせず、戻り値を見て失敗時の経路も用意しておく。</figcaption>
</figure>

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
この例は TreePaste と同じく `SetForegroundWindow` の戻り値を見ていない。呼び出しに失敗すると、図の上段と同じく、ウィンドウが前面にないままメニューが開く。記録や再試行などの対処が要るなら、戻り値を確かめる。  
TreePaste では、`IsOpen = true` だけを実行していた構成から、この呼び出しと次に示す `StaysOpen = false` の設定を 1 度の変更で加えたところ、メニューが閉じるようになった。それぞれ単独の効果は TreePaste では切り分けていない。  

次に、`ContextMenu` 側で `StaysOpen = false` を明示する。  
以下は TreePaste の `CreateTrayContextMenu()` で返却している構成である。  

```csharp
return new System.Windows.Controls.ContextMenu
{
    Items = { showItem, githubItem, separator, exitItem },
    StaysOpen = false
};
```

`ContextMenu.StaysOpen` の既定値は `true` であり（前掲の表。公式リファレンスの既定値 `false` とは食い違う）、明示的に `false` にしない限り外側のクリックでは閉じない。
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

- `SetForegroundWindow` は OS のフォアグラウンド制御制約を受けるため、常に完全な制御を保証するものではない。実測では、別のプロセスのウィンドウが前面にある状態で、直前にユーザー入力を受けていないこのプロセスから自分のウィンドウを前面化しようとすると `false` が返り、前面は切り替わらなかった（前掲の表）。トレイアイコンのクリック直後は許可条件の 1 つを満たすが、**それで成功が保証されるわけではない**。タイマーやバックグラウンド処理から呼ぶ場合はより失敗しやすい。いずれの場合も戻り値を確認し、`false` のときはメニューが閉じない可能性があるものとして扱う。
- `NotifyIcon` と WPF `ContextMenu` を混在させる実装では、UI スレッド上でメニュー操作を行うため `Dispatcher.Invoke` を維持する。
- `StaysOpen = false` を設定しても、表示元の状態が不整合なままでは期待どおりに閉じないケースがある。
- タスクトレイ常駐はウィンドウの有無と寿命が一致しないため、`ShutdownMode` を `OnExplicitShutdown` にしたうえで終了メニューから `Shutdown()` を呼ぶ（呼び忘れるとウィンドウが無いままプロセスが残る。切り分けは[WPF でウィンドウを閉じてもプロセスが終了しない原因の切り分けと ShutdownMode・フォアグラウンドスレッドの扱い](/ja/articles/wpf-application-not-exiting-shutdownmode-threads/)で扱っている）。

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

---
layout: article-ja
title: "WPF で ScrollViewer がスクロールしない原因と解決方法"
date: 2026-07-17
category: WPF
excerpt: "StackPanel の中に置いた ScrollViewer がスクロールしないのは、StackPanel が高さを制約しないためである。原因と、Grid や DockPanel を使った解決方法・選択基準を解説する。"
---

## 概要

WPF の `ScrollViewer` は、内部の要素がビューポートより大きいときにスクロールバーを表示し、スクロールを可能にするコントロールである。
しかし `StackPanel` の中に `ScrollViewer` を置くと、内容がどれだけ増えてもスクロールバーが現れず、要素が下方向に伸び続ける問題が起きる。
本記事では、この現象がレイアウトの測定処理に起因することを説明し、コンテナの選び方による解決方法と選択基準を整理する。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF
- 言語: C# / XAML
- 対象コントロール: `ScrollViewer`、`StackPanel`、`Grid`、`DockPanel`
- アーキテクチャ: MVVM・コードビハインドのいずれにも適用可能

---

## 問題

縦方向の `StackPanel` の中に `ScrollViewer` を配置し、その中へ多数の要素を並べても、スクロールバーが表示されずに画面外まで要素が伸びてしまうことがある。

```xml
<StackPanel>
    <TextBlock Text="ヘッダー" />
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel>
            <!-- 大量の項目 -->
        </StackPanel>
    </ScrollViewer>
</StackPanel>
```

`VerticalScrollBarVisibility="Auto"` を指定しているにもかかわらず、`ScrollViewer` は内容全体の高さまで広がり、スクロールバーは現れない。

---

## 原因・背景

原因は、`StackPanel` が子要素を測定する際に渡す利用可能サイズにある。
`StackPanel` は積み重ねる方向（縦方向の場合は高さ）について、子要素へ**無限の利用可能サイズ**を渡して測定する。
公式ドキュメントでも、`StackPanel` はスタックする方向で子要素を制約しないことが示されている。

`ScrollViewer` は、与えられた高さより内容が大きいときにだけスクロールバーを表示する。
ところが `StackPanel` から無限の高さを渡されると、`ScrollViewer` は内容全体が収まる高さを要求し、オーバーフローが発生しない。
このため、スクロールバーは表示されず、`ScrollViewer` 自体が内容と同じ高さまで伸びてしまう。

問題の本質は `ScrollViewer` 側ではなく、それを囲む `StackPanel` が高さを制約していない点にある。

---

## 解決方法

`ScrollViewer` を、高さが制約されるコンテナに配置する。
具体的には、`Grid` の `*`（Star）指定の行に置くか、`DockPanel` で残り領域に収めるか、明示的な `Height`・`MaxHeight` を与える。
これにより `ScrollViewer` へ有限の高さが渡され、内容がその高さを超えたときにスクロールバーが表示される。

---

## 実装例

### Grid の Star 行に配置する

`Grid` の行を「固定サイズの行」と「残り領域を埋める `*` 行」に分け、スクロールさせたい `ScrollViewer` を `*` 行へ置く。

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
    </Grid.RowDefinitions>

    <TextBlock Grid.Row="0" Text="ヘッダー" />

    <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
        <StackPanel>
            <!-- 大量の項目 -->
        </StackPanel>
    </ScrollViewer>
</Grid>
```

`*` 行は親の残り高さを受け取るため、`ScrollViewer` には有限の高さが渡される。
内容がその高さを超えると、スクロールバーが自動的に表示される。

### DockPanel で残り領域に収める

`DockPanel` は最後の子要素を残り領域へ広げる `LastChildFill`（既定で有効）を持つ。
ヘッダーを上端にドッキングし、`ScrollViewer` を最後の子として残り領域に収める。

```xml
<DockPanel>
    <TextBlock DockPanel.Dock="Top" Text="ヘッダー" />

    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel>
            <!-- 大量の項目 -->
        </StackPanel>
    </ScrollViewer>
</DockPanel>
```

`DockPanel.Dock` を指定しない最後の子は残り領域に収まるため、`ScrollViewer` の高さが制約される。

---

## 注意点

- **`VerticalScrollBarVisibility="Disabled"` はスクロール自体を無効化する:** `Disabled` を指定すると、その方向のスクロールがユーザー操作で行えなくなる。
  スクロールバーを隠しつつスクロールは残したい場合は `Hidden` を使う。
- **内側にスクロール対応コントロールを二重に置かない:** `ScrollViewer` の中へ独自のスクロールを持つ `ListBox` などをそのまま入れると、マウスホイールの操作が競合し、意図した側がスクロールしないことがある。
- **物理スクロールと論理スクロールの違い:** `ScrollViewer.CanContentScroll` が `false`（既定）のときはピクセル単位の物理スクロール、`true` のときは項目単位の論理スクロールとなる。
  仮想化した `ListBox` などは論理スクロールを前提とするため、`ScrollViewer` で囲む場合はこの値の扱いに注意する。

---

## 代替案・比較

| コンテナ | スクロールの挙動 | 適するケース |
| --- | --- | --- |
| `StackPanel`（縦） | 高さを制約せず、`ScrollViewer` はスクロールしない | 内容が短く、スクロールが不要な単純な積み重ね |
| `Grid` の `*` 行 | 残り高さが渡り、内容超過時にスクロールする | ヘッダー・フッターと可変領域を明確に分けたい画面 |
| `DockPanel`（`LastChildFill`） | 残り領域に収まり、内容超過時にスクロールする | 端固定要素と本体領域を組み合わせる画面 |
| 明示的な `Height`・`MaxHeight` | 指定した高さを超えるとスクロールする | 高さの上限を固定したい部分的なリスト |

---

## まとめ

`StackPanel` はスタック方向で子要素に無限の高さを渡すため、内部の `ScrollViewer` はオーバーフローを検知できずスクロールしない。
問題の本質は `ScrollViewer` ではなく、それを囲むコンテナが高さを制約していない点にある。

選択の基準は次のとおりである。

- **ヘッダーやフッターと可変領域を分けたい場合:** `Grid` の `*` 行に `ScrollViewer` を置く。
  役割ごとに行を分けられ、レイアウトの意図が明確になる。
- **端に固定する要素と本体領域を組み合わせる場合:** `DockPanel` を使い、`ScrollViewer` を最後の子として残り領域に収める。
- **部分的なリストの高さ上限だけを決めたい場合:** `MaxHeight` を指定し、その高さを超えたときにスクロールさせる。

いずれの場合も、`ScrollViewer` へ有限の高さが渡る構成にすることが解決の要点である。

---

<!-- 関連記事 -->
- [WPF ListBox 仮想化環境での SelectedItems が消えたように見える問題とその解決法](/ja/articles/wpf-listbox-virtualization-selecteditems/)

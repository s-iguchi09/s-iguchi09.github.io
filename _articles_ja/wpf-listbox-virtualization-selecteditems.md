---
layout: article-ja
title: "WPF ListBox 仮想化環境での SelectedItems が消えたように見える問題とその解決法"
date: 2026-04-24
category: WPF
excerpt: "ListBox の仮想化有効時に選択状態が維持されない理由と、IsSelected を各アイテムに持たせて MVVM で安定させる解決方法を解説する。ItemContainerStyle のバインドだけでは選択が失われる条件と、SelectionChanged を併用した対処を実測付きで示す。"
image: /images/articles/wpf-listbox-virtualization-selecteditems/listbox-selection-sync-measurement.png
---

## 概要

WPF の `ListBox` は、大量データを表示するとき `VirtualizingStackPanel` による UI 仮想化が有効になる。
仮想化が有効だと、画面外にあるアイテムのコンテナ(`ListBoxItem`)は破棄され、必要になった時点で再生成される。
このとき、選択状態の管理をコンテナに依存していると、スクロール後に「以前選択した項目が `SelectedItems` に残っていない」ように見えることがある。

対策として広く知られているのは、各アイテムの ViewModel に `IsSelected` を持たせ、`ItemContainerStyle` で `ListBoxItem.IsSelected` を TwoWay バインドする方法である。
ただし**この構成だけでは不十分である**。
実体化されていないコンテナにはバインドが存在しないため、`Ctrl + A` や `Shift` による範囲選択が画面外に及ぶと、その選択はデータ側に届かない。
さらに悪いことに、後からスクロールしてコンテナが実体化されると、データ側の `false` が UI へ書き戻され、**いったん成立していた選択が失われる**。

本記事では、この非対称性を実測で示したうえで、`SelectionChanged` を併用して両方向の同期を成立させる構成を示す。

---

## 前提・対象環境

- フレームワーク / 言語: .NET 10 / C# 14（コード例は .NET 6 / C# 10 以降でそのまま動作する）
- 対象コントロール: WPF `ListBox`(`System.Windows.Controls`)
- アーキテクチャ: MVVM(各アイテム ViewModel が `IsSelected` を公開する)
- OS: Windows 11(WPF は Windows 専用)
- 検証環境: 表示スケール 100%、既定テーマ(Aero2)

以降の例では、UI 仮想化が有効な状態(`ListBox` の既定)で、1 万件規模のコレクションを `ListBox` にバインドすることを前提とする。
`SelectionMode` は複数選択を扱う `Extended` を用いる。
挙動は .NET 6 以降で変わらない。

本記事の数値は、この環境で実際にアプリケーションを起動し、`SelectedItems.Count` とデータ側の `IsSelected` が真である件数を数えて得たものである。

---

## 原因・背景

`ListBox` の UI 仮想化では、スクロールに応じてコンテナが作り直される。
選択状態を次のように扱っている場合、仮想化の影響を受けやすくなる。

- `ListBoxItem` を直接参照して選択を管理している
- Visual Tree からコンテナをたどって `SelectedItems` を構築している
- 再生成されたコンテナに対して選択状態を復元していない

失われているのはデータそのものではなく、コンテナ依存の選択同期である。
`VirtualizationMode="Recycling"` ではコンテナが使い回されるため、再利用されたコンテナに前の選択状態が残る、あるいは復元されないといった不整合がさらに起きやすくなる。

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-listbox-virtualization-selecteditems/virtualization-selection-owner.svg" alt="選択状態の保持先を比較した図。ListBoxItem 側に持たせるとコンテナの再利用で状態が失われ、アイテムの ViewModel に持たせて TwoWay バインドすると状態が保たれる。" width="840" height="330" loading="lazy">
  <figcaption>選択状態をどこに置くかで、コンテナ再利用時の結果が変わる。上段はコンテナの <code>IsSelected</code> だけに状態がある場合で、スクロールでコンテナが作り直されると復元する手がかりが残らない。下段はデータ側に <code>IsSelected</code> を持たせ <code>ItemContainerStyle</code> で双方向にバインドした場合で、再生成されたコンテナはデータから状態を読み直す。</figcaption>
</figure>

### 実体化されるコンテナは件数に依存しない

仮想化が有効なとき、同時に存在する `ListBoxItem` は表示範囲に必要な数だけである。
高さ 600px の `ListBox` で計測すると、コレクションの件数を 100 件から 100,000 件まで変えても、実体化されるコンテナは 31 個で一定であった。

この 31 個だけがバインドを持ち、残り 9,969 件にはバインドが存在しない。
「選択状態がデータ側にあれば安全」という理解は、**データからコンテナへの方向にしか当てはまらない**。
コンテナからデータへ書き戻す方向は、実体化済みの 31 件でしか働かない。

---

## 解決策: 各アイテムに IsSelected を持たせる

複数選択を MVVM で扱う土台として、各アイテム ViewModel に `IsSelected` を持たせる。
選択状態がデータ側にあれば、コンテナが破棄・再生成されても値は保持される。

### ViewModel の例

各行を表す ViewModel に、変更通知付きの `IsSelected` を実装する。

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class RowItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public int Id { get; }
    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public RowItemViewModel(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

`IsSelected` の変更通知は、ViewModel 側で選択状態を変更したときに、実体化済みの `ListBoxItem` へ反映するために必要となる。初期表示やコンテナ再生成の際はバインドが現在値を読み取るため通知は不要だが、双方向に同期させるうえで `INotifyPropertyChanged` を実装しておく。

### 画面全体の ViewModel の例

リスト全体を保持し、選択済みアイテムをデータ側から取得できるようにする。

```csharp
using System.Collections.ObjectModel;
using System.Linq;

public class MainViewModel
{
    public ObservableCollection<RowItemViewModel> Items { get; } = new();

    public MainViewModel()
    {
        for (int i = 1; i <= 10000; i++)
        {
            Items.Add(new RowItemViewModel(i, $"Row {i}"));
        }
    }

    public RowItemViewModel[] GetSelectedItems()
        => Items.Where(x => x.IsSelected).ToArray();
}
```

`GetSelectedItems` はコンテナではなくデータ(`IsSelected`)を走査するため、スクロール位置や仮想化の状態に関わらず、`IsSelected` に反映済みの選択を漏れなく取得できる。

### XAML の例

`ItemContainerStyle` で `ListBoxItem.IsSelected` を各アイテムの `IsSelected` に TwoWay バインドする。

```xml
<ListBox x:Name="RowListBox"
         ItemsSource="{Binding Items}"
         SelectionMode="Extended"
         ScrollViewer.CanContentScroll="True"
         VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling">
    <ListBox.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding Id}" Width="80"/>
                <TextBlock Text="{Binding Name}"/>
            </StackPanel>
        </DataTemplate>
    </ListBox.ItemTemplate>

    <ListBox.ItemContainerStyle>
        <Style TargetType="ListBoxItem">
            <Setter Property="IsSelected"
                    Value="{Binding IsSelected, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
        </Style>
    </ListBox.ItemContainerStyle>
</ListBox>
```

コンテナが再生成されると、バインドが `IsSelected` の値を読み直して選択状態を復元する。
ここまでが一般に紹介される構成である。

---

## ItemContainerStyle のバインドだけでは選択が失われる

上の構成に対して、`Ctrl + A`(`SelectAll`)で全件を選択し、そのままページ送りでスクロールしたときに何が起きるかを計測した。
比較のため、`SelectionChanged` でデータ側へ明示的に書き戻す構成と、両方を併用する構成も同時に測った。

<figure class="article-figure">
  <img src="/images/articles/wpf-listbox-virtualization-selecteditems/listbox-selection-sync-measurement.png" alt="仮想化した ListBox で 10,000 件を全選択した後の SelectedItems と IsSelected の件数を、3 つの同期構成で比較した表。ItemContainerStyle のバインドのみの構成では、スクロール後に SelectedItems が 10,000 から 9,845 に減っている。" width="549" height="281" loading="lazy">
  <figcaption>.NET 10 / Windows 11 で、10,000 件をバインドした仮想化済み <code>ListBox</code> に対して <code>SelectAll()</code> を実行し、続けて 10 ページ分スクロールしたときの実測値。<code>ItemContainerStyle</code> のバインドだけの構成では、スクロールによって選択が失われている。</figcaption>
</figure>

読み取れることは 3 点ある。

**1. `SelectAll()` の直後、データ側には 31 件しか届いていない。**
`SelectedItems` は 10,000 件を正しく保持している。
一方でデータ側の `IsSelected` が真になったのは、実体化済みのコンテナに対応する 31 件だけである。
バインドが存在しない 9,969 件には、選択が伝わらない。

**2. スクロールすると、成立していた選択が壊れる。**
10 ページ分スクロールした後、`SelectedItems` は 10,000 件から 9,845 件へ減っている。
新しく実体化されたコンテナが、データ側の `IsSelected`(まだ `false`)を読み取り、その値で自身の選択状態を上書きするためである。
`ItemContainerStyle` のバインドは選択を守るどころか、この経路では選択を消す方向に働く。

**3. `SelectionChanged` を使うと両方が一致する。**
選択の変化をイベントで受けてデータ側へ書き戻す構成では、`SelectAll()` の直後もスクロール後も 10,000 件で一致した。
`SelectionChanged` の `e.AddedItems` / `e.RemovedItems` は、コンテナが実体化されているかどうかに関係なく、変化した項目をすべて含むためである。

### 推奨する構成: バインドと SelectionChanged の併用

どちらか一方ではなく、両方を使う。
役割は次のように分かれる。

| 経路 | 担当 | 対象範囲 |
| --- | --- | --- |
| UI での選択操作 → データ | `SelectionChanged` | 全件 |
| データ → UI(コンテナ実体化時の復元) | `ItemContainerStyle` のバインド | 実体化されたコンテナ |

`SelectionChanged` が全件をデータへ書き戻すため、後からコンテナが実体化されても、バインドは正しい値(`true`)を読み取る。
上の表で「both」の行が両方 10,000 件で一致しているのはこのためである。

コードビハインドに次のハンドラーを置く。

```csharp
private void RowListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    // 実体化されていないアイテムにもバインドは無いため、選択の変化はここで
    // データ側へ反映する。e.AddedItems / e.RemovedItems はコンテナの
    // 実体化状態に関わらず、変化した項目をすべて含む。
    foreach (RowItemViewModel item in e.AddedItems)
    {
        item.IsSelected = true;
    }

    foreach (RowItemViewModel item in e.RemovedItems)
    {
        item.IsSelected = false;
    }
}
```

MVVM を保ちたい場合は、同じ処理を添付ビヘイビアや `System.Windows.Interactivity` の `EventTrigger` から呼び出す。
`SelectionChanged` は `ListBox` の実装詳細ではなくユーザー操作の通知であるため、ViewModel のコマンドへ委譲しても構成上の問題はない。

XAML 側でイベントを結び付ける。

```xml
<ListBox x:Name="RowListBox"
         ItemsSource="{Binding Items}"
         SelectionMode="Extended"
         SelectionChanged="RowListBox_SelectionChanged"
         VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling">
    <!-- ItemTemplate と ItemContainerStyle は前掲のまま -->
</ListBox>
```

### データ側から選択を変更する場合

ViewModel で `IsSelected` を書き換えた場合、その効果は実体化済みのコンテナにしか即座には現れない。
5,000 件を `true` にしても、`SelectedItems` に載るのは表示範囲の分だけである。

ただしこれは選択が失われているわけではない。
`ScrollIntoView` などでその行が実体化されると、バインドがデータから `true` を読み取り、`SelectedItems` に加わる。
実測でも、画面外の 1 件をデータ側で選択した直後は `SelectedItems` が 0 件であったが、その行までスクロールすると 1 件になった。

したがって**選択集合をアプリケーションのロジックから参照するときは、`SelectedItems` ではなくデータ側の `IsSelected` を数える**。
前掲の `GetSelectedItems` がその役割を果たす。

---

## Shift 範囲選択への対応

本手法は `SelectionMode="Extended"` を維持するため、`Shift` による範囲選択や `Ctrl` による追加選択は WPF の標準動作に任せられる。
`Shift` による範囲選択が行われると、`ListBox` は選択された範囲の項目を `SelectedItems` に加え、`SelectionChanged` の `e.AddedItems` に載せる。

範囲が画面外に及んでも `e.AddedItems` には含まれるため、前節の `SelectionChanged` ハンドラーがあればデータ側の `IsSelected` も全件更新される。
ハンドラーが無い場合は、実体化済みのコンテナ分しか更新されない点が `Ctrl + A` と同じである。

---

## 仮想化を壊さない

ここまでの前提は、仮想化が実際に働いていることである。
`ScrollViewer.CanContentScroll` を `False` にすると、スクロール単位がアイテム単位からピクセル単位に変わり、仮想化が無効になる。

その影響を計測した結果が次の表である。

<figure class="article-figure">
  <img src="/images/articles/wpf-listbox-virtualization-selecteditems/listbox-virtualization-cost.png" alt="CanContentScroll の値による違いを比較した表。True では ListBoxItem が 31 個、visual が 152 個であるのに対し、False では 10,000 個と 40,028 個に増え、レイアウト時間も 2 桁大きくなっている。" width="463" height="160" loading="lazy">
  <figcaption>.NET 10 / Windows 11 で、10,000 件をバインドした <code>ListBox</code> の <code>ScrollViewer.CanContentScroll</code> を切り替えて計測した値。5 回試行の最小値を採っている。<code>False</code> にすると全件分のコンテナが構築され、レイアウト時間は 2 桁大きくなる。所要時間は実行環境に依存するため、絶対値ではなく比率として読む。</figcaption>
</figure>

`False` にすると、実体化される `ListBoxItem` は 31 個から 10,000 個へ、visual の総数は 152 個から 40,028 個へ増える。
レイアウト時間の差は 2 桁に達する。

滑らかなピクセル単位スクロールを求めて `CanContentScroll="False"` を設定すると、仮想化が失われて大量件数では実用にならない。
`VirtualizingPanel.ScrollUnit="Pixel"` を使えば、仮想化を保ったままピクセル単位のスクロールにできる。

---

## 注意点

### 1. SelectedItems を直接 TwoWay バインドしない

`SelectedItems` はコレクションだが、WPF の標準コントロールではそのまま素直に TwoWay バインドできない(依存関係プロパティではなく読み取り専用のコレクションであるため)。
複数選択を MVVM で扱う場合は、`IsSelected` パターンと `SelectionChanged` の併用を採用するのが実装・保守の両面で現実的である。

### 2. 選択集合はデータ側から求める

`ListBox.SelectedItems` は、UI 操作で行われた選択については、実体化されていないアイテムも含めて反映する。
一方、ViewModel 側で `IsSelected` を変更した分は、そのアイテムのコンテナが実体化されるまで `SelectedItems` に現れない。
両者は常には一致しないため、**アプリケーションのロジックが参照する選択集合はデータ側(`IsSelected`)に一本化する**。

### 3. コンテナ依存のロジックを避ける

`ItemContainerGenerator.ContainerFromIndex` や Visual Tree の走査に依存すると、仮想化とコンテナの再利用の影響を受けやすくなる。
コンテナを列挙して選択を集計するコードは、画面外のアイテムを取りこぼす。
同じ制約は階層構造を扱う `TreeView` にも当てはまる([WPF TreeView で任意のノードをコードから選択・展開する方法と SelectedItem が読み取り専用である理由](/ja/articles/wpf-treeview-select-item-programmatically/))。

### 4. ItemContainerStyle のバインドだけで済ませない

本記事で最も注意を要する点である。
`ItemContainerStyle` のバインドは、実体化されたコンテナにしか存在しない。
`SelectionChanged` を併用しないまま `Ctrl + A` や広範囲の `Shift` 選択を許すと、スクロールによって選択が減る。

### 5. アイテム数が少なければ標準の SelectedItems で足りる

すべてのコンテナが実体化される規模(数十件程度)であれば、これらの非対称性は表面化しない。
`ListBox.SelectedItems` をそのまま読む実装で問題ない。
本記事の構成が必要になるのは、仮想化が実際に働く件数を扱う場合である。

### 6. ItemTemplate の中身も描画コストに効く

仮想化が有効でも、実体化されたコンテナ 31 個分のテンプレートは毎回構築される。
`ItemTemplate` が重いと、スクロール時のコンテナ再生成で体感速度に影響する。
テンプレート内のコントロール選択については「[WPF で Label を大量配置すると遅い原因と TextBlock への置き換え指針](/ja/articles/wpf-label-vs-textblock-performance/)」で扱う。

---

## まとめ

WPF の `ListBox` で仮想化を有効にした場合、選択状態をコンテナに依存して管理していると、スクロール後に `SelectedItems` が消えたように見えることがある。
対処の要点は、選択状態をデータ側に持たせることと、**その同期を両方向で成立させること**である。

- 各アイテム ViewModel に `IsSelected` を持たせる
- `ListBoxItem.IsSelected` を `IsSelected` に TwoWay バインドする(データ → UI)
- `SelectionChanged` の `e.AddedItems` / `e.RemovedItems` をデータ側へ書き戻す(UI → データ)
- アプリケーションのロジックは `SelectedItems` ではなくデータ側の `IsSelected` を参照する
- `SelectionMode="Extended"` のまま標準の Shift / Ctrl 選択を使う
- 仮想化を維持するため `CanContentScroll="True"` を保ち、ピクセル単位のスクロールが必要なら `VirtualizingPanel.ScrollUnit="Pixel"` を使う

バインドだけ、あるいは `SelectionChanged` だけでは片方向しか成立しない。
数千〜数万件規模のリストで複数選択を扱う場合は、両方を組み合わせる。
選択が少数かつ仮想化が不要な小さいリストであれば、標準の `SelectedItems` をそのまま使う方が簡潔である。

---

<!-- 関連記事 -->
- [WPF で Label を大量配置すると遅い原因と TextBlock への置き換え指針](/ja/articles/wpf-label-vs-textblock-performance/)

---
layout: article-ja
title: "WPF TreeView で任意のノードをコードから選択・展開する方法と SelectedItem が読み取り専用である理由"
date: 2026-08-10
category: WPF
excerpt: "TreeView.SelectedItem は読み取り専用で代入もバインドもできない。選択の実体が TreeViewItem 側にある点を踏まえ、ItemContainerStyle と ItemContainerGenerator の 2 方式を実測して比較する。"
image: /images/articles/wpf-treeview-select-item-programmatically/treeview-select-from-viewmodel.png
---

## 概要

検索でヒットしたフォルダーへのジャンプ、前回終了時の選択位置の復元、追加した項目の選択。
`TreeView` を使う画面では、こうした「コードから任意のノードを選ぶ」処理が求められる場面が多い。
ところが `ListBox` や `DataGrid` と同じ要領で `treeView.SelectedItem = node;` と書くとコンパイルエラーになり、XAML でバインドしても通常の構成ではビルドが通らない。

本記事では、この制約が `TreeView` の選択状態の持ち方に由来することを説明し、コードから選択を指示する 2 つの方式と、表示位置・フォーカスの制御を担う添付ビヘイビアを組み合わせた実装を示す。
記載した挙動と例外メッセージは、いずれも .NET 10 / Windows 11 で実際に動かして確認した結果である。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF（挙動の確認環境は .NET 10 / Windows 11）
- 言語: C# 9 以降 / XAML（コード例はコレクション式（C# 12）を使い、nullable 参照型の有効化を前提とする。C# 11 以前では `[]` を target-typed new（`new()`）に読み替える）
- 対象コントロール: `TreeView` / `TreeViewItem` / `HierarchicalDataTemplate`
- アーキテクチャ: MVVM（選択状態を ViewModel が持つ構成）およびコードビハインドからコンテナを操作する構成
- その他制約: `TreeView` は既定で仮想化しない。仮想化を有効にした場合の差異は「注意点」で扱う

---

## 問題

階層データを `HierarchicalDataTemplate` で表示する、ごく一般的な `TreeView` を対象とする。

```xml
<TreeView x:Name="Tree" ItemsSource="{Binding Roots}">
    <TreeView.ItemTemplate>
        <HierarchicalDataTemplate ItemsSource="{Binding Children}">
            <TextBlock Text="{Binding Name}" />
        </HierarchicalDataTemplate>
    </TreeView.ItemTemplate>
</TreeView>
```

この `TreeView` に対して目的のノードをコードから選択しようとすると、想定される 3 つの経路がいずれも塞がっていることが分かる。

第 1 に、CLR プロパティへの代入はコンパイルできない。
`TreeView.SelectedItem` は getter しか持たないためである。

```csharp
Tree.SelectedItem = target;
// error CS0200: Property or indexer 'TreeView.SelectedItem' cannot be assigned to
// -- it is read only
```

第 2 に、依存関係プロパティとして直接書き込む迂回も失敗する。
`SetValue` は実行時に例外を投げる。

```csharp
Tree.SetValue(TreeView.SelectedItemProperty, target);
// InvalidOperationException: 'SelectedItem' property was registered as read-only
// and cannot be modified without an authorization key.
```

第 3 に、XAML からのバインドも通らない。
XAML をコンパイルする通常のプロジェクト構成では、次の記述はビルドの時点で失敗する。

```xml
<!-- ビルドエラー MC3065 になる -->
<TreeView ItemsSource="{Binding Roots}"
          SelectedItem="{Binding CurrentNode, Mode=TwoWay}" />
```

```text
error MC3065: 'SelectedItem' property is read-only and cannot be set from markup.
```

同じ XAML を `XamlReader.Parse` で実行時に読み込む構成では、読み込みの時点で `XamlParseException` となり、内部例外に `ArgumentException: 'SelectedItem' property cannot be data-bound. (Parameter 'dp')` が入る。
コードから `BindingOperations.SetBinding` を呼んだ場合も同じ `ArgumentException` になる。

`Mode` を `OneWay` にしても結果は変わらない点が、この制約を分かりにくくしている。
読み取り専用の依存関係プロパティは値の書き込み方向を問わずバインディングの**ターゲット**にできず、`Mode` の指定では回避できない。

引用したエラー・例外メッセージは英語リソースのものである。
日本語環境では対応する日本語のメッセージが出力される。

---

## 原因・背景

`TreeView` は選択状態を自分では保持していない。
選択されているという状態を持つのは各 `TreeViewItem` であり、その `IsSelected` プロパティが実体である。
`TreeView.SelectedItem` は、選択されているコンテナに対応するデータ項目を外から読み出すための射影にすぎない。

このため `SelectedItem` は読み取り専用の依存関係プロパティとして登録されている。
実際に `TreeView.SelectedItemProperty.ReadOnly` は `true` を返す。
読み取り専用の依存関係プロパティは、登録時に得られる `DependencyPropertyKey` を保持しているコードだけが値を書き込める。
外部からの `SetValue` が `InvalidOperationException` になるのも、マークアップからの設定やバインドが拒否されるのも、この登録方法の帰結である。

したがって「コードから選択する」という操作は、実質的に「目的のノードに対応する `TreeViewItem` を存在させ、その `IsSelected` を `true` にする」という操作に置き換わる。
ここで 2 つ目の壁が現れる。
`TreeViewItem` は XAML に静的に並んでいるのではなく、コンテナが必要になった時点で `ItemContainerGenerator` が生成するためである。

生成のタイミングは実測すると次のようになる。

| 状態 | `ItemContainerGenerator.Status` | `ContainerFromItem` の戻り値 |
| --- | --- | --- |
| ルート項目（表示済み） | `ContainersGenerated` | コンテナ |
| 折りたたまれた親の子 | `NotStarted` | `null` |
| `IsExpanded = true` の直後 | `NotStarted` | `null` |
| 上記のあと `UpdateLayout()` 実行後 | `ContainersGenerated` | コンテナ |

折りたたまれたノードの子はコンテナが 1 つも作られていない。
さらに、親を展開してもコンテナはその場では作られず、レイアウトパスが走ってはじめて生成される。
`IsExpanded = true` を実行した直後に `ContainerFromItem` を呼んでも `null` が返るのはこのためである。

以上から、この問題は「読み取り専用プロパティをどう書き換えるか」ではなく、「選択という状態をどこに置き、コンテナの生成タイミングにどう対処するか」の設計問題として扱うのが適切である。

---

## 解決方法

選択状態と展開状態を ViewModel 側のノードに持たせ、`ItemContainerStyle` の `Setter` で `TreeViewItem` の `IsSelected` / `IsExpanded` と双方向にバインドする。

この方式が有効なのは、バインドの適用時点がコンテナの生成時点になるためである。
コンテナが存在しない状態で ViewModel のプロパティを変更しても、後からコンテナが生成された時点でスタイルの `Setter` が評価され、その時の ViewModel の値が反映される。
呼び出し側はコンテナの生成タイミングを気にする必要がなく、`UpdateLayout` も不要になる。

`ItemContainerStyle` の `Setter` に書いた `Binding` は、コンテナの `DataContext`、すなわち対応するデータ項目を起点に解決される。
`{Binding IsSelected}` はノードの ViewModel の `IsSelected` を指す。
このため、ノードの型に `IsSelected` / `IsExpanded` を用意しておく必要がある。
`ItemsControl` 系のコントロールでコンテナの `DataContext` がデータ項目へ切り替わる仕組みは [WPF の DataTemplate 内から親の DataContext にバインドできない原因と RelativeSource の使い分け](/ja/articles/wpf-datatemplate-parent-datacontext-binding/) で扱っている。

---

## 実装例

ノードの ViewModel には、表示用の情報に加えて選択状態・展開状態、および親への参照を持たせる。
親への参照は、目的のノードを表示するために祖先をすべて展開する処理で使う。

```csharp
public sealed class FolderNode : INotifyPropertyChanged
{
    private bool isSelected;
    private bool isExpanded;

    public FolderNode(string name) => Name = name;

    public string Name { get; }

    public FolderNode? Parent { get; private set; }

    public ObservableCollection<FolderNode> Children { get; } = [];

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected != value)
            {
                isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded != value)
            {
                isExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    public FolderNode Add(FolderNode child)
    {
        child.Parent = this;
        Children.Add(child);
        return child;
    }

    /// <summary>ルートまでの祖先を展開し、自ノードを選択する。</summary>
    public void SelectAndReveal()
    {
        for (FolderNode? ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            ancestor.IsExpanded = true;
        }

        IsSelected = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

`INotifyPropertyChanged` の実装は省略できない。
双方向バインドのうち UI からの書き戻しは通知が無くても働くが、ViewModel からコンテナへ値を反映する方向は変更通知に依存するためである。

XAML 側では `ItemContainerStyle` に 2 つの `Setter` を置く。
`ItemTemplate` はノードの見た目を、`ItemContainerStyle` はコンテナの状態を担当する、という役割の分離になる。

```xml
<DockPanel Margin="12">
    <TextBlock DockPanel.Dock="Bottom" Margin="4,10,0,0"
               FontFamily="Consolas, Courier New" FontSize="12" Foreground="#333D4D"
               Text="{Binding SelectedItem.Name, ElementName=Tree,
                      StringFormat='TreeView.SelectedItem = {0}'}" />

    <TreeView x:Name="Tree" ItemsSource="{Binding Roots}"
              behaviors:RevealSelectedItemBehavior.IsEnabled="True">
        <TreeView.ItemContainerStyle>
            <Style TargetType="TreeViewItem">
                <Setter Property="IsExpanded" Value="{Binding IsExpanded, Mode=TwoWay}" />
                <Setter Property="IsSelected" Value="{Binding IsSelected, Mode=TwoWay}" />
            </Style>
        </TreeView.ItemContainerStyle>
        <TreeView.ItemTemplate>
            <HierarchicalDataTemplate ItemsSource="{Binding Children}">
                <TextBlock Text="{Binding Name}" />
            </HierarchicalDataTemplate>
        </TreeView.ItemTemplate>
    </TreeView>
</DockPanel>
```

`behaviors` は、後述する添付ビヘイビアを定義した名前空間へ割り当てた XAML 名前空間の接頭辞である（`xmlns:behaviors="clr-namespace:（名前空間）"`）。
`StringFormat` の値を引用符で囲んでいるのは、マークアップ拡張の中で `=` を含む文字列をそのまま書くとパーサーが名前付き引数の区切りと解釈するためである。
また、この `TextBlock` は読み取り専用の `SelectedItem` をバインドの**ソース**として読んでいる。
読み取り専用の依存関係プロパティに書き込めないのはターゲットになる場合であり、値を読み出す用途では制約を受けない。

選択の呼び出しは ViewModel のノードに対して行う。
`TreeView` にもコンテナにも触れない。
`Roots` はウィンドウの `DataContext` に設定した ViewModel が持つルートノードのコレクション、`FindNode` はアプリケーション側で用意した探索処理である。

```csharp
FolderNode target = FindNode(root, "drivers");
target.SelectAndReveal();
```

`SelectAndReveal` の中で祖先を展開する順序は、ルート方向・末端方向のどちらでもよい。
ViewModel のプロパティを設定しているだけであり、コンテナが生成された時点でその値が読み出されるためである。

選択されたノードを表示範囲へスクロールし、フォーカスを移す処理は添付ビヘイビアに切り出す。
`TreeViewItem.Selected` はバブルする RoutedEvent であるため、`TreeView` 側で 1 か所に登録すればすべてのノードを扱える。

```csharp
public static class RevealSelectedItemBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(RevealSelectedItemBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value)
        => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element)
        => (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TreeView treeView)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            treeView.AddHandler(TreeViewItem.SelectedEvent, new RoutedEventHandler(OnItemSelected));
        }
        else
        {
            treeView.RemoveHandler(TreeViewItem.SelectedEvent, new RoutedEventHandler(OnItemSelected));
        }
    }

    private static void OnItemSelected(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem item)
        {
            return;
        }

        item.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                item.BringIntoView();
                item.Focus();
            }));
    }
}
```

`BringIntoView` を `DispatcherPriority.Loaded` へ遅延させているのは、コンテナが生成された直後はレイアウトが確定しておらず、スクロール位置を計算できないためである。
`Focus` を併せて呼ぶと選択がアクティブな配色で描画される。
フォーカスを移したくない画面では `Focus` の行を外す。

上記の XAML とコードをそのまま実行し、3 階層下の `drivers` に対して `SelectAndReveal` を呼んだ結果が次の図である。

<figure class="article-figure">
  <img src="/images/articles/wpf-treeview-select-item-programmatically/treeview-select-from-viewmodel.png" alt="TreeView で C: / Windows / System32 が展開され、その下の drivers が選択色で強調表示されている。下部のテキストに TreeView.SelectedItem = drivers と表示されている。" width="326" height="293" loading="lazy">
  <figcaption>ViewModel の <code>IsExpanded</code> / <code>IsSelected</code> を変更しただけで、祖先が展開され目的のノードが選択された状態。下部の行は読み取り専用の <code>TreeView.SelectedItem</code> をバインドのソースとして読み出したもので、コンテナ側の選択に追従していることを示す。選択部分の配色は OS のアクセントカラー設定に従う（.NET 10 / Windows 11 で生成）。</figcaption>
</figure>

---

## 注意点

- **選択しただけでは表示範囲へスクロールしない。**
200 件のノードを持つ `TreeView` で末尾付近のノードを選択しても、`ScrollViewer` の `VerticalOffset` は `0` のまま変化しなかった。
キーボードやマウスによる選択と異なり、`IsSelected` の変更は表示位置に影響しない。
`BringIntoView` を明示的に呼ぶ必要がある。
- **祖先を展開しないと選択が反映されない。**
祖先が折りたたまれたまま末端ノードの `IsSelected` を `true` にした場合、`TreeView.SelectedItem` は `null` のままだった。
コンテナが存在しないため `Setter` の適用対象が無い。
祖先を展開すると、その時点でコンテナが生成されて選択が反映される。
これは値が失われるのではなく適用が遅延するだけであり、`SelectAndReveal` のように展開と選択をまとめて指示しておけば問題にならない。
- **コンテナへ直接代入したときの結果は `Setter` の書き方で変わる。**
`Setter` に `Mode=TwoWay` の `Binding` を書いた構成では、コンテナの `IsSelected` へ代入してもバインドは維持され、値は ViewModel へ書き戻される。
UI 上の操作と同じ扱いになるだけで、以後の ViewModel 側の変更も引き続き反映される。
一方、`Mode=OneWay` の `Binding` やリテラル値を書いた `Setter` では、代入がローカル値となってスタイルの `Setter` より優先される。
実測でも、`TwoWay` では代入後の値の供給元（`DependencyPropertyHelper.GetValueSource` の `BaseValueSource`）が `Style` のままだったのに対し、`OneWay` では `Local` へ変わり、その後 ViewModel から選択し直しても反映されなくなった。
この優先順位の詳細は [WPF で Style の Trigger・DataTrigger が効かない原因と依存関係プロパティの値優先順位](/ja/articles/wpf-style-trigger-not-working-local-value/) で扱っている。
- **`TreeView` は単一選択である。**
ViewModel 側で複数のノードの `IsSelected` を `true` にしても、実際に選択されるのは 1 つだけである。
別のノードが選択されると、直前に選択されていたノードのコンテナの `IsSelected` が `false` になり、双方向バインド経由で ViewModel にも書き戻される。
選択の排他制御を ViewModel 側で実装する必要はない。
- **ノードの型に該当プロパティが無いとバインディングエラーになる。**
`Setter` の `Binding` はコンテナの `DataContext`、すなわちデータ項目を起点に解決される。
階層ごとに異なる型を混在させる場合は、共通の基底クラスかインターフェイスに `IsSelected` / `IsExpanded` を持たせる。
解決に失敗したバインドは出力ウィンドウに記録される。
メッセージの読み方は [WPF バインディングエラーの読み方と出力ウィンドウを使った原因特定](/ja/articles/wpf-binding-error-debugging-output-window/) で扱っている。
- **仮想化を有効にすると、画面外のノードはそのままでは選択できない。**
`VirtualizingStackPanel.IsVirtualizing="True"` を指定した `TreeView` で画面外のノードの `IsSelected` を `true` にしても、`TreeView.SelectedItem` は `null` のままだった。
コンテナが生成されていないためであり、スクロールして当該ノードが実体化した時点で選択が反映される。
`ItemContainerGenerator` を辿る方式では `ContainerFromItem` が `null` を返すため、コンテナを取得できない。
`BringIndexIntoView` を公開したカスタムの `VirtualizingStackPanel` を `TreeView` と `TreeViewItem` の双方の `ItemsPanel` に据え、階層ごとにコンテナを実体化させれば取得できるが、実装量は大きく増える。
仮想化と選択状態の関係は [WPF ListBox 仮想化環境での SelectedItems が消えたように見える問題とその解決法](/ja/articles/wpf-listbox-virtualization-selecteditems/) でも扱っている。

---

## 代替案・比較

選択を指示する 2 方式と添付ビヘイビアに、選択結果を読み出すだけの `SelectedItemChanged` を加えて比較する。

| 方法 | 選択の指示 | 仮想化との相性 | メリット | デメリット |
| --- | --- | --- | --- | --- |
| `ItemContainerStyle` で双方向バインド | ViewModel のプロパティ | 実体化した時点で反映 | コンテナの生成タイミングを意識しない。MVVM から離れない | ノードの型に状態を持たせる必要がある |
| `ItemContainerGenerator` を辿る | コンテナへの直接代入 | 画面外は実体化させないと取得できない | ViewModel を変更せずに済む | 各階層で `UpdateLayout` が必要。双方向バインドを併用しない構成では設定した値がローカル値になる |
| 添付ビヘイビア | 上記のいずれかを内包 | 内包した方式に従う | 表示位置やフォーカスの制御を再利用できる | 単体では選択の指示手段にならない |
| `SelectedItemChanged` | 指示できない（読み出しのみ） | 影響なし | UI 上の選択を ViewModel へ渡せる | 逆方向、すなわちコードからの選択には使えない |

`ItemContainerGenerator` を辿る方式は、ViewModel を変更できない場合や、既存のコードビハインドへ最小の追加で対応する場合の選択肢になる。
ルートから目的のノードまでのパスを受け取り、各階層でコンテナを取得しながら展開していく。

```csharp
public static bool SelectByPath(TreeView treeView, IReadOnlyList<object> path)
{
    ItemsControl parent = treeView;

    for (int i = 0; i < path.Count; i++)
    {
        // 直前に展開した階層（初回はルート）のコンテナを生成させる。
        parent.UpdateLayout();

        if (parent.ItemContainerGenerator.ContainerFromItem(path[i]) is not TreeViewItem container)
        {
            return false;
        }

        if (i == path.Count - 1)
        {
            container.IsSelected = true;
            container.BringIntoView();
            return true;
        }

        container.IsExpanded = true;
        parent = container;
    }

    return false;
}
```

`UpdateLayout` の呼び出しがこの実装の要である。
同じコードから `UpdateLayout` を取り除くと、ルートの子を取得する時点で `ContainerFromItem` が `null` を返し、メソッドは `false` を返して選択に失敗した。
展開してからコンテナを取得するまでの間に、レイアウトパスを 1 回挟む必要がある。

なお `UpdateLayout` は同期的にレイアウトパス全体を実行するため、階層が深いツリーや項目数の多いツリーでは相応のコストになる。
このメソッドの呼び出し回数はパスの階層数に比例するため、常時走る処理からではなく、選択を移動する操作の中でのみ呼び出す。

---

## まとめ

`TreeView.SelectedItem` が読み取り専用なのは実装上の制限ではなく、選択の状態を保持しているのが `TreeViewItem` 側であることの反映である。
コードから選択するには、目的のノードのコンテナを存在させ、その `IsSelected` を `true` にするという経路をとる。

方式の選択基準は次のとおりである。

- **MVVM で構成された画面:**
ノードの ViewModel に `IsSelected` / `IsExpanded` を持たせ、`ItemContainerStyle` で双方向にバインドする。
コンテナが未生成でも指示が失われず、`UpdateLayout` も不要になるため、既定の選択肢とする。
- **ViewModel を変更できない場合:**
`ItemContainerGenerator` を辿って `TreeViewItem` を取得する。
各階層で `UpdateLayout` を挟むこと、双方向バインドを併用しない構成では設定した値がローカル値になることの 2 点を許容できる場合に限る。
- **表示位置やフォーカスの制御:**
`TreeViewItem.Selected` を `TreeView` で受ける添付ビヘイビアに切り出す。
選択の指示方法とは独立しているため、上記のいずれの方式とも組み合わせられる。
- **仮想化を有効にする場合:**
画面外のノードはコンテナを持たないため、コンテナへの直接操作を前提とした実装はそのままでは成立しない。
双方向バインドの方式に統一する。

---
layout: article-ja
title: "WPF で Mode を書かないバインドが、ユーザー操作で黙って外れる仕組み"
date: 2026-10-06
category: WPF
excerpt: "Mode を書かずに TreeViewItem の IsExpanded や ColumnDefinition の Width をバインドすると、展開ボタンや GridSplitter の操作でバインドが黙って外れる。既定の向きと書き込みの経路を実測し、外れる条件と避け方を示す。"
image: /images/articles/wpf-binding-mode-omitted-detached/binding-mode-user-input.svg
---

## 概要

`TreeView` のノードの開閉をビューモデルに持たせ、`ItemContainerStyle` で `IsExpanded` をバインドする書き方は、MVVM でよく使われる。
ところが `Mode` を書かずにバインドすると、ユーザーが展開ボタンで一度開いたノードは、以後ビューモデルから閉じても反映されなくなる。
例外は出ず、出力ウィンドウにもバインドのエラーは出ない。

原因は 2 つの組み合わせにある。
1 つは、`Mode` を書かないバインドの向きはプロパティの既定に従い、`TreeViewItem.IsExpanded` の既定は片方向であること。
もう 1 つは、展開ボタンがテンプレートの中のバインドを通して `IsExpanded` に値を書き込み、その書き込みが片方向のバインドを働かなくすることである。

本記事では、`Mode` を書かずにバインドされやすいプロパティの既定の向きと、実際のマウスとキーボードで操作したときにバインドがどうなるかを実測した。
そのうえで、バインドが外れる条件と、外さないための書き方を示す。

---

## 前提・対象環境

- フレームワーク: WPF（.NET Framework 4.0 以降 / .NET Core 3.0 以降）
- 対象: `Mode` を省略した `{Binding}`、`TreeViewItem.IsExpanded`、`ColumnDefinition.Width` と `GridSplitter`、`Expander.IsExpanded` など
- アーキテクチャ: MVVM（ビューモデルは `INotifyPropertyChanged` を実装する）
- 検証環境: .NET 10 / Windows 11（既定のテーマで、Fluent テーマは測っていない）
- 計測方法: OS のマウスとキーボードの入力（`SendInput`）で、各条件を 1 回ずつ操作した。
  操作の後に、`BindingOperations.GetBindingExpressionBase`・`DependencyPropertyHelper.GetValueSource`・ソースの値を読んだ。
  さらにソースに値を代入して（変更を通知させて）表示を読み、操作の間に出たバインドのエラーと警告を `PresentationTraceSources.DataBindingSource` で数えた。
  この計測は `tools/screenshot-capture` のシーンとして実装している。

---

## 現象

フォルダーの木を表示し、各ノードの開閉をビューモデルの `IsExpanded` に結ぶ。
`Setter` の `Binding` には `Mode` を書いていない。

```xml
<TreeView x:Name="folderTree" ItemsSource="{Binding Folders}">
  <TreeView.ItemContainerStyle>
    <Style TargetType="TreeViewItem">
      <Setter Property="IsExpanded" Value="{Binding IsExpanded}" />
    </Style>
  </TreeView.ItemContainerStyle>
  <TreeView.ItemTemplate>
    <HierarchicalDataTemplate ItemsSource="{Binding Children}">
      <TextBlock Text="{Binding Name}" />
    </HierarchicalDataTemplate>
  </TreeView.ItemTemplate>
</TreeView>
```

ビューモデルの `IsExpanded` を変えれば、ノードは開閉する。
ところが、展開ボタンで開いたノードは、その後にビューモデルの `IsExpanded` を `false` にしても閉じない。
次の表は、操作の後にバインドがどうなったかを実測した結果である。
1〜8 行目がこの XAML（6〜8 行目は `Mode=TwoWay` を足したもの）で、9〜16 行目は比較のための別の構成、17 行目はエラーの数え方の確認である。

{% include tables/articles/wpf-binding-mode-omitted-detached/binding-mode-user-input.ja.md %}

.NET 10 / Windows 11（既定のテーマ）で、実際のマウスとキーボードで各条件を 1 回ずつ操作した結果。4 行目は実際の入力を使わずコードで設定し、5・10 行目はクリックの後にコードで `ClearValue` を呼んだ。`GridSplitter` は 60 DIP（表示スケールによらない WPF の単位）ドラッグした。「操作後のバインド」は `BindingOperations.GetBindingExpressionBase` が null を返したかどうかで、「外れる」には `Setter` のバインドがローカル値に隠れた場合も含む。ソースの初期値は `False`（`ColumnDefinition` の行は 100）で、「ソースに代入した後の表示」はソースに操作前の値を代入し直した後の値である（5・10 行目だけは、ソースに `True` を代入した後の値）。「操作後の値の供給元」は `DependencyPropertyHelper.GetValueSource` の `BaseValueSource` で、「式」は `IsExpression`、「現在値」は `IsCurrent` が `True` であることを示す。「バインドエラー」は操作の間に `PresentationTraceSources.DataBindingSource` へ出たエラーと警告の数で、最後の行は、存在しないパスへのバインドがこの数え方で 1 件と数えられることを確かめたものである。
{: .table-caption}

展開ボタンをクリックした 1 行目では、表示は `True` になったが、ソースは `False` のままだった。
バインドは働かなくなり、ソースに `False` を代入し直しても表示は `True` のままだった。
バインドエラーは 0 件で、出力ウィンドウを見ても気付けない。

---

## 内部で何が起きているか

### Mode を書かないと、向きはプロパティが決める

`Binding.Mode` を書かないと `BindingMode.Default` になり、向きはターゲットのプロパティのメタデータで決まる（[BindingMode](https://learn.microsoft.com/dotnet/api/system.windows.data.bindingmode)）。
メタデータの `BindsTwoWayByDefault` が `True` なら双方向、`False` なら片方向である。
次の表は、`Mode` を書かずにバインドされやすいプロパティのメタデータを読んだ結果である。

{% include tables/articles/wpf-binding-mode-omitted-detached/binding-mode-defaults.ja.md %}

.NET 10 で、各プロパティの `GetMetadata` が返す `FrameworkPropertyMetadata` を読んだ値。括弧内に型を書いた行はその型、それ以外の行は名前の先頭の型で読んだ。
{: .table-caption}

同じ `TreeViewItem` でも、`IsSelected` は `True`、`IsExpanded` は `False` である。
`Expander.IsExpanded` は `True` なので、同じ「開閉」でもコントロールによって既定の向きが違う。
`ColumnDefinition.Width` と `RowDefinition.Height` も `False` だった。
`DefaultUpdateSourceTrigger` は、`TextBox.Text` だけが `LostFocus` で、ほかは `PropertyChanged` だった。

### 展開ボタンは、テンプレートのバインドを通して値を書き込む

展開ボタンは、`TreeViewItem` の既定のテンプレートの中にある `ToggleButton` である。
その `IsChecked` が、どう `IsExpanded` に結ばれているかを読んだ。

{% include tables/articles/wpf-binding-mode-omitted-detached/binding-mode-template-buttons.ja.md %}

.NET 10 / Windows 11（既定のテーマ）で、テンプレートを適用した `TreeViewItem` と `Expander` の中の `ToggleButton` から、`BindingOperations.GetBinding` で `IsChecked` のバインドを読んだ値。
{: .table-caption}

展開ボタンの `IsChecked` は、テンプレートを適用した `TreeViewItem` 自身（`TemplatedParent`）の `IsExpanded` に、`Mode` を書かない `{Binding IsExpanded}` で結ばれている。
`ToggleButton.IsChecked` の既定は双方向なので（前掲の既定の表の「ToggleButton.IsChecked」の行）、このバインドは双方向に働く。

ボタンを押すと、このバインドが `TreeViewItem.IsExpanded` に値を書き込む。
実際のクリックを使わず、ボタンの `IsChecked` にコードで `True` を設定しただけの 4 行目でも、結果はクリックと同じだった。
クリックの結果は、この経路の書き込みだけで再現できる。

この書き込みは、`TreeViewItem.IsExpanded` から見れば通常の値の設定（ローカル値）である。
表の 1 行目のとおり、片方向のバインドを持つプロパティに値が設定されると、値の供給元は `Local` になり、バインドは働かなくなった。

### Setter のバインドは隠れ、直接書いたバインドは置き換わる

働かなくなり方は、バインドをどこに書いたかで違う。

`ItemContainerStyle` の `Setter` のバインドは、ローカル値より優先順位が低い。
ローカル値が入ると、`Setter` のバインドは隠れる。
クリックの後に `ClearValue` でローカル値を消した 5 行目では、値の供給元が「Style、式」に戻った。
その後にソースを `True` にすると、表示も `True` になった。
`Setter` のバインドは消えておらず、隠れていただけである。

`TreeViewItem` に `IsExpanded="{Binding Value}"` を直接書いた場合は、バインド自体がローカル値の位置にある。
クリックで書き込まれた値がバインドを置き換え（9 行目）、`ClearValue` の後は値の供給元が `Default` になり、ソースを `True` にしても表示は `False` のままだった（10 行目）。

### 書き込まれる側が双方向なら、値はソースへ渡る

`Mode=TwoWay` を書いた 6 行目では、同じクリックの後も値の供給元は「Style、式」のままで、ソースが `True` に更新された。
双方向のバインドを持つプロパティへの書き込みは、バインドを通してソースへ渡り、バインドは働き続けた。

### 右矢印キーとダブルクリックは、SetCurrentValue で書き込む

右矢印キー（→）と見出しのダブルクリックで開いた 2・3 行目では、バインドは残った。
値の供給元は「Style、式、現在値」で、`IsCurrent` が `True` だった。
これは、値が `SetCurrentValue` で設定されたことを示す（[ValueSource.IsCurrent](https://learn.microsoft.com/dotnet/api/system.windows.valuesource.iscurrent)）。
`SetCurrentValue` は、値の供給元を変えずに実効値だけを変え、バインドを残す（[DependencyObject.SetCurrentValue](https://learn.microsoft.com/dotnet/api/system.windows.dependencyobject.setcurrentvalue)）。

ただし、バインドは片方向のままなので、ソースは更新されなかった。
ビューモデルが `IsExpanded` の変更を通知すると（値は `False` のままでも）、表示はソースの値に戻され、ユーザーが開いたノードは閉じた。
`Mode=TwoWay` を書いた 7・8 行目では、同じ操作でソースが `True` に更新された。
双方向のバインドでは値がソースへ渡り、値の供給元に「現在値」は付かなかった。

---

## 経路から導ける帰結

**同じプロパティでも、操作の経路で結果が変わる。**
`TreeViewItem.IsExpanded` への実際の入力（展開ボタン・右矢印キー・見出しのダブルクリック）のうちでは、展開ボタンのクリックだけがバインドを外した。
右矢印キーと見出しのダブルクリックでは外れなかった。
不具合の報告を受けて右矢印キーやダブルクリックで試すと、再現しない。

**外れるかどうかは、書き込まれる側のバインドの実際の向きで決まる。**
`Expander.IsExpanded` は既定が `True` で、`Mode` を書かなくてもクリックの後にバインドが残り、ソースが更新された（11 行目）。
同じ `Expander` に `Mode=OneWay` を書いた 12 行目では、テンプレートの見出しが `Mode=TwoWay` で結ばれているにもかかわらず（前掲のテンプレートの表）、値の供給元が `Local` になり、ソースは `False` のままだった。
`Mode` を書かなければ、その向きはプロパティの既定である。
`MenuItem.IsChecked` と `CheckBox` の `IsChecked` も既定が `True` で、`Mode` を書かなくてもバインドが残った（13・14 行目）。

**操作が通常の値の設定で書き込むプロパティで、既定が片方向なら、同じことが起きる。**
`ColumnDefinition.Width` は既定が `False` で、`GridSplitter` のドラッグでバインドが外れ、ソースは 100 のままだった（15 行目）。
`Mode=TwoWay` を書くと、バインドは残り、ソースが 160 に更新された（16 行目）。
`RowDefinition.Height`・`DatePicker.Text`・`Window.Left` なども既定は `False` だが、これらを操作したときの結果は本記事では測っていない。

---

## 実装例

### ユーザーが変えるプロパティには Mode=TwoWay を書く

`TreeViewItem.IsExpanded` は、`Setter` のバインドに `Mode=TwoWay` を書く。
「現象」の節の XAML の `Setter` を、次のように変える。

```xml
<Setter Property="IsExpanded" Value="{Binding IsExpanded, Mode=TwoWay}" />
```

展開ボタン・右矢印キー・見出しのダブルクリックのどれで開いても、ビューモデルの `IsExpanded` が `True` になった（6〜8 行目）。
その後にビューモデルから `False` にすると、ノードは閉じた。

`GridSplitter` で幅を変える列も、同じく `Mode=TwoWay` を書く。
`ColumnDefinition.Width` の型は `GridLength` なので、ビューモデルのプロパティも `GridLength` にする。

```xml
<Grid>
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="{Binding NavigationWidth, Mode=TwoWay}" />
    <ColumnDefinition Width="Auto" />
    <ColumnDefinition />
  </Grid.ColumnDefinitions>
  <GridSplitter Grid.Column="1" Width="6" HorizontalAlignment="Center" />
</Grid>
```

`GridSplitter` の `HorizontalAlignment` を `Center` にしているのは、既定の `Right` では調整される列の組み合わせが変わるためである（[GridSplitter のデモページ](/ja/apps/wpf-standard-control-demo/gridsplitter.html)で実測している）。
この構成でドラッグすると、ビューモデルの幅が更新された（16 行目）。

### バインドが残っているかを確かめる

バインドが外れてもエラーは出ないため、確かめるにはコードで読む。
次のクラスは、プロパティの既定の向きを返すメソッドと、バインドの状態と値の供給元を出力するメソッドを持つ。
本記事の計測も、同じ API で値を読んでいる。

```csharp
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;

public static class BindingReport
{
    // Mode を書かないバインドの向きを決める、メタデータの既定。
    // インスタンスを渡すと、その型で上書きされたメタデータが返る。
    public static bool BindsTwoWayByDefault(DependencyObject target, DependencyProperty property) =>
        property.GetMetadata(target) is FrameworkPropertyMetadata metadata && metadata.BindsTwoWayByDefault;

    // バインドの有無と、値がどこから来ているか（Debug ビルドで出力ウィンドウに出る）。
    public static void Write(DependencyObject target, DependencyProperty property)
    {
        BindingExpressionBase expression = BindingOperations.GetBindingExpressionBase(target, property);
        ValueSource source = DependencyPropertyHelper.GetValueSource(target, property);
        Debug.WriteLine(
            $"{property.Name}: binding {(expression == null ? "none" : "attached")}, " +
            $"two-way by default {BindsTwoWayByDefault(target, property)}, " +
            $"{source.BaseValueSource}" +
            (source.IsExpression ? ", expression" : "") +
            (source.IsCurrent ? ", current" : ""));
    }
}
```

`TreeViewItem` は、`ItemContainerGenerator` でコンテナーを取得して渡す。
最上位のノードは `TreeView` の `ItemContainerGenerator` から取れるが、子のノードは親の `TreeViewItem` の `ItemContainerGenerator` から取る。

```csharp
var item = (TreeViewItem)folderTree.ItemContainerGenerator.ContainerFromItem(folder);
BindingReport.Write(item, TreeViewItem.IsExpandedProperty);
```

`binding none` と `Local` が出ていれば、バインドはローカル値で置き換わっているか、`Setter` のバインドがローカル値に隠れているか、最初からバインドが無い（表では、たとえば 1 行目と 9 行目がこの出方になった）。
`binding none` と `Default` は、たとえば置き換わった後に `ClearValue` した状態である（10 行目）。
最初からバインドも値も設定していない場合も、この出方になる。
`expression` と `current` が出ていれば、バインドは残っているが、値は `SetCurrentValue` で変えられたものである。

---

## 注意点

- **片方向のまま「表示だけをビューモデルに従わせる」構成は、ユーザーが開閉できるノードでは成り立たない。**
  展開ボタンではバインドが働かなくなり、右矢印キーではビューモデルの通知で表示が戻された。
  ユーザーが変える値は、双方向でビューモデルに受け取る。
- **右矢印キーやダブルクリックでは再現しない。**
  `TreeViewItem.IsExpanded` への実際の入力のうち、バインドを外したのは展開ボタンのクリックだけだった。
  不具合の報告を受けて確かめるときは、展開ボタンをクリックする。
- **`Setter` のバインドは、ローカル値に隠れる。**
  `Style` の `Setter` はローカル値より優先順位が低いため、一度ローカル値が入ると `Setter` は働かない。
  値の優先順位は [WPF で Style の Trigger・DataTrigger が効かない原因と依存関係プロパティの値優先順位](/ja/articles/wpf-style-trigger-not-working-local-value/) で扱っている。
- **コードからの代入でも、片方向のバインドは働かなくなる。**
  コンテナーの `IsSelected` へ代入した場合に、`Setter` の片方向のバインドがローカル値に隠れて働かなくなることは、[WPF TreeView で任意のノードをコードから選択・展開する方法と SelectedItem が読み取り専用である理由](/ja/articles/wpf-treeview-select-item-programmatically/) で実測している。
  本記事の 4 行目では、展開ボタンの `IsChecked` にコードで設定した値がテンプレートのバインドを通して `IsExpanded` に書き込まれ、`Setter` のバインドが同じく隠れた。
- **既定が `True` のプロパティでも、`Mode` を書いておくと向きが読み取れる。**
  `Mode` を書かないバインドは、既定が `True` のプロパティでは双方向として働き、本記事の計測でも外れなかった。
  `IsExpanded` のように既定が直感と違うプロパティがあるため、ユーザーが変える値には `Mode=TwoWay` を書いておくと、XAML から向きが分かる。

---

## まとめ

`Mode` を書かないバインドがユーザー操作で外れるのは、次の 2 つがそろったときである。

- ターゲットのプロパティの `BindsTwoWayByDefault` が `False` である（`TreeViewItem.IsExpanded`・`ColumnDefinition.Width` など）。
- ユーザーの操作が、そのプロパティに通常の値の設定で書き込む（展開ボタン・`GridSplitter`）。

このとき、直接書いたバインドは置き換わり、`Setter` のバインドは隠れて、どちらも働かなくなる。
ソースは更新されず、エラーも出ない。
ユーザーが変える値をビューモデルに持たせるなら、プロパティの既定に頼らず `Mode=TwoWay` を書く。
外れているかどうかは、`BindingOperations.GetBindingExpressionBase` と `DependencyPropertyHelper.GetValueSource` で確かめられる。

---

## 関連記事

- [WPF TreeView で任意のノードをコードから選択・展開する方法と SelectedItem が読み取り専用である理由](/ja/articles/wpf-treeview-select-item-programmatically/)
- [WPF で Style の Trigger・DataTrigger が効かない原因と依存関係プロパティの値優先順位](/ja/articles/wpf-style-trigger-not-working-local-value/)
- [WPF の UserControl に定義した DependencyProperty へ内部からバインドできない原因と DataContext の設計](/ja/articles/wpf-usercontrol-dependencyproperty-binding-not-working/)
- [WPF で TextBox の UpdateSource を View から呼び出すときの落とし穴と実装](/ja/articles/wpf-textbox-updatesource-from-view-pitfalls/)
- [TreeView（WPF 標準コントロールデモアプリ）](/ja/apps/wpf-standard-control-demo/treeview.html)：`IsExpanded` と `IsSelected` の既定の向き、展開ボタンで外れるバインド
- [GridSplitter（WPF 標準コントロールデモアプリ）](/ja/apps/wpf-standard-control-demo/gridsplitter.html)：`HorizontalAlignment` の既定と、調整される列の組み合わせ

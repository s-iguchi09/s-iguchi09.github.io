---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/popup.html
title: "Popup"
badge: "Overlays"
lead: "Popup は、Child をアプリケーションの上に浮かぶ別のウィンドウに、別の要素を基準にして表示します。ComboBox や Menu のドロップダウンにも使われています。"
description: "WPF の Popup を .NET 10 で実測して解説。Placement ごとに子がどこに出るか、画面の端でどうなるか、StaysOpen の本当の既定値、AllowsTransparency で何が変わるかを確かめます。ウィンドウを動かしたときの動きも示します。"
---

## 子は別のウィンドウになる

**Popup** は `FrameworkElement` を継承しています。開いたとき、`Child` は Popup を置いたウィンドウの中には描かれません。子は独自のウィンドウハンドルを持ち、その親はポップアップ専用のルートの下にある内部のデコレーターでした。別のウィンドウなので、子はアプリケーションのウィンドウの外へ、さらに作業領域の下端の外へもはみ出せます。

また、アプリケーションのウィンドウには付いていきません。開いたままウィンドウを (40, 40) 動かしても、子は動きませんでした。ウィンドウが動いたらポップアップを閉じます。

ComboBox と最上位の MenuItem の既定のテンプレートには、`PART_Popup` という名前の Popup があり、独自のドロップダウンのコントロールも同じようにテンプレートの中に置けます。

## 閉じ方：StaysOpen の既定値は True

既定値は、`IsOpen` が `False`、`StaysOpen` が `True`、`AllowsTransparency` が `False`、`Placement` が `Bottom`、`PopupAnimation` が `None` です。`IsOpen` は既定で TwoWay にバインドされるため、ポップアップが自分で閉じると、バインドしたチェックボックスのチェックも外れます。

`StaysOpen` は、ほかの場所をクリックしても開いたままにするかどうかです。`False` では、実際のマウスでウィンドウの空いた場所をクリックするとポップアップが閉じ、`IsOpen` にバインドしたチェックボックスのチェックも外れました。`True` では同じクリックでも開いたままだったので、別の方法で閉じる必要があります。外側のクリックで閉じたい選択肢のパネルや補足の表示には、`StaysOpen="False"` を設定します。

## 透過と、アニメーションが動くかどうか

`AllowsTransparency` は、ポップアップのウィンドウを透過にできるかどうかです。`True` ではポップアップのウィンドウにレイヤードの属性（`WS_EX_LAYERED`）が付き、`False` では付きませんでした。

`PopupAnimation` が動くかどうかもこれで決まります。`Fade` で `AllowsTransparency="True"` のとき、ポップアップのルートの不透明度は、開いた直後にはまだ途中で（計測した回では開いて約 100 ms 後に 0.48。値は回ごとに変わります）、約 0.5 秒後に 1 になりました。`False` では最初から 1 で、フェードしませんでした。`PopupAnimation` には `AllowsTransparency="True"` を組み合わせます。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/popup/popup-behavior.svg" alt="Popup の動作の計測結果の表。FrameworkElement を継承し StaysOpen の既定値は True、子は別のウィンドウにあり、ComboBox と MenuItem のテンプレートには PART_Popup があり、StaysOpen が False なら外側のクリックで閉じてバインドしたチェックボックスも外れ、True なら閉じず、子はウィンドウと一緒に動かず、AllowsTransparency が True のときだけレイヤードのウィンドウになって Fade が動く" width="1085" height="320" loading="lazy">
  <figcaption>既定値、別のウィンドウ、<code>StaysOpen</code>、透過。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 子が出る位置

`Placement` は、基準の要素に対して子をどこに出すかです。基準が 150 × 30、子が 120 × 40 のとき、基準の左上から見た子の左上は、`Bottom` で (0, 30)、`Top` で (0, -40)、`Right` で (150, 0)、`Left` で (-120, 0)、`Center` で中心がそろう (15, -5) でした。`PlacementRectangle` なしの `Absolute` では、子は画面の (0, 0) に出ました。

`HorizontalOffset` と `VerticalOffset` は、配置の後に加えるずらし量です。計測したどの配置でも、正の値で子は右と下へ動きました。`Top` と `Left` も同じです。(20, 10) を加えると、`Top` では (20, -30) と、基準に 10 近づきました。

出る側が固定だと思ってはいけません。画面の端で収まらないときは WPF が位置を変えますが、どう変えるかは `Placement` の値とどの端に接したかで異なります。基準を画面の下端の 10 上に置くと、`Bottom` でも子は基準の上に出ました。作業領域の下端の 10 上に置いたときは下に出て、作業領域の下端を越えました。

`PlacementTarget` は明示します。ないと、Popup を置いた要素が基準になり、200 × 60 の Grid に置いた Popup を `Bottom` にすると、子は Grid から見て (0, 60) に出ました。`PlacementRectangle` は基準の要素の範囲の代わりに使う矩形で、(0, 0, 100, 50) では、子は矩形の下の (0, 50) に出ました。

<figure class="article-figure">
  <img src="/images/wpf-standard-control-demo/verification/popup/popup-placement.svg" alt="基準 150 × 30、子 120 × 40 の Popup の位置の計測結果の表。Bottom は (0, 30)、Top は (0, -40)、Right は (150, 0)、Left は (-120, 0)、Center は (15, -5)、正のずらし量は右と下へ動かし、作業領域の下端の近くでは下に出て作業領域の外へはみ出し、画面の下端の近くでは基準の上に移り、矩形なしの Absolute は (0, 0)、PlacementTarget なしでは Popup を置いた Grid が基準になる" width="818" height="380" loading="lazy">
  <figcaption>配置ごとに子が出る位置。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの Popup のページ。左にコントロールの一覧、右に最初の節の IsOpen / StaysOpen / AllowsTransparency](/images/wpf-standard-control-demo/popup.png){: .screenshot-img}

デモアプリの Popup のページには、`IsOpen`・`StaysOpen`・`AllowsTransparency`（`IsOpen` はチェックボックスにバインド）、位置とずらし量、`PopupAnimation`（ポップアップに `AllowsTransparency="True"` を設定）、`Child` と `PlacementRectangle`（ポップアップに `PlacementTarget` がなく、矩形 (0, 0, 100, 50) を使用）の 4 つの欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、最初の欄（`PopupUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CheckBox x:Name="IsOpenCheckBox" Content="IsOpen" />
  <CheckBox x:Name="StaysOpenCheckBox" Content="StaysOpen" IsChecked="True" />
  <CheckBox x:Name="AllowsTransparencyCheckBox" Content="AllowsTransparency" IsChecked="True" />

  <Grid>
    <Button x:Name="BasicPlacementTarget" Content="Placement Target" />
    <Popup x:Name="BasicPopup"
           AllowsTransparency="{Binding IsChecked, ElementName=AllowsTransparencyCheckBox}"
           IsOpen="{Binding IsChecked, ElementName=IsOpenCheckBox}"
           Placement="Bottom"
           PlacementTarget="{Binding ElementName=BasicPlacementTarget}"
           StaysOpen="{Binding IsChecked, ElementName=StaysOpenCheckBox}">
      <Border Background="White" BorderBrush="Gray" BorderThickness="1">
        <TextBlock Text="Basic Popup Content" />
      </Border>
    </Popup>
  </Grid>
</StackPanel>
```

## 関連するコントロールと記事

- [ToolTip](/ja/apps/wpf-standard-control-demo/tooltip.html) — マウスを乗せると自動で表示されます。
- [ComboBox](/ja/apps/wpf-standard-control-demo/combobox.html) — ドロップダウンの一覧が Popup です。
- [Menu](/ja/apps/wpf-standard-control-demo/menu.html) — 最上位の項目がサブメニューを Popup で開きます。
- [ToggleButton](/ja/apps/wpf-standard-control-demo/togglebutton.html) — Popup の開閉によく使われます。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`PopupDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/PopupDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。位置は、子と基準の要素の画面上の位置の差を、デバイスに依存しないピクセルで表しています。ポップアップの外側のクリックは、計測用のウィンドウの空いた場所を実際のマウスでクリックして行いました。

[GitHub で Popup のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/PopupUsage){: target="_blank" rel="noopener noreferrer"}

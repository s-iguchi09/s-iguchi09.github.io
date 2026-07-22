---
layout: article-ja
title: "WPF で TextBox の UpdateSource を View から呼び出すときの落とし穴と実装"
date: 2026-07-22
category: WPF
excerpt: "GetBindingExpression().UpdateSource() を View から呼ぶ際に起きる null・一括更新・方向取り違え・MVVM 設計の落とし穴を、一次情報を基に整理する。"
---

## 概要

WPF で入力の確定を「送信」ボタンなどのタイミングまで遅らせるには、`UpdateSourceTrigger=Explicit` にしたうえで、View 側から `BindingExpression.UpdateSource()` を呼んでソースへ書き戻す。
この呼び出し自体は 1 行だが、実務では `GetBindingExpression` が `null` を返して `NullReferenceException` になったり、複数の入力欄を一括で書き戻せずに一部だけ古い値が残ったりと、呼ぶ側に固有の落とし穴がある。
本記事では、`UpdateSource()` を View から呼ぶ実装を軸に、`null` を返す条件・複数バインディングの一括更新・`UpdateTarget()` との方向の違い・MVVM を崩さない設計を、公式ドキュメントの挙動に基づいて整理する。
更新の「タイミング」を決める `UpdateSourceTrigger` 三値そのものの使い分けは扱わない。

---

## 前提・対象環境

- フレームワーク／言語: .NET 8 / C# 12（`UpdateSource` は .NET Framework 3.0 以降の WPF で利用可能）
- 対象コントロール・機能: `TextBox.Text` の双方向バインディング（`Mode=TwoWay` / `OneWayToSource`）
- アーキテクチャ: MVVM（ViewModel のプロパティを View の `TextBox` へバインド）
- 前提知識: `INotifyPropertyChanged` による変更通知、`UpdateSourceTrigger` の基本

`UpdateSource()` は、ターゲット（`TextBox.Text`）の現在値をソース（ViewModel）へ書き戻すメソッドである。
`UpdateSourceTrigger=Explicit` のバインディングでは、このメソッドを呼ばない限りソースは一切更新されない。

---

## 問題

`Explicit` にしたバインディングを View から書き戻そうとして、次のコードが期待どおり動かない場面がある。

```csharp
// 送信ボタンのクリック時に呼ぶが、例外や無反応になることがある
BindingExpression be = amountBox.GetBindingExpression(TextBox.TextProperty);
be.UpdateSource();
```

典型的には 3 通りの失敗が起きる。
`GetBindingExpression` が `null` を返して 2 行目が `NullReferenceException` になる、`UpdateSource()` を呼んでもソースが更新されない、あるいはフォーム内の複数 `TextBox` のうち明示的に呼んだものだけが反映され残りが古いままになる、というものである。

---

## 原因・背景

第一の原因は、`GetBindingExpression` が「その依存プロパティに**単一の `Binding` のアクティブなバインディングがある場合のみ**」`BindingExpression` を返し、無ければ `null` を返す点にある。
公式リファレンスは、戻り値の `null` チェックがバインディングの有無を判定する手段であると明記している。
`Text="固定文字列"` のようにリテラルを代入している場合、`Text` が `MultiBinding` の場合（この場合は `GetMultiBindingExpression` を使う）、あるいは対象 `TextBox` が別コントロールのテンプレート内にあってその名前を直接参照できない場合に `null` になる。

第二に、`UpdateSource()` はバインディングの `Mode` が `TwoWay` または `OneWayToSource` でないと何もしない。
`OneWay` や `OneTime` のバインディングに対して呼んでも、例外は出ないまま黙って無視される。
また、バインディングがターゲットからデタッチ済みの状態で呼ぶと `InvalidOperationException` を送出する。

第三に、`UpdateSource()` は呼び出したその 1 つの `BindingExpression` だけを書き戻す。
フォーム全体を確定したい場合は、対象の各 `TextBox` について個別に呼ぶか、後述する一括更新の仕組みを使う必要がある。

---

## 解決方法

まず取得した `BindingExpression` を必ず `null` チェックし、`null` の場合はバインドされていない・`MultiBinding` である・テンプレート内であるといった原因を切り分ける。
単一要素の書き戻しは、`null` 条件演算子 `?.` で安全に呼ぶ。
複数要素をまとめて確定するフォームでは、ビジュアルツリーを走査して各 `TextBox` に呼ぶ方法と、`BindingGroup` で一括更新する方法がある。
View の分離を保ちたい場合は、コードビハインドに直接書かず、添付プロパティ（ビヘイビア）として再利用可能にする。

---

## 実装例

単一の `TextBox` を書き戻す基本形は次のとおりである。
`GetBindingExpression` の戻り値を `?.` で受け、`null`（未バインド等）のときは何もしないようにする。

```csharp
// amountBox は UpdateSourceTrigger=Explicit でバインドした TextBox
BindingExpression be = amountBox.GetBindingExpression(TextBox.TextProperty);
be?.UpdateSource();
```

`?.` により、バインディングが存在しないケースでも `NullReferenceException` を避けられる。
`null` を「異常」として扱いたい場合は、明示的に分岐してログ出力や早期 return を行う。

フォーム内の複数 `TextBox` をまとめて書き戻すには、ビジュアルツリーを再帰的に走査し、各 `TextBox.Text` のバインディングに `UpdateSource()` を呼ぶ。

```csharp
// 指定要素配下のすべての TextBox.Text バインディングを書き戻す
static void UpdateAllTextSources(DependencyObject root)
{
    int count = VisualTreeHelper.GetChildrenCount(root);
    for (int i = 0; i < count; i++)
    {
        DependencyObject child = VisualTreeHelper.GetChild(root, i);
        if (child is TextBox textBox)
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
        UpdateAllTextSources(child);
    }
}
```

この走査はビジュアルツリー生成後（`Loaded` 以降）に実行する。
仮想化されたリスト内の未生成要素は走査対象に含まれない点に注意する。

検証を伴って複数バインディングを一括で書き戻すなら、`BindingGroup` を使う。
親要素に `BindingGroup` を設定すると、配下のバインディングはグループに参加し、既定では `UpdateSources()` を呼ぶまでソースを更新しない。

```xml
<StackPanel x:Name="formPanel">
    <StackPanel.BindingGroup>
        <BindingGroup />
    </StackPanel.BindingGroup>
    <TextBox Text="{Binding Street}" />
    <TextBox Text="{Binding City}" />
</StackPanel>
```

コードビハインドからは `UpdateSources()` を 1 回呼ぶだけでよい。
このメソッドは各バインディングの `ValidationRule` を実行し、すべて成功した場合にソースへ書き戻して `true` を返す。

```csharp
// すべての参加バインディングを検証し、成功時のみまとめて書き戻す
bool committed = formPanel.BindingGroup.UpdateSources();
```

`UpdateSources()` は検証が 1 つでも失敗すると書き戻しを行わず `false` を返す。
ただし `IEditableObject` の編集トランザクションは終了しないため、確定まで行うには `CommitEdit()` を使う。

ソースからターゲットへ表示を戻したい場合は、`UpdateSource()` ではなく `UpdateTarget()` を呼ぶ。
両者は方向が逆であり、取り違えると「保存したのに入力欄が変わらない」「入力を破棄したいのにソースへ書き込む」といった誤動作になる。

```csharp
// ソース→ターゲット方向を強制する（UpdateSource の逆。入力の破棄・再表示に使う）
amountBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
```

MVVM でコードビハインドを避けたい場合は、`UpdateSource()` の呼び出しを添付プロパティに閉じ込める。
以下は「Enter 押下で書き戻す」挙動を添付ビヘイビアとして再利用可能にした例である。

```csharp
public static class TextBoxBehavior
{
    public static readonly DependencyProperty UpdateSourceOnEnterProperty =
        DependencyProperty.RegisterAttached(
            "UpdateSourceOnEnter",
            typeof(bool),
            typeof(TextBoxBehavior),
            new PropertyMetadata(false, OnChanged));

    public static bool GetUpdateSourceOnEnter(DependencyObject obj) =>
        (bool)obj.GetValue(UpdateSourceOnEnterProperty);

    public static void SetUpdateSourceOnEnter(DependencyObject obj, bool value) =>
        obj.SetValue(UpdateSourceOnEnterProperty, value);

    static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        textBox.KeyDown -= OnKeyDown;
        if ((bool)e.NewValue)
        {
            textBox.KeyDown += OnKeyDown;
        }
    }

    static void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox textBox)
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
    }
}
```

XAML 側では添付プロパティを付けるだけで、コードビハインドに View 固有の処理を書かずに済む。

```xml
<TextBox Text="{Binding Amount, UpdateSourceTrigger=Explicit}"
         local:TextBoxBehavior.UpdateSourceOnEnter="True" />
```

イベント購読を付け外しする際は、上記のように名前付きハンドラで `-=` してから `+=` し、二重購読を避ける。
ラムダ式で購読すると解除できず、購読が積み重なる。

---

## 注意点

- **`null` を握りつぶさない**: `?.` は例外を防ぐが、本来バインドされているはずの要素で `null` が返る場合は設定ミス（`MultiBinding` の取得メソッド違い、テンプレート内要素、名前解決の失敗）を示す。デバッグ時は `null` 分岐でログを残す。
- **`Mode` の制約**: `UpdateSource()` は `TwoWay` / `OneWayToSource` 以外では黙って無視される。反映されないときはまず `Mode` を確認する。
- **デタッチ済みバインディング**: 要素がツリーから外れるなどしてバインディングがデタッチされた後に呼ぶと `InvalidOperationException` になる。
- **一括更新の範囲**: `VisualTreeHelper` の走査は生成済み要素のみが対象で、仮想化で未生成の項目は書き戻されない。`TabControl` の非アクティブタブなど、未実体化の領域にも注意する。
- **`BindingGroup` は検証と一体**: `UpdateSources()` は `ValidationRule` を走らせ、失敗時は書き戻さず `false` を返す。単純な一括書き戻しのつもりで使うと、検証失敗で無反応に見えることがある。
- **`UpdateSource` と `UpdateTarget` の方向**: 前者はターゲット→ソース、後者はソース→ターゲットである。用途（確定か破棄か）に応じて選ぶ。

---

## 代替案・比較

View から書き戻す手段は、対象範囲と設計方針に応じて選ぶ。

| 方法 | 対象範囲 | メリット | デメリット | 適するケース |
|---|---|---|---|---|
| `GetBindingExpression().UpdateSource()` | 単一要素 | 明快で制御しやすい | 要素ごとに呼ぶ必要がある | 特定の 1 入力欄だけを確定する |
| `VisualTreeHelper` 走査で一括 | 配下の全 `TextBox` | 一度で多数を書き戻せる | 未生成要素は対象外・走査コスト | 生成済みの入力群をまとめて確定 |
| `BindingGroup.UpdateSources()` | グループ参加バインディング | 検証と一体で一括確定できる | 検証設計が前提・戻り値の考慮が必要 | 複数項目をまとめて検証・保存するフォーム |
| 添付ビヘイビア経由 | 付与した要素 | View の分離を保てる・再利用可能 | 実装量が増える | MVVM でコードビハインドを避けたい |

単一の確定には `UpdateSource()` を直接呼ぶのが最も明快である。
フォーム全体を検証付きで確定するなら `BindingGroup` が適し、MVVM の分離を保つなら添付ビヘイビアに寄せる。

---

## まとめ

View から `TextBox` のバインディングを書き戻す実装は、`GetBindingExpression(TextBox.TextProperty)?.UpdateSource()` が基本形である。
`GetBindingExpression` はバインディングが無ければ `null` を返すため、`?.` で保護しつつ、本来バインド済みの要素で `null` が返る場合は `MultiBinding`・テンプレート内・名前解決を疑う。
`UpdateSource()` は `TwoWay` / `OneWayToSource` でのみ機能し、デタッチ後は例外になる点も踏まえる。
確定範囲が単一なら直接呼び出し、複数を検証付きで確定するなら `BindingGroup.UpdateSources()`、View の分離を優先するなら添付ビヘイビアを選ぶ。
方向を戻したい場合は `UpdateSource()` ではなく `UpdateTarget()` を使い、取り違えによる誤動作を避ける。

なお、更新をいつ発火させるか（`LostFocus` / `PropertyChanged` / `Explicit` の使い分け）は別の観点であり、[WPF TextBox の UpdateSourceTrigger で入力がソースへ反映されるタイミングを制御する](/ja/articles/wpf-textbox-updatesourcetrigger-binding-timing/)で扱う。

---

<!-- 関連記事 -->
<!-- - [WPF TextBox の UpdateSourceTrigger で入力がソースへ反映されるタイミングを制御する](/ja/articles/wpf-textbox-updatesourcetrigger-binding-timing/) -->

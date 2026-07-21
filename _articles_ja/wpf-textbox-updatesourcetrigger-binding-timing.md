---
layout: article-ja
title: "WPF TextBox の UpdateSourceTrigger で入力がソースへ反映されるタイミングを制御する"
date: 2026-07-21
category: WPF
excerpt: "TextBox.Text の既定が LostFocus であるために起きる「入力が ViewModel に届かない」問題を、UpdateSourceTrigger の三値の使い分けと落とし穴から整理する。"
---

## 概要

WPF の双方向バインディングでは、`TextBox` に入力した文字が即座に ViewModel のプロパティへ反映されないことがある。
文字を入力してもコマンドや別コントロールの表示が古い値のまま更新されず、原因が分からないまま `PropertyChanged` の実装を疑ってしまう場面が典型である。
この挙動の中心にあるのが `Binding.UpdateSourceTrigger` であり、`TextBox.Text` の既定値が他コントロールと異なることが混乱の元になる。
`LostFocus` / `PropertyChanged` / `Explicit` の三値がソース更新のタイミングをどう変えるかを、原因となる設計背景から解き、IME 入力・検証タイミング・フォーカスを奪わないボタンという実務上の落とし穴まで実装例で示す。

---

## 前提・対象環境

- フレームワーク／言語: .NET 8 / C# 12(`UpdateSourceTrigger` は .NET Framework 3.0 以降の WPF で利用可能)
- 対象コントロール・機能: `TextBox.Text` の双方向バインディング(`Mode=TwoWay` / `OneWayToSource`)
- アーキテクチャ: MVVM(ViewModel のプロパティを View の `TextBox` へバインド)
- 前提知識: `INotifyPropertyChanged` による変更通知、基本的なデータバインディング

`UpdateSourceTrigger` は、双方向(`TwoWay`)または `OneWayToSource` のバインディングでのみ意味を持つ。
ターゲット(`TextBox.Text`)からソース(ViewModel)へ値を書き戻す方向の「タイミング」を決める設定であり、ソースからターゲットへの表示更新には影響しない。

---

## 問題

ViewModel のプロパティに `TextBox` をバインドし、その値を使う「保存」ボタンや別の `TextBlock` を用意する。
ユーザーが `TextBox` に文字を入力し、まだ入力欄にカーソルがある状態で、フォーカスを移動させない操作(後述の `Focusable="False"` のボタンなど)で確定すると、ViewModel 側のプロパティには**入力前の古い値**が入っている。

```xml
<!-- 既定のバインディング。UpdateSourceTrigger を指定していない -->
<TextBox Text="{Binding UserName, Mode=TwoWay}" />
<Button Content="保存" Command="{Binding SaveCommand}" Focusable="False" />
```

上記で `Focusable="False"` のボタンをクリックすると、`TextBox` はフォーカスを失わないため、入力した文字が `UserName` に反映されないまま `SaveCommand` が実行される。
`INotifyPropertyChanged` は正しく実装しているのに値が届かない、という形で表面化する。

---

## 原因・背景

原因は、`TextBox.Text` の既定 `UpdateSourceTrigger` が `LostFocus` である点にある。
`UpdateSourceTrigger` の既定は `Default` であり、これは「ターゲット依存プロパティごとに定義された既定の更新タイミング」を意味する。
多くの依存プロパティ(`CheckBox.IsChecked` など)ではこの既定が `PropertyChanged` だが、`TextBox.Text` に限っては `LostFocus` が既定になっている。

これは意図的な設計である。
テキスト入力のたびにソースを更新すると、1 文字ごとに変更通知・検証・関連処理が走り、性能を損なう。
また、確定前に入力を修正(バックスペース)する機会をユーザーから奪ってしまう。
このため WPF は、`TextBox` がフォーカスを失った時点でまとめてソースを更新する `LostFocus` を既定に選んでいる。

ある依存プロパティの既定値はコードでも確認できる。
`DependencyProperty.GetMetadata` で取得したメタデータの `DefaultUpdateSourceTrigger` を見ればよい。

```csharp
// TextBox.Text の既定 UpdateSourceTrigger を取得する
var metadata = (FrameworkPropertyMetadata)TextBox.TextProperty.GetMetadata(typeof(TextBox));
UpdateSourceTrigger def = metadata.DefaultUpdateSourceTrigger; // => LostFocus
```

この結果が `LostFocus` であることが、上記の問題が起きる根拠である。
フォーカスが移動しない限りソース更新は起きない。

---

## 解決方法

タイミングを制御するには、バインディングに `UpdateSourceTrigger` を明示する。
選べる値は次の 3 つであり、更新の契機がそれぞれ異なる。

- `PropertyChanged` — `TextBox.Text` が変わるたび(1 文字入力ごと)に即座にソースを更新する。
- `LostFocus` — `TextBox` がフォーカスを失った時点でソースを更新する(`TextBox.Text` の既定)。
- `Explicit` — アプリが明示的に `UpdateSource()` を呼んだ時にのみソースを更新する。

問題のケース(入力途中でボタン実行しても値を届けたい)では、`PropertyChanged` を指定すれば入力ごとに反映される。
一方、送信ボタンを押したときにだけまとめて確定したいフォームでは `Explicit` が適する。

---

## 実装例

入力の即時反映が必要な場合は、`UpdateSourceTrigger=PropertyChanged` を指定する。
検索ボックスやチャット入力のように、1 文字ごとの反映が自然な UI に向く。

```xml
<!-- 入力のたびに UserName へ反映する -->
<TextBox Text="{Binding UserName, UpdateSourceTrigger=PropertyChanged}" />
```

この指定により、`TextBox` がフォーカスを保持したままでも 1 文字入力するたびに `UserName` が更新される。

入力の確定をユーザーの操作(送信ボタン)まで遅延させたい場合は、`Explicit` を指定する。
まず XAML で `TextBox` に `x:Name` を付与し、バインディングの `UpdateSourceTrigger` を `Explicit` にする。

```xml
<!-- 明示的な更新に切り替える -->
<TextBox x:Name="userNameBox" Text="{Binding UserName, UpdateSourceTrigger=Explicit}" />
```

付与した名前を使い、コードビハインドから対象の `BindingExpression` を取得し、任意のタイミングで `UpdateSource()` を呼んでソースを更新する。

```csharp
// 送信ボタンのクリック時などに呼び出す
BindingExpression be = userNameBox.GetBindingExpression(TextBox.TextProperty);
be.UpdateSource();
```

`Explicit` では `UpdateSource()` を呼ばない限りソースは一切更新されない。
呼び忘れると値が永遠に反映されないため、送信処理の先頭で確実に呼ぶ設計にする。

---

## 注意点

- **IME 変換中の即時更新**: `PropertyChanged` は日本語入力の変換中(未確定文字列)でもソースを更新するため、確定前の中間文字列が ViewModel へ流れ込む。変換確定を待ってから処理したい場合は `LostFocus` にする。後述の `Delay` は更新頻度を抑えるだけで、未確定文字列の流入自体は防げない点に注意する。
- **検証(Validation)のタイミング**: `ValidationRules` は `Binding` に付き、`ValidationStep`(既定 `RawProposedValue`)に応じてソース更新の前後で走るため、`UpdateSourceTrigger` の契機に連動する。一方 `INotifyDataErrorInfo` は ViewModel 側でソース更新後に検証し、結果は `ErrorsChanged` の通知で反映されるため、非同期検証では更新契機と表示タイミングが一致しないことがある。`LostFocus` では入力欄を離れるまで、`PropertyChanged` では 1 文字ごとにソース更新(と `ValidationRules`)が走る。
- **`Delay` による抑制**: `PropertyChanged` の過剰な更新は、`Binding.Delay`(.NET Framework 4.5 以降)で最後の入力から指定ミリ秒後に 1 回だけ更新するよう抑制できる。例: `{Binding UserName, UpdateSourceTrigger=PropertyChanged, Delay=500}`。
- **フォーカスを移動させない確定操作**: `TextBox` がフォーカスを失わず、かつ `UpdateSource()` も呼ばれない経路では、既定の `LostFocus` でソース更新が起きない。クリックで起動する `Focusable="False"` のボタン、Enter で起動する既定ボタン(`IsDefault="True"`)、アクセスキーがこれに該当する。`Focusable="False"` はボタンへのフォーカス移動を防ぐだけで、`IsDefault` やアクセスキーによる起動自体は妨げない点にも注意する。この経路で確定する UI では `PropertyChanged` か `Explicit` を使う。
- **`x:Bind` との違い**: WPF の `{Binding}` は `Explicit` を含む 3 値をサポートする。UWP/WinUI の `{x:Bind}` は `Explicit` を持たない点が異なるため、他プラットフォームの記事を参照する際は混同しない。

---

## 代替案・比較

`TextBox.Text` のソース更新タイミングは、UI の性質に応じて次のように選ぶ。

| 値 | 更新の契機 | メリット | デメリット | 適するケース |
|---|---|---|---|---|
| `LostFocus`(既定) | フォーカスを失った時 | 入力確定後にまとめて更新・検証できる | フォーカスが移らないと反映されない | 通常の入力フォーム、フォーカス移動で確定する UI |
| `PropertyChanged` | 1 文字入力ごと | 入力が即座に反映される | 更新頻度が高く IME 中間文字列も流入 | 検索ボックス、リアルタイムプレビュー、チャット入力 |
| `Explicit` | `UpdateSource()` 呼び出し時 | 確定タイミングを完全に制御できる | 呼び忘れると反映されない | 送信ボタンで一括確定する編集フォーム |

`PropertyChanged` の即時性を保ちつつ更新頻度を抑えたい場合は、`Delay` を併用して最後の入力から一定時間後に 1 回だけ更新する構成が有効である。

---

## まとめ

`TextBox` の入力が ViewModel へ届かない問題の大半は、`TextBox.Text` の既定 `UpdateSourceTrigger` が `LostFocus` であることに起因する。
フォーカスが移動せず `UpdateSource()` も呼ばれなければソースは更新されないため、`Focusable="False"` のボタン・`IsDefault="True"` の既定ボタン・アクセスキーで確定する UI では値が古いまま処理が走る。
入力を即座に反映したい検索・プレビュー系では `PropertyChanged`、送信ボタンで一括確定する編集フォームでは `Explicit`、フォーカス移動で自然に確定する通常フォームでは既定の `LostFocus` を選ぶ。
`PropertyChanged` の更新頻度が問題になる場合は `Delay` で抑制するが、`Delay` は IME 未確定文字列の流入を防ぐものではなく、変換確定後にだけ処理したい場合は `LostFocus` を選ぶ。
更新タイミングは `ValidationRules` の実行時点を左右する(`INotifyDataErrorInfo` の結果は `ErrorsChanged` で別途反映される)ため、`UpdateSourceTrigger` は入力体験と検証設計の両面から選択する。

---

<!-- 関連記事 -->
<!-- - [WPF Binding.StringFormat で数値・通貨・日付を書式化する方法と制約](/ja/articles/wpf-binding-stringformat-number-currency-date/) -->

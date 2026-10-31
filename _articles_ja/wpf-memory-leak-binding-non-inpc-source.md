---
layout: article-ja
title: "WPF のバインドで INotifyPropertyChanged の無いソースがメモリに残る仕組み"
date: 2026-10-31
category: WPF
excerpt: "INotifyPropertyChanged の無いソースにバインドした画面を閉じると、.NET 10 ではウィンドウは回収されるが、ソースは PropertyDescriptor の登録表のキーとして残る。内部の表を読み、残る書き方と残らない書き方を 100 回の開閉で比べた。"
image: /images/articles/wpf-memory-leak-binding-non-inpc-source/binding-non-inpc-retention.svg
---

## 概要

帳票の画面を開いて閉じると、閉じたはずの画面のビューモデルが GC で回収されずに残ることがある。
残ったオブジェクトが大きなデータを抱えていれば、開閉を繰り返したときにメモリリークとして見える。
原因の一つは、`INotifyPropertyChanged`（以下 INPC）を実装していないオブジェクトのプロパティへのバインドである。

画面を閉じたのに残るのだから、画面ごと残っていると考えやすい。
しかし .NET 10 で 100 回開閉して数えると、回収されずに残ったのはウィンドウでも `BindingExpression` でもなく、バインドのソースの側だった。
この計測では、残る数は開いた数に比例して増えてはいなかった。

本記事では、閉じた後もソースへたどれる参照の経路を、WPF の内部の表を読んで確かめる。
そのうえで、どの書き方でソースが残り、どの書き方で残らないかを実測で比べ、ビューモデルの実装例を示す。

---

## 前提・対象環境

- フレームワーク: WPF（.NET）。コード例はコレクション式を使うため、C# 12 以降（.NET 8 以降を対象にすれば既定で使える）を前提とする
- 検証環境: .NET 10（10.0.13）/ Windows 11
- アーキテクチャ: MVVM（ビューはバインドだけで値を表示し、コードビハインドを持たない）

挙動を確かめたのは .NET 10 だけで、.NET 8・.NET 9 と .NET Framework 4.8 は測っていない。
内部の表の観察には非公開のフィールドを使うため、別のバージョンでは同じ名前で読めない可能性がある。

---

## 現象

INPC を実装していないビューモデルを用意する。
帳票のデータの代わりに、1 MB の配列を持たせる。

```csharp
public sealed class ReportViewModel
{
    public string Title { get; set; } = "Monthly report";

    public byte[] Data { get; } = new byte[1_000_000];
}
```

`Title` は通常の自動実装プロパティで、変更を通知する仕組みを持たない。
値を表示するだけの画面では、このように書かれたビューモデルや DTO をそのまま `DataContext` に渡すことがある。

ビューは、`TextBlock` で `Title` を表示するだけのウィンドウである。

```xml
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Width="240" Height="100">
  <TextBlock Text="{Binding Title}" />
</Window>
```

ウィンドウを開くたびに新しい `ReportViewModel` を作って `DataContext` に渡し、表示して閉じる。
これを 100 回繰り返し、GC を呼んでから、回収されずに残っているものを弱参照で数えた。
ソースは 10・50・100 回の時点で、`BindingExpression` とウィンドウは 100 回の時点で数えている。
書き方を変えた 9 通りの結果が次の表である。

{% include tables/articles/wpf-memory-leak-binding-non-inpc-source/binding-non-inpc-retention.ja.md %}

表 1: ウィンドウを開いて閉じる操作を 100 回繰り返し、各時点で、ディスパッチャーを空にして 100 ms 待ってから GC を呼ぶ手順を 5 回行った後に数えた（.NET 10.0.13 / Windows 11、1 回の実行の値）。
ソースはどれも 1 MB の配列を持つ。
「List&lt;T&gt; を追跡」「コレクションを追跡」の行は、ビューモデルではなく `Rows` のオブジェクトが残っているかを数えている。
「開いている間に \_valueChangedHandlers のキーだった回数」は、各回のウィンドウを開いている間に、バインドしたプロパティの `PropertyDescriptor` の登録表に、そのプロパティの持ち主がキーとして入っていた回数である（非公開のフィールドをリフレクションで読んだ）。
「前の BindingExpression が回収される前に開いた回数」は、2 回目以降の 99 回のうち、前の回の `BindingExpression` がまだ回収されていない状態で次のウィンドウを開いた回数である。
{: .table-caption}

「通知なし」の上 3 行（set のあるプロパティの TextBlock と TextBox、get だけのプロパティ）と、「List&lt;T&gt;.Count」の行では、いずれかの時点でソースが回収されずに残った。
100 回の時点で残った数は、どの行も開いた数（100）より少ない。
一方、どの行でも 100 回後に残った `BindingExpression` とウィンドウは 0 である。
INPC を実装したビューモデル、`Mode=OneTime`、件数を返す `RowCount`、`ObservableCollection<T>.Count` では、どの時点でもソースは残らなかった。
オブジェクトが残った行はどれも、100 回すべてで登録表のキーになっていた行である。
残ったオブジェクトは、どのチェックポイントでも、すべて登録表のキーのままだった（シーンで数えて確かめた）。

---

## 内部で何が起きているか

### ソースが変更を通知しないときの監視手段

Microsoft Learn の「[Optimizing Performance: Data Binding](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/optimizing-performance-data-binding)」は、プロパティの変更通知の手段として INPC と、`TypeDescriptor` に結び付いた変更通知の 2 つを挙げている。
計測では、バインドしたプロパティを持つオブジェクトが INPC を実装せず、`Mode=OneTime` でもないとき、そのプロパティの `PropertyDescriptor` の登録表にそのオブジェクトが入っていた（パスの途中のオブジェクトについては後述する）。
表 1 の「開いている間の \_valueChangedHandlers のキー」の列がそれである。
プロパティを持つオブジェクトが INPC を実装している場合（ビューモデルや `ObservableCollection<T>`）と `Mode=OneTime` では、この登録表に入っていなかった。

計測した型では、`PropertyDescriptor` はオブジェクトごとに作られていなかった。
`TypeDescriptor.GetProperties` を型で 2 回呼んでも、インスタンスで呼んでも、同じプロパティには同じインスタンスが返った。
`ICustomTypeDescriptor` を実装した型など、インスタンスごとに別のプロパティ情報を返す型では、この限りではない。
購読者は、同じプロパティに対して返るこのインスタンスの中の表に、**監視対象のオブジェクトをキーとして**登録される。
このインスタンスの非公開フィールド `_valueChangedHandlers` を、ウィンドウを開いている間から順に読んだ結果が次の表である。

{% include tables/articles/wpf-memory-leak-binding-non-inpc-source/binding-non-inpc-cleanup-timeline.ja.md %}

表 2: 1 つのウィンドウについて、開いている間から登録が外れるまでの `_valueChangedHandlers` の推移。
`TimelineViewModel` は表 1 の `ReportViewModel` と同じ形の、この計測だけで使う型である。
`_valueChangedHandlers` とその値の記録の `_source` フィールドはリフレクションで読むだけで、書き換えていない。
「ウィンドウを閉じて GC」と「閉じる前に GC」の GC は、表 1 と同じく、ディスパッチャーを空にして 100 ms 待ってから GC を呼ぶ手順を 5 回行うことを指す。
3 行目は、同じ手順を 250 ms 間隔で 20 回行う（.NET 10.0.13 / Windows 11）。
{: .table-caption}

登録の値（ハンドラー）の `Target` は `MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord` で、バインド自身ではない。
この記録は `_source` フィールドでもソースを指している。
ウィンドウを閉じて GC を呼ぶと、`BindingExpression` は回収されたが、登録とソースは残った。

### ウィンドウは回収され、ソースは残る理由

Microsoft Learn の「[Weak event patterns](https://learn.microsoft.com/dotnet/desktop/wpf/events/weak-event-patterns)」は、WPF のデータバインドの多くの部分に弱いイベントのパターンが使われていると説明している。
この経路でも、`PropertyDescriptor` に直接登録されているのはバインドではなく、`ValueChangedEventManager` の記録である（表 2）。
登録と記録が残っていても、閉じた後の `BindingExpression` は回収され（表 1・表 2）、ウィンドウも回収された（表 1）。

一方、ソースは登録表のキーとして、また記録の `_source` として登録の中から指されている。
この 2 つの参照の型を読んだ結果が次の表である。

{% include tables/articles/wpf-memory-leak-binding-non-inpc-source/binding-non-inpc-internals.ja.md %}

表 3: 非公開のフィールドの型と、`TextBox.Text` の既定の向き。
フィールドはリフレクションで読むだけで、書き換えていない。
登録表の実際の型は、`TimelineViewModel` にバインドしたウィンドウを開いている間に読んだ。
記述子が返り続けるかは、ウィンドウを閉じて GC を呼んだ後（登録が残っている状態）と、一度もバインドしていない `UnboundViewModel` の `Title`（登録が無い状態）のそれぞれで、記述子を弱参照だけで持って GC を呼び、`TypeDescriptor` から取り直したものと同じかを比べた（.NET 10.0.13 / Windows 11）。
{: .table-caption}

登録表は `ConcurrentDictionary<object, EventHandler>` で、キーを通常の（強い）参照で持つ。
記録の `_source` も `object` 型のフィールドで、`WeakReference` ではない。
表 3 のとおり、一度もバインドしていない型でも、シーンが記述子を弱参照だけで持って GC を呼んだ後に、`TypeDescriptor` は同じ `PropertyDescriptor` を返した。
記述子は、登録の有無にかかわらず `TypeDescriptor` から到達できる。
そのため、登録が残っている限り、ソースは記述子から登録表のキーをたどって到達でき、回収されない。
記録は `_source` に加えて記述子（`_pd`）と `ValueChangedEventManager`（`_manager`）も指している（表 3）。
表 2 と表 4 でも、登録が残っていた測定点ではソースは残り、登録が外れた後の測定点では回収されていた。

<figure class="article-figure">
  <img src="/images/articles/wpf-memory-leak-binding-non-inpc-source/binding-non-inpc-reference-path.svg" alt="PropertyDescriptor（ReportViewModel.Title）から _valueChangedHandlers へ実線の矢印があり、そこからキーとして ReportViewModel（byte[] Data 1 MB）へ実線、値（ハンドラー）の Target として ValueChangedRecord へ実線が伸びる。ValueChangedRecord からは _source として ReportViewModel へ実線が、BindingExpression（Window）へ破線が伸びる。" width="760" height="270" loading="lazy">
  <figcaption>閉じた後のソースを保持している参照の経路。実線は強い参照（登録表と _source の型は表 3）、破線は、記録が残っていても BindingExpression が回収されたこと（表 2）を表す。登録表のキー・記録の型・記録の _source は表 2（ReportViewModel と同じ形の TimelineViewModel）で、BindingExpression の回収は表 1 と表 2 で、ウィンドウの回収は表 1 で確かめた。赤は閉じた後も残るソース、灰色は .NET と WPF の内部のオブジェクト（PropertyDescriptor・登録表・記録）、緑は回収されるものを表す（.NET 10.0.13 / Windows 11）。</figcaption>
</figure>

### 登録が外れるきっかけ

表 2 のとおり、登録は 250 ms ごとに GC を呼びながら 5 秒待っても外れなかった。
何をすると外れるかを、次に開くウィンドウの種類を変えて比べたのが次の表である。

{% include tables/articles/wpf-memory-leak-binding-non-inpc-source/binding-non-inpc-cleanup-trigger.ja.md %}

表 4: 各行で、`TimelineViewModel` にバインドした最初のウィンドウを開いて閉じ、GC を呼んで最初の `BindingExpression` を回収してから、次のウィンドウを開いた。
最後の行だけは、最初の `BindingExpression` を強い参照で持ったまま（回収される前に）次のウィンドウを開き、開いた直後の登録を読んでから参照を放した。
どの行も、次のウィンドウを閉じて GC を呼んだ後に、別の型に `OneWay` でバインドしたウィンドウをもう 1 つ開き、その直後の登録を読んだ。
「開いた直後」は、表示してディスパッチャーが空になった時点で、GC はまだ呼んでいない。
GC は表 1 と同じ手順で、各行は表 2 とは別のウィンドウで測った（.NET 10.0.13 / Windows 11）。
{: .table-caption}

登録が外れたのは、変更を監視するバインド（`OneWay`）を持つウィンドウを開いたときで、GC を呼ぶ前の、表示した時点で外れていた。
バインドしたソースの型には左右されず、INPC のビューモデルでも、別の型の別のプロパティでも、同じ型の別のインスタンスでも外れた。
INPC のビューモデルは `PropertyDescriptor` の登録表に入らない（表 1）ので、外したのは `PropertyDescriptor` ごとの登録表への登録ではなく、型をまたいで働く WPF の内部の処理である。
別の型の `Name` の `PropertyDescriptor` は、`Title` とは別のインスタンスだった（表 3）。
バインドの無いウィンドウと、`OneTime` のバインドだけのウィンドウでは、開いたまま GC を呼んでも、閉じて GC を呼んでも外れなかった。

表 4 では、登録が外れたのは、最初のウィンドウの `BindingExpression` が回収済みのときだけである。
最後の行のように、最初の `BindingExpression` が回収される前に次のウィンドウを開くと、その登録は開いた時点で外れなかった。
その後 GC を呼ぶと `BindingExpression` は回収されたが、閉じて GC を呼んだ後も登録は外れなかった。
外れたのは、その後でもう 1 つ `OneWay` のウィンドウを開いたときである。
バインドの無いウィンドウの行と `OneTime` の行も、同じくもう 1 つ開いたときに外れた。
表 1 のシーンは GC をチェックポイントでしか明示的に呼ばないため、前の回の `BindingExpression` が回収される前に次のウィンドウを開いた回が多い（表 1 の「前の BindingExpression が回収される前に開いた回数」）。
そうした回では、開いた時点では登録が外れない。
キーになっていた行で残らなかった分は、登録が外れてから回収されたものである（登録がある間は回収されないため）。
表 4 で確かめた外れ方は、`BindingExpression` が回収された後に `OneWay` のウィンドウを開くことであり、表 1 で残る数は、開閉と、実行時に自動で走る GC との前後関係で説明できる。

---

## 経路から導ける帰結

### 長寿命のソースでは実害が出にくい

残るのはソースであり、ウィンドウではない。
アプリ全体で 1 つの設定オブジェクトのように、もともと最後まで生きているソースであれば、登録が残ってもそのソースが余計に生き延びることはない。
表 1 の「1 つの長寿命のソースを全ウィンドウで共有」の行では、このソースが登録表のキーになっていたが、100 個のウィンドウと `BindingExpression` はすべて回収された。

問題になるのは、画面を開くたびに作られ、画面より長く生きる理由の無いソースが、大きなデータを抱えている場合である。
帳票のデータ、画像、検索結果の一覧などが、ソースと一緒に残る。

### 残るのはバインドしたプロパティを持つオブジェクト

キーになるのは `DataContext` とは限らず、バインドしたプロパティを持つオブジェクトである。
`{Binding Rows.Count}` の `Rows` が `List<T>` であれば、ビューモデルが INPC を実装していても、`Count` を持つ `List<T>` は INPC を実装しない。
そのため、`Count` の監視では `List<T>` が登録表のキーになる。
表 1 の「List&lt;T&gt;.Count」の行は、INPC を実装し、1 MB の配列を 1 つ入れた `List<byte[]>` の `Rows` を持ち、`RowCount` を持たないビューモデルに、`{Binding Rows.Count}` でバインドしたものである（後で示す `ReadOnlyRowsViewModel` から `RowCount` を除いた形）。
表 1 では、どの回も開いている間に `List<T>` が登録表のキーになっており、閉じた後も `List<T>` とその中身が残った。

`ObservableCollection<T>` は INPC を実装しており、表 1 では同じパスでもキーにならず、残らなかった。
表 1 では、`List<T>` のままでも、件数を INPC のビューモデルの `RowCount` から読むようにすると、ビューモデルは登録表のキーにならず、`List<T>` も残らなかった。

### set の無いプロパティも監視される

値を書き換える手段が無いプロパティでも、監視の対象から外れない。
表 1 では、get だけのプロパティにバインドした場合も、登録表のキーになり、ソースが残った。

### OneTime は登録表に入らない

表 1 では、`Mode=OneTime` のバインドは、通知の無いソースでも登録表のキーにならず、ソースは残らなかった。
`OneTime` がソースの変更を監視しないことは、表示の動きでも確かめた。

{% include tables/articles/wpf-memory-leak-binding-non-inpc-source/binding-non-inpc-update-behavior.ja.md %}

表 5: INPC のビューモデル（`Title` の初期値は `Monthly report`）にバインドした要素で、操作の後の値を読んだ。
`TextBlock` の行は表示の値で、`Title` を `Changed` に変えて `PropertyChanged` を発行し、次に `Title` が `Replaced` の別のビューモデルへ `DataContext` を差し替えた。
`TextBox` の行はソースの `Title` の値で、`TextBox` の文字を選んで `SelectedText` を `Edited` に書き換え（その後もバインドが外れていないことを確かめた）、`UpdateSource` を呼んだ（.NET 10.0.13 / Windows 11）。
{: .table-caption}

`OneTime` の表示は、`PropertyChanged` を発行しても変わらず、`DataContext` を差し替えると変わった。
[BindingMode.OneTime](https://learn.microsoft.com/dotnet/api/system.windows.data.bindingmode#system-windows-data-bindingmode-onetime) の説明（起動時と `DataContext` が変わったときに更新する）と同じ動きである。

---

## 実装例

### ビューモデルに INPC を実装する

対処の基本は、バインドするプロパティを持つクラスに INPC を実装することである。
最小の基底クラスを用意し、ビューモデルはそれを継承する。

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class ReportViewModel : ViewModelBase
{
    private string _title = "Monthly report";

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public byte[] Data { get; } = new byte[1_000_000];
}
```

表 1 では、INPC を実装したソースは `PropertyDescriptor` の登録表のキーにならなかった。
`PropertyChanged` を一度も発行しない場合でも、ソースは残らなかった。
表 5 では、`PropertyChanged` を発行すると `OneWay` の表示が変わった。
そのため、setter は `SetProperty` を通して通知する形にしておく。

ビューの XAML は変える必要がない。
`{Binding Title}` のまま、ソース側の型だけで経路が変わる。

### 件数は ObservableCollection か、ビューモデルのプロパティで出す

一覧の件数を表示する場合は、`Rows` を `ObservableCollection<T>` にする。

```csharp
using System.Collections.ObjectModel;

public sealed class RowsViewModel : ViewModelBase
{
    public ObservableCollection<byte[]> Rows { get; } = [];
}
```

XAML は `{Binding Rows.Count}` のままでよい。

一覧を変えない読み取り専用のデータで `List<T>` を使い続けたい場合は、件数を返すプロパティをビューモデルに持たせる。

```csharp
using System.Collections.Generic;

public sealed class ReadOnlyRowsViewModel : ViewModelBase
{
    public List<byte[]> Rows { get; } = [];

    public int RowCount => Rows.Count;
}
```

XAML は `{Binding RowCount}` にする。
バインドが読むのは INPC を実装したビューモデルの `RowCount` であり、表 1 ではビューモデルも `List<T>` も残らなかった。
`RowCount` は `Rows` の変更を通知しないため、画面を開いた後に要素を増減させる一覧には `ObservableCollection<T>` を使う。

### 変わらない値は OneTime にする

画面を開いている間に変わらない値で、ソースのクラスを変更できない場合（外部のライブラリの DTO など）は、`Mode=OneTime` を指定する。

```xml
<TextBlock Text="{Binding Title, Mode=OneTime}" />
```

表 1 では、`OneTime` のバインドは登録表に入らず、ソースが INPC を実装していなくても残らなかった。
その代わり、表 5 のとおり、表示後にソースのプロパティの値が変わっても画面は更新されない（`DataContext` を差し替えたときは更新される）。
入力を受ける `TextBox` の `Text` は既定で `TwoWay` である（表 3 の `BindsTwoWayByDefault`。既定の向きの調べ方は「[WPF で Mode を書かないバインドが、ユーザー操作で黙って外れる仕組み](/ja/articles/wpf-binding-mode-omitted-detached/)」で扱っている）。
表 5 では、`TextBox` の文字を書き換えて `UpdateSource` を呼ぶと、既定の `TwoWay` ではソースに値が入り、`OneTime` ではソースの値が変わらなかった。
この方法は表示だけの要素に限る。

---

## 注意点

- **内部の型名は観察のためだけに使う。** `_valueChangedHandlers`・`ValueChangedRecord`・`_source` は非公開の実装で、別のバージョンでは名前も構造も変わり得る。アプリのコードから触らない。
- **メモリプロファイラーで保持元を見る。** 閉じた画面のビューモデルが残っているとき、保持元が `System.ComponentModel.ReflectPropertyDescriptor`（表 3）の `_valueChangedHandlers`（`ConcurrentDictionary`）か、`ValueChangedEventManager` の記録の `_source` であれば、本記事の経路である。
- **残る数で判定しない。** 表 1 でも、100 回の時点で残ったのは開いた数の一部だけである。「数回開閉して増えなかった」ことは、この経路が無いことの証明にならない。バインドしたプロパティを持つオブジェクト（パスの各段）が INPC を実装しているか、`OneTime` かを書き方で確かめる。
- **INPC を実装していないのは、ビューモデルとは限らない。** パスの途中のオブジェクト（`List<T>`、DTO、設定クラスなど）も対象になる。`{Binding A.B.C}` のようなパスでは、各段のオブジェクトの型を見る。
- **共有のソースなら急いで直す必要はない。** 最後まで生きるソースへのバインドでは、ウィンドウと `BindingExpression` は回収される（表 1）。

---

## まとめ

計測では、`OneWay`・`TwoWay` でバインドしたプロパティを持つオブジェクトが INPC を実装していないと、そのオブジェクトは同じプロパティに対して使い回される `PropertyDescriptor` の登録表に、キーとして入っていた。
このオブジェクトは `DataContext` とは限らず、`{Binding Rows.Count}` の `List<T>` のようにパスの途中のオブジェクトのこともある。
登録表のキーと記録の `_source` はどちらも強い参照で、.NET 10 では、`BindingExpression` とウィンドウが回収されても、登録が残っている間はそのオブジェクトは回収されない。
登録は GC と待機では外れず、閉じたウィンドウの `BindingExpression` が回収された後に、`OneWay` のバインドを持つ別のウィンドウを開いた時点で外れた。
バインドの無いウィンドウや、`OneTime` のバインドだけのウィンドウでは外れなかった。

画面ごとに作るビューモデルや、大きなデータを持つオブジェクトにバインドする場合は、そのクラスに INPC を実装する。
一覧の件数は `ObservableCollection<T>` か、ビューモデルの件数プロパティから表示する。
クラスを変更できず、値が変わらないものは `Mode=OneTime` にする。
アプリ全体で 1 つの長寿命のソースであれば、ウィンドウは残らないため、優先度は低い。

---

## 関連記事

- [WPF で Mode を書かないバインドが、ユーザー操作で黙って外れる仕組み](/ja/articles/wpf-binding-mode-omitted-detached/)
- [WPF バインディングエラーの読み方と出力ウィンドウを使った原因特定](/ja/articles/wpf-binding-error-debugging-output-window/)
- [WPF で ObservableCollection をバックグラウンドスレッドから更新するとクロススレッド例外が発生する問題の解決方法](/ja/articles/wpf-observablecollection-cross-thread-update/)

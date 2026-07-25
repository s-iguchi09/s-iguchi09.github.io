---
layout: article-ja
title: "WPF で BitmapImage に表示した画像ファイルが削除・上書きできなくなる問題の解決方法"
date: 2026-07-25
category: WPF
excerpt: "BitmapImage で表示した画像ファイルがロックされ、削除・上書きできなくなる。原因である既定のキャッシュ動作と、BitmapCacheOption.OnLoad・StreamSource による解決方法を整理する。"
---

## 概要

WPF の `Image` コントロールに `BitmapImage` を与えてローカルの画像ファイルを表示すると、アプリケーションの実行中はそのファイルを削除・上書きできなくなる。
`File.Delete` や書き込み用の `FileStream` が「別のプロセスで使用されているため、プロセスはファイルにアクセスできません」という趣旨の `IOException` で失敗する。
本記事では、この現象が `BitmapImage` による明示的なロックではなく、既定のキャッシュ動作が画像ソースへのアクセスを保持し続けることに起因する点を説明する。
そのうえで `BitmapCacheOption.OnLoad` による解決方法、`UriSource` と `StreamSource` の書き分け、`BitmapCacheOption` 各値の比較、`Freeze` とメモリ消費の注意点を整理する。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF（.NET Framework 3.0 以降でも同じ挙動）
- 言語: C# / XAML
- 対象クラス・機能: `System.Windows.Controls.Image`、`BitmapImage`、`BitmapCacheOption`、`BitmapCreateOptions`
- アーキテクチャ: MVVM・コードビハインドのいずれにも適用可能
- 前提: 表示対象がビルド時に埋め込むアプリケーションリソースではなく、実行時に差し替えられるローカルファイル（ユーザーが選択した画像、ダウンロードした一時ファイルなど）
- コード例は名前空間 `System` / `System.IO` / `System.Windows.Media.Imaging` を前提とする

---

## 問題

ファイルパスから `BitmapImage` を生成して `Image.Source` に設定した後、同じファイルを削除または上書きしようとすると例外が発生する。
以下は、最も素直に書いた場合の再現コードである。

```csharp
// 画像を表示する
PreviewImage.Source = new BitmapImage(new Uri(path, UriKind.Absolute));

// 同じファイルを削除・上書きしようとすると IOException になる
File.Delete(path);
```

`Image` の表示自体は成功するが、`File.Delete` はファイルが使用中である旨の `IOException` を送出する。
上書き保存やリネームも同様に失敗する。
厄介なのは、この失敗が確実に再現するとは限らない点である。
表示を止めて `BitmapImage` への参照を捨てた後にガベージコレクションが走ると、ロックが解けて削除に成功することがある。
このため「たまに削除できる」不安定な不具合として現れやすい。

XAML で直接パスを与えた場合も同じ結果になる。
次の記述は `BitmapImage` の既定値をそのまま使うため、同様にファイルを保持し続ける。

```xml
<Image Source="{Binding ImagePath}" Stretch="Uniform" />
```

---

## 原因・背景

原因は `BitmapImage` の**キャッシュ方針**にある。
`BitmapImage.CacheOption` の既定値は `BitmapCacheOption.Default` であり、公式ドキュメントはこの既定の動作を次のように説明している。

> 既定の `OnDemand` キャッシュ オプションは、ビットマップが必要になるまでストリームへのアクセスを保持し、クリーンアップはガベージ コレクターによって処理される。

ここで `Default` と `OnDemand` が併記されているのは記述の揺れではない。
`BitmapCacheOption` 列挙型では `Default` と `OnDemand` の値がいずれも `0` と定義されており、両者は同一の値である。
したがって「既定値は `Default`」と「既定は `OnDemand` の動作」は矛盾しない。

`OnDemand` は、画像データの要求があった時点で必要な分だけを読み出す遅延方式である。
デコードを後回しにできるためメモリと起動コストを抑えられるが、その代償として、後から読み出せるように**画像ソースへのアクセスを保持し続ける**必要がある。
`UriSource` にローカルファイルを指定した場合、WPF が内部で開いたファイルストリームがこれに当たる。
ストリームを解放するのはガベージコレクターであり、アプリケーションが明示的に閉じる手段は無い。
これが「削除できたりできなかったりする」挙動の正体である。

問題の本質は、`BitmapImage` がファイルを排他的に掴むことではなく、**遅延デコードのためにソースを開いたまま保持する設計**にある。
したがって解決策は「ロックを外す」ことではなく、「読み込み時点でデコードを完了させ、ソースを保持する必要をなくす」ことになる。

---

## 解決方法

`CacheOption` に `BitmapCacheOption.OnLoad` を指定する。
`OnLoad` は読み込み時に画像全体をメモリへキャッシュし、以降の画像データ要求はすべてメモリストアから満たされる。
ソースを読み続ける必要が無くなるため、初期化完了後にファイルやストリームを解放できる。

`CacheOption` はプロパティであり、`BitmapImage` の初期化中にしか設定できない。
`BitmapImage` は `ISupportInitialize` を実装しており、プロパティの設定は `BeginInit` と `EndInit` の間で行う必要がある。
初期化完了後のプロパティ変更は無視される。

アプローチは 2 つある。

- **`UriSource` + `OnLoad`** — パスを直接与える。記述が短く、通常はこれで足りる。
- **`StreamSource` + `OnLoad`** — 自前で開いたストリームを与え、初期化後に確実に閉じる。ファイルの開き方（共有モードなど）を制御したい場合に適する。

---

## 実装例

### UriSource に OnLoad を組み合わせる

`BeginInit` / `EndInit` ブロック内で `CacheOption` と `UriSource` を設定する。
`EndInit` の時点でデコードが完了するため、戻り値を受け取った後はファイルを自由に削除・上書きできる。

```csharp
private static BitmapImage LoadWithoutLocking(string path)
{
    var bitmap = new BitmapImage();
    bitmap.BeginInit();
    bitmap.CacheOption = BitmapCacheOption.OnLoad;
    bitmap.UriSource = new Uri(path, UriKind.Absolute);
    bitmap.EndInit();
    bitmap.Freeze();
    return bitmap;
}
```

`Freeze` の呼び出しは必須ではないが、`BitmapImage` は `Freezable` の派生クラスであり、凍結すると変更通知のコストが無くなるうえ、スレッド間で共有できるようになる。
非同期に画像を読み込んで UI スレッドへ渡す構成では、凍結が事実上の前提となる。

ここで注意が必要なのは、`BitmapImage(Uri)` コンストラクタとの違いである。
このコンストラクタで生成した `BitmapImage` は**自動的に初期化済み**となり、以降のプロパティ変更は無視される。
そのため、次のコードは `OnLoad` が反映されずロックが残る。

```csharp
// 生成時点で初期化が完了しているため、CacheOption の変更は無視される
var bitmap = new BitmapImage(new Uri(path, UriKind.Absolute));
bitmap.CacheOption = BitmapCacheOption.OnLoad;
```

`OnLoad` を効かせるには、引数なしコンストラクタと `BeginInit` / `EndInit` の組み合わせを使う必要がある。

### StreamSource に自前のストリームを与える

ファイルの開き方を制御したい場合は、`FileStream` を自分で開いて `StreamSource` に渡す。
`OnLoad` を指定していれば `EndInit` の完了時点でデコードが済んでいるため、`using` ブロックを抜けてストリームを破棄しても画像は表示できる。

```csharp
private static BitmapImage LoadFromStream(string path)
{
    var bitmap = new BitmapImage();
    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
    {
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
    }

    bitmap.Freeze();
    return bitmap;
}
```

`FileShare.ReadWrite` を指定しているため、読み込み中に他のプロセスが同じファイルを書き換えていても開ける。
なお `StreamSource` と `UriSource` の両方を設定した場合、`StreamSource` は無視される。
この方式を選ぶときは `UriSource` を設定しないこと。

### XAML で指定する

XAML では、`Source` に文字列を書く短縮記法ではなく `BitmapImage` を要素として明示し、`CacheOption` を指定する。
XAML パーサーがオブジェクト要素の解析時に `BeginInit` / `EndInit` を呼ぶため、この記述でも `OnLoad` は有効になる。

```xml
<Image Stretch="Uniform">
    <Image.Source>
        <BitmapImage UriSource="{Binding ImagePath}" CacheOption="OnLoad" />
    </Image.Source>
</Image>
```

バインドしたパスが切り替わるたびに新しい `BitmapImage` が生成され、その都度メモリへキャッシュされる。
一覧のサムネイル表示など画像数が多い場面では、後述のデコードサイズ指定を併用する。

---

## 注意点

- **メモリ消費と引き換えである:** `OnLoad` は画像全体をメモリへ展開する。大きな画像や多数のサムネイルでは消費量が問題になるため、`DecodePixelWidth` または `DecodePixelHeight` を設定して表示サイズ相当でデコードする。縦横比を保つには、両方ではなくいずれか一方のみを設定する。
- **上書き後に古い画像が表示される:** WPF は URI 単位で画像をキャッシュするため、同じパスのファイルを差し替えて再読み込みしても以前の画像が表示されることがある。`CreateOptions` に `BitmapCreateOptions.IgnoreImageCache` を指定すると、同じ `Uri` を共有する既存のキャッシュエントリが置き換えられる。
- **`Freeze` できない条件がある:** データバインドまたはアニメーション対象のプロパティを持つ場合、`DynamicResource` で設定されたプロパティを持つ場合、凍結できない子オブジェクトを含む場合は凍結できない。事前に `CanFreeze` で判定する。
- **凍結後の変更は例外になる:** 凍結した `Freezable` を変更しようとすると `InvalidOperationException` が発生する。読み込み後に加工が必要なら、凍結せずに扱うか `Clone` で変更可能な複製を作る。
- **未凍結のオブジェクトはスレッドをまたげない:** `IsFrozen` が `false` の `Freezable` は生成したスレッドからのみアクセスでき、別スレッドから触ると `InvalidOperationException` になる。バックグラウンドで読み込んだ画像を UI スレッドへ渡す場合は、渡す前に凍結する。
- **`BitmapCacheOption.None` は解決策にならない:** `None` はメモリストアを作らず、すべての要求を画像ファイルから直接満たす。ソースへのアクセスは保持され続けるため、ロックの回避には使えない。

---

## 代替案・比較

| 方法 | ファイルの解放 | メモリ | 適するケース |
| --- | --- | --- | --- |
| `UriSource` + 既定（`Default` / `OnDemand`） | 解放されない（GC 任せ） | 遅延デコードで小さい | ビルドに埋め込んだリソース画像など、ファイルを差し替えない場合 |
| `UriSource` + `OnLoad` | 初期化完了時に解放 | 画像全体を保持 | 実行時に削除・上書きし得るローカルファイル。既定の選択 |
| `StreamSource` + `OnLoad` | `using` で明示的に解放 | 画像全体を保持 | 共有モードの指定やメモリ上のデータからの生成が必要な場合 |
| `BitmapCacheOption.None` | 解放されない | 最小 | 画像ファイルを保持したままでよく、メモリを最優先する場合 |

`UriSource` と `StreamSource` の選択基準は明確である。
パスから読むだけなら `UriSource` で足りる。
`FileShare` の指定、ネットワーク越しや暗号化されたデータの復号結果など、ストリームの取得方法を自分で決める必要がある場合に `StreamSource` を選ぶ。

---

## まとめ

`BitmapImage` による画像ファイルのロックは、既定のキャッシュ方針が遅延デコードのためにソースを開いたまま保持することに起因する。
解決策の選択基準は次のとおりである。

- **ローカルファイルを表示し、後から削除・上書きする可能性がある場合:** `BeginInit` / `EndInit` ブロック内で `CacheOption` に `OnLoad` を指定する。これが既定の選択となる。
- **ファイルの共有モードを制御したい、またはメモリ上のデータから生成する場合:** `StreamSource` と `OnLoad` を組み合わせ、初期化後にストリームを明示的に破棄する。
- **画像を差し替えて再読み込みする場合:** `BitmapCreateOptions.IgnoreImageCache` を併用し、URI 単位のキャッシュによる古い画像の表示を防ぐ。
- **バックグラウンドで読み込む場合:** `EndInit` の後に `Freeze` してから UI スレッドへ渡す。

いずれの場合も、`BitmapImage(Uri)` コンストラクタでは初期化が自動完了してプロパティ変更が無視される点を踏まえ、`OnLoad` を使うときは必ず引数なしコンストラクタと `BeginInit` / `EndInit` を用いる、という前提で実装するのが要点である。

---

<!-- 関連記事 -->
- [WPF で ObservableCollection をバックグラウンドスレッドから更新するとクロススレッド例外が発生する問題の解決方法](/ja/articles/wpf-observablecollection-cross-thread-update/)

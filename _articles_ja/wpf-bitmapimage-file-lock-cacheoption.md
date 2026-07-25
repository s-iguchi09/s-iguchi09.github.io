---
layout: article-ja
title: "WPF の BitmapImage で表示した画像ファイルが削除・上書きできなくなる問題の解決方法"
date: 2026-07-25
category: WPF
excerpt: "BitmapImage で表示した画像ファイルがロックされ、削除・上書きできなくなる。原因である既定のキャッシュ動作と、BitmapCacheOption.OnLoad・StreamSource による解決方法を整理する。"
---

## 概要

WPF の `Image` コントロールに `BitmapImage` を与えてローカルの画像ファイルを表示すると、アプリケーションの実行中はそのファイルを削除・上書きできなくなる。
本記事では、この現象が `BitmapImage` による明示的なロックではない点を説明する。
実際の原因は、既定のキャッシュ方針が画像ソースへのアクセスを保持し続けることにある。
そのうえで `BitmapCacheOption.OnLoad` による解決方法、`UriSource` と `StreamSource` の書き分け、`BitmapCacheOption` 各値の比較、`Freeze` とメモリ消費の注意点を整理する。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF（`BitmapImage`・`BitmapCacheOption` は .NET Framework 3.0 から提供されている）
- 言語: C# / XAML
- 対象クラス・機能: `System.Windows.Controls.Image`、`BitmapImage`、`BitmapCacheOption`、`BitmapCreateOptions`
- アーキテクチャ: MVVM・コードビハインドのいずれにも適用可能
- 前提: 表示対象がビルド時に埋め込むアプリケーションリソースではなく、実行時に差し替えられるローカルファイル（ユーザーが選択した画像、ダウンロードした一時ファイルなど）
- 名前空間: コード例は `System` / `System.IO` / `System.Windows.Media.Imaging` を前提とする

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

`Image` の表示自体は成功するが、`File.Delete` は対象ファイルが使用中である旨の `IOException` を送出する。
上書き保存やリネームも同様に失敗する。
この失敗は確実に再現するとは限らない。
表示を止めて `BitmapImage` への参照を捨てた後にガベージコレクションが走ると、ファイルが解放されて削除に成功することがある。
このため「たまに削除できる」不安定な不具合として現れやすい。

XAML でパスを与えた場合も同じ結果になる。
次の記述は、`Source` にパス文字列を直接与えて画像を表示する最も短い書き方である。

```xml
<Image Source="{Binding ImagePath}" Stretch="Uniform" />
```

`Source` に文字列を直接与える書き方は `ImageSourceConverter` による型変換に委ねられており、`CacheOption` を指定する余地が無い。
そのため既定のキャッシュ動作のまま読み込まれ、コードから生成した場合と同じくファイルが保持される。

---

## 原因・背景

原因は `BitmapImage` の**キャッシュ方針**にある。
`BitmapImage.CacheOption` の既定値は `BitmapCacheOption.Default` である。
`CacheOption` プロパティの公式ドキュメントは、既定の動作を次のように説明している。

> 既定の `OnDemand` キャッシュ オプションは、イメージが必要になるまでストリームへのアクセスを保持し、クリーンアップはガベージ コレクターによって処理されます。

出典: [BitmapImage.CacheOption プロパティ](https://learn.microsoft.com/ja-jp/dotnet/api/system.windows.media.imaging.bitmapimage.cacheoption)

`Default` と `OnDemand` が併記されるのは、`BitmapCacheOption` 列挙型で両者の値がいずれも `0` と定義されており、同一の値だからである。
ただし列挙型側の説明文は一致していない。
`Default` は「イメージ全体をメモリにキャッシュする」、`OnDemand` は「要求されたデータのみのメモリ ストアを作成する」と記述されており、公式ドキュメント内で食い違っている。
値が同一である以上、実際に適用される動作は一つである。
`CacheOption` の注釈は `OnDemand` を既定として名指ししたうえで、「イメージが必要になるまでストリームへのアクセスを保持し」と説明している。

`OnDemand` は、要求されたデータの分だけメモリストアを作る方式である。
最初の要求では画像が直接読み込まれ、以降の要求はキャッシュから満たされる。
後続の読み出しに備え、画像ソースへのアクセスが保持される。
オブジェクトの初期化そのものを必要になるまで遅らせるのは `CacheOption` ではなく `BitmapCreateOptions.DelayCreation` の役割であり、両者は独立した設定である。

`UriSource` にローカルファイルを指定した場合、保持されるのは WPF が内部で開いたファイルストリームだと考えられる。
これがファイルを開いたままにする実体であり、アプリケーション側から明示的に閉じる手段は無い。
解放を担うのはガベージコレクターであり、これが「削除できたりできなかったりする」挙動の背景である。

公式ドキュメントが `OnLoad` の効果として明示しているのは、「`BitmapImage` の作成に使ったストリームを閉じられる」という点である。
`UriSource` に渡したローカルファイルについて同じ文言があるわけではないが、保持されるのは同じく内部のストリームであるため、同じ仕組みが働く。

問題の本質は、`BitmapImage` がファイルを排他的に掴むことではなく、**後続の読み出しに備えてソースを開いたまま保持する設計**にある。
したがって解決策は「ロックを外す」ことではなく、「読み込み時点で画像全体をメモリへ取り込み、ソースを保持する必要をなくす」ことになる。

---

## 解決方法

`CacheOption` に `BitmapCacheOption.OnLoad` を指定する。
`OnLoad` は読み込み時に画像全体をメモリへキャッシュし、以降の画像データ要求はすべてメモリストアから満たされる。
ソースを読み続ける必要が無くなるため、初期化完了後にファイルやストリームを解放できる。

`CacheOption` は `BitmapImage` の初期化中にしか設定できない。
`BitmapImage` は `ISupportInitialize` を実装しており、プロパティの設定は `BeginInit` と `EndInit` の間で行う必要がある。
初期化完了後のプロパティ変更は無視される。

アプローチは 2 つある。

- **`UriSource` + `OnLoad`** — パスを直接与える。
  XAML・コードとも記述が短く、通常はこれで足りる。
- **`StreamSource` + `OnLoad`** — 自前で開いたストリームを与え、初期化後に確実に閉じる。
  ファイルの開き方やデータの取得元を制御したい場合に適する。

---

## 実装例

### UriSource に OnLoad を組み合わせる

`BeginInit` / `EndInit` ブロック内で `CacheOption` と `UriSource` を設定する。
`EndInit` の時点で画像全体がメモリへ取り込まれるため、戻り値を受け取った後はファイルを削除・上書きできる。

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
ローカルファイルを `OnLoad` で読み込んだ直後は凍結可能だが、条件が読めない場面では後述のとおり `CanFreeze` で判定してから呼ぶ。

ここで注意が必要なのは、`BitmapImage(Uri)` コンストラクタとの違いである。
このコンストラクタで生成した `BitmapImage` は**自動的に初期化済み**となり、以降のプロパティ変更は無視される。
そのため、次のコードは `OnLoad` が反映されずファイルが保持されたままになる。

```csharp
// 生成時点で初期化が完了しているため、CacheOption の変更は無視される
var bitmap = new BitmapImage(new Uri(path, UriKind.Absolute));
bitmap.CacheOption = BitmapCacheOption.OnLoad;
```

`OnLoad` を効かせるには、引数なしコンストラクタと `BeginInit` / `EndInit` の組み合わせを使う必要がある。

### StreamSource に自前のストリームを与える

ファイルの開き方を制御したい場合は、`FileStream` を自分で開いて `StreamSource` に渡す。
`OnLoad` を指定していれば `EndInit` の完了時点で画像全体がメモリへ取り込まれているため、`using` ブロックを抜けてストリームを破棄しても画像は表示できる。

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

`FileShare` は、ファイルを開いている間に、同一プロセス・他プロセスを問わず後続のオープンへ許可するアクセス種別を指定する。
`FileShare.ReadWrite` を指定しているため、この `FileStream` を開いている間も他のプロセスが同じファイルを読み書き用に開ける。
ただし `ReadWrite` が許可するのは読み取りと書き込みだけであり、削除・リネームは含まれない。
読み込み中の削除まで許可する必要がある場合は、`FileShare.ReadWrite | FileShare.Delete` のように `Delete` を併せて指定する。

`StreamSource` と `UriSource` の両方を設定した場合、`StreamSource` は無視される。
この方式では `UriSource` を設定しない。

### XAML での指定と制約

表示する画像が固定パスで決まっている場合は、XAML だけで完結する。
`Source` に文字列を書いて型コンバーターに任せるのではなく、`BitmapImage` をオブジェクト要素として記述し、`CacheOption` を指定する。
オブジェクト要素に書いたプロパティ設定は初期化の一部として反映されるため、この記述で `OnLoad` が有効になる。

```xml
<Image Stretch="Uniform">
    <Image.Source>
        <BitmapImage UriSource="C:\work\preview.png" CacheOption="OnLoad" />
    </Image.Source>
</Image>
```

この `BitmapImage` は XAML の解析時に一度だけ生成される。
初期化後のプロパティ変更は無視されるため、`UriSource` にバインドを設定してもパスの切り替えは反映されない。
表示する画像を実行時に差し替える場合は、パス文字列から `BitmapImage` を生成する `IValueConverter` を挟むか、ViewModel 側で `ImageSource` 型のプロパティを公開して `Image.Source` にバインドする。
後者では値が変わるたびに新しい `ImageSource` が渡されるため、前掲の `LoadWithoutLocking` で生成した `BitmapImage` をそのまま代入する。

---

## 注意点

- **メモリ消費とのトレードオフ:** `OnLoad` は画像全体をメモリへ展開する。
  大きな画像や多数のサムネイルでは消費量が問題になるため、`DecodePixelWidth` または `DecodePixelHeight` を設定して表示サイズ相当でデコードする。
  縦横比を保つには、両方ではなくいずれか一方のみを設定する。
- **デコードサイズ指定の効果は形式によって異なる:** JPEG と PNG のコーデックは指定したサイズへ直接デコードするが、それ以外の形式では原寸でデコードしてから目的のサイズへスケールされる。
  BMP や TIFF などではピーク時のメモリ削減効果が期待どおりにならない。
- **上書き後に古い画像が表示される:** WPF は画像キャッシュを URI 単位で管理するため、同じパスのファイルを差し替えて再読み込みしても以前の画像が表示されることがある。
  `CreateOptions` に `BitmapCreateOptions.IgnoreImageCache` を指定すると、同じ `Uri` を共有する既存のキャッシュエントリが置き換えられる。
- **`Freeze` できない条件がある:** データバインドまたはアニメーション対象のプロパティを持つ場合、`DynamicResource` で設定されたプロパティを持つ場合、凍結できない子オブジェクトを含む場合は凍結できない。
  条件が読めない場面では `CanFreeze` で判定してから `Freeze` を呼ぶ。
- **凍結後の変更は例外になる:** 凍結した `Freezable` を変更しようとすると `InvalidOperationException` が発生する。
  読み込み後に加工が必要なら、凍結せずに扱うか `Clone` で変更可能な複製を作る。
- **未凍結のオブジェクトはスレッドをまたげない:** `IsFrozen` が `false` の `Freezable` は生成したスレッドからのみアクセスでき、別スレッドから触ると `InvalidOperationException` になる。
  バックグラウンドで読み込んだ画像を UI スレッドへ渡す場合は、渡す前に凍結する。
- **`BitmapCacheOption.None` は解決策にならない:** `None` はメモリストアを作らず、すべての要求を画像ファイルから直接満たす。
  この動作上ソースへのアクセスを保持し続ける必要があるため、ロックの回避には使えない。

---

## 代替案・比較

| 方法 | ファイルの解放 | メモリストア | 適するケース |
| --- | --- | --- | --- |
| `UriSource` + 既定（`Default` / `OnDemand`） | 解放されない（GC 任せ） | 要求されたデータ分のみ作成 | 実行中に差し替えの発生しない固定的な画像 |
| `UriSource` + `OnLoad` | 初期化完了時に解放 | 読み込み時に画像全体を作成 | 実行時に削除・上書きし得るローカルファイル |
| `StreamSource` + `OnLoad` | `using` で明示的に解放 | 読み込み時に画像全体を作成 | 共有モードの指定やメモリ上のデータからの生成が必要な場合 |
| `BitmapCacheOption.None` | 解放されない | 作成しない | 要求のたびにファイルから読み直してよい場合 |

`UriSource` と `StreamSource` の選択基準は明確である。
パスから読むだけなら `UriSource` で足りる。
`FileShare` の指定、ネットワーク越しや暗号化されたデータの復号結果など、ストリームの取得方法を自分で決める必要がある場合に `StreamSource` を選ぶ。

---

## まとめ

`BitmapImage` による画像ファイルのロックは、既定のキャッシュ方針が後続の読み出しに備えてソースを開いたまま保持することに起因する。
解決策の選択基準は次のとおりである。

- **ローカルファイルを表示し、後から削除・上書きする可能性がある場合:** `BeginInit` / `EndInit` ブロック内で `CacheOption` に `OnLoad` を指定する。
  これを第一候補とする。
- **ファイルの共有モードを制御したい、またはメモリ上のデータから生成する場合:** `StreamSource` と `OnLoad` を組み合わせ、初期化後にストリームを明示的に破棄する。
- **画像を差し替えて再読み込みする場合:** `BitmapCreateOptions.IgnoreImageCache` を併用し、URI 単位のキャッシュによる古い画像の表示を防ぐ。
- **バックグラウンドで読み込む場合:** `EndInit` の後に `Freeze` してから UI スレッドへ渡す。

いずれの方法にも共通する前提として、`BitmapImage(Uri)` コンストラクタは初期化を自動的に完了させ、以降のプロパティ変更を無視する。
`OnLoad` を使う実装では、必ず引数なしコンストラクタと `BeginInit` / `EndInit` の組み合わせを用いる。

---

<!-- 関連記事 -->
- [WPF で ObservableCollection をバックグラウンドスレッドから更新するとクロススレッド例外が発生する問題の解決方法](/ja/articles/wpf-observablecollection-cross-thread-update/)

# 記事用スクリーンショット生成ツール

技術記事に載せるスクリーンショットを、実際に WPF アプリケーションを起動して取得するツール。
モックアップではなく本物のウィンドウをキャプチャすることで、記事が実際の動作確認に基づくことを示す。

出力先はリポジトリ内の `images/articles/<slug>/` である。

## 実行環境

- Windows（ウィンドウを実表示してキャプチャするため、デスクトップセッションが必要）
- .NET 10 SDK

Fluent テーマ（`ThemeMode`）を使うシーンがあるため、`net10.0-windows` を対象にしている。

## 実行方法

全シーンを取得する。

```bash
dotnet run --project tools/screenshot-capture -c Release
```

記事の slug を指定すると、そのシーンだけを取得する。

```bash
dotnet run --project tools/screenshot-capture -c Release -- wpf-label-underscore-issue
```

実行中はウィンドウが順に表示されてフォーカスを奪う。数秒で終了する。
保存したファイルのパスが標準出力に列挙される。

## シーンの追加

1. `Scenes/` に `IScene` を実装したクラスを追加する。
   - `Slug` に対応する記事の slug を返す。
   - `CaptureAsync` で `SceneContext.ShootAsync(window, fileName)` を呼び、ウィンドウを保存する。
   - フォーカスやテンプレートパーツの操作など、表示後に行う処理は `ShootAsync` の `beforeCapture` に渡す。
2. `Program.cs` の `AllScenes` に登録する。
3. 実行して `images/articles/<slug>/` に出力されることを確認する。

記事に載せる XAML と図の内容が食い違わないよう、UI は可能な範囲で `SceneContext.LoadXaml<T>` に
記事と同じ XAML 文字列を渡して組み立てる。既定の名前空間は補われるため、`xmlns` は書かなくてよい。

「記述したマークアップ → 実際の描画結果」を並べる図は `DemoLayout.BuildComparisonWindow` を使う。
図中の文言は日英で共有するため、コードと矢印だけで構成し、自然言語を入れない。

## 実装上の注意

- キャプチャは Win32 の `PrintWindow`（`PW_RENDERFULLCONTENT`）で行う。
  `RenderTargetBitmap` と異なり、タイトルバーとウィンドウ枠を含んだ実際の見た目が得られる。
- ドロップシャドウ分の余白を除くため、`DwmGetWindowAttribute` の `DWMWA_EXTENDED_FRAME_BOUNDS` で
  切り出し範囲を求めている。
- 取得される画像は表示スケール 100%（96 DPI）のとき等倍になる。
  スケールを変更している環境では出力サイズが変わるため、記事の `width` / `height` 属性と合わなくなる。
- `UseWPF` を有効にすると暗黙 using から `System.IO` が外れるため、`.csproj` で明示的に足している。

## 記事側の記法

生成した画像の埋め込み方（`figure` 要素・`alt`・パス・front matter の `image`）は
`docs/rules/article/guidelines.md` の §11 に定める。

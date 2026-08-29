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

## 検証記録（`docs/verification/`）

シーンが「実際に動かして確かめていること」を `Verifies` として宣言すると、実行時に
`docs/verification/<slug>.yml` へ自動で書き出される。

```yaml
slug: "wpf-binding-error-debugging-output-window"
scene: "BindingErrorTraceScene"
environment:
  runtime: ".NET 10.0.10"
  os: "Microsoft Windows 10.0.26200"
verifies:
  - "パス解決失敗が Error 40、ConvertBack 失敗が Error 7、空のインデクサーが Error 17 であること"
images:
  - "images/articles/.../binding-error-trace-matrix.png"
```

**目的は、同じ検証を何度も繰り返さないことである。**
記事の書式（`検証環境` の行があるか、PNG を参照しているか）から推測すると、書き方の違いで
検証済みの記事を未検証と誤判定する。実際にそれが起きて、既に実測済みの記事を再検証したことがある。

```bash
# 実測で検証済みの記事:
ls docs/verification/ | sed 's/\.yml$//' | sort

# 未検証の記事:
comm -23 \
  <(ls _articles_ja/*.md | xargs -n1 basename | sed 's/\.md$//' | sort) \
  <(ls docs/verification/ 2>/dev/null | sed 's/\.yml$//' | sort)
```

このファイルは手で編集しない。内容を変えるにはシーンの `Verifies` を直して再実行する。
`docs` は `_config.yml` の `exclude` に入っているため、サイトには出力されない。

図を描くだけで何も検証していないシーンは `Verifies` を空のままにする。その場合は記録も作られない。

## シーンの追加

1. `Scenes/` に `IScene` を実装したクラスを追加する。
   - `Slug` に対応する記事の slug を返す。
   - `CaptureAsync` で `SceneContext.ShootAsync(window, fileName)` を呼び、ウィンドウを保存する。
   - フォーカスやテンプレートパーツの操作など、表示後に行う処理は `ShootAsync` の `beforeCapture` に渡す。
   - 実行結果で記事の主張を確かめている場合は、`Verifies` にその内容を書く。
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
- タイトルバー・枠の色と Mica 背景は `DwmSetWindowAttribute` でウィンドウ単位に固定している。
  既定では OS のアクセントカラー設定が反映され、撮影環境ごとに図の色が変わってしまうため。
  Windows の設定自体は変更しない。
- ただし **コントロール内部のアクセント色（Fluent テーマのフォーカス下線など）は OS の設定がそのまま出る。**
  テンプレートが実体化した後にリソースを差し替えても反映されないため、色を揃えたい場合は
  撮影前に Windows の[設定] > [個人用設定] > [色]でアクセントカラーを既定に戻す。
- 取得される画像は表示スケール 100%（96 DPI）のとき等倍になる。
  スケールを変更している環境では出力サイズが変わるため、記事の `width` / `height` 属性と合わなくなる。
- `UseWPF` を有効にすると暗黙 using から `System.IO` が外れるため、`.csproj` で明示的に足している。

## 記事側の記法

生成した画像の埋め込み方（`figure` 要素・`alt`・パス・front matter の `image`）は
`docs/rules/article/guidelines.md` の §11 に定める。

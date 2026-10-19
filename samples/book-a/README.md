# 「エラーも出ずに壊れる WPF」付録サンプル

本の各章の**再現版**（問題が起きるコード）と**修正版**を、MVVM の形で収めたソリューション。
各章の内容は、サイトの記事の実測にもとづく。

## 実行環境

- Windows 11
- .NET 10 SDK

## 実行方法

リポジトリのルートで次を実行する。

```bash
dotnet run --project samples/book-a/src/BookA.Samples
```

章ごとのタブに、再現版と修正版を左右に並べて表示する。
両者は別のビューモデルを持つので、片方の操作はもう片方に影響しない。

## 構成

| プロジェクト | 中身 |
|---|---|
| `src/BookA.Mvvm` | 全章が使う最小の MVVM の基盤（`ObservableObject`・`RelayCommand`）。外部のライブラリは使わない |
| `src/Ch01.BindingModeOmitted` | 第 1 章: Mode を書かないバインドが、ユーザー操作で黙って外れる |
| `src/BookA.Samples` | 起動用のアプリ |

各章のプロジェクトは、ビューモデル（`*ViewModel.cs`）と、再現版・修正版のビュー（`ReproView.xaml`・`FixedView.xaml`）を持つ。
ビューのコードビハインドには `InitializeComponent` しか書かない。
再現版と修正版は同じビューモデルのクラスを使い（インスタンスは別）、違いはビューの XAML だけにしてある。

## 記事の実測と同じ動作であることの検証

各章のサンプルは、記事の計測と同じ実際のマウスとキーボードの操作で動かし、記事の表と同じ結果になることを確かめている。
検証は `tools/screenshot-capture/Scenes/BookA/` のシーンで、結果は `verification/` に書き出す。
リポジトリのルートで次を実行する。

```bash
dotnet run --project tools/screenshot-capture -c Release -- book-a-ch01-binding-mode-omitted
```

実行中はウィンドウが前面に出て、マウスカーソルが動く。
結果が記事の表と 1 か所でも食い違うと、レポートを書いたうえで失敗（終了コード 1）になる。

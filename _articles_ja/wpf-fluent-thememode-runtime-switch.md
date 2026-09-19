---
layout: article-ja
title: "WPF の ThemeMode を実行時に切り替えても一部だけ Light のまま残る原因の切り分け"
date: 2026-09-19
category: WPF
excerpt: "Application.ThemeMode を Dark に切り替えても、特定のウィンドウや文字色だけが Light のまま残る。原因はウィンドウ側の Fluent 辞書、ネストした辞書、StaticResource の 3 系統に分かれる。実測した対応表から切り分け手順と対処を整理する。"
image: /images/articles/wpf-fluent-thememode-runtime-switch/switched-main-window.png
---

## 概要

`Application.ThemeMode` は実行中にも書き換えられるため、設定画面にライトとダークの切り替えを置く実装に使える。
ただし、コントロールの色とスタイルについて書き換えが届くのは、`Application.Resources` に置かれた Fluent のリソースディクショナリだけである。
その辞書を経由せずに色を決めている部分は、切り替えても Light のまま残る。
例外は発生せず、バインディングエラーも出力されないため、どこが追従していないのかを画面から探すことになる。

実測すると、追従しない原因は 3 系統に分かれた。
ウィンドウ側が自前の Fluent 辞書を持っている場合、Fluent 辞書を別の辞書の中にネストしている場合、ブラシを `StaticResource` で固定している場合である。
本記事では、`ThemeMode` の切り替えが何を差し替えているかを実測で示し、そこから原因を絞り込む手順と、原因ごとの対処を整理する。

---

## 前提・対象環境

- フレームワーク: .NET 9 以降の WPF（`net9.0-windows` 以降。`ThemeMode` は .NET 9 で追加された）
- 対象 API: `Application.ThemeMode`、`Window.ThemeMode`、Fluent テーマのリソースディクショナリ
- アーキテクチャ: MVVM・コードビハインドのいずれでも同じ（切り替え処理はコードから行う）
- 検証環境: .NET 10.0.10 / Windows 11（OS の「アプリのモード」はダーク）
- ビルドの確認: .NET SDK 10.0.302 で新規作成した WPF プロジェクト
- 計測方法: アプリ全体を Light にした状態でウィンドウを開き、`Application.ThemeMode` を Dark へ切り替えた。切り替えの前後で、`DynamicResource` で参照したブラシの値、`Application.Resources` のマージ辞書の並び、`Foreground` のローカル値の型を読み出した。この計測は `tools/screenshot-capture` のシーンとして実装している

`ThemeMode` は実験的 API として公開されており、[.NET 9 の WPF における変更点](https://learn.microsoft.com/dotnet/desktop/wpf/whats-new/net90#thememode)が述べるとおり、コードから `ThemeMode` プロパティにアクセスするとエラー `WPF0001` になる。
上記のプロジェクトで確かめたところ、`App.xaml` の `ThemeMode="Dark"` 属性だけではエラーにならず、コードビハインドで `this.ThemeMode` に代入した時点で `error WPF0001` となった。
実行時の切り替えは必ずコードから行うため、このエラーの抑制は避けられない。
属性は `ThemeMode` 構造体にも付いており（[ThemeMode 構造体](https://learn.microsoft.com/dotnet/api/system.windows.thememode)）、ViewModel に相当する通常のクラスでは `ThemeMode.Dark` を参照しただけでエラーになった。
一方、`Window` の派生クラスの中では、`ThemeMode.Dark` の参照だけではエラーにならず、プロパティにアクセスした行でエラーになった。
プロジェクトファイルで抑制した場合は、どちらもエラーなくビルドできた。
本記事のコードは該当箇所を `#pragma warning disable WPF0001` で囲んでいる。
MVVM のように参照箇所がクラスをまたいで散らばる場合は、プロジェクトファイルに `<NoWarn>$(NoWarn);WPF0001</NoWarn>` を指定するほうが扱いやすい。

---

## 問題

次の XAML は、Fluent のブラシを `DynamicResource` と `StaticResource` の両方で参照するテキストと、Fluent のスタイルが当たる標準コントロールを並べたものである。
アプリ全体を Light にした状態で、同じ内容のウィンドウを 2 枚開いておく。

```xml
<Border Background="{DynamicResource ApplicationBackgroundBrush}" Padding="20">
  <StackPanel>
    <TextBlock Text="DynamicResource"
               Foreground="{DynamicResource TextFillColorPrimaryBrush}" />
    <TextBlock Text="StaticResource" Margin="0,8,0,0"
               Foreground="{StaticResource TextFillColorPrimaryBrush}" />
    <StackPanel Orientation="Horizontal" Margin="0,14,0,0">
      <Button Content="Save" />
      <CheckBox Content="Overwrite" Margin="14,0,0,0" VerticalAlignment="Center" />
    </StackPanel>
  </StackPanel>
</Border>
```

`TextFillColorPrimaryBrush` と `ApplicationBackgroundBrush` は、Fluent テーマのリソースディクショナリが定義するキーである。
2 枚目のウィンドウ（`SettingsWindow`）だけは、表示前に `ThemeMode = ThemeMode.Light` を指定している。

この状態で、次のコードを実行してアプリ全体を Dark へ切り替える。

```csharp
#pragma warning disable WPF0001
Application.Current.ThemeMode = ThemeMode.Dark;
#pragma warning restore WPF0001
```

切り替え後、1 枚目のウィンドウは暗くなるが、`StaticResource` で参照したテキストだけが暗い背景に沈んで読めなくなる。
2 枚目のウィンドウは、背景もボタンも明るいまま残る。

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-thememode-runtime-switch/switched-main-window.png" alt="Dark へ切り替えた後の MainWindow。背景とボタンは暗く、DynamicResource のテキストは白に変わったが、StaticResource のテキストは Light 用の暗い色のまま残り、暗い背景の上でほとんど読めない" width="286" height="164" loading="lazy">
  <figcaption>Application.ThemeMode を Light から Dark へ切り替えた直後の MainWindow（Window.ThemeMode は未指定）。StaticResource で参照したテキストだけが切り替わっていない。.NET 10 / Windows 11 で撮影。タイトルバーは撮影ツールが白に固定している。</figcaption>
</figure>

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-thememode-runtime-switch/switched-pinned-window.png" alt="同じ切り替えの後の SettingsWindow。背景・テキスト・ボタン・チェックボックスがすべて明るい配色のまま残っている" width="286" height="164" loading="lazy">
  <figcaption>同じ切り替えの後の SettingsWindow（Window.ThemeMode="Light" を指定）。ウィンドウ全体が Light のまま残る。.NET 10 / Windows 11 で撮影。</figcaption>
</figure>

---

## 症状から絞り込めること

切り分けの起点は、`Application.ThemeMode` を変えたときに何が差し替わるかである。
アプリ全体の `ThemeMode` を順に設定し、`Application.Resources.MergedDictionaries` の中身を読み出した結果を次に示す。

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-thememode-runtime-switch/thememode-app-dictionaries.svg" alt="Application.ThemeMode ごとの Application.Resources.MergedDictionaries。None では空、Light では Fluent.Light.xaml、Dark では Fluent.Dark.xaml、再び None にすると空に戻る" width="394" height="200" loading="lazy">
  <figcaption>Application.ThemeMode を None → Light → Dark → None の順に設定したときの Application.Resources.MergedDictionaries。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

`ThemeMode` が行っているのは、`Application.Resources` 直下に置いた `Fluent.Light.xaml` と `Fluent.Dark.xaml` の入れ替えである。
コントロールの外観やブラシは、この辞書からリソース検索で解決される。
したがって、切り替え後も Light のまま残るのは、**入れ替えた辞書より先に別の Fluent 辞書が見つかる**か、**検索の結果を値として保持し続けている**かのどちらかである。
症状の出方から、どちらに当たるかをおおよそ判別できる。

| 症状 | 疑う原因 |
|---|---|
| 特定のウィンドウだけが、背景も標準コントロールもまるごと Light のまま残る | ウィンドウ自身が Fluent 辞書を持っている |
| `Window.ThemeMode` を指定していないウィンドウでも、`DynamicResource` で参照した Fluent のブラシまで Light の値のまま残る | Fluent 辞書が別の辞書の中にネストしている |
| 標準コントロールは切り替わるが、自前で色を指定した文字や背景だけが残る | ブラシを `StaticResource` やコードでの代入で固定している |

---

## 切り分けの手順

判別を確定させるには、切り替えた直後に 3 か所を読み出す。
次のメソッドは、各ウィンドウの `ThemeMode` と `Window.Resources` の辞書、`Application.Resources` の辞書をネストまでたどって文字列にし、指定した要素の `Foreground` がどう設定されているかを最後に加える。

```csharp
#pragma warning disable WPF0001
static string DumpThemeState(DependencyObject? probe = null)
{
    var text = new StringBuilder();
    Application app = Application.Current;
    text.AppendLine($"Application.ThemeMode = {app.ThemeMode.Value}");
    AppendDictionaries(text, app.Resources, "App");

    foreach (Window window in app.Windows)
    {
        text.AppendLine($"[{window.Title}] Window.ThemeMode = {window.ThemeMode.Value}");
        AppendDictionaries(text, window.Resources, $"[{window.Title}]");
    }

    if (probe is not null)
    {
        // DynamicResource なら ResourceReferenceExpression、値を固定していれば SolidColorBrush。
        object local = probe.ReadLocalValue(TextBlock.ForegroundProperty);
        string kind = local == DependencyProperty.UnsetValue ? "(no local value)" : local.GetType().Name;
        text.AppendLine($"Foreground local value = {kind}");
    }

    return text.ToString();
}

static void AppendDictionaries(StringBuilder text, ResourceDictionary dictionary, string prefix)
{
    foreach (ResourceDictionary merged in dictionary.MergedDictionaries)
    {
        text.AppendLine($"{prefix} > {merged.Source?.OriginalString ?? "(no Source)"}");
        AppendDictionaries(text, merged, prefix + " >");
    }
}
#pragma warning restore WPF0001
```

`StringBuilder` は `System.Text`、`TextBlock` は `System.Windows.Controls`、`Debug` は `System.Diagnostics`、それ以外の型は `System.Windows` にある。
`TextBlock.ForegroundProperty` は `Control.ForegroundProperty` と同じ依存関係プロパティなので、`Button` などを渡しても例外にはならない。
ただし、Fluent のスタイルから色が決まる標準コントロールでは `(no local value)` となり（後述の参照方法の表の最終行）、参照方法の判別には使えない。
戻り値は `Debug.WriteLine(DumpThemeState(element))` のように出力して確認する。

`Styles.xaml` の中に `Fluent.Light.xaml` をネストし、`SettingsWindow` に `Window.ThemeMode="Light"` を指定した状態で Dark へ切り替え、`MainWindow` の `StaticResource` のテキストを渡した出力を次に示す。
3 系統の原因がすべて出力に現れている。

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-fluent-thememode-runtime-switch/thememode-dump-output.svg" alt="DumpThemeState の出力。App の直下に Fluent.Dark.xaml と Styles.xaml が並び、Styles.xaml の下に Fluent.Light.xaml がある。SettingsWindow は Window.ThemeMode が Light で、その下に Fluent.Light.xaml がある。最後の行は Foreground local value = SolidColorBrush" width="876" height="320" loading="lazy">
  <figcaption>3 系統の原因を含む状態で Dark へ切り替えた直後の DumpThemeState の出力。.NET 10 / Windows 11 で実行。</figcaption>
</figure>

出力の読み方は次のとおりである。

- **`[ウィンドウ名] Window.ThemeMode` が `None` 以外と出るウィンドウ**（この例では `Light`）は、ウィンドウ自身が Fluent 辞書を持っている。直後の `[ウィンドウ名] >` の行に、その辞書が出る。
- **`App > >` のように `>` が 2 つ以上続く行に `Fluent.` を含む URI が出る**場合は、Fluent 辞書がネストしている。
- **`Foreground local value = SolidColorBrush`** であれば、その要素はブラシの値そのものを保持しており、切り替えに追従しない。`ResourceReferenceExpression` であれば `DynamicResource` で参照できている。ただしこの型名は WPF の内部実装の型であり、公開された契約ではない。本記事の値は検証環境（.NET 10）で読み出したものである。`(no local value)` は、値が Style・テンプレート・継承・既定値など、ローカル値以外で決まっていることを示す。

---

## 原因別の対処

各ウィンドウ構成で `Application.ThemeMode` を `Light` から `Dark` へ切り替え、`DynamicResource` で参照したブラシが追従したかを測った結果を次に示す。
ブラシの値は Light で `#E4000000`、Dark で `#FFFFFFFF` となる。

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-thememode-runtime-switch/thememode-follow-matrix.svg" alt="ウィンドウ構成ごとの追従結果。Window.ThemeMode 未指定・None 指定・App resources への直接マージは追従し、Window.ThemeMode=Light・Window.Resources へのマージ・Styles.xaml へのネストは追従しない" width="831" height="260" loading="lazy">
  <figcaption>Application.ThemeMode を Light から Dark へ切り替えたときの、ウィンドウ構成ごとの TextFillColorPrimaryBrush（DynamicResource 参照）の値。Window.ThemeMode 列は切り替え後に読んだ値。Application.Resources に辞書を置く 2 構成（図中の App resources と Styles.xaml）は、ThemeMode を設定する前に辞書をマージした。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

### ウィンドウ自身が Fluent 辞書を持っている

`Window.ThemeMode` を設定すると、そのウィンドウの `Resources` に Fluent 辞書が読み込まれる（[Window.ThemeMode の解説](https://learn.microsoft.com/dotnet/api/system.windows.window.thememode)）。
リソース検索は要素に近いところから進むため、ウィンドウの辞書が見つかった時点でアプリ側の辞書までは届かない。
アプリ全体を `Dark` にしても、`Window.ThemeMode="Light"` のウィンドウが明るいまま残るのはこのためである。
`Window.Resources` に `Fluent.Light.xaml` を手でマージした場合も同じ結果になり、`Window.ThemeMode` は `Light` と読めた。
コード上で `ThemeMode` を設定していないウィンドウでも、この値は確認する価値がある。

対処は、ウィンドウ単位の指定をやめてアプリ側の設定に任せることである。
ウィンドウ単位の指定は、実行時に `Window.ThemeMode = ThemeMode.None` とすれば外れる。
アプリ側が `None` 以外のときにウィンドウへ `None` を指定すると、アプリ側の `ThemeMode` が適用される（[ThemeMode.None の解説](https://learn.microsoft.com/dotnet/api/system.windows.thememode.none)）。
実測でも、`Window.ThemeMode=None` のウィンドウは切り替えに追従した。

### Fluent 辞書が別の辞書の中にネストしている

`Styles.xaml` などの独自の辞書の中に `Fluent.Light.xaml` を入れ、その辞書を `App.xaml` からマージしている構成では、Dark に切り替えてもブラシが Light の値のまま残った。
切り替えの前後で `Application.Resources` のマージ辞書の並びを読み出すと、原因が分かる。

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-fluent-thememode-runtime-switch/thememode-manual-dictionaries.svg" alt="手でマージした辞書の並び。直接マージした Fluent.Light.xaml は Dark で Fluent.Dark.xaml に置き換わる。Styles.xaml にネストした場合は、Light で Fluent.Light.xaml、Styles.xaml の順に並び、Dark にすると先頭だけが Fluent.Dark.xaml に変わり、Styles.xaml の中の Fluent.Light.xaml は残る" width="869" height="200" loading="lazy">
  <figcaption>Fluent.Light.xaml を Application.Resources へ直接マージした構成と、Styles.xaml の中にネストした構成での Application.Resources.MergedDictionaries。どちらも ThemeMode を設定する前に辞書をマージした。角かっこはネストした辞書の中身。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

`ThemeMode` が入れ替えるのは `Application.Resources` の直下に並ぶ Fluent 辞書であり、別の辞書の中にネストした Fluent 辞書は対象にならない。
この構成では、`ThemeMode` の辞書は既存の `Styles.xaml` より前（先頭）に入り、`Styles.xaml` の中の `Fluent.Light.xaml` はその後ろに残った。
`MergedDictionaries` は後に追加された辞書から先に検索されるため（[ResourceDictionary.MergedDictionaries の解説](https://learn.microsoft.com/dotnet/api/system.windows.resourcedictionary.mergeddictionaries)）、後ろにある `Styles.xaml` の Light のブラシが先に見つかり続ける。
対処は、ネストした辞書から Fluent の参照を取り除き、Fluent 辞書の管理を `ThemeMode` に一本化することである。

同じ `Fluent.Light.xaml` でも、`Application.Resources` 直下へ手でマージした場合は、`ThemeMode` がその辞書を `Fluent.Dark.xaml` に置き換え、ブラシも追従した。
ただし [Application.ThemeMode の解説](https://learn.microsoft.com/dotnet/api/system.windows.application.thememode)は、Fluent 辞書を手でマージしないよう勧めており、手でマージした辞書は `ThemeMode` の辞書より優先されるとも述べている。
今回の環境で置き換えられた結果を根拠に、手でマージする構成を選ぶ理由は無い。
`ThemeMode` を使う場合は、Fluent 辞書を手でマージしない構成が妥当である。

### ブラシを StaticResource やコードでの代入で固定している

標準コントロールは切り替わるのに、自前で色を付けた文字や背景だけが残る場合は、ブラシの参照方法が原因として疑わしい。
同じブラシを 3 通りの方法で参照し、切り替えの前後で値を読み出した結果を次に示す。
比較のため、`Foreground` を指定していない `Button` を最終行に加えている。

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-thememode-runtime-switch/thememode-reference-kinds.svg" alt="ブラシの参照方法ごとの追従結果。StaticResource と FindResource の代入は ReadLocalValue が SolidColorBrush で追従せず、DynamicResource は ResourceReferenceExpression で追従する。Foreground を指定していない Button はローカル値が無く、Fluent のスタイル経由で追従する" width="808" height="200" loading="lazy">
  <figcaption>TextFillColorPrimaryBrush を参照方法ごとに Foreground へ設定し、Application.ThemeMode を Light から Dark へ切り替えた結果。ReadLocalValue 列は Foreground のローカル値の型。最終行は Foreground を指定していない Button。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

`StaticResource` は、XAML の読み込み時に見つかったブラシを 1 回だけ代入する。
コードで `FindResource` の戻り値を代入した場合も同じで、その時点の Light のブラシが値として残る。
辞書が Dark 版に入れ替わっても、要素はすでに受け取ったブラシを持ち続けるため追従しない。
`DynamicResource` はキーへの参照を保持し、辞書の入れ替えを受けて値を引き直す。

対処は、テーマで変わる色をすべて `DynamicResource` で参照することである。
コードから設定する場合は、`element.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush")` のように、値ではなくキーを渡す。
`StaticResource` と `DynamicResource` の違いそのものは、[WPF で StaticResource を変更しても画面が更新されない原因と解決方法](/ja/articles/wpf-staticresource-vs-dynamicresource/)で扱っている。

---

## 実装例

ウィンドウ側の固定をまとめて外すには、切り替えの処理を 1 か所に集め、アプリ側の `ThemeMode` を変えるたびに各ウィンドウの指定も外す。
次の `ApplyTheme` は、アプリ全体の `ThemeMode` を設定したうえで、`Application.Windows` のすべてのウィンドウについて `Window.ThemeMode` を `None` に戻し、`Window.Resources` に手でマージされた Fluent 辞書を取り除く。

```csharp
#pragma warning disable WPF0001
public static class ThemeSwitcher
{
    public static void ApplyTheme(ThemeMode mode)
    {
        Application app = Application.Current;
        app.ThemeMode = mode;

        foreach (Window window in app.Windows)
        {
            // None にすると、アプリ側の ThemeMode が適用される。
            window.ThemeMode = ThemeMode.None;

            // 手でマージした Fluent 辞書は、アプリ側の辞書より近くにあるため外す。
            foreach (ResourceDictionary dictionary in window.Resources.MergedDictionaries
                         .Where(IsFluentDictionary)
                         .ToList())
            {
                window.Resources.MergedDictionaries.Remove(dictionary);
            }
        }
    }

    private static bool IsFluentDictionary(ResourceDictionary dictionary) =>
        dictionary.Source?.OriginalString.Contains(
            "PresentationFramework.Fluent;component/Themes/",
            StringComparison.OrdinalIgnoreCase) == true;
}
#pragma warning restore WPF0001
```

`Where` と `ToList` には `System.Linq` が必要である（`ImplicitUsings` が有効なら記述不要）。
呼び出し側の `ThemeSwitcher.ApplyTheme(ThemeMode.Dark)` も `ThemeMode` 構造体を参照する。
前述のビルド確認では、ViewModel に相当する通常のクラスから呼ぶ場合、呼び出し側でも `WPF0001` の抑制が要った。
`Application.Windows` が列挙するのは、UI スレッドで生成され、まだ閉じていないウィンドウである（[Application.Windows の解説](https://learn.microsoft.com/dotnet/api/system.windows.application.windows)）。
このプロパティは `Application` を生成したスレッドからしか使えないため、`ApplyTheme` も UI スレッドから呼ぶ。
バックグラウンドの処理から切り替える場合は、`Application.Current.Dispatcher.Invoke` を通して UI スレッドで実行する。
`ApplyTheme` が処理するのは、呼び出した時点で開いているウィンドウだけである。
その後に生成したウィンドウは、次に `ApplyTheme` を呼ぶまで処理されない。
別の UI スレッドで生成したウィンドウは、`Application.Windows` に含まれないため処理されない。
そうしたウィンドウでは `ThemeMode` を指定しないことが前提となる。

前節と同じウィンドウ構成で、`Application.ThemeMode` を直接書き換える代わりにこのメソッドを通した結果を次に示す。

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-thememode-runtime-switch/thememode-helper-matrix.svg" alt="ApplyTheme を通した場合の追従結果。Window.ThemeMode=Light と Window.Resources へのマージも追従するようになったが、Styles.xaml へのネストだけは追従しない" width="831" height="260" loading="lazy">
  <figcaption>Light のアプリで ApplyTheme(ThemeMode.Dark) を呼んだときの、ウィンドウ構成ごとの TextFillColorPrimaryBrush（DynamicResource 参照）の値。Window.ThemeMode 列は切り替え後に読んだ値。辞書を置く順序は前節の図と同じ。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

ウィンドウ単位で固定していた 2 つの構成は追従するようになった。
一方、ネストした辞書の構成はこのメソッドでも直らない。
ネストはアプリ全体の辞書構成の問題であり、切り替えの処理で吸収するのではなく、前節のとおり辞書の構成を直す必要がある。
`StaticResource` で固定したブラシも、このメソッドでは直らない。

---

## 注意点

- **ウィンドウ単位で意図的に配色を固定しているウィンドウも解除される。** `ApplyTheme` は例外なく `Window.ThemeMode` を `None` に戻す。常に Light で表示したいウィンドウがある場合は、そのウィンドウを除外する条件を加える必要がある。
- **アプリ側で `ThemeMode` を設定すると、ウィンドウに `None` を指定しても Fluent は外れない。** アプリ側が `None` 以外のときは、`None` のウィンドウにもアプリ側の Fluent テーマが適用される（[ThemeMode.None の解説](https://learn.microsoft.com/dotnet/api/system.windows.thememode.none)）。
- **`ThemeMode` は実験的 API である。** [Application.ThemeMode の解説](https://learn.microsoft.com/dotnet/api/system.windows.application.thememode)は、将来のバージョンで削除される可能性があると述べている。切り替え処理を `ThemeSwitcher` のように 1 か所へ集めておくと、API が変わったときの修正範囲を限定できる。
- **`ThemeMode.System` は Windows の設定に応じて Light か Dark を選ぶ**（[ThemeMode.System の解説](https://learn.microsoft.com/dotnet/api/system.windows.thememode.system)）。実測したのはアプリのモードがダークの環境だけであり、ライト側と、実行中に OS の設定を切り替えたときの追従は、OS の設定を変更する必要があるため計測していない。

検証環境で `ThemeMode.System` を設定した結果を次に示す。
`Application.Resources` には明暗を固定しない `Fluent.xaml` がマージされ、ブラシは Dark と同じ値になった。

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-thememode-runtime-switch/thememode-system.svg" alt="ThemeMode.System の実測。AppsUseLightTheme が 0 の環境で、マージされた辞書は Fluent.xaml、TextFillColorPrimaryBrush は #FFFFFFFF" width="571" height="110" loading="lazy">
  <figcaption>Application.ThemeMode に System を設定したときの、マージされた辞書と TextFillColorPrimaryBrush の値。AppsUseLightTheme はレジストリから読み出した値（0 はダーク）。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

---

## まとめ

`Application.ThemeMode` の切り替えは、`Application.Resources` 直下の Fluent 辞書を Light 版から Dark 版へ入れ替える操作である。
追従しない部分の原因は、`DumpThemeState` のように `Window.ThemeMode`・マージ辞書の構成・`Foreground` のローカル値の型を読み出せば特定できる。

ウィンドウが Fluent 辞書を持っている場合は、`Window.ThemeMode` を `None` に戻して辞書を外す処理を、切り替えと同じ場所に置く。
Fluent 辞書がネストしている場合は、辞書の構成を直して `ThemeMode` に管理を一本化する。
ブラシを固定している場合は、`DynamicResource` または `SetResourceReference` に置き換える。
実行時の切り替えを前提とするアプリでは、`ThemeMode` をアプリ側だけに設定し、Fluent 辞書を手でマージせず、テーマで変わる色を `DynamicResource` で参照する構成を既定とするのが妥当である。

---

## 関連記事

- [WPF Fluent テーマでカスタム Style を持つコントロールだけ旧外観に戻る問題](/ja/articles/wpf-fluent-theme-custom-style-not-applied/)
- [WPF で Fluent デザインを追加ライブラリなしで適用する方法](/ja/articles/wpf-fluent-design-with-systemcolors/)
- [WPF Fluent テーマの TextBox でクリアボタンを非表示にする方法](/ja/articles/wpf-fluent-textbox-hide-clear-button/)
- [WPF で StaticResource を変更しても画面が更新されない原因と解決方法](/ja/articles/wpf-staticresource-vs-dynamicresource/)

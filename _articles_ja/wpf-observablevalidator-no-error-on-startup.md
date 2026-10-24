---
layout: article-ja
title: "WPF で CommunityToolkit.Mvvm の ObservableValidator が起動直後に検証エラーを出さない理由"
seo_title: "WPF の ObservableValidator が起動直後に検証エラーを出さない理由"
date: 2026-10-24
category: WPF
excerpt: "ObservableValidator の必須チェックは起動直後の空欄にエラーを出さず、HasErrors で止めた保存ボタンも押せてしまう。HasErrors の False が未検証を含むことを実測で確かめ、ValidateAllProperties を呼ぶ場所を比べる。"
image: /images/articles/wpf-observablevalidator-no-error-on-startup/observablevalidator-startup.png
---

## 概要

CommunityToolkit.Mvvm（以下 Toolkit）の `ObservableValidator` で名前を必須にすると、画面を開いた直後の空欄には赤枠もメッセージも出ない。
保存コマンドの `CanExecute` を「`HasErrors` が `False`」にしておいても、起動直後の保存ボタンは有効で、押すと空の名前のまま保存まで進む。

本記事では、`ObservableValidator` の検証が値を設定したときにしか走らないことを、書き方ごとの `Validation.HasError` の実測で示す。
そのうえで、`ValidateAllProperties()` をコンストラクターで呼ぶ書き方と保存処理の先頭で呼ぶ書き方を、保存ボタンの実測とともに比べ、選び方を示す。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF
- ライブラリ: CommunityToolkit.Mvvm 8.4.0（比較に Prism.Core 8.1.97。パッケージ名は Prism.Core、アセンブリ名は Prism。8.1.97 は MIT ライセンスで提供された最後の版で、9 以降はライセンスが変わった）
- 検証環境: .NET 10 / Windows 11
- 追検証: .NET Framework 4.8 向けにビルドし、4.8.1（4.8.9363）/ Windows 11 で実行（動作対象の範囲の外だが、.NET Framework 4.8 のまま保守されているアプリケーションも読者に含まれるため確かめた）
- アーキテクチャ: MVVM（ビューはバインドだけで動かし、コードビハインドを持たない）

追検証では、同じ規則を別のビューで再現した MVVM のサンプルを、.NET 10 と .NET Framework 4.8.1 の両方で動かして比べた。
比べたのは、setter で `SetProperty(ref _name, value, true)` を呼ぶ書き方の起動直後と 21 文字の入力後、およびそれにコンストラクターの `ValidateAllProperties()` を足した書き方の起動直後と 21 文字の入力後で、どれも両者で同じだった。
ソース生成の書き方、Prism の書き方、保存ボタンの表は、.NET Framework では測っていない。
なお、本記事のコードはラムダの破棄パラメーター `(_, _)` など C# 9 以降の構文を使う。
.NET Framework の既定の言語バージョンは C# 7.3 なので、追検証のサンプルはプロジェクトの `LangVersion` を `latest` にし、.NET Framework に無い言語機能の型を補う PolySharp を加えてビルドした。
対象のフレームワークの既定より新しい言語バージョンを使うことは、[Microsoft のサポート外](https://learn.microsoft.com/ja-jp/dotnet/csharp/language-reference/language-versioning)である。

---

## 問題

名前を必須かつ 20 文字までとする入力欄を、Toolkit のソース生成で書く。

```csharp
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed partial class AccountViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(20, ErrorMessage = "Name must be 20 characters or fewer.")]
    private string _name = "";

    public AccountViewModel()
    {
        // エラーが増減したら、CanExecute を問い合わせ直させる。
        ErrorsChanged += (_, _) => SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        // 保存する
    }

    private bool CanSave() => !HasErrors;
}
```

`[NotifyDataErrorInfo]` を付けると、生成された `Name` の setter が、値が変わるたびに検証を呼ぶ。
`ErrorsChanged` で `NotifyCanExecuteChanged()` を呼んでいるのは、Toolkit のコマンドが `CommandManager` の再問い合わせでは更新されないためである（[WPF で CommunityToolkit.Mvvm と Prism のコマンドに InvalidateRequerySuggested が効かない理由](/ja/articles/wpf-mvvm-library-command-requery/)）。

ビューは次のとおりで、エラーのメッセージを `TextBox` の下に描く `ErrorTemplate` を `Style` から当てている。
既定の `ErrorTemplate` は赤枠だけでメッセージを出さないため差し替えた（[WPF で入力検証のエラーが表示されない原因と IDataErrorInfo / INotifyDataErrorInfo の使い分け](/ja/articles/wpf-validation-error-not-displayed/)）。

```xml
<StackPanel Margin="8">
  <StackPanel.Resources>
    <ControlTemplate x:Key="FieldErrorTemplate">
      <StackPanel>
        <Border BorderBrush="#D13438" BorderThickness="1">
          <AdornedElementPlaceholder x:Name="Adorned" />
        </Border>
        <TextBlock Margin="2,2,0,0" FontSize="11" Foreground="#D13438"
                   Text="{Binding ElementName=Adorned,
                          Path=AdornedElement.(Validation.Errors)/ErrorContent}" />
      </StackPanel>
    </ControlTemplate>
    <Style TargetType="TextBox">
      <Setter Property="Validation.ErrorTemplate" Value="{StaticResource FieldErrorTemplate}" />
      <Setter Property="Margin" Value="0,0,0,22" />
    </Style>
  </StackPanel.Resources>

  <TextBlock Text="Name (required, up to 20 characters)" />
  <StackPanel Orientation="Horizontal">
    <TextBox Width="240" Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />
    <Button Content="Save" Command="{Binding SaveCommand}" Padding="14,2" Margin="12,0,0,22" />
  </StackPanel>
</StackPanel>
```

この画面を開くと、名前は空なのにエラーは出ず、保存ボタンは有効である。
保存ボタンを押すと、`Save` が空の名前のまま最後まで進む。

<figure class="article-figure">
  <img src="/images/articles/wpf-observablevalidator-no-error-on-startup/observablevalidator-startup.png" alt="名前の入力欄と保存ボタンを 1 組とした 3 組の画面。[NotifyDataErrorInfo] だけの上の組は、起動直後の空欄に赤枠もメッセージも無く、保存ボタンが有効。コンストラクターで ValidateAllProperties を呼ぶ中の組は、起動直後から赤枠と Name is required. が出て、保存ボタンが無効。保存処理の先頭で ValidateAllProperties を呼ぶ下の組は、保存ボタンを押した後に赤枠と Name is required. が出ている。" width="408" height="392" loading="lazy">
  <figcaption>.NET 10 / Windows 11、CommunityToolkit.Mvvm 8.4.0 で、同じ XAML から作ったビュー 3 つに、それぞれ別のビューモデルを渡した画面。上の 2 組は起動直後、下の組は保存ボタンを押した後に撮った。上は本節のコード、中はそのコンストラクターで <code>ValidateAllProperties()</code> を呼ぶ書き方、下は「保存処理の先頭で呼ぶ」の節のコードである。</figcaption>
</figure>

---

## 原因・背景

`ObservableValidator` は、プロパティごとのエラーを自分の中に持ち、`INotifyDataErrorInfo` の `GetErrors` と `HasErrors` でそれを返す。
エラーが入るのは検証を呼んだときだけである。
[公式の解説](https://learn.microsoft.com/ja-jp/dotnet/communitytoolkit/mvvm/observablevalidator)は、`SetProperty` の `bool` の引数に `true` を渡すと値の更新時に検証すると説明している。
`[NotifyDataErrorInfo]` を付けた setter も、値が変わったときに `ValidateProperty` を呼ぶ（[ObservableProperty 属性の解説](https://learn.microsoft.com/ja-jp/dotnet/communitytoolkit/mvvm/generators/observableproperty#requesting-property-validation)）。
フィールドの初期値 `""` は setter を通らないので、画面を開いた時点では一度も検証されていない。
`GetErrors` は空で、`HasErrors` は `False` を返す。
[`INotifyDataErrorInfo.HasErrors` のリファレンス](https://learn.microsoft.com/ja-jp/dotnet/api/system.componentmodel.inotifydataerrorinfo.haserrors)は、`HasErrors` がアクセスした時点で既知のエラーが無ければ `False` を返すとしている。
したがって、まだ検証していないときも `False` になる。

書き方ごとに、起動直後・21 文字を入力した後・入力を消して空にした後の `Validation.HasError` を測った。

{% include tables/articles/wpf-observablevalidator-no-error-on-startup/observablevalidator-display-matrix.ja.md %}

.NET 10 / Windows 11、CommunityToolkit.Mvvm 8.4.0 / Prism.Core 8.1.97 で、保存ボタンを除いた同じビュー（名前の入力欄だけ）を使った。名前の入力欄に WPF の入力処理（`InputManager`）を通して 21 文字を入力し、続けて `SelectAll()` で全選択して、入力欄に Backspace のキー操作のイベントを送って消した。各段階の後に入力欄の `Validation.HasError` を読んだ。`True` の段階では、起動直後と空にした後は必須のメッセージ、21 文字の後は長さのメッセージが `ErrorTemplate` に描かれていることも確かめた。
{: .table-caption}

**setter で検証し、コンストラクターでは検証しない 1・3・5 行目は、起動直後は `False` で、21 文字を入力した後に `True`（長さのエラー）になり、空にした後も `True`（必須のエラー）のままである。**
必須のエラーは、値を一度変えてから空に戻したときにしか出ない。
Prism.Core 8.1.97 には検証属性を読む基底クラスが無いため、5・6 行目は setter で規則を確かめて `ErrorsContainer.SetErrors` に渡す処理を書いた。
setter でだけ検証を呼ぶ実装なら、この Prism の書き方でも同じになる。

2 行目は、`[NotifyDataErrorInfo]` を付け忘れた書き方である。
検証属性は生成されたプロパティへ写されるが、setter は検証を呼ばないので、21 文字を入れても空にしてもエラーは出ない。
[`[NotifyDataErrorInfo]` に検証属性が無い場合](https://learn.microsoft.com/ja-jp/dotnet/communitytoolkit/mvvm/generators/errors/mvvmtk0026)はエラー MVVMTK0026 になるが、その逆の、検証属性があって `[NotifyDataErrorInfo]` が無い場合は、この計測のツールを `dotnet build --no-incremental` でビルドした出力に警告が 1 件も無かった。

コンストラクターで検証した 4 行目と 6 行目は、起動直後から `True` になった。
ビューモデルを `DataContext` に渡す前にできていたエラーも、画面を表示した時点で出ていた。

---

## ValidateAllProperties を呼ぶ 2 つの場所

画面を開いた時点の値も検証するには、`ValidateAllProperties()` を呼ぶ。
[公式の解説](https://learn.microsoft.com/ja-jp/dotnet/communitytoolkit/mvvm/observablevalidator)によれば、このメソッドは検証属性が 1 つ以上付いた public のインスタンスプロパティをすべて検証する。
呼ぶ場所は、コンストラクターと保存処理の先頭の 2 つである。

### コンストラクターで呼ぶ

「問題」の節のコードのコンストラクターで、`ValidateAllProperties()` を呼ぶ。

```csharp
public AccountViewModel()
{
    ValidateAllProperties();

    // エラーが増減したら、CanExecute を問い合わせ直させる。
    ErrorsChanged += (_, _) => SaveCommand.NotifyCanExecuteChanged();
}
```

画面を開いた時点で必須のエラーが出て、`CanExecute` を `!HasErrors` にした保存ボタンは無効になる。
`ValidateAllProperties()` を購読より前に呼んでも、保存ボタンは起動直後から無効だった。
ビューモデルはコンストラクターが終わってから `DataContext` に渡されるので、ボタンの `Command` がバインドされる時点で、エラーはすでにできている。
[.NET 10.0.10 の ButtonBase のソース](https://github.com/dotnet/wpf/blob/v10.0.10/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/ButtonBase.cs)によれば、`Command` が設定されると `OnCommandChanged` から `UpdateCanExecute` が呼ばれ、ボタンは `CanExecute` を読む。
入力を始めた後は、`[NotifyDataErrorInfo]` により値が変わるたびに検証され、正しい名前を入力すると保存ボタンは有効に戻った（「軸ごとの結論」の表の最後の列）。

### 保存処理の先頭で呼ぶ

`CanExecute` を付けずに、`Save` の中で `ValidateAllProperties()` を呼んでから `HasErrors` を見る。

```csharp
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed partial class AccountViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(20, ErrorMessage = "Name must be 20 characters or fewer.")]
    private string _name = "";

    [RelayCommand]
    private void Save()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }

        // 保存する
    }
}
```

ビューの XAML は「問題」の節のものをそのまま使う。
保存ボタンを押すまで空欄にエラーは出ず、押した時点で `Name is required.` が出て、`Save` は保存の前に戻る。
入力を始めた後は、`[NotifyDataErrorInfo]` により値が変わるたびに検証される。
`CanExecute` を使わないので、`ErrorsChanged` を購読してコマンドに知らせる必要も無い。

---

## 判断の軸

- **空欄のエラーを見せる時点。**
  コンストラクターで呼ぶと、何も入力していない欄が最初から赤い。
  保存処理の先頭で呼ぶと、保存ボタンを押すまで赤くならない。
- **保存の止め方（ボタンの無効化か、押した後の中止か）。**
  `CanExecute` で押せなくするなら、起動直後の `HasErrors` を正しくするためにコンストラクターでの検証が要る。

---

## 軸ごとの結論

保存ボタンの止め方ごとに、名前が空のまま保存ボタンを押した結果と、続けて正しい名前を入力した後の保存ボタンの状態を測った。

{% include tables/articles/wpf-observablevalidator-no-error-on-startup/observablevalidator-save-matrix.ja.md %}

.NET 10 / Windows 11、CommunityToolkit.Mvvm 8.4.0 で、名前を空のまま、起動直後の保存ボタンの `IsEnabled` と入力欄の `Validation.HasError` を読んだ。ボタンが有効なら UI オートメーションの `Invoke` で押し（`Click` からコマンドが実行される）、保存まで進んだ回数（`Save` が呼ばれても、先頭の検証で戻った場合は数えない）と `Validation.HasError` を読んだ。最後に、名前の入力欄へ正しい名前（taro）を `InputManager` を通して入力し、保存ボタンの `IsEnabled` を読んだ。1・2 行目は「問題」の節のコード（2 行目はそのコンストラクターで `ValidateAllProperties()` を呼ぶ）、3 行目は「保存処理の先頭で呼ぶ」の節のコードである。
{: .table-caption}

**`CanExecute` を `!HasErrors` にしただけでは、空の名前のまま 1 回保存まで進んだ。**
コンストラクターで検証すればボタンは押せなくなり、保存処理の先頭で検証すれば押しても保存まで進まない。
正しい名前を入力した後は、どの書き方でも保存ボタンは有効だった（2 行目は無効から有効に戻った）。

2 つの軸に沿ってまとめると次のとおりである。

| 検証する時点 | 空欄のエラーを見せる時点 | 保存の止め方 | 向く画面 |
|---|---|---|---|
| setter だけ（`[NotifyDataErrorInfo]`） | 値を一度変えて空に戻した後 | 起動直後の未入力の値では止まらない | 単独では使わない |
| コンストラクターで `ValidateAllProperties()` | 画面を開いた時点 | ボタンを無効にする | 入力の不足を最初から見せたい画面 |
| 保存処理の先頭で `ValidateAllProperties()` | 保存ボタンを押した時点 | 押した後に中止する | 最初から赤い画面を避けたい画面 |

---

## 注意点

- **`[NotifyDataErrorInfo]` を付け忘れても、ビルドは通る。**
  検証属性を付けただけでは setter は検証しない（表示の表の 2 行目）。
  付け忘れると、`ValidateAllProperties()` や `ValidateProperty()` などの検証のメソッドを呼んだときにしかエラーができない。
- **`ValidateAllProperties()` が見るのは、検証属性の付いた public のインスタンスプロパティだけである。**
  [公式の解説](https://learn.microsoft.com/ja-jp/dotnet/communitytoolkit/mvvm/observablevalidator)の説明による。
  属性を使わず setter で独自に検証しているプロパティは、このメソッドの対象にならない。
  本記事では測っておらず、計測は検証属性の付いたプロパティが 1 つの場合だけである。
- **入力欄をリセットする用途には `ClearAllErrors()` が用意されている。**
  [公式の解説](https://learn.microsoft.com/ja-jp/dotnet/communitytoolkit/mvvm/observablevalidator)は、フォームをもう一度入力させるときのリセットに使えるとしている。
  入力済みの値をコードで `""` に戻すと、`[NotifyDataErrorInfo]` の setter を通って検証が走るので、入力欄を消したとき（表示の表の「入力を消して空にした後」）と同じく必須のエラーができると考えられる。
  本記事では、コードからの代入も `ClearAllErrors()` も測っていない。
- **`CanExecute` を `HasErrors` に連動させるなら、`ErrorsChanged` でコマンドに知らせる。**
  Toolkit の `RelayCommand` は、`NotifyCanExecuteChanged()` を呼ばない限り `CanExecute` の変化をボタンに知らせない（[WPF で CommunityToolkit.Mvvm と Prism のコマンドに InvalidateRequerySuggested が効かない理由](/ja/articles/wpf-mvvm-library-command-requery/)）。

---

## まとめ

`ObservableValidator` のエラーは、検証を呼んだときにしかできない。
setter で検証する書き方（`SetProperty` の `true` や `[NotifyDataErrorInfo]`）では、フィールドの初期値は一度も検証されず、起動直後の空欄にはエラーが出なかった。
`HasErrors` は「まだ検証していない」ときも `False` を返すため、`CanExecute` を `!HasErrors` にしただけでは、空の名前のまま保存まで進んだ。
Prism の `ErrorsContainer` で setter からだけ検証する書き方も、同じ結果だった。

画面を開いた時点の値も検証するには、`ValidateAllProperties()` を呼ぶ。
コンストラクターで呼べば起動直後からエラーが出て保存ボタンが無効になり、保存処理の先頭で呼べば押した時点でエラーを出して保存を止める。
どちらにするかは、空欄のエラーを画面を開いた時点で見せたいか、保存ボタンを無効にしたいかで決まる。

---

## 関連記事

- [WPF で入力検証のエラーが表示されない原因と IDataErrorInfo / INotifyDataErrorInfo の使い分け](/ja/articles/wpf-validation-error-not-displayed/)
- [WPF で CommunityToolkit.Mvvm と Prism のコマンドに InvalidateRequerySuggested が効かない理由](/ja/articles/wpf-mvvm-library-command-requery/)

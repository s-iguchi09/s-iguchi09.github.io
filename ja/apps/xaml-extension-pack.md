---
layout: app-page
permalink: /ja/apps/xaml-extension-pack.html
title: "Xaml.ExtensionPack | WPF 向け MVVM 拡張ライブラリ"
heading: "Xaml.ExtensionPack"
lead: "WPF 向けの軽量な XAML 拡張ライブラリです。利用側に第三者製の実行時依存を追加しません。 MVVM の土台となる機能を、単一の小さなライブラリとして提供します。"
description: "WPF 向けの軽量な XAML 拡張ライブラリです。利用側に第三者製の実行時依存を追加しません。BindableBase、RelayCommand、AsyncRelayCommand といった MVVM の土台を提供する小さなライブラリです。各機能の使用例も掲載しています。"
---

## 概要

Xaml.ExtensionPack は、WPF / XAML アプリケーション開発で繰り返し書くことになる定型コードを削減するための小さなライブラリです。 中核となるのは、MVVM アプリケーションでほぼ必ず自前実装することになる 4 つ —— 変更通知の基底クラス、同期コマンド、非同期コマンド、安全な fire-and-forget ヘルパー —— で、 これらのコマンドを公開するための小さなインターフェイスが付属します。

このライブラリは**利用側のプロジェクトにサードパーティ製の依存関係を持ち込みません**。 導入しても依存関係のツリーが付いてくることはなく、特定のアプリケーション構成を強制することもありません。 必要な型だけを採用し、それ以外は使わないという導入の仕方ができます。

なお、ライブラリ自体はビルド時の参照を 1 つ持っています。 `net472` 向けのビルドに必要な参照アセンブリを供給する `Microsoft.NETFramework.ReferenceAssemblies` です。 これは `PrivateAssets="all"` を指定して宣言しているため、このプロジェクトの内部にとどまり、 利用側へは伝播しません。

## インストール

本プロジェクトは NuGet パッケージを生成する構成になっていますが、 **NuGet.org へはまだ公開していません**。 NuGet.org だけをソースにしている場合、`dotnet add package` には解決先がありません。 パッケージを置いたローカルフィードやプライベートフィードが設定済みであれば、通常どおり解決できます。 利用できるフィードがない場合は、リポジトリをクローンしてプロジェクトを直接参照してください。

```shell
git clone https://github.com/s-iguchi09/Xaml.ExtensionPack.git
```

```shell
dotnet add YourApp.csproj reference path/to/Xaml.ExtensionPack/src/Xaml.ExtensionPack/Xaml.ExtensionPack.csproj
```

ローカルでパックし、ローカルフィードから参照する方法もあります。 パッケージを任意のディレクトリへ出力し、そのディレクトリを NuGet のソースとして登録したうえで、 バージョンを明示してパッケージを追加します。 プロジェクトファイルでバージョンを指定していないため、`dotnet pack` の出力は `1.0.0` になります。

```shell
dotnet pack src/Xaml.ExtensionPack/Xaml.ExtensionPack.csproj -c Release -o ./local-feed
```

```shell
dotnet nuget add source ./local-feed --name xaml-extensionpack-local
```

```shell
dotnet add YourApp.csproj package Xaml.ExtensionPack --version 1.0.0 --source ./local-feed
```

ソースとバージョンは、どちらも明示しておく価値があります。 このパッケージ ID は NuGet.org で取得されていないため、ソースを指定しないと、 将来だれかが同じ名前で公開したパッケージを引いてしまう可能性があり、 バージョンを固定していないと、そうなったときに大きい方が選ばれてしまいます。 継続的に使う場合は、`nuget.config` の [Package Source Mapping](https://learn.microsoft.com/nuget/consume-packages/package-source-mapping){: target="_blank" rel="noopener noreferrer"} で ID をローカルフィードに固定します。 このセクションを定義すると、推移的な依存を含むプロジェクト内のすべてのパッケージがいずれかのパターンに 一致している必要があります。下の例の `*` がその受け皿です。 `packageSourceMapping` の各キーは、`packageSources` で宣言したソースを指します。 `<clear />` は、継承した設定を除外したいときに指定します。 NuGet はディレクトリをさかのぼって見つけたすべての `nuget.config` に加え、 ユーザー単位・マシン単位の設定についても、コレクション要素を結合して適用します。 そのため上位に設定がある環境では、`<clear />` がないと、 継承されたソースや、同じ ID に対する継承されたマッピングが残ってしまいます。 継承する設定がなければ必須ではありませんが、 どの環境でも同じ結果になるよう、書いておくほうが安全です。

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="xaml-extensionpack-local" value="./local-feed" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="xaml-extensionpack-local">
      <package pattern="Xaml.ExtensionPack" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

これには制約が 2 つあります。 1 つは対応ツールの要件です。Package Source Mapping は NuGet 6.0 で追加されたため、 Visual Studio 2022、.NET SDK 6.0.100、nuget.exe 6.0.0 のいずれか以降が必要です。 それより古いツールはこのセクションを警告なく無視するので、 CI を含め、このプロジェクトを restore するすべての環境が対応バージョンである必要があります。 もう 1 つは適用範囲です。効くのは restore がパッケージをダウンロードするときだけで、 `dotnet add package` が他のソースへメタデータを問い合わせること自体は止まりません。 その点でも `--source` の指定が必要になります。

公開されている型はすべて `Xaml.ExtensionPack` 名前空間に配置しているため、using ディレクティブは 1 行で済みます。

```csharp
using Xaml.ExtensionPack;
```

## 開発背景：重い依存関係なしで定型コードを削る

MVVM パターンで構築した WPF アプリケーションでは、 `INotifyPropertyChanged` の実装、`ICommand` のラッパー、非同期処理を安全に投げ放つためのヘルパーといった、 同じ形の基盤コードが積み上がっていきます。 同種の機能を提供するコミュニティ製ツールキットも存在しますが、 大きな依存関係ツリーを引き込んだり、プロジェクトが必要としない設計上の規約まで要求したりすることが少なくありません。

さらに、選択肢を絞り込む実務的な制約があります。 業務系の WPF アプリケーションでは .NET Framework 4.7.2 を対象とし続けている案件が今も多いためです。 ただし、ここで効くのは `net472` を明示的に対象にしているかどうかではありません。 .NET Framework 4.6.1 以降は `netstandard2.0` 向けのアセットを利用できるため、 それを同梱しているツールキットは引き続き使えます。 なお、この組み合わせについて Microsoft が推奨しているのは 4.7.2 以降です。 それより前のバージョンには既知の問題があります。 使えなくなるのは `net472` と互換なアセットを一切持たない場合、 つまりモダン .NET だけを対象にしたパッケージです。

ここまでは `netstandard2.0` のライブラリ一般の話で、 本ライブラリが提供する互換性とは分けて考える必要があります。 Xaml.ExtensionPack は `netstandard2.0` のアセットを同梱しておらず、 .NET Framework 向けには `net472` を対象としています。 そのため 4.6.1 から 4.7.1 のプロジェクトではこのアセットが選択されません。 本ライブラリに関しては、4.7.2 は推奨ではなく下限です。

Xaml.ExtensionPack は単一パッケージで `net10.0` / `net8.0` / `net472` を マルチターゲットしているため、どの対象でも同じ API を提供します。 .NET Framework からモダン .NET への移行をまたいでも、 このライブラリに依存している部分の ViewModel のコードは書き換える必要がありません。 それ以外の API に触れているコードについては、別途検討が必要です。

## 提供する機能

| 型 | 種別 | 役割 |
| --- | --- | --- |
| `BindableBase` | 抽象クラス | `INotifyPropertyChanged` を実装します。 変更後のコールバックを受け取れる `SetProperty` と、 文字列指定・ラムダ式指定の両方の `OnPropertyChanged` を提供します。 |
| `RelayCommand` | クラス | `Action` と、任意の `Func<bool>`（`CanExecute` 用）を受け取る デリゲートベースのコマンドです。 |
| `RelayCommand<T>` | クラス | `CommandParameter` を `T` として受け取る型付き版です。 パラメーターが null の場合を参照型・値型の双方で処理します。 |
| `AsyncRelayCommand` | クラス | `Func<Task>` をラップする非同期コマンドです。 `IsExecuting` を公開し、実行中の再入を遮断します。 |
| `AsyncRelayCommand<T>` | クラス | `Func<T, Task>` をラップする、上記の型付き版です。 |
| `IRaiseCanExecuteChanged` | インターフェース | `ICommand` に `RaiseCanExecuteChanged()` を追加したインターフェースです。 ViewModel が具象コマンド型に依存せずに実行可否を再評価できます。 |
| `IAsyncCommand` | インターフェース | 非同期コマンドの契約です。`ICommand` のメンバーに加えて `ExecuteAsync` と `IsExecuting` を公開します。 |
| `TaskExtensions.FireAndForget` | 拡張メソッド | `Task` を `async void` メソッドの中で await し、例外を任意のハンドラーに渡します。 例外が観測されないまま残ることを防ぎます。 |

## 使い方

### BindableBase —— プロパティの変更通知

ViewModel を `BindableBase` から派生させ、セッターの処理を `SetProperty` に通します。 内部で `EqualityComparer<T>.Default` により現在値と比較し、 実際に値が変わったときだけ `PropertyChanged` を発行するため、 不要な通知がバインディングに届きません。

```csharp
public class MainViewModel : BindableBase
{
    private string _userName = string.Empty;

    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value);
    }
}
```

`Action` を受け取るオーバーロードは、値が変わったときにだけコールバックを実行します。 依存するコマンドや派生プロパティを更新したい場面で使えます。

```csharp
private int _quantity;

public int Quantity
{
    get => _quantity;
    set => SetProperty(ref _quantity, value, () => SubmitCommand.RaiseCanExecuteChanged());
}
```

設定対象とは別のプロパティについて通知したい場合は、ラムダ式のオーバーロードを使うとリファクタリングに強くなります。 プロパティ名を変更しても通知先が自動的に追随するため、文字列リテラルで指定した場合のような修正漏れが起きません。

```csharp
set
{
    SetProperty(ref _firstName, value);
    OnPropertyChanged(() => FullName);
}
```

### RelayCommand —— メソッドをボタンにバインドする

`RelayCommand` は実行処理のデリゲートと、任意の実行可否判定を受け取ります。 `CanExecute` が `false` を返している間、WPF がバインド先のコントロールを自動的に無効化します。

```csharp
public class MainViewModel : BindableBase
{
    private string _userName = string.Empty;

    public RelayCommand SubmitCommand { get; }

    public MainViewModel()
    {
        SubmitCommand = new RelayCommand(Submit, () => !string.IsNullOrEmpty(UserName));
    }

    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value, () => SubmitCommand.RaiseCanExecuteChanged());
    }

    private void Submit()
    {
        // ...
    }
}
```

```xml
<Button Content="送信" Command="{Binding SubmitCommand}" />
```

注意点として、`CanExecute` は自動では再評価されません。 判定が依存している状態が変化したタイミングで `RaiseCanExecuteChanged()` を呼ぶ必要があります。 前述の `SetProperty` のコールバック付きオーバーロードが、その呼び出し場所として使いやすい選択肢です。

### RelayCommand&lt;T> —— コマンドパラメーターを受け取る

```csharp
public RelayCommand<Order> DeleteCommand { get; }

// コンストラクター内
DeleteCommand = new RelayCommand<Order>(
    order => Orders.Remove(order!),
    order => order is not null);
```

```xml
<Button Content="削除"
        Command="{Binding DataContext.DeleteCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
        CommandParameter="{Binding}" />
```

### AsyncRelayCommand —— 二重実行を防ぐ

非同期コマンドでよく起きる不具合が、1 回目の処理が終わる前にユーザーがボタンを続けて押してしまうケースです。 `AsyncRelayCommand` はタスクの実行中に `IsExecuting` を立て、 その間 `CanExecute` が `false` を返すため、処理が完了するまでボタンが自動的に無効になります。 ボタンの有効・無効を切り替えるだけであれば、ViewModel 側で実行中フラグを自前管理する必要はありません。

この仕組みが対象にしているのは、UI スレッド上で続けて呼び出されるケース、 つまりボタンにバインドしたコマンドで実際に起きる状況です。 排他制御の仕組みではありません。 `ExecuteAsync` は `CanExecute` の確認と `IsExecuting` の設定を 別々の手順として行うため、複数のスレッドから同時に呼び出すと、両方が確認を通過しえます。 複数スレッドから並行して実行する場合は、呼び出し側で同期を取ってください。

ボタンにバインドする前に知っておくべき挙動が 1 つあります。 クリック時に WPF が通る経路である `ICommand.Execute` は、 `ExecuteAsync(parameter).FireAndForget()` を**ハンドラーを渡さずに**呼び出します。 後述のとおり `FireAndForget` はハンドラーを省略すると例外を握り潰すため、 この経路では非同期処理で起きた失敗が何も表に出ないまま消えます。 コマンドに渡すデリゲートの内側で捕捉するか、 `ExecuteAsync` を自分で呼び、処理できる場所で await してください。

```csharp
LoadCommand = new AsyncRelayCommand(async () =>
{
    try
    {
        Items = await _repository.GetItemsAsync();
    }
    catch (Exception ex)
    {
        _logger.Error(ex);
    }
});
```

```csharp
public AsyncRelayCommand LoadCommand { get; }

// コンストラクター内
LoadCommand = new AsyncRelayCommand(LoadAsync);

private async Task LoadAsync()
{
    Items = await _repository.GetItemsAsync();
}
```

進捗表示を切り替える場合、`LoadCommand.IsExecuting` へ直接バインドしては**いけません**。 このプロパティは値が変わったときに `CanExecuteChanged` を発行するだけで、 `PropertyChanged` は発行しません（コマンドのクラスは `INotifyPropertyChanged` を実装していません）。 そのためバインドは初回に評価されたきり更新されず、進捗表示は現れも消えもしません。 実行状態は、変更通知を発行する ViewModel のプロパティとして公開してください。

```csharp
private bool _isLoading;

public bool IsLoading
{
    get => _isLoading;
    private set => SetProperty(ref _isLoading, value);
}

private async Task LoadAsync()
{
    IsLoading = true;
    try
    {
        Items = await _repository.GetItemsAsync();
    }
    finally
    {
        IsLoading = false;
    }
}
```

```xml
<ProgressBar IsIndeterminate="True"
             Visibility="{Binding IsLoading,
                          Converter={StaticResource BooleanToVisibilityConverter}}" />
```

`StaticResource` は XAML の読み込み時にキーを解決するため、 同じキーのコンバーターをあらかじめ定義しておく必要があります。 定義場所は `Window.Resources`、`App.xaml`、 あるいはマージする `ResourceDictionary` のいずれかです。 該当するリソースが存在しない場合、バインドが未設定のまま放置されるのではなく、XAML の読み込み自体が例外になります。

```xml
<Window.Resources>
    <BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" />
</Window.Resources>
```

なお、ボタン自体の制御には `IsExecuting` がそのまま効きます。 `CanExecute` がこの値を参照しており、コマンドの仕組みが監視しているのは `CanExecuteChanged` のほうであるため、バインド先のコントロールは自動で無効化・再有効化されます。

### FireAndForget —— await せずにタスクを開始する

`Task` を返す非同期メソッドを await せずに呼び出すと、タスク内で発生した例外は その `Task` に保持され、`Task` は faulted 状態のまま残ります。 失敗したその場で例外が表に出ることはありません。 そのタスクを await するか `Wait()` を呼べば呼び出し元に再スローされ、 `Exception` を読めば例外は投げられずに `AggregateException` として取得できますが、 戻り値を捨てる呼び出し方では、そのいずれも起きないためです。 なお `async void` のメソッドは別の話で、例外を保持する `Task` がなく、 呼び出し元のコンテキスト上で送出されます。

ただし、例外がそのまま握り潰されるわけではありません。 例外を保持している内部オブジェクトがファイナライズされる際に、ランタイムが `TaskScheduler.UnobservedTaskException` を発生させます。 ファイナライザーを持つのは `Task` 自体ではなく、この内部オブジェクトの側です。 もっともこれは診断用の経路であって、エラー処理の代わりにはなりません。 発生するのはその内部オブジェクトのファイナライザーが動いたタイミングであり、 失敗から大きく遅れることも、プログラムの無関係な箇所で起きることもあります。 現行の .NET、および .NET Framework 4.5 以降では、ハンドラーが観測したかどうかに関わらず プロセスはそのまま動き続けます。 .NET Framework では、構成ファイルで以前の動作に戻せます。

```xml
<configuration>
  <runtime>
    <ThrowUnobservedTaskExceptions enabled="true"/>
  </runtime>
</configuration>
```

このイベントは診断や記録のために購読しておく価値がありますが、購読は任意であり、 発生するタイミングも遅いため、ここで初めて表に出た時点では、 その処理に固有の対応を取れる段階を過ぎています。

`FireAndForget` は内部でタスクを await し、捕捉した例外を渡されたハンドラーに引き渡します。 これにより、失敗が起きたその場で観測されます。タスクが正常に完了した場合は何も行いません。

```csharp
_notifier.SendAsync(message).FireAndForget(ex => _logger.Error(ex));
```

使う前に知っておく必要のある性質が 2 つあります。 1 つは、ハンドラーが省略可能である点です。省略すると例外は報告されずに握り潰されるため、 失敗を捨てることが目的でない限り、必ず渡してください。 もう 1 つは、ハンドラーが `async void` メソッドの `catch` の中で実行される点です。 そのため、ハンドラー自身が投げた例外は捕捉されず、呼び出し元のコンテキストへ未処理例外として届きます。 ハンドラーの処理は、ログ出力のように例外を投げにくいものに留めてください。

## 設計上の判断

- **`RaiseCanExecuteChanged` をインターフェース経由で公開している。** 具象型の `RelayCommand` ではなく `IRaiseCanExecuteChanged` に依存させることで、 コマンドのプロパティを同期版と非同期版のあいだで差し替えても、実行可否を更新する側のコードに手を入れずに済みます。
- **コマンドパラメーターの null を明示的に処理している。** `RelayCommand<T>` は渡された `CommandParameter` が null のとき、値型か参照型かを判別して処理します。 型チェックに失敗して何も起きない、という挙動になりません。
- **非同期の再入を `CanExecute` で遮断している。** 実行中は `CanExecute` が `false` を返すため、 コマンドが呼び出しを受け付けたうえで内部的に破棄するのではなく、UI 側に実行中であることが自動的に反映されます。 `IsExecuting` の設定時には `CanExecuteChanged` を発行してバインド先を更新し、 `ExecuteAsync` も先頭で同じ条件を確認して、成り立たない場合は直ちに処理を終えます。 バインドを経由せず直接呼び出す場合はこちらが効きます。
- **ソースジェネレーター・アナライザー・独自の属性を使っていない。** いずれも通常のクラスとして実装しており、使っている属性は `SetProperty` と `OnPropertyChanged` の引数に付く `[CallerMemberName]` だけです。 そのため呼び出し箇所から挙動を追うことができ、 デバッグ時も生成された partial クラスではなく実際のコードにステップインできます。

## 技術スタック

- **言語：** C#（latest）
- **ターゲットフレームワーク：** .NET 10 / .NET 8 / .NET Framework 4.7.2
- **依存関係：** 実行時の依存関係なし。net472 のビルドに使う参照が 1 つあるが、利用側へは伝播しない
- **ライセンス：** MIT

## リポジトリ

ソースコード、サンプルアプリケーション、Issue は GitHub で公開しています。

[GitHub リポジトリを見る —— Xaml.ExtensionPack](https://github.com/s-iguchi09/Xaml.ExtensionPack){: .github-link target="_blank" rel="noopener noreferrer"}

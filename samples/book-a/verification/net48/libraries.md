<!-- tools/book-a-net48 が実行時に自動生成する。手で編集しない。 -->
<!-- 再生成: dotnet run --project tools/book-a-net48 -c Release -- book-a-libraries -->

# MVVM ライブラリで書いた場合の第 4・5・10 章

環境: .NET Framework 4.8.9363.0（ランタイムは .NET Framework 4.8.1、Release 533509。ビルドの対象は .NETFramework,Version=v4.8） / Microsoft Windows 10.0.26200

付録サンプルのビュー（XAML）はそのままにして、ビューモデルだけを CommunityToolkit.Mvvm 8.4.0 と Prism.Core 8.1.97 で書き直し、各章のシーンと同じ手順で測った。記事に値が無いので、公式文書などから予想した値と比べる。第 4 章の値は保存ボタンの IsEnabled の「入力前 → 入力後」、第 5 章は HasError・Errors の数・装飾（adorner）の数かメッセージ。第 10 章は計測のために DispatcherUnhandledException を Handled にしている。実際のアプリでハンドラーが無ければ、そこに届いた例外でプロセスが終わる。

| 操作 | 予想の根拠 | 予想 | 実測 | 予想と一致 |
|---|---|---|---|---|
| 第 4 章、Toolkit の RelayCommand、setter は SetProperty だけ | Toolkit の文書: CanExecute の変化は NotifyCanExecuteChanged を呼ぶまで反映されない | False → False | False → False | 一致 |
| 第 4 章、同じ書き方で、入力の後に CommandManager.InvalidateRequerySuggested() | Toolkit の文書: CanExecute の変化は NotifyCanExecuteChanged を呼ぶまで反映されない | False → False | False → False | 一致 |
| 第 4 章、Toolkit の [ObservableProperty] に [NotifyCanExecuteChangedFor] | Toolkit の文書: CanExecute の変化は NotifyCanExecuteChanged を呼ぶまで反映されない | False → True | False → True | 一致 |
| 第 4 章、Prism の DelegateCommand、setter は SetProperty だけ | Prism.Core は WPF に依存せず、CommandManager を使わない（RaiseCanExecuteChanged か ObservesProperty で通知する） | False → False | False → False | 一致 |
| 第 4 章、同じ書き方で、入力の後に CommandManager.InvalidateRequerySuggested() | Prism.Core は WPF に依存せず、CommandManager を使わない（RaiseCanExecuteChanged か ObservesProperty で通知する） | False → False | False → False | 一致 |
| 第 4 章、Prism の DelegateCommand に ObservesProperty(() => Name) | Prism.Core は WPF に依存せず、CommandManager を使わない（RaiseCanExecuteChanged か ObservesProperty で通知する） | False → True | False → True | 一致 |
| 第 5 章、Toolkit の ObservableValidator、validate: true だけ、起動直後（名前は空） | Toolkit の文書: SetProperty の validate: true は設定時に検証する。ValidateAllProperties はすべてを検証する | HasError False、Errors 0、装飾 0 | HasError False、Errors 0、装飾 0 | 一致 |
| 第 5 章、同じ書き方で、21 文字を入力 | Toolkit の文書: SetProperty の validate: true は設定時に検証する。ValidateAllProperties はすべてを検証する | 名前は 20 文字以内にしてください。 | 名前は 20 文字以内にしてください。 | 一致 |
| 第 5 章、コンストラクターで ValidateAllProperties()、起動直後（名前は空） | Toolkit の文書: SetProperty の validate: true は設定時に検証する。ValidateAllProperties はすべてを検証する | HasError True、Errors 1、装飾 1 | HasError True、Errors 1、装飾 1 | 一致 |
| 第 5 章、同じ書き方で、起動直後のメッセージ | Toolkit の文書: SetProperty の validate: true は設定時に検証する。ValidateAllProperties はすべてを検証する | 名前は必須です。 | 名前は必須です。 | 一致 |
| 第 5 章、同じ書き方で、21 文字を入力 | Toolkit の文書: SetProperty の validate: true は設定時に検証する。ValidateAllProperties はすべてを検証する | 名前は 20 文字以内にしてください。 | 名前は 20 文字以内にしてください。 | 一致 |
| 第 5 章、同じ書き方で、正しい名前（taro）を入力 | Toolkit の文書: SetProperty の validate: true は設定時に検証する。ValidateAllProperties はすべてを検証する | HasError False、Errors 0、装飾 0 | HasError False、Errors 0、装飾 0 | 一致 |
| 第 10 章、Toolkit の AsyncRelayCommand（既定）で、try / catch なし | Toolkit の文書（AsyncRelayCommandOptions）: 既定は例外を呼び出し元のコンテキストで投げ直す | DispatcherUnhandledException 呼ばれる（1 回）、AppDomain.UnhandledException —、UnobservedTaskException —、画面: （なし） | DispatcherUnhandledException 呼ばれる（1 回）、AppDomain.UnhandledException —、UnobservedTaskException —、画面: （なし） | 一致 |
| 第 10 章、Toolkit の AsyncRelayCommand に FlowExceptionsToTaskScheduler | Toolkit の文書（AsyncRelayCommandOptions）: 呼び出し元では投げず、UnobservedTaskException に流す（GC までは届かない） | DispatcherUnhandledException —、AppDomain.UnhandledException —、UnobservedTaskException —、画面: （なし） | DispatcherUnhandledException —、AppDomain.UnhandledException —、UnobservedTaskException —、画面: （なし） | 一致 |
| 第 10 章、同じ書き方で、実行後の ExecutionTask の状態 | Toolkit の文書（AsyncRelayCommandOptions）: 失敗は ExecutionTask から読める | Faulted | Faulted | 一致 |
| 第 10 章、Prism の DelegateCommand に async のラムダ、try / catch なし | async のラムダは async void になり、例外は UI スレッドの同期コンテキストに投げ直される | DispatcherUnhandledException 呼ばれる（1 回）、AppDomain.UnhandledException —、UnobservedTaskException —、画面: （なし） | DispatcherUnhandledException 呼ばれる（1 回）、AppDomain.UnhandledException —、UnobservedTaskException —、画面: （なし） | 一致 |

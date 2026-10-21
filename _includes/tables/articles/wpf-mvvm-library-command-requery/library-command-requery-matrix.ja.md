| コマンドとビューモデル | 入力前 | 名前を入れた後 | InvalidateRequerySuggested の後 | フォーカスを移した後 |
|---|---|---|---|---|
| 自作の RelayCommand、CanExecuteChanged を CommandManager.RequerySuggested へ転送（名前をキーで入力） | False | True | True | True |
| 同じ自作のコマンド、名前をビューモデルへコードで代入 | False | False | True | True |
| Toolkit の RelayCommand、setter は SetProperty だけ | False | False | False | False |
| Toolkit の RelayCommand、setter で NotifyCanExecuteChanged | False | True | True | True |
| Toolkit の \[ObservableProperty\] に \[NotifyCanExecuteChangedFor\] | False | True | True | True |
| Prism の DelegateCommand、setter は SetProperty だけ | False | False | False | False |
| Prism の DelegateCommand、setter で RaiseCanExecuteChanged | False | True | True | True |
| Prism の DelegateCommand に ObservesProperty(() =&gt; Name) | False | True | True | True |

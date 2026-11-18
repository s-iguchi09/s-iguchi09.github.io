| 一覧 | 止め方 | SelectedCustomer（ビューモデル） | SelectedItem / SelectedIndex | 画面で選ばれている行 | CurrentItem | 詳細欄 | 一覧の 1 行目 | ボタンを押す直前の一覧の IsEnabled |
|---|---|---|---|---|---|---|---|---|
| ListBox | なし（編集用のコピー） | \"beta\" | \"beta\" / 1 | \"beta\" | \"alpha\" | \"beta\" | \"alpha\" | True |
| ListBox | なし（IEditableObject） | \"beta\" | \"beta\" / 1 | \"beta\" | \"edited\" | \"beta\" | \"edited\" | True |
| ListBox | セッターで拒んで PropertyChanged | \"alpha\" | \"alpha\" / 1 | \"beta\" | \"alpha\" | \"edited\" | \"alpha\" | True |
| ListBox | セッターで拒んで Dispatcher.BeginInvoke（Normal の優先度）で PropertyChanged | \"alpha\" | \"alpha\" / 1 | \"beta\" | \"alpha\" | \"edited\" | \"alpha\" | True |
| ListBox | IsSynchronizedWithCurrentItem ＋ CurrentChanging で e.Cancel = true | \"alpha\" | \"beta\" / 1 | \"beta\" | \"alpha\" | \"edited\" | \"alpha\" | True |
| ListBox | IsEnabled=\"&#123;Binding CanChangeSelection&#125;\"、詳細欄は UpdateSourceTrigger=PropertyChanged | \"alpha\" | \"alpha\" / 0 | \"alpha\" | \"alpha\" | \"edited\" | \"alpha\" | False |
| DataGrid | セッターで拒んで PropertyChanged | \"alpha\" | \"alpha\" / 1 | \"beta\" | \"alpha\" | \"edited\" | \"alpha\" | True |
| DataGrid | セッターで拒んで Dispatcher.BeginInvoke（Normal の優先度）で PropertyChanged | \"alpha\" | \"alpha\" / 1 | \"beta\" | \"alpha\" | \"edited\" | \"alpha\" | True |
| DataGrid | IsSynchronizedWithCurrentItem ＋ CurrentChanging で e.Cancel = true | \"alpha\" | \"beta\" / 1 | \"beta\" | \"alpha\" | \"edited\" | \"alpha\" | True |
| DataGrid | IsEnabled=\"&#123;Binding CanChangeSelection&#125;\"、詳細欄は UpdateSourceTrigger=PropertyChanged | \"alpha\" | \"alpha\" / 0 | \"alpha\" | \"alpha\" | \"edited\" | \"alpha\" | False |
| ListBox | IsEnabled=\"&#123;Binding CanChangeSelection&#125;\"、詳細欄は既定のトリガー | \"beta\" | \"beta\" / 1 | \"beta\" | \"alpha\" | \"beta\" | \"alpha\" | True |
| ListBox | IsEnabled=\"&#123;Binding CanChangeSelection&#125;\"、詳細欄は UpdateSourceTrigger=PropertyChanged、先に Cancel をクリック | \"beta\" | \"beta\" / 1 | \"beta\" | \"alpha\" | \"beta\" | \"alpha\" | False → True（間に Cancel をクリック） |

| 詳細欄のバインド先 | 入力の直後のソース | Cancel の実行時のソース | Cancel 後の一覧の表示 | Cancel 後の詳細欄 |
|---|---|---|---|---|
| SelectedCustomer.Name（既定のトリガー） | \"alpha\" | \"edited\" | \"edited\" | \"edited\" |
| SelectedCustomer.Name, UpdateSourceTrigger=PropertyChanged | \"edited\" | \"edited\" | \"edited\" | \"edited\" |
| Editing.Name（編集用のコピー） | \"alpha\" | \"alpha\" | \"alpha\" | \"alpha\" |
| SelectedCustomer.Name ＋ IEditableObject | \"alpha\" | \"edited\" | \"alpha\" | \"alpha\" |

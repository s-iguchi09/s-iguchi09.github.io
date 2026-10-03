| 条件 | 計測値 |
|---|---|
| 自動生成の列、宣言は Zeta、Alpha（get のみ）、Mid | Zeta（編集できる）、Alpha（読み取り専用）、Mid（編集できる） |
| 自動生成の列、デモの SampleItem（IDataErrorInfo） | Name（編集できる）、Value（編集できる）、Error（読み取り専用） |
| 2 列、FrozenColumnCount = 3（デモのスライダーの最大値） | 例外なし、表示できる、値 2 |
| デモのデータ、3 行目に Error、RowValidationRules 空: HasError の最初 / 編集後 | False / False |
| デモのデータ、3 行目に Error、RowValidationRules + DataErrorValidationRule: HasError の最初 / 編集後 | True / True |
| DataGrid が IsReadOnly=True: CanUserAddRows | False (Default) |
| 列に IsReadOnly=False を指定: IsReadOnly / BeginEdit() | False / False |
| 新規行 / CanUserAddRows、ItemsSource List&lt;Person&gt; | 表示される / True |
| 新規行 / CanUserAddRows、ItemsSource ObservableCollection&lt;Person&gt; | 表示される / True |
| 新規行 / CanUserAddRows、ItemsSource Person\[\]（配列） | 表示されない / False |
| 新規行 / CanUserAddRows、ItemsSource 引数なしのコンストラクターが無い型の List&lt;T&gt; | 表示されない / False |
| 2 行目を選んで Delete キー: 残った項目 | 2 (Name 1, Name 3) |

| case | measured |
|---|---|
| auto columns, declared Zeta, Alpha (get-only), Mid | Zeta (editable), Alpha (read-only), Mid (editable) |
| auto columns, demo SampleItem (IDataErrorInfo) | Name (editable), Value (editable), Error (read-only) |
| 2 columns, FrozenColumnCount = 3 (demo slider max) | no exception, shown; value 2 |
| demo data, row 3 Error, RowValidationRules empty: HasError start / edited | False / False |
| demo data, row 3 Error, RowValidationRules + DataErrorValidationRule: HasError start / edited | True / True |
| DataGrid IsReadOnly=True: CanUserAddRows | False (Default) |
| column IsReadOnly=False set: IsReadOnly / BeginEdit() | False / False |
| new-item row / CanUserAddRows, ItemsSource List&lt;Person&gt; | shown / True |
| new-item row / CanUserAddRows, ItemsSource ObservableCollection&lt;Person&gt; | shown / True |
| new-item row / CanUserAddRows, ItemsSource Person\[\] (array) | not shown / False |
| new-item row / CanUserAddRows, ItemsSource List&lt;T&gt; without parameterless ctor | not shown / False |
| row 2 selected, Delete key: items left | 2 (Name 1, Name 3) |

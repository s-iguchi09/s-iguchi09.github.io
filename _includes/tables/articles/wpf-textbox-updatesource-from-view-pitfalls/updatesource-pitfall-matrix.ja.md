| Text の設定のしかた | GetBindingExpression の状態 | そのまま UpdateSource() | Text を変えた後 / 入力した後 |
|---|---|---|---|
| Text=\"literal\" | null | 呼べない | 呼べない |
| MultiBinding | null | 呼べない | 呼べない |
| TemplateBinding | null | 呼べない | 呼べない |
| Binding Mode=OneTime | 取得できる | 変化なし | InvalidOperationException |
| Binding Mode=OneWay | 取得できる | 変化なし | InvalidOperationException |
| Binding Mode=OneWayToSource | 取得できる | 変化なし | ソースが更新される |
| Binding Mode=TwoWay | 取得できる | 変化なし | ソースが更新される |
| OneWay、TextInput で入力 | バインドされたまま | 呼んでいない | 変わらない |
| OneTime、TextInput で入力 | バインドされたまま | 呼んでいない | 変わらない |
| TwoWay、TextInput で入力 | バインドされたまま | 呼んでいない | 変わらない |
| TwoWay、続けて ClearBinding | 取得できる | - | InvalidOperationException |

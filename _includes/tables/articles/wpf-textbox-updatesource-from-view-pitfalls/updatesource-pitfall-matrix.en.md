| how Text is set | GetBindingExpression state | UpdateSource() as-is | after editing Text / after typing |
|---|---|---|---|
| Text=\"literal\" | null | cannot call | cannot call |
| MultiBinding | null | cannot call | cannot call |
| TemplateBinding | null | cannot call | cannot call |
| Binding Mode=OneTime | obtained | no change | InvalidOperationException |
| Binding Mode=OneWay | obtained | no change | InvalidOperationException |
| Binding Mode=OneWayToSource | obtained | no change | source updated |
| Binding Mode=TwoWay | obtained | no change | source updated |
| OneWay, typed via TextInput | still bound | not called | unchanged |
| OneTime, typed via TextInput | still bound | not called | unchanged |
| TwoWay, typed via TextInput | still bound | not called | unchanged |
| TwoWay, then ClearBinding | obtained | - | InvalidOperationException |

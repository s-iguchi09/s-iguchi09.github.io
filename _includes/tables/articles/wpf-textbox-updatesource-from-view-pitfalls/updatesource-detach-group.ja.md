| 条件 | 結果 |
|---|---|
| TwoWay、TextBox をツリーから外してから UpdateSource() | 例外なし、Status PathError、ソース = before |
| OneTime: ソースを変えてから UpdateTarget() | Text before -&gt; before（呼ぶ前） -&gt; changed（UpdateTarget の後） |
| BindingGroup.UpdateSources()、規則なし | 戻り値 True、ソース = after |
| BindingGroup.UpdateSources()、RawProposedValue の規則が失敗 | 戻り値 False、ソース = before |
| BindingGroup.UpdateSources()、UpdatedValue の規則が失敗 | 戻り値 False、ソース = after |

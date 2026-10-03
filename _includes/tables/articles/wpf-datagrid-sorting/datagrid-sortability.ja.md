| 列 | SortMemberPath | CanUserSort | 見出しをクリックした後の並び順 |
|---|---|---|---|
| DataGridTextColumn、Binding だけ | Name | True | alice, bob, carol |
| + SortMemberPath=Score を明示 | Score | True | bob, carol, alice |
| + CanUserSort=False | Name | False | carol, alice, bob |
| DataGridTemplateColumn、SortMemberPath なし | （空） | False | carol, alice, bob |
| DataGridTemplateColumn + SortMemberPath=Name | Name | True | alice, bob, carol |

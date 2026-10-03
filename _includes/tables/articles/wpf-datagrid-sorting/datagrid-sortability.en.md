| column | SortMemberPath | CanUserSort | order after a header click |
|---|---|---|---|
| DataGridTextColumn, Binding only | Name | True | alice, bob, carol |
| + explicit SortMemberPath=Score | Score | True | bob, carol, alice |
| + CanUserSort=False | Name | False | carol, alice, bob |
| DataGridTemplateColumn, no SortMemberPath | (empty) | False | carol, alice, bob |
| DataGridTemplateColumn + SortMemberPath=Name | Name | True | alice, bob, carol |

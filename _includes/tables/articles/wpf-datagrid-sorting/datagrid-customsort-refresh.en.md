| operation | SortDescriptions | CustomSort | order / selection |
|---|---|---|---|
| SortDescriptions: Name asc | 1 | null | alice, bob, carol |
| then CustomSort = by name length | 0 | set | bob, alice, carol |
| then SortDescriptions.Add(Name desc) | 1 | null | carol, bob, alice |
| Items.Refresh() with a row selected | - | - | SelectedItem bob, CurrentCell bob |

| operation | SortDescriptions | column.SortDirection | order |
|---|---|---|---|
| initial | 0 | null | carol, alice, bob |
| SortDescriptions.Add only | 1 | null | alice, bob, carol |
| + column.SortDirection | 1 | Ascending | alice, bob, carol |
| then SortDescriptions.Clear() only | 0 | Ascending | carol, alice, bob |
| + clear column.SortDirection | 0 | null | carol, alice, bob |
| one SortDescription (Score desc), with a tie | 1 | null | alice, carol, anna, bob |
| two SortDescriptions (Score desc, Name asc), with a tie | 2 | null | alice, anna, carol, bob |
| ItemsSource = ICollectionView; view.SortDescriptions.Clear() | 0 | Ascending | carol, alice, bob |
| column header click (standard sort) | 1 | Ascending | alice, bob, carol |

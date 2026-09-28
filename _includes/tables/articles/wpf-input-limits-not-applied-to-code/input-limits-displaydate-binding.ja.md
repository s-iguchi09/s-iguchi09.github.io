| バインド | 結果 |
|---|---|
| DisplayDateStart、既定（TwoWay） | DisplayDateStart 2026-04-05、ソース 2026-04-10、バインドは残る。ソースを 04-12 にすると 2026-04-12 |
| DisplayDateStart、Mode=OneWay | DisplayDateStart 2026-04-05、ソース 2026-04-10、バインドは残る。ソースを 04-12 にすると 2026-04-12 |
| DisplayDateStart、読み取り専用、Source を指定 | SetBinding: InvalidOperationException（読み取り専用のプロパティ） |
| DisplayDateStart、読み取り専用、表示中に DataContext を設定 | InvalidOperationException（読み取り専用のプロパティ） |
| DisplayDateStart、読み取り専用、表示前に DataContext を設定 | DataContext の設定時: 例外なし、表示時: InvalidOperationException（読み取り専用のプロパティ） |
| DisplayDateStart、読み取り専用、表示前に親の DataContext を設定 | DataContext の設定時: 例外なし、表示時: InvalidOperationException（読み取り専用のプロパティ） |
| DisplayDateEnd、読み取り専用、Source を指定 | SetBinding: InvalidOperationException（読み取り専用のプロパティ） |
| DisplayDateEnd、読み取り専用、表示中に DataContext を設定 | InvalidOperationException（読み取り専用のプロパティ） |
| DisplayDateEnd、読み取り専用、表示前に DataContext を設定 | DataContext の設定時: 例外なし、表示時: InvalidOperationException（読み取り専用のプロパティ） |
| DisplayDateEnd、読み取り専用、表示前に親の DataContext を設定 | DataContext の設定時: 例外なし、表示時: InvalidOperationException（読み取り専用のプロパティ） |

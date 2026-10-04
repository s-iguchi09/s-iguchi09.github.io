| 条件 | 計測値 |
|---|---|
| CanExecute が false: IsEnabled / IsEnabled=\"True\" を指定したボタン | False / False |
| CanExecute が true に変わる: IsEnabled / InvalidateRequerySuggested の後 | False / True |
| 同じコマンドのボタンが 20 個: フォーカスが 1 回移るごとの CanExecute の呼び出し回数 | 20 |
| デモの XAML: 読み込み中に CanExecute へ渡された引数 | ShowMessageText, ShowMessageText |
| コードから TextBox.Text を空にする: CanExecute の呼び出し回数 / IsEnabled | 1 / False |
| TextBox.Text を Hello にしてクリック: Execute の引数 | Hello |
| 項目のテンプレート、CommandParameter=\"&#123;Binding&#125;\": 2 行目を実際にクリック: Execute が受け取った値 | Row 2 |

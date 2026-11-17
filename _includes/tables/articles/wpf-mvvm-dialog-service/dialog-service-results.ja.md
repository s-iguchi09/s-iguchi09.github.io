| 操作 | 戻り値 | 呼び出し後の状態 |
|---|---|---|
| DeleteCommand → OK | Confirm: true | 顧客 2 件 → 1 件 |
| DeleteCommand → キャンセル | Confirm: false | 顧客 2 件 → 2 件 |
| ImportCommand → キャンセル | OpenFile: null | ImportPath: null |
| ImportCommand → ファイル名を入れて開く | OpenFile: ファイルの完全なパス | ImportPath: 同じパス |
| EditCommand →「x」を入力して OK | ShowDialog: true | Customer.Name: \"Alicex\" |
| EditCommand →「x」を入力してキャンセル | ShowDialog: false | Customer.Name: \"Alice\" |
| EditCommand →「x」を入力して × | ShowDialog: false | Customer.Name: \"Alice\" |
| Window.ShowDialog() そのもの → × | ShowDialog(): false | DialogResult は設定していない |
| Customer そのものをバインド →「x」を入力してキャンセル | ShowDialog: false | Customer.Name: \"Alicex\" |
| どのウィンドウの DataContext でもないビューモデルの DeleteCommand | 例外 InvalidOperationException | 表示中のウィンドウ 1 → 1、Application.Windows 1 → 1、顧客 1 → 1 件 |
| どのウィンドウの DataContext でもないビューモデルの EditCommand | 例外 InvalidOperationException | 表示中のウィンドウ 1 → 1、Application.Windows 1 → 1、顧客 1 → 1 件 |
| 同じ EditCommand・ウィンドウを作ってから所有者を探す順序のサービス | 例外 InvalidOperationException | 表示中のウィンドウ 1 → 1、Application.Windows 1 → 2、顧客 1 → 1 件 |

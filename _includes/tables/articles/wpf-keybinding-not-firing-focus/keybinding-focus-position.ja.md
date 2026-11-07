| キー | KeyBinding の置き場所 | キーを押す前の操作 | キーを押したときのキーボードフォーカス | 切り分けの手順の判定 | Window の PreviewKeyDown | 実行されたコマンド |
|---|---|---|---|---|---|---|
| Ctrl+S | Window | Keyboard.Focus(titleBox) | Window の TextBox | 4: 経路の上 | 呼ばれた | SaveCommand |
| Ctrl+S | Window | Keyboard.Focus(okButton) | Button | 4: 経路の上 | 呼ばれた | SaveCommand |
| Ctrl+S | Window | 表示して Activate しただけ | Window | 4: 経路の上 | 呼ばれた | SaveCommand |
| Ctrl+S | Window | Keyboard.ClearFocus() | なし（Keyboard.FocusedElement が null） | 1: フォーカスが無い | 呼ばれない | なし |
| Ctrl+S | Window | Keyboard.ClearFocus() の後、Keyboard.Focus(window) | Window | 4: 経路の上 | 呼ばれた | SaveCommand |
| Ctrl+S | Window | Keyboard.Focus(okButton) の後、okButton.IsEnabled = false | Window | 4: 経路の上 | 呼ばれた | SaveCommand |
| Ctrl+S | Window | Keyboard.Focus(titleBox) の後、titleBox.Visibility = Collapsed | Window | 4: 経路の上 | 呼ばれた | SaveCommand |
| Ctrl+S | Window | Owner 付きの別ウィンドウの TextBox にフォーカス | Owner 付きの別ウィンドウの TextBox | 2: 別のウィンドウ | 呼ばれない | なし |
| Ctrl+S | Window と Owner 付きの別ウィンドウ | Owner 付きの別ウィンドウの TextBox にフォーカス | Owner 付きの別ウィンドウの TextBox | 4: 経路の上 | 呼ばれない | SaveCommand |
| Ctrl+S | Window と Owner 付きの別ウィンドウ（別ウィンドウに DataContext を設定しない） | Owner 付きの別ウィンドウの TextBox にフォーカス | Owner 付きの別ウィンドウの TextBox | 4: 経路の上 | 呼ばれない | なし |
| Ctrl+D | UserControl | Keyboard.Focus(panelBox) | UserControl の TextBox | 4: 経路の上 | 呼ばれた | DeleteCommand |
| Ctrl+D | UserControl | Keyboard.Focus(titleBox) | Window の TextBox | 3: 置いた要素の外 | 呼ばれた | なし |
| Ctrl+D | UserControl | Keyboard.Focus(titleBox) の後、UserControl の TextBlock をクリック | Window の TextBox | 3: 置いた要素の外 | 呼ばれた | なし |
| Ctrl+D | UserControl（Focusable=\"True\"） | Keyboard.Focus(titleBox) の後、UserControl の TextBlock をクリック | Window の TextBox | 3: 置いた要素の外 | 呼ばれた | なし |
| Ctrl+D | UserControl | Keyboard.Focus(panel) | Window | 3: 置いた要素の外 | 呼ばれた | なし |
| Ctrl+D | UserControl（Focusable=\"True\"） | Keyboard.Focus(panel) | UserControl | 4: 経路の上 | 呼ばれた | DeleteCommand |
| Ctrl+D | Window（UserControl からは外す） | Keyboard.Focus(titleBox) | Window の TextBox | 4: 経路の上 | 呼ばれた | DeleteCommand |

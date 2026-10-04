| 条件 | 計測値 |
|---|---|
| 既定値: IsMainMenu; IsCheckable, IsChecked, StaysOpenOnClick, InputGestureText | True; False, False, False, （空） |
| デモの Role の欄: 4 つの項目 | TopLevelHeader, SubmenuHeader, SubmenuItem, TopLevelItem |
| TopLevelItem に子を追加: その Role | TopLevelHeader |
| IsCheckable True、StaysOpenOnClick True: 開いたか; クリック: チェック / CheckBox / 開いたままか; もう一度 | True; True / True / True; False / True |
| コードで CheckBox をチェック: 項目の IsChecked | True |
| IsCheckable True、StaysOpenOnClick False: 開いたか; クリック: チェック / CheckBox / 開いたままか; もう一度 | True; True / True / False; - |
| IsCheckable False、StaysOpenOnClick True: 開いたか; クリック: チェック / CheckBox / 開いたままか; もう一度 | True; False / False / True; False / True |
| チェックできる項目 2 つを両方クリック: IsChecked | True, True |
| IsMainMenu True、実際に Alt: 強調表示 / サブメニューが開くか / TextBox がフォーカスを保つか | True / False / False |
| IsMainMenu True、実際に F10: 強調表示 / サブメニューが開くか / TextBox がフォーカスを保つか | False / False / True |
| IsMainMenu True、実際に Alt+M: 強調表示 / サブメニューが開くか / TextBox がフォーカスを保つか | True / True / False |
| IsMainMenu False、実際に Alt: 強調表示 / サブメニューが開くか / TextBox がフォーカスを保つか | False / False / True |
| IsMainMenu False、実際に F10: 強調表示 / サブメニューが開くか / TextBox がフォーカスを保つか | False / False / True |
| IsMainMenu False、実際に Alt+M: 強調表示 / サブメニューが開くか / TextBox がフォーカスを保つか | True / True / False |
| IsMainMenu True、実際に F10、Button にフォーカス: 強調表示 / 開くか / メニューにフォーカスがあるか | True / False / True |
| IsMainMenu True、InputManager 経由で F10、TextBox にフォーカス: 強調表示 / 開くか / メニューにフォーカスがあるか | True / False / True |
| InputGestureText \"Ctrl+O\": 表示されるか / Copy の項目の InputGestureText | 表示される（幅 35.83） / Ctrl+C |
| TextBox にフォーカスがある状態で実際に Ctrl+O: Click の回数 | 0 |
| Copy の項目、メニューは閉じたまま、TextBox にフォーカス、選択なし: IsEnabled / CanExecute | True / False |
| 実際のクリックで開く: 選択なし; 全選択（フォーカス）; Button にフォーカス | False; True (MenuItem); False |
| Observation Target: Highlighted / Pressed / SubmenuOpen / Suspending: 前; ホバー | False / False / False / False; True / False / False / False |
| 実際にボタンを押す; 離す | True / True / True / True; True / False / True / True |
| File をクリック: 開くか / Suspending; Edit にホバー: File が開いているか; Edit が開くか / Suspending | True / True; False; True / True |
| ウィンドウの KeyBinding Ctrl+O、実際に Ctrl+O: 実行されたか / 項目の InputGestureText | 1 / （空） |

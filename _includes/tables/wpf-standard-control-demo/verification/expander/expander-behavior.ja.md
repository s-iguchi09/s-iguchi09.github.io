| 条件 | 計測値 |
|---|---|
| 基底クラス / IsExpanded の既定値、既定で双方向か / ExpandDirection | HeaderedContentControl / False, True / Down |
| 既定のテンプレート: 見出しの要素 / VisualStateGroups | ToggleButton（名前 HeaderSite） / 0 |
| トリガー | IsExpanded=True; ExpandDirection=Right, Up, Left; IsEnabled=False |
| IsExpanded を False に: 内容の Visibility（直後 / 50 ms 後） | Collapsed / Collapsed |
| 見出しを実際にマウスでクリック: IsExpanded / バインドした CheckBox / イベント | True / True / Expanded |
| 続けて CheckBox を外す: IsExpanded / イベント | False / Collapsed |
| 折りたたみ中: 子の Loaded / 測定の回数 / 1000 項目の ListBox で作られた項目の数 | 1 / 0 / 0 |
| 展開した後 | 2 / 1 / 11 |

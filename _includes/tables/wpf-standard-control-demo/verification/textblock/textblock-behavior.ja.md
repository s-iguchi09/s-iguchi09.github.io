| 条件 | 計測値 |
|---|---|
| 基底クラス / Focusable / Padding / TextWrapping、TextTrimming | FrameworkElement / False / 0,0,0,0 / NoWrap, None |
| Inlines に \"Hello \" + Bold \"world\": レイアウト前の Text / レイアウト後 | \"\" / \"\" |
| 続けて Text = \"Replaced\": 例外 / Inlines の数 | 例外なし / 1 |
| Bold \"Name: \" の後の Text をバインドした Run: Run.Text / 変更後 | \"bound\" / \"changed\" |
| \"Say Supercalifragilisticexpialidocious now\"、幅 100: 行数 | NoWrap 1、切り取られる / Wrap 4 / WrapWithOverflow 3、切り取られる |
| CharacterEllipsis: 幅 100 の Grid の中の幅 / 横の StackPanel 100 | 100 / 220.43 |
| Padding=10: 大きさ なし / あり | 61.57 x 15.96 / 81.57 x 35.96 |

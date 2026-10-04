| 条件 | 計測値 |
|---|---|
| 基底クラス / IsOpen, StaysOpen, AllowsTransparency, Placement, PopupAnimation | FrameworkElement / False, True, False, Bottom, None |
| 既定のテンプレートの中の Popup: ComboBox / 最上位の MenuItem | Popup PART\_Popup / Popup PART\_Popup |
| 開いた状態: 子が別のウィンドウハンドルにあるか / 子の親 | True / NonLogicalAdornerDecorator |
| StaysOpen=False、外側をマウスでクリック: IsOpen / バインドした CheckBox | False / False |
| StaysOpen=True、外側をマウスでクリック: IsOpen / バインドした CheckBox | True / True |
| 開いた状態でウィンドウを (40, 40) 動かす: 子が動いた量 | (0, 0) |
| AllowsTransparency=False、Fade: レイヤードウィンドウか / 開いた後の不透明度 | False / 1（18 ms 時点）, 1（76 ms 時点）, 1（480 ms 時点） |
| AllowsTransparency=True、Fade: レイヤードウィンドウか / 開いた後の不透明度 | True / 1（43 ms 時点）, 0.48（97 ms 時点）, 1（500 ms 時点） |

| case | measured |
|---|---|
| base class / IsOpen, StaysOpen, AllowsTransparency, Placement, PopupAnimation | FrameworkElement / False, True, False, Bottom, None |
| Popup in the default template: ComboBox / top-level MenuItem | Popup PART\_Popup / Popup PART\_Popup |
| open: child in a separate window handle / child\'s parent | True / NonLogicalAdornerDecorator |
| StaysOpen=False, mouse click outside it: IsOpen / bound CheckBox | False / False |
| StaysOpen=True, mouse click outside it: IsOpen / bound CheckBox | True / True |
| open, window moved by (40, 40): child moved by | (0, 0) |
| AllowsTransparency=False, Fade: layered window / opacity after opening | False / 1 at 18 ms, 1 at 76 ms, 1 at 480 ms |
| AllowsTransparency=True, Fade: layered window / opacity after opening | True / 1 at 43 ms, 0.48 at 97 ms, 1 at 500 ms |

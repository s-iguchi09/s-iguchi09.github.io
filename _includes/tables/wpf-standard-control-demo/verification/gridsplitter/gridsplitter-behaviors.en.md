| case | measured |
|---|---|
| ShowsPreview=False: before / during drag / after release | 197.5 / 5 / 197.5  \|  237.5 / 5 / 157.5  \|  237.5 / 5 / 157.5 |
| ShowsPreview=False: re-measures of left content over 10 drag steps | 10 |
| ShowsPreview=True: preview element in the adorner layer | Control (Style is the splitter\'s PreviewStyle: True) |
| ShowsPreview=True: visuals inside the preview element | Rectangle Fill=\#80000000 Opacity=1 |
| ShowsPreview=True: before / during drag / after release | 197.5 / 5 / 197.5  \|  197.5 / 5 / 197.5  \|  237.5 / 5 / 157.5 |
| ShowsPreview=True: re-measures of left content over 10 drag steps | 0 |
| DragIncrement=1, dragged 27 | left column moved by 27 |
| DragIncrement=20, dragged 9 | left column moved by 0 |
| DragIncrement=20, dragged 11 | left column moved by 20 |
| DragIncrement=20, dragged 27 | left column moved by 20 |
| DragIncrement=20, dragged 31 | left column moved by 40 |
| KeyboardIncrement=default, Right arrow once | left column moved by 10 |
| KeyboardIncrement=25, Right arrow once | left column moved by 25 |
| Focusable=True: Focus() / IsKeyboardFocused | True / True |
| Focusable=False: Focus() / IsKeyboardFocused | False / False |
| left MinWidth=none, dragged -1000 | 0 / 5 / 395 |
| left MinWidth=50, dragged -1000 | 50 / 5 / 345 |
| dragged 40, then Esc before release: before / during / after | 197.5 / 5 / 197.5  \|  237.5 / 5 / 157.5  \|  197.5 / 5 / 197.5 |
| \* \| \*: ColumnDefinition.Width after drag | 227.5\* \| 167.5\* |
| 200 \| \*: ColumnDefinition.Width after drag | 230 \| \* |
| Auto \| \*: ColumnDefinition.Width after drag | 90 \| \* |
| Width bound to a source property, Mode=not set: source after drag / binding | \* / removed |
| Width bound to a source property, Mode=TwoWay: source after drag / binding | 227.5\* / kept |
| IsDragging: before / after left button down / after CancelDrag() | False / True / False |

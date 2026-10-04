| case | measured |
|---|---|
| defaults: EditingMode, EditingModeInverted, ActiveEditingMode, Background | Ink, EraseByStroke, Ink, \#FFFFFFFF |
| pen: Color; Width, Height; StylusTip, IsHighlighter; recognizer | Black; 2.0031496062992127, same; Ellipse, False; True |
| Background after style (source) / SystemColors.WindowBrush; metadata | \#FFFFFFFF (Style) / \#FFFFFFFF; null |
| demo\'s start: EditingMode, EditingModeInverted | Ink, InkAndGesture |
| pen color, background; contrast ratio | AntiqueWhite (\#FFFAEBD7), AliceBlue (\#FFF0F8FF); 1.09 : 1 |
| Ink, real drag: strokes; ActiveEditingMode while dragging; stroke color, width | 1; Ink; Black, 2 |
| DefaultDrawingAttributes changed in place (as the demo does): 1st stroke; 2nd stroke | Black, 2; Red, 10 |
| same DrawingAttributes object: default and 1st stroke; 1st and 2nd | False; False |
| Background null, real drag left to right: strokes; Gesture event | 1; none |
| None, real drag left to right: strokes; Gesture event | 0; none |
| GestureOnly, real drag left to right: strokes; Gesture event | 0; Right |
| InkAndGesture, real drag left to right: strokes; Gesture event | 0; Right |
| InkAndGesture, Gesture handler sets Cancel, real drag left to right: strokes; Gesture event | 1; Right |
| InkAndGesture, SetEnabledGestures(Circle), real drag left to right: strokes; Gesture event | 1; NoGesture |
| EraseByStroke, one line from x 50 to 250, real drag across x 150: strokes | 0 |
| EraseByPoint, one line from x 50 to 250, real drag across x 150: strokes | 2 (x 49 to 146 and x 154 to 251) |
| Select, real click on the line: selected strokes; Copy can execute | 1; True |
| Ink mode, SelectAll can execute: no strokes; one stroke; executed: selected | False; False; 0 |
| Select mode, SelectAll can execute: no strokes; one stroke; executed: selected | False; True; 1 |
| after SelectAll: Copy can execute; Undo can execute | True; False |
| 2 strokes saved as ISF and loaded: bytes; strokes | 66; 2 |

| 条件 | 計測値 |
|---|---|
| 既定値: EditingMode, EditingModeInverted, ActiveEditingMode, Background | Ink, EraseByStroke, Ink, \#FFFFFFFF |
| ペン: Color; Width, Height; StylusTip, IsHighlighter; 認識エンジンがあるか | Black; 2.0031496062992127, 同じ; Ellipse, False; True |
| スタイル適用後の Background（出どころ） / SystemColors.WindowBrush; メタデータの既定値 | \#FFFFFFFF (Style) / \#FFFFFFFF; null |
| デモの初期値: EditingMode, EditingModeInverted | Ink, InkAndGesture |
| ペンの色, 背景; コントラスト比 | AntiqueWhite (\#FFFAEBD7), AliceBlue (\#FFF0F8FF); 1.09 : 1 |
| Ink、実際にドラッグ: ストローク数; ドラッグ中の ActiveEditingMode; ストロークの色, 幅 | 1; Ink; Black, 2 |
| DefaultDrawingAttributes をその場で変更（デモと同じ）: 1 本目; 2 本目 | Black, 2; Red, 10 |
| 同じ DrawingAttributes のオブジェクトか: 既定値と 1 本目; 1 本目と 2 本目 | False; False |
| Background が null、左から右へ実際にドラッグ: ストローク数; Gesture イベント | 1; なし |
| None、左から右へ実際にドラッグ: ストローク数; Gesture イベント | 0; なし |
| GestureOnly、左から右へ実際にドラッグ: ストローク数; Gesture イベント | 0; Right |
| InkAndGesture、左から右へ実際にドラッグ: ストローク数; Gesture イベント | 0; Right |
| InkAndGesture、Gesture のハンドラーで Cancel を設定、左から右へ実際にドラッグ: ストローク数; Gesture イベント | 1; Right |
| InkAndGesture、SetEnabledGestures(Circle)、左から右へ実際にドラッグ: ストローク数; Gesture イベント | 1; NoGesture |
| EraseByStroke、x 50〜250 の線 1 本を、x 150 を横切るように実際にドラッグ: ストローク数 | 0 |
| EraseByPoint、x 50〜250 の線 1 本を、x 150 を横切るように実際にドラッグ: ストローク数 | 2（x 49〜146 と x 154〜251） |
| Select、線を実際にクリック: 選択されたストローク数; Copy が実行できるか | 1; True |
| Ink モード、SelectAll が実行できるか: ストロークなし; 1 本; 実行後の選択数 | False; False; 0 |
| Select モード、SelectAll が実行できるか: ストロークなし; 1 本; 実行後の選択数 | False; True; 1 |
| SelectAll の後: Copy が実行できるか; Undo が実行できるか | True; False |
| ストローク 2 本を ISF で保存して読み込む: バイト数; ストローク数 | 66; 2 |

| 条件 | 計測値 |
|---|---|
| 基底クラス、Focusable、Stretch と StretchDirection の既定値 | FrameworkElement, False; Uniform, Both |
| 大きい画像、UniformToFill / Both、300 x 200 の中: 大きさ、左上の位置、クリップ | 400 x 200; 0, 0; 300 x 200 |
| 大きい画像、UniformToFill / Both、Center に配置、300 x 200 の中: 大きさ、左上の位置、クリップ | 400 x 200; -50, 0; 300 x 200 |
| 大きい画像、None / UpOnly、300 x 200 の中: 大きさ、左上の位置、クリップ | 600.08 x 300.04; 0, 0; 300 x 200 |
| 72 DPI の 100 x 50 ピクセル、Stretch None: 大きさ | 133.36 x 66.68 |
| Source にパスをバインド、PNG ファイル: Source の型、大きさ | BitmapFrameDecode, 100.01 x 50.01 |
| ファイルを削除: 表示中 / Source = null の後 / GC の後 | ロックされている（IOException） / ロックされている（IOException） / 削除できる |
| Source にパスをバインド、存在しないファイル: Source の型、大きさ | null, 0 x 0 |
| Source にパスをバインド、SVG ファイル: Source の型、大きさ | null, 0 x 0 |
| CacheOption OnLoad の BitmapImage を表示中: ファイルを削除 | 削除できる |
| 600 x 300 を DecodePixelWidth 100 で: ピクセル数、DIP での大きさ、Stretch None の大きさ | 100 x 50; 100.01 x 50.01; 100.01 x 50.01 |
| デモの最初のパス img0.jpg: 存在するか、ピクセル数、DPI | True; 3840 x 2400; 96 |

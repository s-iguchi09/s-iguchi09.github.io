| case | measured |
|---|---|
| base class, Focusable; defaults Stretch, StretchDirection | FrameworkElement, False; Uniform, Both |
| large image, UniformToFill / Both in 300 x 200: size; top-left; clip | 400 x 200; 0, 0; 300 x 200 |
| large image, UniformToFill / Both, aligned Center in 300 x 200: size; top-left; clip | 400 x 200; -50, 0; 300 x 200 |
| large image, None / UpOnly in 300 x 200: size; top-left; clip | 600.08 x 300.04; 0, 0; 300 x 200 |
| 100 x 50 pixels at 72 DPI, Stretch None: size | 133.36 x 66.68 |
| path bound to Source, PNG file: Source type, size | BitmapFrameDecode, 100.01 x 50.01 |
| delete the file: while shown; Source = null; then GC | locked (IOException); locked (IOException); deleted |
| path bound to Source, missing file: Source type, size | null, 0 x 0 |
| path bound to Source, SVG file: Source type, size | null, 0 x 0 |
| BitmapImage with CacheOption OnLoad, shown: delete the file | deleted |
| 600 x 300 with DecodePixelWidth 100: pixels; size in DIPs; Stretch None size | 100 x 50; 100.01 x 50.01; 100.01 x 50.01 |
| demo\'s start path img0.jpg: exists; pixels; DPI | True; 3840 x 2400; 96 |

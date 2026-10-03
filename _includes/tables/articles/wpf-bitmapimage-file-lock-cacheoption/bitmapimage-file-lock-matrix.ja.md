| 画像の読み込み方 | 大きさ | File.Delete | 上書き（File.Copy） | 名前の変更（File.Move） |
|---|---|---|---|---|
| new BitmapImage(uri) | 64x48 | IOException | IOException | IOException |
| + CacheOption = OnLoad | 64x48 | IOException | IOException | IOException |
| BeginInit / EndInit | 64x48 | IOException | IOException | IOException |
| + CacheOption = OnLoad | 64x48 | OK | OK | OK |
| + CreateOptions = IgnoreImageCache | 64x48 | IOException | IOException | IOException |
| StreamSource + OnLoad | 64x48 | OK | OK | OK |
| ImageSourceConverter | 64x48 | IOException | IOException | IOException |
| （既定の読み込み方）GC の後 | - | OK | - | - |

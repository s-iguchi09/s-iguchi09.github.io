| how the image is loaded | size | File.Delete | overwrite (File.Copy) | rename (File.Move) |
|---|---|---|---|---|
| new BitmapImage(uri) | 64x48 | IOException | IOException | IOException |
| + CacheOption = OnLoad | 64x48 | IOException | IOException | IOException |
| BeginInit / EndInit | 64x48 | IOException | IOException | IOException |
| + CacheOption = OnLoad | 64x48 | OK | OK | OK |
| + CreateOptions = IgnoreImageCache | 64x48 | IOException | IOException | IOException |
| StreamSource + OnLoad | 64x48 | OK | OK | OK |
| ImageSourceConverter | 64x48 | IOException | IOException | IOException |
| (default) after GC | - | OK | - | - |

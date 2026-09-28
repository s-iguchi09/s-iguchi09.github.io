| receiver | member declaration | result | call or reason |
|---|---|---|---|
| extension(Directory) | public static void DeleteIfExists(string path) | ✓ compiles | Directory.DeleteIfExists(path) |
| extension(Directory) | public void DeleteIfExists() | ✕ CS9303 | instance member needs a named receiver parameter |
| extension(Directory dir) | public static void DeleteIfExists(string path) | ✕ CS0721 | static types cannot be used as parameters |

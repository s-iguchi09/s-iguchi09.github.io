| レシーバー | メンバーの宣言 | 結果 | 呼び出し方・理由 |
|---|---|---|---|
| extension(Directory) | public static void DeleteIfExists(string path) | ✓ コンパイルが通る | Directory.DeleteIfExists(path) |
| extension(Directory) | public void DeleteIfExists() | ✕ CS9303 | インスタンスメンバーには名前付きのレシーバーが要る |
| extension(Directory dir) | public static void DeleteIfExists(string path) | ✕ CS0721 | 静的な型は引数に使えない |

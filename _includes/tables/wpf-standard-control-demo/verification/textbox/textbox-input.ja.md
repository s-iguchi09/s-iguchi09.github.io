| 条件 | 操作後の Text |
|---|---|
| MaxLength=5、\"ABCDEFGH\" を入力 | \"ABCDE\" |
| MaxLength=5、コードから Text = \"ABCDEFGH\" | \"ABCDEFGH\" |
| MaxLength=5、\"ABCDEFGH\" にしたソースへ Text をバインド | \"ABCDEFGH\" |
| CharacterCasing=Upper、\"hello\" を入力 | \"HELLO\" |
| CharacterCasing=Upper、コードから Text = \"hello\" | \"hello\" |
| IsReadOnly=True、\"abc\" の後に \"xyz\" を入力 | \"abc\" |
| IsReadOnly=True、SelectAll() の後の SelectedText | \"abc\" |
| AcceptsReturn=False、\"a\" と \"b\" の間で Enter | \"ab\" |
| AcceptsReturn=True、\"a\" と \"b\" の間で Enter | \"a\\r\\nb\" |
| Default: \"abc\" を入力。入力中のソースへの書き込み、フォーカスが移った後 | 0 (空)、1 (abc) |
| PropertyChanged: \"abc\" を入力。入力中のソースへの書き込み、フォーカスが移った後 | 3 (abc)、3 (abc) |
| IsReadOnly: \"x\" を入力した後の Text、全選択で Copy / Cut / Paste が実行できるか | error details、True / False / False |

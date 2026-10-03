| case | Text after |
|---|---|
| MaxLength=5, typed \"ABCDEFGH\" | \"ABCDE\" |
| MaxLength=5, Text = \"ABCDEFGH\" from code | \"ABCDEFGH\" |
| MaxLength=5, Text bound to a source set to \"ABCDEFGH\" | \"ABCDEFGH\" |
| CharacterCasing=Upper, typed \"hello\" | \"HELLO\" |
| CharacterCasing=Upper, Text = \"hello\" from code | \"hello\" |
| IsReadOnly=True, typed \"xyz\" after \"abc\" | \"abc\" |
| IsReadOnly=True, SelectAll(): SelectedText | \"abc\" |
| AcceptsReturn=False, Enter between \"a\" and \"b\" | \"ab\" |
| AcceptsReturn=True, Enter between \"a\" and \"b\" | \"a\\r\\nb\" |
| Default: \"abc\" typed: source writes while typing; after focus leaves | 0 ((empty)); 1 (abc) |
| PropertyChanged: \"abc\" typed: source writes while typing; after focus leaves | 3 (abc); 3 (abc) |
| IsReadOnly: text after typing \"x\"; all selected: Copy / Cut / Paste can execute | error details; True / False / False |

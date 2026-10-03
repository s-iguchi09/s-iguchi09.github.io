| where the XAML is loaded | result |
|---|---|
| XamlReader, no DataContext | XamlParseException (inner InvalidOperationException) |
| DataTemplate, DataContext holds a valid path | shown: 64x48, UriSource = current ImagePath |
| then ImagePath changed to a 64x96 image | shown: 64x48, UriSource = earlier path |

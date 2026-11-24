| XAML | 結果 |
|---|---|
| Converter=&#123;StaticResource StringParameterBoolToVisibility&#125;, ConverterParameter=True | 例外なし。IsBusy=True → Collapsed、IsBusy=False → Visible |
| Converter=&#123;StaticResource StringParameterBoolToVisibility&#125;, ConverterParameter=Ture | 例外なし。IsBusy=True → Visible、IsBusy=False → Collapsed |
| &lt;local:BoolToVisibilityConverter x:Key=\"BoolToHidden\" FalseValue=\"Hidden\" /&gt; | XamlReader.Parse で例外なし |
| &lt;local:BoolToVisibilityConverter x:Key=\"BoolToHidden\" FalseValue=\"Hiden\" /&gt; | XamlReader.Parse で XamlParseException |

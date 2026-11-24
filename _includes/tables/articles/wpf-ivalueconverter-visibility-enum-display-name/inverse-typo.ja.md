| XAML | 結果 |
|---|---|
| Converter=&#123;StaticResource StringParameterBoolToVisibility&#125;, ConverterParameter=True | 例外なし。True → Collapsed、False → Visible |
| Converter=&#123;StaticResource StringParameterBoolToVisibility&#125;, ConverterParameter=Ture | 例外なし。True → Visible、False → Collapsed |
| &lt;local:BoolToVisibilityConverter x:Key=\"BoolToHidden\" FalseValue=\"Hiden\" /&gt; | XamlReader.Parse で XamlParseException |

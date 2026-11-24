| XAML | result |
|---|---|
| Converter=&#123;StaticResource StringParameterBoolToVisibility&#125;, ConverterParameter=True | no exception; True → Collapsed, False → Visible |
| Converter=&#123;StaticResource StringParameterBoolToVisibility&#125;, ConverterParameter=Ture | no exception; True → Visible, False → Collapsed |
| &lt;local:BoolToVisibilityConverter x:Key=\"BoolToHidden\" FalseValue=\"Hiden\" /&gt; | XamlParseException from XamlReader.Parse |

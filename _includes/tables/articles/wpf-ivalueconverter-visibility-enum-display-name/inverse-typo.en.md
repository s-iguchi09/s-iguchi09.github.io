| XAML | result |
|---|---|
| Converter=&#123;StaticResource StringParameterBoolToVisibility&#125;, ConverterParameter=True | no exception; IsBusy=True → Collapsed, IsBusy=False → Visible |
| Converter=&#123;StaticResource StringParameterBoolToVisibility&#125;, ConverterParameter=Ture | no exception; IsBusy=True → Visible, IsBusy=False → Collapsed |
| &lt;local:BoolToVisibilityConverter x:Key=\"BoolToHidden\" FalseValue=\"Hidden\" /&gt; | no exception from XamlReader.Parse |
| &lt;local:BoolToVisibilityConverter x:Key=\"BoolToHidden\" FalseValue=\"Hiden\" /&gt; | XamlParseException from XamlReader.Parse |

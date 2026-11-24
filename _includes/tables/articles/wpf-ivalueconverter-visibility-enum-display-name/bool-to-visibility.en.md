| binding | IsBusy = True | IsBusy = False | source null | source \"True\" (string) |
|---|---|---|---|---|
| Converter=&#123;StaticResource BooleanToVisibility&#125; | Visible | Collapsed | Collapsed | Collapsed |
| Converter=&#123;StaticResource BooleanToVisibility&#125;, ConverterParameter=&#123;StaticResource True&#125; | Visible | Collapsed | Collapsed | Collapsed |
| Converter=&#123;StaticResource BoolToVisibility&#125; | Visible | Collapsed | Collapsed | Collapsed |
| Converter=&#123;StaticResource InverseBoolToVisibility&#125; | Collapsed | Visible | Visible | Visible |
| Converter=&#123;StaticResource BoolToHidden&#125; | Visible | Hidden | Hidden | Hidden |
| Converter=&#123;StaticResource ParameterBoolToVisibility&#125;, ConverterParameter=True | Visible | Collapsed | Collapsed | Collapsed |
| Converter=&#123;StaticResource ParameterBoolToVisibility&#125;, ConverterParameter=&#123;StaticResource True&#125; | Collapsed | Visible | Visible | Visible |
| Converter=&#123;StaticResource StringParameterBoolToVisibility&#125;, ConverterParameter=True | Collapsed | Visible | Visible | Visible |

| placement of DataTemplate.Triggers | result |
|---|---|
| inside &lt;Grid&gt; | loading succeeds; showing the grid throws XamlParseException |
| under &lt;DataTemplate&gt;, after &lt;Grid&gt; | before BeginEdit: TextBlock Visible, TextBox Collapsed; after: TextBlock Collapsed, TextBox Visible |
| under &lt;DataTemplate&gt;, before &lt;Grid&gt; | loading the template: NullReferenceException |
| under &lt;DataTemplate&gt;, before &lt;Grid&gt; (dotnet build) | build fails: MC4111 (target \'display\' must come before its Setter) |

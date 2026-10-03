| DataTemplate.Triggers の置き場所 | 結果 |
|---|---|
| &lt;Grid&gt; の中 | 読み込みは通り、グリッドの表示で XamlParseException |
| &lt;DataTemplate&gt; の直下、&lt;Grid&gt; の後 | BeginEdit の前: TextBlock Visible, TextBox Collapsed、後: TextBlock Collapsed, TextBox Visible |
| &lt;DataTemplate&gt; の直下、&lt;Grid&gt; の前 | テンプレートの読み込みで NullReferenceException |
| &lt;DataTemplate&gt; の直下、&lt;Grid&gt; の前（dotnet build） | ビルドが失敗: MC4111（対象の \'display\' が Setter より前に要る） |

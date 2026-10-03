| 条件 | 計測値 |
|---|---|
| 呼び出し側で Title=&#123;Binding HeaderText&#125;、内側で &#123;Binding Title&#125; | Title from PageViewModel、内側 （空）、Error 40 が 1 件 |
| 呼び出し側の ViewModel にも Title がある | Title VM-TITLE、内側 VM-OWN-TITLE、エラー 0 件 |
| 親に DataContext なし、Title を設定 | 内側 （空）、エラー 0 件 |
| DataContext を内側の Grid に引き継ぐ | 内側の DataContext は InfoCard、カードの DataContext は PageViewModel、エラー 0 件 |
| 引き継いだうえで、AncestorType 経由の DataContext.HeaderText | from PageViewModel、エラー 0 件 |
| BindsTwoWayByDefault、内側で xyz と入力 | HeaderText xyz、エラー 0 件 |
| DataContext = this、呼び出し側が Title をバインド | Title (unset)、Error 40 が 1 件 |
| DataContext = this、呼び出し側の DataContext に Title が無い | 内側 （空）、Error 40 が 1 件 |
| DataContext = this、呼び出し側の DataContext に Title がある | 内側 from Detail.Title、エラー 0 件 |
| 呼び出し側は OneWay、内側で Title = x | バインドは外れる、HeaderText=new -&gt; Title x、エラー 0 件 |
| 呼び出し側は OneWay、内側で TwoWay の書き戻し | バインドは外れる、HeaderText=new -&gt; Title x、エラー 0 件 |
| 呼び出し側は OneWay、内側で SetCurrentValue | バインドは残る、HeaderText=new -&gt; Title new、エラー 0 件 |
| 呼び出し側も要素に Root という名前を付ける | 内側 -&gt; the card、呼び出し側 -&gt; caller\'s Root、エラー 0 件 |

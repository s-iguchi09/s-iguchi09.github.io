| case | measured |
|---|---|
| caller Title=&#123;Binding HeaderText&#125;, inner &#123;Binding Title&#125; | Title from PageViewModel, inner (empty); Error 40 x1 |
| caller\'s view model also has Title | Title VM-TITLE, inner VM-OWN-TITLE; 0 errors |
| parent without DataContext, Title set | inner (empty); 0 errors |
| DataContext delegated to the inner Grid | inner sees InfoCard, card keeps PageViewModel; 0 errors |
| delegated, DataContext.HeaderText via AncestorType | from PageViewModel; 0 errors |
| BindsTwoWayByDefault, type xyz inside | HeaderText xyz; 0 errors |
| DataContext = this, caller binds Title | Title (unset); Error 40 x1 |
| DataContext = this, caller\'s DataContext lacks Title | inner (empty); Error 40 x1 |
| DataContext = this, caller\'s DataContext has Title | inner from Detail.Title; 0 errors |
| caller OneWay, inside Title = x | binding removed; HeaderText=new -&gt; Title x; 0 errors |
| caller OneWay, inside inner TwoWay write-back | binding removed; HeaderText=new -&gt; Title x; 0 errors |
| caller OneWay, inside SetCurrentValue | binding kept; HeaderText=new -&gt; Title new; 0 errors |
| caller also names an element Root | inside -&gt; the card, caller -&gt; caller\'s Root; 0 errors |

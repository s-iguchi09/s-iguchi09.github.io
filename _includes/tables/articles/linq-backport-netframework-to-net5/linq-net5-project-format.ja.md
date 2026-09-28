| プロジェクトの形式 | シンボル | ポリフィル | ビルドの結果 |
|---|---|---|---|
| SDK 形式、&lt;TargetFramework&gt;net48&lt;/TargetFramework&gt; | NET471\_OR\_GREATER が定義される | 除外される | ビルド成功 |
| SDK 形式 + &lt;DisableImplicitFrameworkDefines&gt;true | NET471\_OR\_GREATER が定義されない | コンパイルされる | CS0121（呼び出しがあいまい） |
| 従来形式（非 SDK）、&lt;TargetFrameworkVersion&gt;v4.8 | NET471\_OR\_GREATER が定義されない | コンパイルされる | CS0121（呼び出しがあいまい） |
| 従来形式（非 SDK）+ &lt;DefineConstants&gt;NET471\_OR\_GREATER | NET471\_OR\_GREATER が定義される | 除外される | ビルド成功 |

| project format | symbol | polyfill | build result |
|---|---|---|---|
| SDK-style, &lt;TargetFramework&gt;net48&lt;/TargetFramework&gt; | NET471\_OR\_GREATER defined | skipped | build succeeded |
| SDK-style + &lt;DisableImplicitFrameworkDefines&gt;true | NET471\_OR\_GREATER not defined | compiled in | CS0121 (ambiguous call) |
| legacy (non-SDK), &lt;TargetFrameworkVersion&gt;v4.8 | NET471\_OR\_GREATER not defined | compiled in | CS0121 (ambiguous call) |
| legacy (non-SDK) + &lt;DefineConstants&gt;NET471\_OR\_GREATER | NET471\_OR\_GREATER defined | skipped | build succeeded |

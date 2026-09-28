|  | net48 | 不足する型 | ポリフィルを足すと |
|---|---|---|---|
| ??= | OK | - | - |
| ! | OK | - | - |
| new() | OK | - | - |
| \[1, 2, 3\] | OK | - | - |
| C(string p) | OK | - | - |
| a\[^1\] | NG | Index | OK |
| a\[1\.\.3\] | NG | Range +1 | OK |
| &#123; get; init; &#125; | NG | IsExternalInit | OK |
| struct s with &#123; &#125; | OK | - | - |
| record struct s with &#123; &#125; | OK | - | - |
| readonly record struct s with &#123; &#125; | NG | IsExternalInit | OK |
| record r with &#123; &#125; | NG | IsExternalInit | OK |
| required + init | NG | IsExternalInit +2 | OK |
| required + set | NG | RequiredMemberAttribute +1 | OK |
| record with required | NG | IsExternalInit +3 | NG |
| + SetsRequiredMembersAttribute | NG | IsExternalInit +3 | OK |
| \[SetsRequiredMembers\] ctor | NG | IsExternalInit +3 | NG |
| + SetsRequiredMembersAttribute | NG | IsExternalInit +3 | OK |

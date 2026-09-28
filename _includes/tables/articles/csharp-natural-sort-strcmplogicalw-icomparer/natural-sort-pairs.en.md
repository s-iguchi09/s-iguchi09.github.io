| pair | StrCmpLogicalW | CompareOrdinal |
|---|---|---|
| \"item2\" vs \"item10\" | -1 (left first) | +1 (right first) |
| \"item10\" vs \"item2\" | +1 (right first) | -1 (left first) |
| \"item2\" vs \"item2\" | 0 (equal) | 0 (equal) |
| \"Item2\" vs \"item2\" | 0 (equal) | -1 (left first) |
| \"item02\" vs \"item2\" | -1 (left first) | -1 (left first) |
| \"a\" vs \"B\" | -1 (left first) | +1 (right first) |

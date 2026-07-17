---
layout: article-en
title: "Expressing SQL Outer Joins in LINQ — Implementing LeftJoin, RightJoin and Shuffle"
date: 2026-07-16
category: C#
excerpt: "Implementing LeftJoin, RightJoin and Shuffle on .NET Framework, mapping them to SQL outer joins and covering the IQueryable translation pitfall."
---

## Overview

An outer join that SQL writes with a single `LEFT JOIN` clause required composing three methods in LINQ — `GroupJoin`, `SelectMany` and `DefaultIfEmpty` — for over a decade.
.NET 10 finally closes that gap by adding `LeftJoin` and `RightJoin` as first-class operators.
Alongside them, random reordering — long imitated with the `OrderBy(_ => Guid.NewGuid())` pseudo-idiom — was standardized as `Shuffle`.

Starting from the correspondence between SQL join clauses and LINQ idioms, this article implements polyfills that make the three operators available on .NET Framework.
It then digs into a concern unique to this backport, born from the fact that outer joins live next door to database queries: applying the polyfill to `IQueryable<T>` silently breaks query translation.

---

## Prerequisites / Environment

- Frameworks: .NET Framework 4.8 (backport target) / .NET 10+ (future migration target)
- APIs: LINQ `LeftJoin` (2 signatures), `RightJoin` (2 signatures), `Shuffle` (1 signature)
- Approach: apply `#nullable enable`; disable automatically on migration via `#if !NET10_0_OR_GREATER`
- Language version: nullable annotations on unconstrained type parameters (`TInner?` / `TOuter?`) require `LangVersion` 9.0 or later (recommended: `latest`). No other project configuration changes

---

## How SQL Join Clauses Map to LINQ Idioms

The three operators added in .NET 10 are the following.

| Method | Added in | Corresponding operation |
| --- | --- | --- |
| `LeftJoin<TOuter, TInner, TKey, TResult>` | .NET 10.0 | SQL `LEFT OUTER JOIN` (keeps every outer element) |
| `RightJoin<TOuter, TInner, TKey, TResult>` | .NET 10.0 | SQL `RIGHT OUTER JOIN` (keeps every inner element) |
| `Shuffle<TSource>` | .NET 10.0 | Random reordering |

LINQ before .NET 10 has no dedicated outer-join operator — only `Join` (inner join).
An operation that SQL expresses in one clause mapped to this:

```sql
-- SQL: keep employees with no matching department
SELECT e.Name, d.DeptName
FROM Employee e
LEFT JOIN Department d ON e.DeptId = d.DeptId
```

```csharp
// LINQ (.NET 9 and earlier): composing GroupJoin + SelectMany + DefaultIfEmpty
var result = employees
    .GroupJoin(departments, e => e.DeptId, d => d.DeptId, (e, ds) => new { e, ds })
    .SelectMany(g => g.ds.DefaultIfEmpty(), (g, d) => new { g.e.Name, d?.DeptName });
```

The composed idiom buries the intent — "outer join" — in structure, and misplacing `SelectMany` or `DefaultIfEmpty` quietly turns it into an inner or cross join.
Random ordering has the same shape of problem: `OrderBy(_ => Guid.NewGuid())` generates a key per element, pays for a full sort, and offers no uniformity guarantee as a shuffle.

---

## Implementation

The following is the complete polyfill for `LeftJoin` (2 signatures), `RightJoin` (2 signatures) and `Shuffle` (1 signature).
`LeftJoin` / `RightJoin` embed the composition idiom above in the same shape as the built-ins, and `Shuffle` buffers the source into an array before applying a Fisher–Yates shuffle.
The reasons for placing the polyfill in `System.Linq` and for the migration guard are covered in the [series foundation article](/articles/linq-backport-netframework-to-net5/).
Add it to the project as, for example, `LinqExtensions.Net10.cs`.

```csharp
#nullable enable

using System;
using System.Collections.Generic;

#if !NET10_0_OR_GREATER // Active only below .NET 10.0 (e.g. .NET Framework)

namespace System.Linq
{
    /// <summary>
    /// Provides extension methods that backfill LINQ methods introduced in .NET 10.0 for older target frameworks.
    /// </summary>
    public static partial class LinqExtensions
    {
        // ==========================================
        // 1. LeftJoin (left outer join)
        // ==========================================
        public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(
            this IEnumerable<TOuter> outer,
            IEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector,
            Func<TInner, TKey> innerKeySelector,
            Func<TOuter, TInner?, TResult> resultSelector)
            => outer.LeftJoin(inner, outerKeySelector, innerKeySelector, resultSelector, comparer: null);

        public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(
            this IEnumerable<TOuter> outer,
            IEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector,
            Func<TInner, TKey> innerKeySelector,
            Func<TOuter, TInner?, TResult> resultSelector,
            IEqualityComparer<TKey>? comparer)
        {
            if (outer == null) throw new ArgumentNullException(nameof(outer));
            if (inner == null) throw new ArgumentNullException(nameof(inner));
            if (outerKeySelector == null) throw new ArgumentNullException(nameof(outerKeySelector));
            if (innerKeySelector == null) throw new ArgumentNullException(nameof(innerKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            // Group matching inner elements per outer element; supply default(TInner) when none match.
            return outer
                .GroupJoin(inner, outerKeySelector, innerKeySelector, (o, inners) => new { o, inners }, comparer)
                .SelectMany(g => g.inners.DefaultIfEmpty(), (g, i) => resultSelector(g.o, i));
        }

        // ==========================================
        // 2. RightJoin (right outer join)
        // ==========================================
        public static IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult>(
            this IEnumerable<TOuter> outer,
            IEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector,
            Func<TInner, TKey> innerKeySelector,
            Func<TOuter?, TInner, TResult> resultSelector)
            => outer.RightJoin(inner, outerKeySelector, innerKeySelector, resultSelector, comparer: null);

        public static IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult>(
            this IEnumerable<TOuter> outer,
            IEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector,
            Func<TInner, TKey> innerKeySelector,
            Func<TOuter?, TInner, TResult> resultSelector,
            IEqualityComparer<TKey>? comparer)
        {
            if (outer == null) throw new ArgumentNullException(nameof(outer));
            if (inner == null) throw new ArgumentNullException(nameof(inner));
            if (outerKeySelector == null) throw new ArgumentNullException(nameof(outerKeySelector));
            if (innerKeySelector == null) throw new ArgumentNullException(nameof(innerKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            // GroupJoin pivoted on the inner sequence; supply default(TOuter) when none match.
            return inner
                .GroupJoin(outer, innerKeySelector, outerKeySelector, (i, outers) => new { i, outers }, comparer)
                .SelectMany(g => g.outers.DefaultIfEmpty(), (g, o) => resultSelector(o, g.i));
        }

        // ==========================================
        // 3. Shuffle (random reordering)
        // ==========================================
        public static IEnumerable<TSource> Shuffle<TSource>(this IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return ShuffleIterator(source);
        }

        private static IEnumerable<TSource> ShuffleIterator<TSource>(IEnumerable<TSource> source)
        {
            var buffer = source.ToArray();

            // Fisher–Yates: swap each position from the tail with an undecided element.
            for (int i = buffer.Length - 1; i > 0; i--)
            {
                int j = SharedRandom.Next(i + 1);
                if (j != i)
                {
                    (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
                }
            }

            foreach (var item in buffer)
            {
                yield return item;
            }
        }

#if NET6_0_OR_GREATER
        // .NET 6+ provides a thread-safe shared instance.
        private static Random SharedRandom => Random.Shared;
#else
        // .NET Framework has no Random.Shared, so keep one instance per thread.
        [ThreadStatic]
        private static Random? _threadRandom;
        private static Random SharedRandom => _threadRandom ??= new Random();
#endif
    }
}

#endif
```

The result selectors of `LeftJoin` / `RightJoin` carry the same nullable annotations as the built-ins — `TInner?` for `LeftJoin`, `TOuter?` for `RightJoin`.
The signature itself thus documents which side can be missing, and nullable analysis agrees before and after migration.
`Shuffle`'s random source branches further on a nested `#if NET6_0_OR_GREATER`: where `Random.Shared` is unavailable, a `[ThreadStatic]` instance provides thread safety.

---

## Using `LeftJoin` / `RightJoin`

### `LeftJoin`: Keep Every Outer (Left) Element

For outer elements with no matching inner element, the result selector receives `default(TInner)` (`null` for reference types) as its second argument.

```csharp
var employees = new[]
{
    new { Name = "Sato",      DeptId = 10 },
    new { Name = "Suzuki",    DeptId = 20 },
    new { Name = "Takahashi", DeptId = 99 }, // No matching department
};

var departments = new[]
{
    new { DeptId = 10, DeptName = "Sales" },
    new { DeptId = 20, DeptName = "Engineering" },
};

var result = employees.LeftJoin(
    departments,
    e => e.DeptId,
    d => d.DeptId,
    (e, d) => $"{e.Name}: {d?.DeptName ?? "(unassigned)"}");
// Sato: Sales
// Suzuki: Engineering
// Takahashi: (unassigned)
```

Matching SQL's `LEFT JOIN ... ON e.DeptId = d.DeptId`, the unmatched "Takahashi" stays in the output.
The second selector argument `d` is nullable and must be null-checked before use.

### `RightJoin`: Keep Every Inner (Right) Element

For inner elements with no matching outer element, the result selector receives `default(TOuter)` as its first argument.

```csharp
var employees = new[]
{
    new { Name = "Sato",   DeptId = 10 },
    new { Name = "Suzuki", DeptId = 20 },
};

var departments = new[]
{
    new { DeptId = 10, DeptName = "Sales" },
    new { DeptId = 20, DeptName = "Engineering" },
    new { DeptId = 30, DeptName = "General Affairs" }, // No employees assigned
};

var result = employees.RightJoin(
    departments,
    e => e.DeptId,
    d => d.DeptId,
    (e, d) => $"{d.DeptName}: {e?.Name ?? "(vacant)"}");
// Sales: Sato
// Engineering: Suzuki
// General Affairs: (vacant)
```

`RightJoin(outer, inner, ...)` preserves every element of `inner`, symmetric to `LeftJoin`; the implementation simply pivots the `GroupJoin` onto the inner sequence.

---

## `Shuffle` versus Pseudo-Shuffles

Random ordering via `OrderBy(_ => Guid.NewGuid())` is widespread but carries two problems.
It generates a GUID per element and pays for an $O(n \log n)$ sort, and the distribution of generated GUIDs as sort keys guarantees no uniformity of the resulting permutation.

`Shuffle` uses Fisher–Yates, producing each permutation with equal probability in a single $O(n)$ pass.

```csharp
var deck = Enumerable.Range(1, 52);

var shuffled = deck.Shuffle().ToArray();
// e.g. [17, 3, 50, 28, ...] (differs per call)
```

`Shuffle` is deferred, but like `OrderBy` it buffers the entire source before yielding the first element.

```csharp
var query = Enumerable.Range(1, 3).Shuffle();

var first = query.ToArray();  // Source is enumerated and shuffled here
var second = query.ToArray(); // Re-enumerating yields a different order
```

Because deferred queries re-shuffle on every enumeration, materialize once with `ToArray` / `ToList` when a fixed order is needed.
The randomness is non-cryptographic; for lotteries or anything requiring unpredictability, use `System.Security.Cryptography` randomness instead.

---

## The `IQueryable<T>` Pitfall

Outer joins live next door to database queries, which gives this polyfill a risk the other backports do not have.
It extends `Enumerable` (`IEnumerable<T>`), but `IQueryable<T>` inherits `IEnumerable<T>`, so the compiler happily applies it to Entity Framework queries too.

What happens then is not a runtime error but **silent performance degradation**.
With no `Queryable` counterpart available, the `Enumerable` polyfill binds, and the join is never translated to SQL — it executes client-side.
Entire tables are transferred and joined in memory, and nothing looks wrong until data volume grows.

For server-side outer joins in database queries below .NET 10, keep writing the provider-translatable `GroupJoin(...).SelectMany(..., DefaultIfEmpty())` form.
`AsEnumerable` only draws the client-evaluation boundary; it does not keep the join on the server.
.NET 10 does add `LeftJoin` / `RightJoin` to `Queryable` as well, but their translatability depends on the provider, and this article's scope is `Enumerable` only.

---

## Migration Guard

The polyfill is wrapped in `#if !NET10_0_OR_GREATER`.
`LeftJoin`, `RightJoin` and `Shuffle` do not exist before .NET 10, so guarding with `!NETCOREAPP` or `!NET9_0_OR_GREATER` would disable the polyfill in .NET 8 / .NET 9 builds and break compilation.
The general rule — disable at and above the version that introduced the methods — is laid out in the [.NET 6 backport article](/articles/linq-backport-netframework-to-net6/).

---

## Caveats

- **Join direction**: `LeftJoin` preserves every element of the first argument (`outer`); `RightJoin` preserves every element of the second (`inner`). The nullable selector argument is the inner element for `LeftJoin` and the outer element for `RightJoin` — null-check before use.
- **Key equality**: the comparer-free overloads use `EqualityComparer<TKey>.Default`. Pass an `IEqualityComparer<TKey>` for case-insensitive joins and the like. Delegation from the smaller overloads pins resolution with the named argument `comparer:` (the same technique used in the [ToDictionary backport](/articles/linq-backport-netframework-to-net8/)).
- **`Shuffle` cannot handle infinite sequences**: the entire source is buffered at enumeration start, so an unbounded sequence never completes.
- **Compile with C# 9 or later**: the nullable annotations on unconstrained type parameters (`TInner?` / `TOuter?`) fail with errors such as `CS8627` under the .NET Framework 4.8 default `LangVersion` (7.3). Set `<LangVersion>9.0</LangVersion>` (or `latest`) in the `.csproj`.
- **No name collisions**: these signatures do not exist in .NET Framework, and they differ from `Join` and `OrderBy` in name and parameters, so overload resolution is unaffected.

---

## Alternatives / Comparison

| Approach | Pros | Cons | Best for |
| --- | --- | --- | --- |
| Hand-rolled polyfill (this article) | No dependency; the name states the SQL-equivalent intent | The `IQueryable` misapplication risk must be managed | Mostly in-memory joins and shuffles |
| Write `GroupJoin` + `DefaultIfEmpty` inline | No extra code; translates under `IQueryable` too | Verbose; easy to get subtly wrong | Outer joins in database queries |
| Substitute `OrderBy(_ => Guid.NewGuid())` | No extra code | Inefficient; no uniformity guarantee | Small collections where rigor is irrelevant |
| Adopt MoreLINQ or similar | Implemented and tested | External dependency; API differs from the built-ins | Projects already accepting the dependency |
| Upgrade to .NET 10 | Root fix; `Queryable` versions available | Migration cost | When migration is feasible |

Until a .NET 10 migration, the pragmatic split is: polyfill for in-memory collections, classic `GroupJoin` idiom for database queries.

---

## Summary

.NET 10's `LeftJoin`, `RightJoin` and `Shuffle` promote operations that were standard in SQL — or imitated through pseudo-idioms — to first-class LINQ operators.
The backport rests on three points.

- `LeftJoin` / `RightJoin` embed the classic `GroupJoin` + `SelectMany` + `DefaultIfEmpty` idiom and use nullable annotations to state which side can be missing
- `Shuffle` performs a uniform Fisher–Yates shuffle, fixing both the inefficiency and the bias of `Guid.NewGuid()` sorting
- The polyfill is `Enumerable`-only; applied to an `IQueryable<T>` database query it falls back to client evaluation — keep the classic idiom for database queries

| Method | Side preserved | Nullable selector argument | Evaluation |
| --- | --- | --- | --- |
| `LeftJoin` | Outer (left) | Inner element `TInner?` | Deferred |
| `RightJoin` | Inner (right) | Outer element `TOuter?` | Deferred |
| `Shuffle` | — | — | Deferred (full buffering at enumeration) |

---

## Related Articles

- [Designing LINQ Polyfills That Preserve Lazy Evaluation — Implementing Append, Prepend, TakeLast and SkipLast](/articles/linq-backport-netframework-to-net5/)
- [Replacing GroupBy and Full-Sort Workarounds — Implementing Chunk, MaxBy, MinBy and DistinctBy](/articles/linq-backport-netframework-to-net6/)
- [Order and OrderDescending by Pure Delegation — A Minimal Polyfill with IOrderedEnumerable Compatibility](/articles/linq-backport-netframework-to-net7/)
- [Selector-Free ToDictionary — Designing for Overload Resolution and the notnull Constraint](/articles/linq-backport-netframework-to-net8/)
- [Key-Based Aggregation Without GroupBy — Dictionary-Backed CountBy, AggregateBy and Index](/articles/linq-backport-netframework-to-net9/)

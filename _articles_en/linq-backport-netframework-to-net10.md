---
layout: article-en
title: "Expressing SQL Outer Joins in LINQ — Implementing LeftJoin, RightJoin and Shuffle"
seo_title: "SQL Outer Joins in LINQ: LeftJoin, RightJoin and Shuffle"
date: 2026-07-16
category: C#
excerpt: "Implementing LeftJoin, RightJoin and Shuffle on .NET Framework, mapping them to SQL outer joins and covering the IQueryable translation pitfall."
image: /images/articles/linq-backport-netframework-to-net10/linq-leftjoin-rightjoin-shuffle.png
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
- Verification environment: .NET 10 / Windows 11

The polyfill implementation in this article was built and run for both `net48` and `net10.0` in the environment above, and the outputs were compared.
On `net10.0` the migration `#if` guard disables the polyfill, so the BCL implementation is used.
The following points were confirmed in that environment:

- The default value that `LeftJoin` / `RightJoin` pass for a row with no counterpart matches on both targets.
- `Shuffle` returns a permutation of the original sequence (compared after sorting, since the order is random).

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

<figure class="article-figure">
  <img src="/images/articles/linq-backport-netframework-to-net10/linq-leftjoin-rightjoin-shuffle.png" alt="Results of LeftJoin and RightJoin over two sequences. LeftJoin yields the default value, shown as null, for the missing right side, RightJoin does the same for the missing left side, and Shuffle reorders the elements." width="448" height="218" loading="lazy">
  <figcaption>Evaluation results when the two sequences contain non-matching keys. <code>LeftJoin</code> keeps the left side and fills the counterpart with its default value; <code>RightJoin</code> does the reverse. The figure uses value tuples, so the missing side is <code>default</code> rather than <code>null</code>, and is printed as null. <code>Shuffle</code> randomizes the order, so that row shows the result of a single run.</figcaption>
</figure>

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
        // new Random() is seeded from the clock there, so threads that start together would get
        // the same seed and the same order. Take each thread's seed from one locked instance instead.
        private static readonly Random SeedSource = new Random();

        [ThreadStatic]
        private static Random? _threadRandom;

        private static Random SharedRandom
        {
            get
            {
                if (_threadRandom == null)
                {
                    int seed;
                    lock (SeedSource)
                    {
                        seed = SeedSource.Next();
                    }

                    _threadRandom = new Random(seed);
                }

                return _threadRandom;
            }
        }
#endif
    }
}

#endif
```

Whether this implementation returns what the standard LINQ returns can be checked by building the same calling code for `net48` (polyfill active) and for `net10.0` (built-in active), running both, and comparing the output.

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/linq-backport-netframework-to-net10/linq-net10-polyfill-parity.svg" alt="A table comparing the output of the same calling code run against the net48 polyfill and the net10.0 built-in. LeftJoin, RightJoin, and Shuffle all produce identical results, boundary cases included. Eight threads started together also get eight different Shuffle orders on both." width="1078" height="320" loading="lazy">
  <figcaption>The implementation above, built as-is for <code>net48</code> and built for <code>net10.0</code> where <code>#if</code> switches it to the built-in, run through one and the same driver. Measured with .NET SDK 10.0.302.</figcaption>
</figure>

`Shuffle` draws on randomness, so its output is re-sorted before the comparison. The two sides also agree on the default value handed to a row that has no counterpart.

The result selectors of `LeftJoin` / `RightJoin` carry the same nullable annotations as the built-ins — `TInner?` for `LeftJoin`, `TOuter?` for `RightJoin`.
The signature itself thus documents which side can be missing, and nullable analysis agrees before and after migration.
`Shuffle`'s random source branches further on a nested `#if NET6_0_OR_GREATER`: where `Random.Shared` is unavailable, a `[ThreadStatic]` instance provides thread safety. On .NET Framework, `new Random()` takes its seed from the clock, as the [`Random` constructor reference](https://learn.microsoft.com/dotnet/api/system.random.-ctor) notes, so instances created at the same moment produce the same numbers. With a plain `new Random()` per thread, eight threads started together returned one and the same order; seeding each thread from one locked instance, as above, gave eight different orders (the `8 threads` row of the table).

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
It generates a GUID per element and pays for an $O(n \log n)$ sort, and GUIDs are not specified as a source of uniformly random sort keys, so nothing guarantees a uniform permutation.

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
var second = query.ToArray(); // Re-enumerating can yield a different order
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
`LeftJoin`, `RightJoin` and `Shuffle` do not exist before .NET 10, so the wrong guard breaks compilation. `!NETCOREAPP` disables the polyfill on every target where `NETCOREAPP` is defined (.NET Core and .NET 5–9), whereas `!NET9_0_OR_GREATER` disables it only on `net9.0` and later (on .NET 8 the `NET9_0_OR_GREATER` symbol is undefined, so the polyfill stays active; on .NET 9 it is disabled and fails to compile). Both switch the polyfill off where the operators are missing, so the correct guard is `#if !NET10_0_OR_GREATER`.
The general rule — disable at and above the version that introduced the methods — is laid out in the [.NET 6 backport article](/articles/linq-backport-netframework-to-net6/).

---

## Caveats

- **Join direction**: `LeftJoin` preserves every element of the first argument (`outer`); `RightJoin` preserves every element of the second (`inner`). The nullable selector argument is the inner element for `LeftJoin` and the outer element for `RightJoin` — null-check before use.
- **Key equality**: the comparer-free overloads use `EqualityComparer<TKey>.Default`. Pass an `IEqualityComparer<TKey>` for case-insensitive joins and the like. Delegation from the smaller overloads pins resolution with the named argument `comparer:` (the same technique used in the [ToDictionary backport](/articles/linq-backport-netframework-to-net8/)).
- **`Shuffle` cannot handle infinite sequences**: the entire source is buffered at enumeration start, so an unbounded sequence never completes.
- **Compile with C# 9 or later**: the nullable annotations on unconstrained type parameters (`TInner?` / `TOuter?`) fail under the .NET Framework 4.8 default `LangVersion` (7.3) with `CS8370` and `CS8627`, and `LangVersion` 8.0 still reports `CS8627`. Set `<LangVersion>9.0</LangVersion>` (or `latest`) in the `.csproj`.
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
- `Shuffle` performs a Fisher–Yates shuffle in one pass, replacing the inefficient `Guid.NewGuid()` sort, whose uniformity nothing guarantees; on .NET Framework, seed each thread's `Random` separately
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

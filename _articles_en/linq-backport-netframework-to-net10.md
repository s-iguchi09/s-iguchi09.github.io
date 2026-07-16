---
layout: article-en
title: "Backporting Missing LINQ Methods from .NET 10 to .NET Framework"
date: 2026-07-17
category: C#
excerpt: "Backporting the .NET 10 LeftJoin, RightJoin, and Shuffle operators to .NET Framework with #nullable enable and conditional compilation."
---

## Overview

When migrating from .NET Framework to modern .NET incrementally, or when a codebase must remain on .NET Framework for the foreseeable future, a persistent source of friction is the set of LINQ methods that exist in modern .NET but not in .NET Framework.

This article covers the **three LINQ operators added in .NET 10** (the outer-join operators `LeftJoin` and `RightJoin`, and the random-ordering operator `Shuffle`) and explains how to implement them as extension methods (polyfills) that behave identically to the originals.
It also covers a modern implementation using `#nullable enable` and a conditional-compilation technique that eliminates migration cost when eventually upgrading to .NET 10 or later.

---

## Prerequisites / Environment

- Frameworks: .NET Framework 4.8 / .NET 10+
- APIs: `LeftJoin` (two signatures), `RightJoin` (two signatures), and `Shuffle` (one signature)
- Nullable context: `#nullable enable`
- Migration guard: `#if !NET10_0_OR_GREATER`
- Language version: because the polyfill uses nullable annotations on unconstrained type parameters (`TInner?` / `TOuter?`), set `LangVersion` to 9.0 or later (recommended: `latest`). The target framework and other project settings are left unchanged.

---

## Problem

.NET 10 adds practical operators to the public `Enumerable` class for the first time in several releases.
The additions are the outer-join operators `LeftJoin` and `RightJoin`, and the random-ordering operator `Shuffle`, and these are unavailable in .NET Framework environments.

| Method | Added in | Description |
| --- | --- | --- |
| `LeftJoin<TOuter, TInner, TKey, TResult>` | .NET 10.0 | Performs a left outer equijoin that keeps every element of the outer sequence |
| `RightJoin<TOuter, TInner, TKey, TResult>` | .NET 10.0 | Performs a right outer equijoin that keeps every element of the inner sequence |
| `Shuffle<TSource>` | .NET 10.0 | Reorders the elements of a sequence into a random order |

Both `LeftJoin` and `RightJoin` also have an overload that accepts an `IEqualityComparer<TKey>`.

Without these methods, .NET Framework environments require boilerplate workarounds to produce the same results.

- A left outer join is expressed by combining `GroupJoin` with `SelectMany` and `DefaultIfEmpty`.
- Random ordering is approximated with a pseudo-idiom such as `OrderBy(_ => Guid.NewGuid())`.

The former is boilerplate that is not the essence of the join, and a mistake in it produces an unintended join result.
The latter does not guarantee a stable uniform distribution for the element count, and it is inefficient because it generates a key per element and sorts.

---

## Background

Each release of modern .NET has added operators to the public `Enumerable` class incrementally.
.NET 6 added `Chunk`, `MaxBy`, `MinBy`, and `DistinctBy`; .NET 7 added `Order` and `OrderDescending`; .NET 8 added the selector-free `ToDictionary` overloads; and .NET 9 added `CountBy`, `AggregateBy`, and `Index`.

.NET 10 standardizes the outer-join operators `LeftJoin` and `RightJoin`, long absent from standard LINQ, for the first time.
The left and right outer joins that were previously expressed by hand with `GroupJoin` and `DefaultIfEmpty` are now provided as dedicated methods corresponding to SQL `LEFT JOIN` / `RIGHT JOIN`.
`Shuffle`, which reorders a sequence into a random order, was added as well.
`Shuffle` uses a non-cryptographic random number generator and removes both the inefficiency and the distribution bias of a pseudo-shuffle via `OrderBy`.

These were first added in .NET 10.0 and are not available in any earlier runtime.

The methods added between .NET Framework 4.8 and .NET 5 (`Append`, `Prepend`, `TakeLast`, `SkipLast`) are covered in a [separate article](/articles/linq-backport-netframework-to-net5/), the four methods added in .NET 6 in [another](/articles/linq-backport-netframework-to-net6/), `Order` / `OrderDescending` from .NET 7 in [another](/articles/linq-backport-netframework-to-net7/), the `ToDictionary` overloads from .NET 8 in [another](/articles/linq-backport-netframework-to-net8/), and `CountBy` / `AggregateBy` / `Index` from .NET 9 in [another](/articles/linq-backport-netframework-to-net9/).

---

## Solution

By placing extension methods in the same namespace as the original LINQ (`System.Linq`), existing source files pick up the polyfills automatically without any changes.
Any file that already has `using System.Linq;` gains the missing methods transparently.

A `#if !NET10_0_OR_GREATER` guard ensures the implementation is skipped entirely once the project is upgraded to .NET 10 or later.
No file deletions or code rewrites are needed at migration time.

There are three key implementation details.
The first is to express `LeftJoin` and `RightJoin` with the same `GroupJoin` + `SelectMany` + `DefaultIfEmpty` composition as the originals.
The second is to match the original nullable annotation on the result-selector argument (`TInner?` for the inner element in `LeftJoin`, `TOuter?` for the outer element in `RightJoin`).
The third is to implement `Shuffle` as a deferred iterator like the original while securing a thread-safe random source even on .NET Framework, which has no `Random.Shared`.

---

## Implementation

The following is a complete polyfill for `LeftJoin` (two signatures), `RightJoin` (two signatures), and `Shuffle` (one signature).
`LeftJoin` and `RightJoin` are built on `GroupJoin` just like the originals, and `Shuffle` buffers the source into an array before reordering it with the Fisher–Yates algorithm.
Copy it into a file such as `LinqExtensions.Net10.cs` in your project.

```csharp
#nullable enable

using System;
using System.Collections.Generic;

#if !NET10_0_OR_GREATER // Active only in environments below .NET 10 (e.g. .NET Framework)

namespace System.Linq
{
    /// <summary>
    /// Backports .NET 10.0 LINQ methods to older target frameworks.
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

            // Group matching inner elements per outer element; supply one default(TInner) when none match.
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

            // Pivot on the inner sequence via GroupJoin; supply one default(TOuter) when none match.
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

            // Fisher–Yates: swap each element from the tail with a random unfixed element.
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
        // .NET 6 and later provide a thread-safe shared instance.
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

The class is active only when the `NET10_0_OR_GREATER` symbol is not defined — that is, in any runtime below .NET 10, including .NET Framework.

---

## Method Walkthroughs

### Left outer join with `LeftJoin`

A left outer join keeps every element of the outer (left) sequence and supplies a default when no matching inner (right) element exists.
For an unmatched outer element, `default(TInner)` (`null` for reference types) is passed to the second argument of the result selector.

```csharp
var employees = new[]
{
    new { Name = "Sato", DeptId = 10 },
    new { Name = "Suzuki", DeptId = 20 },
    new { Name = "Takahashi", DeptId = 99 }, // no matching department
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

"Takahashi", who has no matching department, is still present in the output, and the department name is `null`, so it can fall back to a default.
The second argument `d` of the result selector is nullable, so a `null` check is required before dereferencing it.

### Right outer join with `RightJoin`

A right outer join keeps every element of the inner (right) sequence and supplies a default when no matching outer (left) element exists.
For an unmatched inner element, `default(TOuter)` (`null` for reference types) is passed to the first argument of the result selector.

```csharp
var employees = new[]
{
    new { Name = "Sato", DeptId = 10 },
    new { Name = "Suzuki", DeptId = 20 },
};

var departments = new[]
{
    new { DeptId = 10, DeptName = "Sales" },
    new { DeptId = 20, DeptName = "Engineering" },
    new { DeptId = 30, DeptName = "General Affairs" }, // no employees
};

var result = employees.RightJoin(
    departments,
    e => e.DeptId,
    d => d.DeptId,
    (e, d) => $"{d.DeptName}: {e?.Name ?? "(no members)"}");
// Sales: Sato
// Engineering: Suzuki
// General Affairs: (no members)
```

"General Affairs", which has no members, is still present in the output, and the employee side is `null`.
`RightJoin(outer, inner, ...)` keeps every element of `inner`, making it symmetric with `LeftJoin`, and joins with the inner sequence as the axis.

### Random reordering with `Shuffle`

`Shuffle` enumerates the elements of a sequence in a random order.
It performs a uniformly distributed shuffle with the Fisher–Yates algorithm, more efficiently and without the bias of a pseudo-idiom such as `OrderBy(_ => Guid.NewGuid())`.

```csharp
var deck = Enumerable.Range(1, 52);

var shuffled = deck.Shuffle().ToArray();
// e.g. [17, 3, 50, 28, ...] (different on each call)
```

Because it reorders in a single pass over `n` elements, its cost is lower than a pseudo-shuffle that generates keys and sorts.
The random source is non-cryptographic, so do not use it for security purposes such as drawings or token generation.

### Deferred execution and buffering

`LeftJoin`, `RightJoin`, and `Shuffle` are all **deferred** operators; the source is not traversed until the result is enumerated.
However, `Shuffle`, like `OrderBy`, buffers the entire source before returning the first element.

```csharp
var query = Enumerable.Range(1, 3).Shuffle();

var first = query.ToArray();  // the source is enumerated and shuffled here
var second = query.ToArray(); // re-enumeration yields a different order
```

Because it is deferred, enumerating the same query twice makes `Shuffle` produce a different order each time.
To fix the order, materialize it once with `ToArray` or `ToList` and reuse the result.

---

## Choosing the Right Conditional-Compilation Symbol

This implementation uses `#if !NET10_0_OR_GREATER`, which differs from the `#if !NETCOREAPP` guard used in the [.NET 5 backport article](/articles/linq-backport-netframework-to-net5/).

`LeftJoin`, `RightJoin`, and `Shuffle` do not exist prior to .NET 10.
Guarding with `NETCOREAPP` or `NET9_0_OR_GREATER` would therefore disable the polyfill on .NET 8 and .NET 9 builds, causing a compile error there.

| Symbol | .NET Framework | .NET 9 | .NET 10+ |
| --- | --- | --- | --- |
| `!NETCOREAPP` | Polyfill enabled | **Polyfill disabled (compile error)** | Polyfill disabled |
| `!NET9_0_OR_GREATER` | Polyfill enabled | **Polyfill disabled (compile error)** | Polyfill disabled |
| `!NET10_0_OR_GREATER` | Polyfill enabled | Polyfill enabled | Polyfill disabled |

`!NET10_0_OR_GREATER` enables the polyfill on all runtimes below .NET 10, including .NET 9, and disables it automatically once the project targets .NET 10 or later.
The random source for `Shuffle` branches further with a nested `#if NET6_0_OR_GREATER`, using `Random.Shared` on .NET 6–9 and a `[ThreadStatic]` instance on .NET Framework.

---

## Caveats

- **Which side is preserved**: `LeftJoin` keeps every element of the first argument (`outer`), and `RightJoin` keeps every element of the second argument (`inner`). The argument that becomes nullable in the result selector is the inner element for `LeftJoin` and the outer element for `RightJoin`. Perform a `null` check before dereferencing it.
- **Key equality comparison**: The overload without a `comparer` uses `EqualityComparer<TKey>.Default`. For a case-insensitive join, use the overload that accepts an `IEqualityComparer<TKey>`. Where the shorter overload delegates to the comparer overload, the `comparer:` named argument ensures resolution to the intended method.
- **`Shuffle` is deferred**: `Shuffle` reorders on every enumeration, so enumerating the same query multiple times yields a different order each time. Materialize with `ToArray` / `ToList` to fix the order. Because it buffers the entire source when enumeration begins, it cannot be used on infinite sequences.
- **`Shuffle`'s randomness is not for security**: It uses a non-cryptographic random number generator, so for uses that require unpredictability, such as drawings, use a random source from `System.Security.Cryptography` instead.
- **No name collision occurs**: These polyfills have signatures absent from the original library, and they differ in name and arguments from the existing `Join` and `OrderBy`, so there is no ambiguity in overload resolution. After migration, the same-named original methods take precedence and conditional compilation disables the polyfill.
- **Compile with C# 9 or later**: The polyfill uses nullable annotations on unconstrained type parameters (`TInner?` / `TOuter?`) and `#nullable enable`, which require C# 9 or later. The default `LangVersion` of .NET Framework 4.8 (7.3) fails to compile with errors such as `CS8627`, so specify `<LangVersion>9.0</LangVersion>` (or `latest`) in the `.csproj`.
- **`IEnumerable<T>` only**: This polyfill consists of `Enumerable` extension methods and does not apply to `IQueryable<T>`. .NET 10 also adds `LeftJoin` / `RightJoin` to `Queryable`, but this article is limited to `Enumerable`. When these are used on an `IQueryable<T>` (for example, with Entity Framework), an unsupported provider does not translate the query to the data source and may fall back to client-side `Enumerable` evaluation. Express joins against a database with operators the provider can translate, as before.

---

## Alternatives

| Approach | Pros | Cons | Best for |
| --- | --- | --- | --- |
| Custom polyfill (this article) | No external dependencies; same name and feel as the original | Implementation and maintenance effort | Projects that minimize dependencies |
| Inline `GroupJoin` + `DefaultIfEmpty` | No additional code | Verbose; easy to get wrong | Few join call sites |
| `OrderBy(_ => Guid.NewGuid())` | No additional code | Inefficient; distribution not guaranteed | Reordering a few elements without strict correctness |
| A library such as MoreLINQ | Implemented and tested | Adds a dependency; API differs from the original | When a dependency is already acceptable |
| Upgrade to .NET 10 | Resolves the root cause; gains language features | Migration cost | When migration is technically and organizationally feasible |

Writing the boilerplate inline requires no extra code, but it leaves behind the work of finding and replacing every call site later when standardizing on the original `LeftJoin` / `Shuffle` after a .NET 10 migration.
Introducing the polyfill from this article allows code to be written with the same names as the originals before migration; at migration time, the file can stay in place while conditional compilation switches automatically to the originals.

---

## Summary

This article covered `LeftJoin`, `RightJoin`, and `Shuffle` added in .NET 10 and how to backport them safely to .NET Framework.

Three implementation points are worth remembering.

- **Match the original composition and annotation**: Express `LeftJoin` and `RightJoin` with `GroupJoin` + `SelectMany` + `DefaultIfEmpty`, and match the original nullable annotation on the result selector so that nullable analysis stays consistent before and after migration.
- **Use `#if !NET10_0_OR_GREATER`**: These operators are absent from .NET 9 and earlier, so `!NETCOREAPP` or `!NET9_0_OR_GREATER` would cause a compile error on .NET 9 builds.
- **Understand `Shuffle`'s deferred execution and random source**: `Shuffle` is deferred but buffers the whole source when enumeration begins, and its random source switches between `Random.Shared` and a `[ThreadStatic]` instance depending on the framework.

| Method | Evaluation | Side preserved | Nullable argument |
| --- | --- | --- | --- |
| `LeftJoin` | Deferred | Outer (left) | Inner element `TInner?` |
| `RightJoin` | Deferred | Inner (right) | Outer element `TOuter?` |
| `Shuffle` | Deferred (buffers on enumeration) | — | — |

---

## Related Articles

- [Backporting Missing LINQ Methods from .NET 9 to .NET Framework](/articles/linq-backport-netframework-to-net9/)
- [Backporting Missing LINQ Methods from .NET 8 to .NET Framework](/articles/linq-backport-netframework-to-net8/)
- [Backporting Missing LINQ Methods from .NET 7 to .NET Framework](/articles/linq-backport-netframework-to-net7/)
- [Backporting Missing LINQ Methods from .NET 6 to .NET Framework](/articles/linq-backport-netframework-to-net6/)
- [Backporting Missing LINQ Methods from .NET 5 to .NET Framework](/articles/linq-backport-netframework-to-net5/)

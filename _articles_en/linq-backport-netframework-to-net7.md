---
layout: article-en
title: "Order and OrderDescending by Pure Delegation — A Minimal Polyfill with IOrderedEnumerable Compatibility"
date: 2026-07-14
category: C#
excerpt: "A delegation-only polyfill for Order and OrderDescending, focused on ThenBy compatibility via IOrderedEnumerable and runtime-specific sort exceptions."
---

## Overview

The polyfill for `Order` and `OrderDescending`, added in .NET 7, needs only one line of body per method.
No iterator, no `Queue<T>` — each method simply delegates to the existing `OrderBy` / `OrderByDescending` with an identity lambda.

Precisely because the implementation is trivial, what decides the quality of this polyfill is not the algorithm but **compatibility details**.
This article presents the delegation-based implementation and then examines the two questions that determine compatibility.

- Returning anything other than `IOrderedEnumerable<T>` breaks `ThenBy` chaining and diverges from the original API
- The exception thrown for types with no default ordering differs between .NET Framework and .NET Core-based runtimes

A polyfill built purely on delegation to existing APIs is the counterpart to the hand-written iterator style (see the [foundation article](/articles/linq-backport-netframework-to-net5/)), and this article doubles as the reference example for that pattern.

---

## Prerequisites / Environment

- Frameworks: .NET Framework 4.8 (backport target) / .NET 7+ (future migration target)
- APIs: LINQ `Order` / `OrderDescending` (4 signatures, including the `IComparer<T>` overloads)
- Approach: apply `#nullable enable`; disable automatically on migration via `#if !NET7_0_OR_GREATER`
- No project configuration changes (such as `.csproj` language version)

---

## The Identity-Lambda Boilerplate

`Order` / `OrderDescending` sort a sequence by the elements themselves.

| Method | Added in | Description |
| --- | --- | --- |
| `Order<T>` | .NET 7.0 | Sorts a sequence ascending by the elements themselves |
| `OrderDescending<T>` | .NET 7.0 | Sorts a sequence descending by the elements themselves |

Without them, .NET Framework code keeps writing `OrderBy(x => x)` / `OrderByDescending(x => x)` even when sorting by the value itself.
The `x => x` is boilerplate unrelated to the sorting intent and a common site for minor mistakes such as grabbing the wrong key selector.
A dedicated method with the identity function baked in removes that noise, and .NET 7 standardized exactly that.

---

## Implementation by Delegation

The following is the complete polyfill for all four signatures.
Each method just passes an identity lambda to `OrderBy` / `OrderByDescending`, so sort stability and culture-sensitive comparison behavior are identical to the originals.
Add it to the project as, for example, `LinqExtensions.Net7.cs`.

```csharp
#nullable enable

using System;
using System.Collections.Generic;

#if !NET7_0_OR_GREATER // Active only below .NET 7.0 (e.g. .NET Framework)

namespace System.Linq
{
    /// <summary>
    /// Provides extension methods that backfill LINQ methods introduced in .NET 7.0 for older target frameworks.
    /// </summary>
    public static partial class LinqExtensions
    {
        // ==========================================
        // 1. Order
        // ==========================================
        public static IOrderedEnumerable<TSource> Order<TSource>(this IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.OrderBy(x => x);
        }

        public static IOrderedEnumerable<TSource> Order<TSource>(this IEnumerable<TSource> source, IComparer<TSource>? comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.OrderBy(x => x, comparer);
        }

        // ==========================================
        // 2. OrderDescending
        // ==========================================
        public static IOrderedEnumerable<TSource> OrderDescending<TSource>(this IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.OrderByDescending(x => x);
        }

        public static IOrderedEnumerable<TSource> OrderDescending<TSource>(this IEnumerable<TSource> source, IComparer<TSource>? comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.OrderByDescending(x => x, comparer);
        }
    }
}

#endif
```

Because the delegation style contains no `yield return`, the validation/iterator split required for hand-written iterators ([principle 1 of the foundation article](/articles/linq-backport-netframework-to-net5/)) does not apply.
The `source` null check runs immediately at call time, while the sort itself stays lazy through the delegated `OrderBy`.
Since the work is handed to a proven existing API, there is no room for algorithmic bugs to creep in.

Basic usage looks like this.

```csharp
var numbers = new[] { 3, 1, 4, 1, 5, 9, 2, 6 };

var ascending = numbers.Order();
// ascending: 1, 1, 2, 3, 4, 5, 6, 9

var descending = numbers.OrderDescending();
// descending: 9, 6, 5, 4, 3, 2, 1, 1
```

The parameterless overloads use the element type's default comparer (`Comparer<T>.Default`), so ordered types such as `int` and `string` sort as-is.
The `IComparer<T>` overloads swap in custom comparison logic.

```csharp
var words = new[] { "banana", "Apple", "cherry" };

var sorted = words.Order(StringComparer.OrdinalIgnoreCase);
// sorted: Apple, banana, cherry
```

---

## Compatibility Question 1: The Return Type `IOrderedEnumerable<T>` Decides ThenBy Chaining

The one real design decision in this delegation polyfill is the return type.
Returning `IEnumerable<T>` produces the same sorted output, but the original `Order` returns `IOrderedEnumerable<T>`, which allows a secondary sort key via `ThenBy` / `ThenByDescending`.

```csharp
var words = new[] { "Banana", "apple", "banana", "Apple" };

// Sort case-insensitively first; break ties with an ordinal comparison
var result = words.Order(StringComparer.OrdinalIgnoreCase)
                  .ThenBy(s => s, StringComparer.Ordinal);
// result: Apple, apple, Banana, banana
```

Under the first key (case-insensitive), `"Apple"`/`"apple"` and `"Banana"`/`"banana"` tie, so the second key (ordinal) decides their order.
With an implementation that narrows the return type to `IEnumerable<T>`, this `ThenBy` fails to compile.

The delegated `OrderBy` already returns `IOrderedEnumerable<T>`, so all the polyfill has to do is not narrow it.
The general lesson for delegation-based polyfills: **expose exactly the type the delegate returns and never drop information** — that is the condition for API compatibility.

---

## Compatibility Question 2: Sort-Time Exceptions Differ per Runtime

The parameterless overloads depend on `Comparer<T>.Default`, so element types that implement neither `IComparable` nor `IComparable<T>` throw at enumeration (sort) time.
Which exception type surfaces depends on the runtime.

- The default comparer itself throws `ArgumentException` (message: "At least one object must implement IComparable.").
- On **.NET Framework**, `OrderBy` does not wrap comparison exceptions in its internal sort, so this `ArgumentException` propagates as-is.
- On **.NET Core 3.0 and later** (including the built-in .NET 7 `Order`), the internal sort wraps comparison exceptions in `InvalidOperationException` (message: "Failed to compare two elements in the array.", with the `ArgumentException` as the inner exception).

In other words, while this polyfill runs on .NET Framework, its exception type does not match the built-in .NET 7 behavior.
This is a structural limit of delegation-based polyfills: the delegate's behavior surfaces unchanged, so wherever the delegate itself differs from modern .NET, the polyfill cannot paper over it.

Code that handles exceptions by type should account for this difference during migration; better yet, pass an explicit `IComparer<T>` whenever sorting arbitrary types.

---

## Migration Guard

The polyfill is wrapped in `#if !NET7_0_OR_GREATER`.
`Order` / `OrderDescending` do not exist before .NET 7, so guarding with `!NETCOREAPP` or `!NET6_0_OR_GREATER` would disable the polyfill in .NET 5 / .NET 6 builds and break compilation.
The general rule — disable at and above the version that introduced the methods — is laid out in the [.NET 6 backport article](/articles/linq-backport-netframework-to-net6/).

---

## Caveats

- **Sorting is stable**: the underlying `OrderBy` is a stable sort, so elements with equal keys keep their input order. This matches the built-in `Order`.
- **Evaluation is lazy**: like `OrderBy`, the sort runs at `foreach` / `.ToList()` time. The `ArgumentNullException` for a `null` source is thrown immediately at call time.
- **Prefer explicit comparers over the default**: as described above, the parameterless overloads throw at runtime for types with no defined ordering. Pass an `IComparer<T>` when the element type is arbitrary.

---

## Alternatives / Comparison

| Approach | Pros | Cons | Best for |
| --- | --- | --- | --- |
| Delegation polyfill (this article) | A few lines; no algorithmic bugs possible | Cannot reproduce behavior the delegate itself lacks (exception type) | Using the `Order` spelling before migrating |
| Write `OrderBy(x => x)` inline | No extra code | Verbose; mass replacement needed when moving to `Order` | Few call sites and no migration planned |
| Upgrade to .NET 7 | Root fix plus language features | Migration cost | When migration is feasible |

Writing `OrderBy(x => x)` inline works fine today, but consolidating to `Order` after a .NET 7 migration means hunting down and replacing every call site.
With the polyfill in place, code uses `Order` from day one, and conditional compilation switches to the built-in implementation automatically at migration time.

---

## Summary

The `Order` / `OrderDescending` polyfill is the minimal possible implementation — pure delegation to an existing API.
Its quality therefore hinges on compatibility, which reduces to two points.

| Question | Decision |
| --- | --- |
| Return type | Keep `IOrderedEnumerable<T>` to preserve `ThenBy` chaining |
| Sort-time exceptions | Types differ from .NET 7 while running on .NET Framework; avoid by passing an explicit `IComparer<T>` |

Choosing between a hand-written iterator polyfill ([foundation article](/articles/linq-backport-netframework-to-net5/)) and this delegation style comes down to whether the target behavior is expressible by composing existing APIs.
When it is, delegation is the safer choice; write an iterator only when it is not.

---

## Related Articles

- [Designing LINQ Polyfills That Preserve Lazy Evaluation — Implementing Append, Prepend, TakeLast and SkipLast](/articles/linq-backport-netframework-to-net5/)
- [Replacing GroupBy and Full-Sort Workarounds — Implementing Chunk, MaxBy, MinBy and DistinctBy](/articles/linq-backport-netframework-to-net6/)
- [Selector-Free ToDictionary — Designing for Overload Resolution and the notnull Constraint](/articles/linq-backport-netframework-to-net8/)
- [Key-Based Aggregation Without GroupBy — Dictionary-Backed CountBy, AggregateBy and Index](/articles/linq-backport-netframework-to-net9/)
- [Expressing SQL Outer Joins in LINQ — Implementing LeftJoin, RightJoin and Shuffle](/articles/linq-backport-netframework-to-net10/)

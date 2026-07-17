---
layout: article-en
title: "Key-Based Aggregation Without GroupBy — Dictionary-Backed CountBy, AggregateBy and Index"
date: 2026-07-16
category: C#
excerpt: "Implementing CountBy, AggregateBy and Index with direct dictionary accumulation, avoiding the intermediate-grouping allocations of GroupBy."
---

## Overview

To count elements per key, `GroupBy` builds a full list of elements for every key in memory.
The final result is just key/number pairs — the grouped element bodies are thrown away.
`CountBy` and `AggregateBy`, added in .NET 9, skip that intermediate grouping and aggregate by holding only per-key state in a dictionary.
The third addition, `Index`, turns the `Select((x, i) => …)` idiom for indexed enumeration into a dedicated method.

This article backports the three methods to .NET Framework.
Its central theme is why the polyfill's internals should use the same dictionary-based accumulation as the built-in implementation instead of delegating to `GroupBy`.
The two approaches differ not just in memory efficiency but in how they treat `null` keys, and the implementation is built by comparing them.

---

## Prerequisites / Environment

- Frameworks: .NET Framework 4.8 (backport target) / .NET 9+ (future migration target)
- APIs: LINQ `CountBy`, `AggregateBy`, `Index` and their overloads
- Approach: apply `#nullable enable`; disable automatically on migration via `#if !NET9_0_OR_GREATER`
- Language version: `#nullable enable` and `where TKey : notnull` require C# 8.0+. The .NET Framework 4.8 default is C# 7.3, so set `LangVersion` to `8.0` or later in the `.csproj`

---

## Problem: The Hidden Allocations of GroupBy Aggregation

The following methods added in .NET 9 are unavailable on .NET Framework.

| Method | Added in | Description |
| --- | --- | --- |
| `CountBy` | .NET 9.0 | Counts elements per key, returning a sequence of `KeyValuePair<TKey, int>` |
| `AggregateBy` | .NET 9.0 | Folds elements per key, returning a sequence of `KeyValuePair<TKey, TAccumulate>` |
| `Index` | .NET 9.0 | Attaches an index to each element, returning `(int Index, TSource Item)` tuples |

Writing the equivalent aggregation without them goes through `GroupBy`.

```csharp
// Count per key via GroupBy
var counts = words.GroupBy(w => w)
                  .Select(g => new { g.Key, Count = g.Count() });
```

The invisible cost of this code is the intermediate grouping.
`GroupBy` walks all elements up front and builds, for every key, a list of references to all elements belonging to that key.
When only a count is needed, those lists are ultimately discarded — but not the moment `Count()` returns: they stay retained until the entire sequence returned by `GroupBy` has been enumerated (every key's group built).
For a million elements across ten keys, a grouping holding a million references stays in memory for the whole time it takes to produce the ten numbers.

The `Index` substitute — `Select((item, index) => (index, item))` — is a different kind of problem: not cost, but intent buried in boilerplate.

---

## Root Cause / Background

`GroupBy` builds element lists because its contract is to return `IGrouping<TKey, TSource>` — a sequence of elements per key.
That is not a defect of `GroupBy`; the defect was paying for that contract in call sites that only aggregate.

.NET 9's `CountBy` / `AggregateBy` introduce an aggregation-only contract — per-key state (a count or an accumulator) is all that needs to be kept — and internally accumulate into a `Dictionary`.
Because of that internal dictionary, both built-ins constrain `where TKey : notnull` and throw at enumeration time if a key is `null`.

---

## Solution: Direct Dictionary Accumulation, Not GroupBy Delegation

There are two candidate internals for the polyfill.

```csharp
// Option A: naive delegation to GroupBy
public static IEnumerable<KeyValuePair<TKey, int>> CountBy<TSource, TKey>(
    this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
    => source.GroupBy(keySelector)
             .Select(g => new KeyValuePair<TKey, int>(g.Key, g.Count()));
```

Option A is shorter but diverges from the built-in behavior on two counts.

| Aspect | GroupBy delegation (A) | Direct dictionary accumulation (B, this article) | Built-in .NET 9 |
| --- | --- | --- | --- |
| Memory | Builds per-key element lists | Holds per-key state only | Holds per-key state only |
| `null` keys | Accepted as one group | `ArgumentNullException` at enumeration | `ArgumentNullException` at enumeration |

`GroupBy` accepts `null` keys as a group of their own, so the delegating implementation silently passes input that the built-in would reject.
Since the whole point of a polyfill is to keep behavior unchanged across migration, option B — the same dictionary-based accumulation as the built-in — wins on exception fidelity as well as memory.

To keep lazy evaluation while validating arguments eagerly, the public methods are split from the `yield return` iterator bodies (see [principle 1 of the foundation article](/articles/linq-backport-netframework-to-net5/)).
`Index` alone involves no aggregation and is served by delegating to the indexed overload of `Select`.

---

## Implementation

The following is the complete polyfill — 4 signatures including the `seed` and `seedSelector` forms of `AggregateBy`.
Add it to the project as, for example, `LinqExtensions.Net9.cs`.

```csharp
#nullable enable

using System;
using System.Collections.Generic;

#if !NET9_0_OR_GREATER // Active only below .NET 9.0 (e.g. .NET Framework)

namespace System.Linq
{
    /// <summary>
    /// Provides extension methods that backfill LINQ methods introduced in .NET 9.0 for older target frameworks.
    /// </summary>
    public static partial class LinqExtensions
    {
        // ==========================================
        // 1. CountBy
        // ==========================================
        public static IEnumerable<KeyValuePair<TKey, int>> CountBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey>? keyComparer = null)
            where TKey : notnull
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return CountByIterator(source, keySelector, keyComparer);
        }

        private static IEnumerable<KeyValuePair<TKey, int>> CountByIterator<TSource, TKey>(
            IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey>? keyComparer)
            where TKey : notnull
        {
            var counts = new Dictionary<TKey, int>(keyComparer);
            foreach (var item in source)
            {
                TKey key = keySelector(item); // A null key throws ArgumentNullException on the next line
                counts.TryGetValue(key, out int count);
                counts[key] = checked(count + 1); // Like .NET 9, overflow past int.MaxValue throws OverflowException
            }

            foreach (KeyValuePair<TKey, int> entry in counts)
            {
                yield return entry;
            }
        }

        // ==========================================
        // 2. AggregateBy (seed / seedSelector forms)
        // ==========================================
        public static IEnumerable<KeyValuePair<TKey, TAccumulate>> AggregateBy<TSource, TKey, TAccumulate>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            TAccumulate seed,
            Func<TAccumulate, TSource, TAccumulate> func,
            IEqualityComparer<TKey>? keyComparer = null)
            where TKey : notnull
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (func == null) throw new ArgumentNullException(nameof(func));

            return AggregateByIterator(source, keySelector, key => seed, func, keyComparer);
        }

        public static IEnumerable<KeyValuePair<TKey, TAccumulate>> AggregateBy<TSource, TKey, TAccumulate>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TKey, TAccumulate> seedSelector,
            Func<TAccumulate, TSource, TAccumulate> func,
            IEqualityComparer<TKey>? keyComparer = null)
            where TKey : notnull
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (seedSelector == null) throw new ArgumentNullException(nameof(seedSelector));
            if (func == null) throw new ArgumentNullException(nameof(func));

            return AggregateByIterator(source, keySelector, seedSelector, func, keyComparer);
        }

        private static IEnumerable<KeyValuePair<TKey, TAccumulate>> AggregateByIterator<TSource, TKey, TAccumulate>(
            IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TKey, TAccumulate> seedSelector,
            Func<TAccumulate, TSource, TAccumulate> func,
            IEqualityComparer<TKey>? keyComparer)
            where TKey : notnull
        {
            var accumulators = new Dictionary<TKey, TAccumulate>(keyComparer);
            foreach (var item in source)
            {
                TKey key = keySelector(item); // A null key throws ArgumentNullException on the next line
                if (!accumulators.TryGetValue(key, out var acc))
                {
                    acc = seedSelector(key);
                }
                accumulators[key] = func(acc!, item);
            }

            foreach (KeyValuePair<TKey, TAccumulate> entry in accumulators)
            {
                yield return entry;
            }
        }

        // ==========================================
        // 3. Index
        // ==========================================
        public static IEnumerable<(int Index, TSource Item)> Index<TSource>(
            this IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.Select((item, index) => (index, item));
        }
    }
}

#endif
```

The class is active only when the `NET9_0_OR_GREATER` symbol is undefined — that is, on any environment below .NET 9, including .NET Framework.
The `seed` form of `AggregateBy` is expressed as a `seedSelector` that ignores the key (`key => seed`) and delegates to the shared accumulation body.

---

## Behavior of Each Method

### `CountBy`: Element Count per Key

`CountBy` counts elements per key returned by the key selector, yielding a sequence of `KeyValuePair<TKey, int>`.

```csharp
var words = new[] { "apple", "banana", "apple", "cherry", "banana", "apple" };

foreach (var pair in words.CountBy(word => word))
{
    Console.WriteLine($"{pair.Key}: {pair.Value}");
}
// apple: 3
// banana: 2
// cherry: 1
```

The enumeration order of the results is not guaranteed.
The internal `Dictionary<TKey, TValue>` has no ordering contract, so code must not rely on any particular order (such as first-appearance order).
When a defined order is required, sort the results explicitly or convert them into an order-preserving collection.
The `IEqualityComparer<TKey>` overload swaps in a different key equality — case-insensitive counting, for example.

### `AggregateBy`: A Per-Key Fold

`AggregateBy` is the per-key version of `Aggregate`, folding independently for each key.
The following computes total sales per region.

```csharp
var sales = new[]
{
    ("Tokyo", 100),
    ("Osaka", 80),
    ("Tokyo", 120),
    ("Osaka", 60),
};

var totals = sales.AggregateBy(
    keySelector: s => s.Item1,
    seed: 0,
    func: (total, s) => total + s.Item2);

foreach (var pair in totals)
{
    Console.WriteLine($"{pair.Key}: {pair.Value}");
}
// Tokyo: 220
// Osaka: 140
```

To vary the initial value per key, use the overload taking a `seedSelector` (`Func<TKey, TAccumulate>`) instead of `seed`.
Each key then starts its fold from its own initial state.

### `Index`: Indexed Enumeration

`Index` attaches a zero-based index to each element, yielding `(int Index, TSource Item)` tuples.

```csharp
var items = new[] { "a", "b", "c" };

foreach (var (index, item) in items.Index())
{
    Console.WriteLine($"{index}: {item}");
}
// 0: a
// 1: b
// 2: c
```

The first tuple element is the index, the second the value.
Note the order is reversed from the `Select((item, index) => ...)` lambda — `Index` puts the index first.

### Lazy Evaluation and Exception Timing

All three methods are lazy; computation runs at `foreach` / `.ToList()` time.
The `ArgumentNullException` for a `null` source or delegate, however, is thrown immediately at call time.

```csharp
IEnumerable<int> numbers = null!;

// ArgumentNullException is thrown on this line, before any enumeration
var query = numbers.CountBy(n => n);
```

When the key selector returns a `null` key mid-enumeration, the `ArgumentNullException` fires at the point the internal dictionary is accessed with that key (the `TryGetValue` call).
This matches the built-in .NET 9 behavior of the internal dictionary rejecting `null` keys.

---

## Migration Guard

The polyfill is wrapped in `#if !NET9_0_OR_GREATER`.
`CountBy`, `AggregateBy` and `Index` do not exist before .NET 9, so guarding with `!NETCOREAPP` or `!NET8_0_OR_GREATER` would disable the polyfill in a .NET 8 build and break compilation.
The general rule — disable at and above the version that introduced the methods — is laid out in the [.NET 6 backport article](/articles/linq-backport-netframework-to-net6/).

---

## Caveats

- **Results are lazy sequences and re-aggregate on every enumeration**: the return value is not a `Dictionary` but a deferred sequence. Materialize with `.ToList()` or `.ToDictionary()` when the result is used more than once; otherwise each enumeration re-walks and re-aggregates the source.
- **Key types are `notnull`**: the polyfill carries the built-in `where TKey : notnull` constraint, and a runtime `null` key throws `ArgumentNullException` at enumeration. To aggregate data with `null` keys, map them first (e.g. `?? "(none)"`).
- **`CountBy` overflow**: counting uses `checked(count + 1)`, so a key whose count exceeds `int.MaxValue` throws `OverflowException`. The built-in .NET 9 also adds with `checked`, so behavior matches.
- **`Index` tuple order / naming confusion**: the tuple is `(Index, Item)` — index first. The method name `Index` also resembles the C# 8 `System.Index` type, but one is a type and the other a method; they do not collide.

---

## Alternatives / Comparison

| Approach | Pros | Cons | Best for |
| --- | --- | --- | --- |
| Dictionary-backed polyfill (this article) | Same memory profile and exception behavior as the built-ins | Somewhat longer implementation | Keeping behavior identical across migration |
| GroupBy-delegating polyfill | A few lines of code | Allocates intermediate groupings; `null`-key behavior differs from the built-ins | Temporary use with the differences understood |
| Write `GroupBy(...).Select(...)` inline | No extra code | Verbose; mass replacement at migration | Few call sites and no migration planned |
| Upgrade to .NET 9 | Root fix plus the allocation savings | Migration cost | When migration is feasible |

---

## Summary

What a `CountBy` / `AggregateBy` backport is judged on is not "returning the same result" but "returning it the same way."
Delegating to `GroupBy` produces identical values while diverging from the built-ins on two counts: intermediate-grouping allocations and tolerance of `null` keys.
Direct dictionary accumulation aligns the memory profile, the exception behavior and the `where TKey : notnull` constraint with the built-ins, making the zero-edit switchover under `#if !NET9_0_OR_GREATER` genuinely safe.

| Method | Implementation | State held |
| --- | --- | --- |
| `CountBy` | Direct dictionary accumulation | Count per key only |
| `AggregateBy` | Direct dictionary accumulation | Accumulator per key only |
| `Index` | Delegation to `Select` | None (pass-through) |

For codebases that routinely aggregate large data sets by key on .NET Framework, adopting this polyfill both cuts allocations today and prepares the migration path.

---

## Related Articles

- [Designing LINQ Polyfills That Preserve Lazy Evaluation — Implementing Append, Prepend, TakeLast and SkipLast](/articles/linq-backport-netframework-to-net5/)
- [Replacing GroupBy and Full-Sort Workarounds — Implementing Chunk, MaxBy, MinBy and DistinctBy](/articles/linq-backport-netframework-to-net6/)
- [Order and OrderDescending by Pure Delegation — A Minimal Polyfill with IOrderedEnumerable Compatibility](/articles/linq-backport-netframework-to-net7/)
- [Selector-Free ToDictionary — Designing for Overload Resolution and the notnull Constraint](/articles/linq-backport-netframework-to-net8/)
- [Expressing SQL Outer Joins in LINQ — Implementing LeftJoin, RightJoin and Shuffle](/articles/linq-backport-netframework-to-net10/)

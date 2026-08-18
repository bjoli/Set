/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 *
 * Copyright (c) 2024-2026 Linus Björnstam
 *
 */

using System.Runtime.CompilerServices;

namespace Set;

public sealed partial class Set<T> :
    IEquatable<Set<T>>
    where T : notnull
{
    public static readonly Set<T> Empty = new(null, EqualityComparer<T>.Default);
    private readonly IEqualityComparer<T> _comparer;
    private readonly NodeBase? _root;

    public Set(IEqualityComparer<T> comparer)
    {
        _root = null;
        _comparer = comparer;
        Count = 0;
    }

    public Set()
    {
        _root = null;
        _comparer = EqualityComparer<T>.Default;
        Count = 0;
    }

    private Set(NodeBase? root, IEqualityComparer<T> comparer)
    {
        _root = root;
        _comparer = comparer;
        Count = 0;
    }

    internal Set(NodeBase? root, IEqualityComparer<T> comparer, int count)
    {
        _root = root;
        _comparer = comparer;
        Count = count;
    }

    public bool IsEmpty => Count == 0;

    public MapKeyCollection<T> Keys => new(_root, Count);
    public MapValueCollection<T> Values => new(_root, Count);

    public int Count { get; }

    public TV this[T key] => Get(key);


    public bool ContainsKey(T key)
    {
        return TryGetValue(key, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(T key, out TV value)
    {
        if (_root == null)
        {
            value = default!;
            return false;
        }

        var hash = _comparer.GetHashCode(key);
        return TrieOps.TryGetValue(_root, key, hash, _comparer, out value);
    }


    public Set<T> Clear()
    {
        return Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetKey(T key, out T value)
    {
        if (_root == null)
        {
            value = default!;
            return false;
        }

        var hash = _comparer.GetHashCode(key);
        return TrieOps.TryGetKey<T>(_root, key, hash, _comparer, out value);
    }

    public TV Get(T key)
    {
        if (TryGetValue(key, out var value)) return value;

        throw new KeyNotFoundException($"The key '{key}' was not present in the map.");
    }
    // Todo: add and set should be using the same TrieOps method, which can conditionally
    // overwrite the value or raise an exception.
    public Set<T> Set(T key, TV value)
    {
        var hash = _comparer.GetHashCode(key);
        var newRoot = TrieOps.Insert(_root, key, value, hash, 0, _comparer, out var added);

        // If nothing was added NOR CHANGED, we can just return the same persistentmap
        if (ReferenceEquals(_root, newRoot)) return this;

        return new Set<T>(newRoot, _comparer, added ? Count + 1 : Count);
    }

    public Set<T> Add(T key, TV value)
    {
        if (ContainsKey(key))
            throw new ArgumentException($"The key '{key}' is already in the map.");
        var hash = _comparer.GetHashCode(key);
        var newRoot = TrieOps.Insert(_root, key, value, hash, 0, _comparer, out var added);

        return new Set<T>(newRoot, _comparer, added ? Count + 1 : Count);
    }

    public Set<T> AddRange(IEnumerable<KeyValuePair<T>> range)
    {
        var newMap = Mutate(transient =>
        {
            foreach (var kvp in range) transient.Set(kvp.Key, kvp.Value);
        });
        return newMap;
    }

    public Set<T> RemoveRange(IEnumerable<T> range)
    {
        var newMap = Mutate(transient =>
        {
            foreach (var k in range) transient.Remove(k);
        });
        return newMap;
    }


    public Set<T> Remove(T key)
    {
        if (_root == null) return this;

        var hash = _comparer.GetHashCode(key);
        var newRoot = TrieOps.Remove<T>(_root!, key, hash, 0, _comparer, out var removed);

        if (!removed) return this;

        if (newRoot == null) return Empty;

        return new Set<T>(newRoot, _comparer, removed ? Count - 1 : Count);
    }

    public bool Equals(Set<T>? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        if (Count != other.Count) return false;
        if (Count == 0) return true;

        var valueComparer = EqualityComparer<TV>.Default;

        // The struct-based enumerator we built earlier makes this allocation-free
        foreach (var kvp in this)
            if (!other.TryGetValue(kvp.Key, out var otherValue) || !valueComparer.Equals(kvp.Value, otherValue))
                return false;

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is Set<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        if (Count == 0) return 0;

        var hash = 0;
        var valueComparer = EqualityComparer<TV>.Default;

        // XOR is commutative, guaranteeing the same hash regardless of internal tree structure
        foreach (var kvp in this)
        {
            var keyHash = _comparer.GetHashCode(kvp.Key);
            var valHash = kvp.Value == null ? 0 : valueComparer.GetHashCode(kvp.Value);

            hash ^= HashCode.Combine(keyHash, valHash);
        }

        return hash;
    }

    public TransientSet<T> ToTransient()
    {
        return new TransientSet<T>(_root, _comparer);
    }

    /// <summary>
    ///     Executes a delegate on the elements of the map
    /// </summary>
    /// <returns>True if the iteration completed all elements, or False if aborted early.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Iter(Func<T, TV, bool> action)
    {
        return TrieOps.Iter(_root, action);
    }

    /// <summary>
    ///     Filters the map, retaining only elements that satisfy the specified predicate.
    /// </summary>
    /// <param name="action">A function to test each value for a condition.</param>
    /// <returns>A new <see cref="Map{T, TV}" /> containing the elements that satisfy the condition.</returns>
    public Set<T> Filter(Func<TV, TV, bool> action)
    {
        var builder = new SetBuilder<T>(_comparer);
        Iter((k, v) =>
        {
            builder.Add(k, v);
            return true;
        });
        return builder.ToImmutable();
    }

    /// <summary>
    ///     Transforms the elements of the map using the specified function.
    /// </summary>
    /// <typeparam name="TNk">The type of the new keys.</typeparam>
    /// <typeparam name="TNv">The type of the new values.</typeparam>
    /// <param name="action">A function to transform each key-value pair.</param>
    /// <param name="comparer">An optional equality comparer for the new keys.</param>
    /// <returns>A new <see cref="Map{NK, NV}" /> containing the transformed elements.</returns>
    public Map<TNk, TNv> MapCar<TNk, TNv>(Func<T, TV, (TNk, TNv)> action, IEqualityComparer<TNk>? comparer = null)
        where TNk : notnull
    {
        var builder = new MapBuilder<TNk, TNv>(comparer);
        Iter((k, v) =>
        {
            var (nk, nv) = action(k, v);
            builder.Add(nk, nv);
            return true;
        });

        return builder.ToImmutable();
    }

    /// <summary>
    ///     Aggregates the values of the map using the specified function.
    /// </summary>
    /// <typeparam name="TState">The type of the accumulator state.</typeparam>
    /// <param name="seed">The initial accumulator value.</param>
    /// <param name="action">A function to aggregate the state and each value.</param>
    /// <returns>The final accumulated state.</returns>
    public TState FoldValues<TState>(TState seed, Func<TState, TV, TState> action)
    {
        Iter((_, v) =>
        {
            seed = action(seed, v);
            return true;
        });
        return seed;
    }

    /// <summary>
    ///     Aggregates the keys of the map using the specified function.
    /// </summary>
    /// <typeparam name="TState">The type of the accumulator state.</typeparam>
    /// <param name="seed">The initial accumulator value.</param>
    /// <param name="action">A function to aggregate the state and each key.</param>
    /// <returns>The final accumulated state.</returns>
    public TState FoldKeys<TState>(TState seed, Func<TState, T, TState> action)
    {
        Iter((k, _) =>
        {
            seed = action(seed, k);
            return true;
        });
        return seed;
    }

    /// <summary>
    ///     Determines whether the map contains elements that satisfy the specified predicate.
    /// </summary>
    /// <param name="pred">A function to test each element for a condition.</param>
    /// <returns><c>true</c> if the map contains an element that satisfies the condition; otherwise, <c>false</c>.</returns>
    public bool Exists(Func<T, TV, bool> pred)
    {
        return Iter((k, v) => { return !pred(k, v); });
    }

    /// <summary>
    ///     Finds the first key in the map that satisfies the specified predicate.
    /// </summary>
    /// <param name="pred">A function to test each key for a condition.</param>
    /// <returns>The key that satisfies the condition.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if no key satisfies the condition.</exception>
    public T FindKey(Func<T, bool> pred)
    {
        var key = default(T);
        var found = false;
        Iter((k, _) =>
        {
            if (pred(k))
            {
                found = true;
                key = k;
                return false;
            }

            return true;
        });
        if (!found)
            throw new KeyNotFoundException("Key not found in map");

        return key!;
    }

    /// <summary>
    ///     Executes the specified action on each element of the map.
    /// </summary>
    /// <param name="action">The action to execute on each key-value pair.</param>
    public void ForEach(Action<T> action)
    {
        Iter((k, v) =>
        {
            action(k, v);
            return true;
        });
    }


    /// <summary>
    ///     Creates a transient version of the map, applies the specified mutation action, and returns an immutable map.
    /// </summary>
    /// <param name="action">The action to apply to the transient map.</param>
    /// <returns>A new <see cref="Map{T, TV}" /> with the mutations applied.</returns>
    public Set<T> Mutate(Action<TransientSet<T>> action)
    {
        var transient = ToTransient();
        action(transient);
        return transient.ToImmutable();
    }

    /// <summary>
    ///     Merges another map into this one using a conflict resolution strategy.
    /// </summary>
    /// <param name="other">The other map to merge.</param>
    /// <param name="conflictResolver">
    ///     An optional thunk called when keys conflict: (key, leftValue, rightValue) => resolvedValue.
    ///     Pass null to default to picking the right value (other overwrites this).
    /// </param>
    public Set<T> Merge(Set<T> other, Func<T, TV, TV, TV>? conflictResolver = null)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;

        var newRoot = TrieOps.Merge(_root, other._root, 0, _comparer, conflictResolver);

        if (ReferenceEquals(_root, newRoot)) return this;
        if (ReferenceEquals(other._root, newRoot)) return other;

        // Recalculate size allocation-free via IterFast
        var counter = 0;
        TrieOps.Iter<T>(newRoot, (_, _) =>
        {
            counter++;
            return true;
        });

        return new Set<T>(newRoot, _comparer, counter);
    }

    public MapEnumerator<T> GetEnumerator()
    {
        return new MapEnumerator<T>(_root);
    }
}
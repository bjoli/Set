/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 *
 * Copyright (c) 2024-2026 Linus Björnstam
 *
 */

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Set;

public sealed partial class Set<T> :
    IEquatable<Set<T>>,
    IEnumerable<T>
   
{
    public static readonly Set<T> Empty = new(null, EqualityComparer<T>.Default);
    private readonly IEqualityComparer<T> _comparer;
    private readonly NodeBase? _root;

    /// <summary>
    ///     The comparer's hash of an element.
    ///
    ///     The suppression is the whole reason this is a method: with no <c>notnull</c> on
    ///     <typeparamref name="T" /> the compiler cannot see that an element is non-null, and
    ///     <see cref="IEqualityComparer{T}.GetHashCode" /> disallows one. The constraint is gone
    ///     because it is unenforceable at the Bjolang boundary — the generated C# carries no
    ///     <c>where</c> clauses at all — and every Bjolang type argument is non-null anyway, so
    ///     requiring it here only produced CS8714 at each generic call site.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int HashOf(T key)
    {
        return _comparer.GetHashCode(key!);
    }

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

    public int Count { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T key)
    {
        if (_root == null)
        {
            return false;
        }

        var hash = HashOf(key);
        return TrieOps.Contains(_root, key, hash, _comparer);
    }


    public Set<T> Clear()
    {
        return Empty;
    }

    public Set<T> Add(T key)
    {
        var hash = HashOf(key);
        var newRoot = TrieOps.Insert(_root, key, hash, 0, _comparer, out var added);

        // If nothing was added NOR CHANGED, we can just return the same persistentmap
        if (ReferenceEquals(_root, newRoot)) return this;

        return new Set<T>(newRoot, _comparer, added ? Count + 1 : Count);
    }

    public Set<T> AddChecked(T key)
    {
        if (Contains(key))
            throw new ArgumentException($"The key '{key}' is already in the map.");
        var hash = HashOf(key);
        var newRoot = TrieOps.Insert(_root, key, hash, 0, _comparer, out var added);

        return new Set<T>(newRoot, _comparer, added ? Count + 1 : Count);
    }

    public Set<T> AddRange(IEnumerable<T> range)
    {
        var newMap = Mutate(transient =>
        {
            foreach (var k in range) transient.Add(k);
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

        var hash = HashOf(key);
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

        foreach (var item in this)
            if (!other.Contains(item))
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

        foreach (var item in this)
        {
            hash ^= HashOf(item);
        }

        return hash;
    }

    public TransientSet<T> ToTransient()
    {
        return new TransientSet<T>(_root, _comparer, Count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Iter(Func<T, bool> action)
    {
        return TrieOps.Iter(_root, action);
    }

    public Set<T> Filter(Func<T, bool> action)
    {
        var builder = new SetBuilder<T>(_comparer);
        Iter((k) =>
        {
            if (action(k)) builder.Add(k);
            return true;
        });
        return builder.ToImmutable();
    }

    public Set<TNew> Map<TNew>(Func<T, TNew> action)
       
    {
        var builder = new SetBuilder<TNew>();
        Iter((k) =>
        {
            var nk = action(k);
            builder.Add(nk);
            return true;
        });

        return builder.ToImmutable();
    }

    /// <summary>
    ///     Aggregates the keys of the map using the specified function.
    /// </summary>
    /// <typeparam name="TState">The type of the accumulator state.</typeparam>
    /// <param name="seed">The initial accumulator value.</param>
    /// <param name="action">A function to aggregate the state and each key.</param>
    /// <returns>The final accumulated state.</returns>
    public TState Fold<TState>(TState seed, Func<TState, T, TState> action)
    {
        Iter((k) =>
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
    public bool Exists(Func<T, bool> pred)
    {
        return !Iter((k) => !pred(k));
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
        Iter((k) =>
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
        Iter((k) =>
        {
            action(k);
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
    public Set<T> Merge(Set<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;

        var newRoot = TrieOps.Merge(_root, other._root, 0, _comparer);

        if (ReferenceEquals(_root, newRoot)) return this;
        if (ReferenceEquals(other._root, newRoot)) return other;

        // Recalculate size allocation-free via IterFast
        var counter = 0;
        TrieOps.Iter<T>(newRoot, (_) =>
        {
            counter++;
            return true;
        });

        return new Set<T>(newRoot, _comparer, counter);
    }

    public SetEnumerator<T> GetEnumerator()
    {
        return new SetEnumerator<T>(_root);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
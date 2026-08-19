/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 *
 * Copyright (c) 2025-2026 Linus Björnstam
 *
 */

using System.Runtime.CompilerServices;

namespace Set;

/// <summary>
///     A mutable, transient version of a <see cref="Map{T, TV}" /> that can be efficiently modified
///     before being converted back to an immutable map.
/// </summary>
public sealed class TransientSet<T> where T : notnull
{
    private readonly IEqualityComparer<T> _comparer;
    private int _count;
    private ulong _ownerId;
    private NodeBase? _root;

    internal TransientSet(NodeBase? root, IEqualityComparer<T> comparer, int count = 0)
    {
        _root = root;
        _comparer = comparer;
        _ownerId = OwnerId.Next();
        _count = count;
    }

    /// <summary>
    ///     Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="value">
    ///     When this method returns, contains the value associated with the specified key, if the key is found;
    ///     otherwise, the default value for the type of the <paramref name="value" /> parameter.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T key)
    {
        if (_root == null)
        {
            
            return false;
        }

        var hash = _comparer.GetHashCode(key);
        return TrieOps.Contains(_root, key, hash, _comparer);
    }


    public void Add(T key)
    {
        var hash = _comparer.GetHashCode(key ?? throw new ArgumentNullException(nameof(key)));
        if (TrieOps.TryGetKey<T>(_root, key, hash, _comparer, out _))
        {
            return;
        }
        _root = TrieOps.InsertTransient(_root, key, hash, 0, _comparer, _ownerId, out  _);
        _count++;
    }
    
    

    /// <summary>
    ///     Removes the element with the specified key from the map.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    public void Remove(T key)
    {
        if (_root == null) return;

        var hash = _comparer.GetHashCode(key ?? throw new ArgumentNullException(nameof(key)));
        _root = TrieOps.RemoveTransient<T>(_root, key, hash, 0, _comparer, out var removed, _ownerId);
        if (removed) _count--;
    }

    /// <summary>
    ///     Creates an immutable <see cref="Map{T, TV}" /> from the contents of this transient map.
    ///     This operation is an O(1) operation and does not involve copying the data.
    /// </summary>
    /// <returns>An immutable map.</returns>
    public Set<T> ToImmutable()
    {
        // Disowns all the nodes we mutated.
        _ownerId = OwnerId.Next();
        return new Set<T>(_root, _comparer, _count);
    }
}
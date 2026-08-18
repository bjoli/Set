/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 *
 * Copyright (c) 2024-2026 Linus Björnstam
 *
 */

using System.Collections.Immutable;

namespace Set;


// This contains the parts of IImmutableDictionary that are already implemented in the main class
// but do not satisfy the types. 
public sealed partial class Set<T> :
    IImmutableDictionary<T>
    where T : notnull
{
    bool IImmutableDictionary<T>.TryGetKey(T key, out T value)
    {
        return TryGetKey(key, out value);
    }

    IImmutableDictionary<T> IImmutableDictionary<T>.Add(T key, TV val)
    {
        return Add(key, val);
    }

    IImmutableDictionary<T> IImmutableDictionary<T>.AddRange(IEnumerable<KeyValuePair<T>> pairs)
    {
        return AddRange(pairs);
    }

    IImmutableDictionary<T> IImmutableDictionary<T>.Clear()
    {
        return Empty;
    }

    bool IImmutableDictionary<T>.Contains(KeyValuePair<T> kvp)
    {
        return Exists((k, v) => k.Equals(kvp.Key) && v!.Equals(kvp.Value));
    }

    IImmutableDictionary<T> IImmutableDictionary<T>.Remove(T key)
    {
        return Remove(key);
    }

    IImmutableDictionary<T> IImmutableDictionary<T>.RemoveRange(IEnumerable<T> keys)
    {
        return RemoveRange(keys);
    }

    IImmutableDictionary<T> IImmutableDictionary<T>.SetItem(T key, TV value)
    {
        return Set(key, value);
    }

    IImmutableDictionary<T> IImmutableDictionary<T>.SetItems(IEnumerable<KeyValuePair<T>> kvPairs)
    {
        var newMap = Mutate(transient =>
        {
            foreach (var kvp in kvPairs) transient.Set(kvp.Key, kvp.Value);
        });
        return newMap;
    }
}
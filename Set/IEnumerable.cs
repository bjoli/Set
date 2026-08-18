/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 *
 * Copyright (c) 2024-2026 Linus Björnstam
 *
 */

using System.Collections;

namespace Set;

// Explicit implementation of IEnumerable to satisfy the interfaces

public sealed partial class Set<T>  :
    IEnumerable<KeyValuePair<T>>
    where T : notnull
{
    
    IEnumerator<KeyValuePair<T>> IEnumerable<KeyValuePair<T>>.GetEnumerator()
    {
        return new MapEnumerator<T>(_root);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new MapEnumerator<T>(_root);
    }

    IEnumerable<T> IReadOnlyDictionary<T>.Keys => Keys;
    IEnumerable<TV> IReadOnlyDictionary<T>.Values => Values;
}
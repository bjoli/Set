/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 *
 * Copyright (c) 2025-2026 Linus Björnstam
 *
 */


using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Set;

internal static class NodeOps
{
    public static (byte capacity, NodeFlags flags, ulong ownerId) UnpackMeta(ulong meta)
    {
        var capacity = (byte)(meta & 0xFF);
        var flags = (NodeFlags)((meta >> 8) & 0xFF);
        var ownerId = meta >> 16;
        return (capacity, flags, ownerId);
    }

    public static byte GetCapacity(ulong meta)
    {
        return (byte)(meta & 0xFF);
    }

    public static NodeFlags GetFlags(ulong meta)
    {
        return (NodeFlags)((meta >> 8) & 0xFF);
    }

    public static ulong GetOwnerId(ulong meta)
    {
        return meta >> 16;
    }

    public static ulong PackMeta(byte capacity, NodeFlags flags, ulong ownerId)
    {
        // Masks ownerId to strictly 48 bits before shifting
        return capacity
               | ((ulong)(byte)flags << 8)
               | ((ownerId & 0x0000FFFFFFFFFFFF) << 16);
    }

    // This is probably the fastest way I have managed to figure out to do this by now. 
    // Using an array of delegates is not faster, nor is using makeGenericType. If you really
    // want to have a go at making this faster, skip ALL nodes and have one node with a variable tail. 
    // and use some kind of unsafe voodoo. 
    public static NodeBase AllocateLeaf<T>(byte capacity, NodeFlags flags, ulong owner, ulong map)
    {
        var meta = PackMeta(capacity, flags, owner);

        return capacity switch
        {
            1 => new Node1<T> { Meta = meta, Map = map },
            2 => new Node2<T> { Meta = meta, Map = map },
            3 => new Node3<T> { Meta = meta, Map = map },
            4 => new Node4<T> { Meta = meta, Map = map },
            5 => new Node5<T> { Meta = meta, Map = map },
            6 => new Node6<T> { Meta = meta, Map = map },
            7 => new Node7<T> { Meta = meta, Map = map },
            8 => new Node8<T> { Meta = meta, Map = map },
            9 => new Node9<T> { Meta = meta, Map = map },
            10 => new Node10<T> { Meta = meta, Map = map },
            11 => new Node11<T> { Meta = meta, Map = map },
            12 => new Node12<T> { Meta = meta, Map = map },
            13 => new Node13<T> { Meta = meta, Map = map },
            14 => new Node14<T> { Meta = meta, Map = map },
            15 => new Node15<T> { Meta = meta, Map = map },
            16 => new Node16<T> { Meta = meta, Map = map },
            17 => new Node17<T> { Meta = meta, Map = map },
            18 => new Node18<T> { Meta = meta, Map = map },
            19 => new Node19<T> { Meta = meta, Map = map },
            20 => new Node20<T> { Meta = meta, Map = map },
            21 => new Node21<T> { Meta = meta, Map = map },
            22 => new Node22<T> { Meta = meta, Map = map },
            23 => new Node23<T> { Meta = meta, Map = map },
            24 => new Node24<T> { Meta = meta, Map = map },
            25 => new Node25<T> { Meta = meta, Map = map },
            26 => new Node26<T> { Meta = meta, Map = map },
            27 => new Node27<T> { Meta = meta, Map = map },
            28 => new Node28<T> { Meta = meta, Map = map },
            29 => new Node29<T> { Meta = meta, Map = map },
            30 => new Node30<T> { Meta = meta, Map = map },
            31 => new Node31<T> { Meta = meta, Map = map },
            32 => new Node32<T> { Meta = meta, Map = map },
            _ => throw new ArgumentOutOfRangeException(nameof(capacity))
        };
    }

    public static NodeBase AllocateInternal<T>(byte capacity, NodeFlags flags, ulong owner, ulong map)
    {
        var meta = PackMeta(capacity, flags, owner);

        return capacity switch
        {
            1 => new InternalNode1<T> { Meta = meta, Map = map },
            2 => new InternalNode2<T> { Meta = meta, Map = map },
            3 => new InternalNode3<T> { Meta = meta, Map = map },
            4 => new InternalNode4<T> { Meta = meta, Map = map },
            5 => new InternalNode5<T> { Meta = meta, Map = map },
            6 => new InternalNode6<T> { Meta = meta, Map = map },
            7 => new InternalNode7<T> { Meta = meta, Map = map },
            8 => new InternalNode8<T> { Meta = meta, Map = map },
            9 => new InternalNode9<T> { Meta = meta, Map = map },
            10 => new InternalNode10<T> { Meta = meta, Map = map },
            11 => new InternalNode11<T> { Meta = meta, Map = map },
            12 => new InternalNode12<T> { Meta = meta, Map = map },
            13 => new InternalNode13<T> { Meta = meta, Map = map },
            14 => new InternalNode14<T> { Meta = meta, Map = map },
            15 => new InternalNode15<T> { Meta = meta, Map = map },
            16 => new InternalNode16<T> { Meta = meta, Map = map },
            17 => new InternalNode17<T> { Meta = meta, Map = map },
            18 => new InternalNode18<T> { Meta = meta, Map = map },
            19 => new InternalNode19<T> { Meta = meta, Map = map },
            20 => new InternalNode20<T> { Meta = meta, Map = map },
            21 => new InternalNode21<T> { Meta = meta, Map = map },
            22 => new InternalNode22<T> { Meta = meta, Map = map },
            23 => new InternalNode23<T> { Meta = meta, Map = map },
            24 => new InternalNode24<T> { Meta = meta, Map = map },
            25 => new InternalNode25<T> { Meta = meta, Map = map },
            26 => new InternalNode26<T> { Meta = meta, Map = map },
            27 => new InternalNode27<T> { Meta = meta, Map = map },
            28 => new InternalNode28<T> { Meta = meta, Map = map },
            29 => new InternalNode29<T> { Meta = meta, Map = map },
            30 => new InternalNode30<T> { Meta = meta, Map = map },
            31 => new InternalNode31<T> { Meta = meta, Map = map },
            32 => new InternalNode32<T> { Meta = meta, Map = map },
            _ => throw new ArgumentOutOfRangeException(nameof(capacity))
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<DataSlot<T>> GetLeafDataSpan<T>(NodeBase node)
    {
        int capacity = GetCapacity(node.Meta);
        var typedNode = Unsafe.As<Node1<T>>(node);
        ref var first = ref Unsafe.As<LeafSlot1<T>, DataSlot<T>>(ref typedNode.Data);
        return MemoryMarshal.CreateSpan(ref first, capacity);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DataSlot<T>[]? GetDataArray<T>(NodeBase node)
    {
        // The Data reference is at the exact same memory offset for all InternalNode1..32 types
        return Unsafe.As<InternalNode1<T>>(node).Data;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<NodeBase> GetChildSpan<T>(NodeBase node)
    {
        int capacity = GetCapacity(node.Meta);
        var typedNode = Unsafe.As<InternalNode1<T>>(node);
        ref var first = ref Unsafe.As<NodeSlot1, NodeBase>(ref typedNode.Children);
        return MemoryMarshal.CreateSpan(ref first, capacity);
    }
}
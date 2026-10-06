/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 *
 * Copyright (c) 2024-2026 Linus Björnstam
 *
 */


using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Set;

internal static partial class TrieOps
{
    // Look, I know this method is a bit of a monster. I apologize for the sheer amount of code here,
    // but the following handles inserting a key-value pair into the trie like this: we navigate down 
    // the tree using chunks of the hash, and depending on whether we hit an empty spot, a leaf, or an 
    // internal node, we either slot it right in or do some painful allocations to expand the node. 
    // It's not pretty, but it avoids even worse performance.
    public static NodeBase Insert<T>(NodeBase? node, T key, int hash, int shift,
        IEqualityComparer<T> comparer, out bool added)
    {
        // The following handles the base case of an empty tree like this: we just allocate a single 
        // leaf node, stuff our data in there, and call it a day.
        if (node == null)
        {
            var bitpos0 = 1u << ((hash >> shift) & 0x1F);
            var newNode = NodeOps.AllocateLeaf<T>(1, NodeFlags.None, 0, bitpos0);
            var mySpan = NodeOps.GetLeafDataSpan<T>(newNode);
            mySpan[0] = new DataSlot<T> { Key = key };
            // We added a value.
            added = true;
            return newNode;
        }

        var flags = NodeOps.GetFlags(node.Meta);

        // The following handles the dreaded hash collision like this: we iterate through an array of 
        // existing slots because their hashes are identical. If we find the key, we update it; otherwise, 
        // we just tack it onto the end.  This is rare enough to not actually be slow.
        // An element with another full hash only shares the path so far, so it gets its own branch.
        if (flags == NodeFlags.Collision)
        {
            var colNode = Unsafe.As<CollisionNode<T>>(node);
            if (colNode.Hash != hash)
            {
                added = true;
                return SplitCollision(colNode, key, hash, shift, 0);
            }

            var oldSlots = colNode.Slots;
            var length = oldSlots.Length;

            for (var i = 0; i < length; i++)
                if (comparer.Equals(oldSlots[i].Key, key))
                {
                    added = false;
                    return node;
                }

            var appendedSlots = new DataSlot<T>[length + 1];
            //Array.Copy(oldSlots, appendedSlots, length);
            oldSlots.AsSpan(0, length).CopyTo(appendedSlots);
            appendedSlots[length] = DataSlot<T>.Data(key);
            added = true;
            return new CollisionNode<T>(appendedSlots, hash);
        }

        var bit = (hash >> shift) & 0x1F;
        var bitpos = 1u << bit;
        var dataMap = (uint)node.Map;
        var dataIdx = BitOperations.PopCount(dataMap & (bitpos - 1));

        // The Interal node is actually my least favourite node. Her we 
        // check the bitmaps to see if the hash fragment points to local data or a child node.
        if (flags == NodeFlags.Internal)
        {
            var dataArray = NodeOps.GetDataArray<T>(node);

            var nodeMap = (uint)(node.Map >> 32);

            if ((dataMap & bitpos) != 0)
            {
                ref readonly var existingSlot =
                    ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(dataArray!), dataIdx);

                if (comparer.Equals(existingSlot.Key, key))
                {
                    added = false;
                    if (true /* values removed */) return node;

                    var newData0 = new DataSlot<T>[dataArray!.Length];
                    dataArray.AsSpan().CopyTo(newData0);
                    newData0[dataIdx] = new DataSlot<T> { Key = key };


                    var childCap = NodeOps.GetCapacity(node.Meta);
                    var newNode = NodeOps.AllocateInternal<T>(childCap, NodeFlags.Internal, 0, node.Map);
                    Unsafe.As<InternalNode1<T>>(newNode).Data = newData0;


                    NodeOps.GetChildSpan<T>(node).CopyTo(NodeOps.GetChildSpan<T>(newNode));
                    return newNode;
                }

                // Data collision -> Promote data slot to sub-node
                var subNode = MergeDataSlots(existingSlot, key, hash, shift + 5, comparer);
                var newMap = ((ulong)(nodeMap | bitpos) << 32) | (dataMap & ~bitpos);

                var newData = new DataSlot<T>[dataArray!.Length - 1];
                dataArray.AsSpan(0, dataIdx).CopyTo(newData);
                dataArray.AsSpan(dataIdx + 1).CopyTo(newData.AsSpan(dataIdx));

                var childCapCurrent = NodeOps.GetCapacity(node.Meta);
                var newNodeObj =
                    NodeOps.AllocateInternal<T>((byte)(childCapCurrent + 1), NodeFlags.Internal, 0,
                        newMap);
                Unsafe.As<InternalNode1<T>>(newNodeObj).Data = newData;

                var nodeIdx = BitOperations.PopCount(nodeMap & (bitpos - 1));
                var childSpan = NodeOps.GetChildSpan<T>(node);
                var newChildSpan = NodeOps.GetChildSpan<T>(newNodeObj);
                childSpan[..nodeIdx].CopyTo(newChildSpan);
                newChildSpan[nodeIdx] = subNode;
                childSpan[nodeIdx..].CopyTo(newChildSpan[(nodeIdx + 1)..]);

                added = true;
                return newNodeObj;
            }

            if ((nodeMap & bitpos) != 0)
            {
                var nodeIdx = BitOperations.PopCount(nodeMap & (bitpos - 1));

                ref var firstChild =
                    ref Unsafe.As<NodeSlot1, NodeBase>(ref Unsafe.As<InternalNode1<T>>(node).Children);
                var childNode = Unsafe.Add(ref firstChild, nodeIdx);

                var newChildNode = Insert(childNode, key, hash, shift + 5, comparer, out added);

                if (ReferenceEquals(childNode, newChildNode)) return node;

                var childCap = NodeOps.GetCapacity(node.Meta);
                var newNodeObj = NodeOps.AllocateInternal<T>(childCap, NodeFlags.Internal, 0, node.Map);
                Unsafe.As<InternalNode1<T>>(newNodeObj).Data = dataArray;

                var childSpan = NodeOps.GetChildSpan<T>(node);
                var newChildSpan = NodeOps.GetChildSpan<T>(newNodeObj);
                childSpan.CopyTo(newChildSpan);
                newChildSpan[nodeIdx] = newChildNode;

                return newNodeObj;
            }

            // Empty slot
            added = true;
            var appendedData = new DataSlot<T>[dataArray!.Length + 1];
            dataArray.AsSpan(0, dataIdx).CopyTo(appendedData);
            appendedData[dataIdx] = new DataSlot<T> { Key = key };
            dataArray.AsSpan(dataIdx).CopyTo(appendedData.AsSpan(dataIdx + 1));

            var childCapEmpty = NodeOps.GetCapacity(node.Meta);
            var appendedNodeObj =
                NodeOps.AllocateInternal<T>(childCapEmpty, NodeFlags.Internal, 0, node.Map | bitpos);
            Unsafe.As<InternalNode1<T>>(appendedNodeObj).Data = appendedData;

            NodeOps.GetChildSpan<T>(node).CopyTo(NodeOps.GetChildSpan<T>(appendedNodeObj));

            return appendedNodeObj;
        }

        // The following handles leaf nodes like this: it's basically a cut-down version of the internal node 
        // logic, since leaves don't have sub-nodes. Sorry for the code duplication.

        if ((dataMap & bitpos) != 0)
        {
            ref var firstSlot =
                ref Unsafe.As<LeafSlot1<T>, DataSlot<T>>(ref Unsafe.As<Node1<T>>(node).Data);
            ref readonly var existingSlot = ref Unsafe.Add(ref firstSlot, dataIdx);

            if (comparer.Equals(existingSlot.Key, key))
            {
                added = false;
                if (true /* values removed */) return node;

                var leafCap = NodeOps.GetCapacity(node.Meta);
                var updatedLeaf = NodeOps.AllocateLeaf<T>(leafCap, NodeFlags.None, 0, node.Map);

                var oldLeafSpan = NodeOps.GetLeafDataSpan<T>(node);
                var newSpan = NodeOps.GetLeafDataSpan<T>(updatedLeaf);
                oldLeafSpan.CopyTo(newSpan);
                newSpan[dataIdx] = new DataSlot<T> { Key = key };
                return updatedLeaf;
            }

            // Leaf collision -> Must upgrade to InternalNode
            added = true;
            var subNode = MergeDataSlots(existingSlot, key, hash, shift + 5, comparer);
            var newMap = ((ulong)bitpos << 32) | (dataMap & ~bitpos);

            var leafSpan = NodeOps.GetLeafDataSpan<T>(node);
            var newData = new DataSlot<T>[leafSpan.Length - 1];
            leafSpan[..dataIdx].CopyTo(newData);
            leafSpan[(dataIdx + 1)..].CopyTo(newData.AsSpan(dataIdx));

            var newNodeObj = NodeOps.AllocateInternal<T>(1, NodeFlags.Internal, 0, newMap);
            Unsafe.As<InternalNode1<T>>(newNodeObj).Data = newData;
            NodeOps.GetChildSpan<T>(newNodeObj)[0] = subNode;

            return newNodeObj;
        }

        // Empty slot in LeafNode -> Append inline
        added = true;
        var currentLeafCap = NodeOps.GetCapacity(node.Meta);
        var expandedLeaf =
            NodeOps.AllocateLeaf<T>((byte)(currentLeafCap + 1), NodeFlags.None, 0, node.Map | bitpos);

        var leafSpanOld = NodeOps.GetLeafDataSpan<T>(node);
        var expandedSpan = NodeOps.GetLeafDataSpan<T>(expandedLeaf);

        leafSpanOld[..dataIdx].CopyTo(expandedSpan);
        expandedSpan[dataIdx] = new DataSlot<T> { Key = key };
        leafSpanOld[dataIdx..].CopyTo(expandedSpan[(dataIdx + 1)..]);

        return expandedLeaf;
    }

    // The following handles merging two values that hash to the same spot. 
    // it figures out how deep we have to go before their hashes finally differ, and builds 
    // up the necessary nodes along the way.
    private static NodeBase MergeDataSlots<T>(DataSlot<T> existingSlot, T newKey, int newHash,
        int shift, IEqualityComparer<T> comparer, ulong ownerId = 0)
    {
        var existingHash = comparer.GetHashCode(existingSlot.Key!);

        // 1. Full 32-bit hash collision
        if (existingHash == newHash)
            return new CollisionNode<T>([
                existingSlot,
                new DataSlot<T> { Key = newKey }
            ], newHash, ownerId);

        var existingBit = (existingHash >> shift) & 0x1F;
        var newBit = (newHash >> shift) & 0x1F;

        // 2. Hashes diverge at this specific bit depth
        if (existingBit != newBit)
        {
            var dataMap = (1u << existingBit) | (1u << newBit);
            var leafNode = NodeOps.AllocateLeaf<T>(2, NodeFlags.None, ownerId, dataMap);
            var span = NodeOps.GetLeafDataSpan<T>(leafNode);

            if (existingBit < newBit)
            {
                span[0] = existingSlot;
                span[1] = new DataSlot<T> { Key = newKey };
            }
            else
            {
                span[0] = new DataSlot<T> { Key = newKey };
                span[1] = existingSlot;
            }

            return leafNode;
        }

        // 3. Bits are identical at this depth, recurse deeper
        var nodeMap = 1u << existingBit;
        var subNode = MergeDataSlots(existingSlot, newKey, newHash, shift + 5, comparer, ownerId);

        var newNodeObj =
            NodeOps.AllocateInternal<T>(1, NodeFlags.Internal, ownerId, (ulong)nodeMap << 32);
        Unsafe.As<InternalNode1<T>>(newNodeObj).Data = Array.Empty<DataSlot<T>>();
        NodeOps.GetChildSpan<T>(newNodeObj)[0] = subNode;

        return newNodeObj;
    }

    private static NodeBase SingleLeaf<T>(DataSlot<T> slot, int hash, int shift, ulong ownerId)
    {
        var leaf = NodeOps.AllocateLeaf<T>(1, NodeFlags.None, ownerId, 1u << ((hash >> shift) & 0x1F));
        NodeOps.GetLeafDataSpan<T>(leaf)[0] = slot;
        return leaf;
    }

    // Replaces a collision node at `shift` with a branch holding it and an element whose full hash
    // differs from the node's, nesting one-child branches while their hash bits still agree.
    // The hashes differ, so they part by shift 30 and the collision node ends no deeper than before.
    internal static NodeBase SplitCollision<T>(CollisionNode<T> colNode, T newKey, int newHash, int shift,
        ulong ownerId)
    {
        var colBit = (colNode.Hash >> shift) & 0x1F;
        var newBit = (newHash >> shift) & 0x1F;

        if (colBit != newBit)
        {
            var map = ((ulong)(1u << colBit) << 32) | (1u << newBit);
            var branch = NodeOps.AllocateInternal<T>(1, NodeFlags.Internal, ownerId, map);
            Unsafe.As<InternalNode1<T>>(branch).Data = [new DataSlot<T> { Key = newKey }];
            NodeOps.GetChildSpan<T>(branch)[0] = colNode;
            return branch;
        }

        var subNode = SplitCollision(colNode, newKey, newHash, shift + 5, ownerId);
        var wrapper = NodeOps.AllocateInternal<T>(1, NodeFlags.Internal, ownerId, (ulong)(1u << colBit) << 32);
        Unsafe.As<InternalNode1<T>>(wrapper).Data = Array.Empty<DataSlot<T>>();
        NodeOps.GetChildSpan<T>(wrapper)[0] = subNode;
        return wrapper;
    }

    // Key lookup! it zips down the tree, decoding the bitmap
    // at each level to figure out exactly which array index holds our data or next node.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Contains<T>(NodeBase? node, T key, int hash, IEqualityComparer<T> comparer)
    {
        return ContainsAt(node, key, hash, 0, comparer);
    }

    // Lookup in a subtree whose root sits at `shift`.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsAt<T>(NodeBase? node, T key, int hash, int shift, IEqualityComparer<T> comparer)
    {
        var current = node;

        while (current != null)
        {
            var meta = current.Meta;
            var flags = (byte)((meta >> 8) & 0xFF);

            if (flags == (byte)NodeFlags.Collision)
            {
                var colNode = Unsafe.As<CollisionNode<T>>(current);
                if (colNode.Hash != hash) break;

                var slots = colNode.Slots;
                for (var i = 0; i < slots.Length; i++)
                    if (comparer.Equals(slots[i].Key, key))
                    {
                        
                        return true;
                    }

                break;
            }

            var bit = (hash >> shift) & 0x1F;
            var bitpos = 1u << bit;
            var map = current.Map;
            var dataMap = (uint)map;

            if (flags == (byte)NodeFlags.Internal)
            {
                if ((dataMap & bitpos) != 0)
                {
                    var dataIdx = BitOperations.PopCount(dataMap & (bitpos - 1));
                    var dataArray = NodeOps.GetDataArray<T>(current);

                    ref readonly var slot = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(dataArray!),
                        dataIdx);

                    if (comparer.Equals(slot.Key, key))
                    {
                        
                        return true;
                    }

                    break;
                }

                var nodeMap = (uint)(map >> 32);
                if ((nodeMap & bitpos) != 0)
                {
                    var nodeIdx = BitOperations.PopCount(nodeMap & (bitpos - 1));

                    ref var firstChild =
                        ref Unsafe.As<NodeSlot1, NodeBase>(ref Unsafe.As<InternalNode1<T>>(current).Children);
                    current = Unsafe.Add(ref firstChild, nodeIdx);

                    shift += 5;
                    continue;
                }

                break;
            }

            // Leaf Node Phase
            if ((dataMap & bitpos) != 0)
            {
                var dataIdx = BitOperations.PopCount(dataMap & (bitpos - 1));

                ref var firstSlot =
                    ref Unsafe.As<LeafSlot1<T>, DataSlot<T>>(ref Unsafe.As<Node1<T>>(current).Data);
                ref readonly var slot = ref Unsafe.Add(ref firstSlot, dataIdx);

                if (comparer.Equals(slot.Key, key))
                {
                    
                    return true;
                }
            }

            break;
        }

        
        return false;
    }

    // Yes, I am the code-duplication-batman!
    // See if you can see the difference to the above function
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetKey<T>(NodeBase? node, T key, int hash, IEqualityComparer<T> comparer,
        out T value)
    {
        var shift = 0;
        var current = node;

        while (current != null)
        {
            var meta = current.Meta;
            var flags = (byte)((meta >> 8) & 0xFF);

            if (flags == (byte)NodeFlags.Collision)
            {
                var colNode = Unsafe.As<CollisionNode<T>>(current);
                if (colNode.Hash != hash) break;

                var slots = colNode.Slots;
                for (var i = 0; i < slots.Length; i++)
                    if (comparer.Equals(slots[i].Key, key))
                    {
                        value = slots[i].Key;
                        return true;
                    }

                break;
            }

            var bit = (hash >> shift) & 0x1F;
            var bitpos = 1u << bit;
            var map = current.Map;
            var dataMap = (uint)map;

            if (flags == (byte)NodeFlags.Internal)
            {
                if ((dataMap & bitpos) != 0)
                {
                    var dataIdx = BitOperations.PopCount(dataMap & (bitpos - 1));
                    var dataArray = NodeOps.GetDataArray<T>(current);

                    ref readonly var slot = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(dataArray!),
                        dataIdx);

                    if (comparer.Equals(slot.Key, key))
                    {
                        value = slot.Key;
                        return true;
                    }

                    break;
                }

                var nodeMap = (uint)(map >> 32);
                if ((nodeMap & bitpos) != 0)
                {
                    var nodeIdx = BitOperations.PopCount(nodeMap & (bitpos - 1));

                    ref var firstChild =
                        ref Unsafe.As<NodeSlot1, NodeBase>(ref Unsafe.As<InternalNode1<T>>(current).Children);
                    current = Unsafe.Add(ref firstChild, nodeIdx);

                    shift += 5;
                    continue;
                }

                break;
            }

            // Leaf Node Phase
            if ((dataMap & bitpos) != 0)
            {
                var dataIdx = BitOperations.PopCount(dataMap & (bitpos - 1));

                ref var firstSlot =
                    ref Unsafe.As<LeafSlot1<T>, DataSlot<T>>(ref Unsafe.As<Node1<T>>(current).Data);
                ref readonly var slot = ref Unsafe.Add(ref firstSlot, dataIdx);

                if (comparer.Equals(slot.Key, key))
                {
                    value = slot.Key;
                    return true;
                }
            }

            break;
        }

        
        value = default!;
        return false;
    }

// The following handles deleting a key from the trie. it hunts down the node, strips out
// the data slot, and then painstakingly re-compacts the tree if a node becomes too sparse. 
// It's a complete nightmare to read, and for that, I am not that sorry.
    public static NodeBase? Remove<T>(NodeBase? node, T key, int hash, int shift, IEqualityComparer<T> comparer,
        out bool removed)
    {
        if (node == null)
        {
            removed = false;
            return null;
        }

        var flags = NodeOps.GetFlags(node.Meta);

        // Phase 1: Collision Node
        if (flags == NodeFlags.Collision)
        {
            var colNode = Unsafe.As<CollisionNode<T>>(node);
            var slots = colNode.Slots;

            if (colNode.Hash == hash)
                for (var i = 0; i < slots.Length; i++)
                    if (comparer.Equals(slots[i].Key, key))
                    {
                        removed = true;
                        if (slots.Length == 1) return null; // Should be rare, collisions usually start at 2

                        // The survivor becomes a one-slot leaf, which the parent pulls up into its own data.
                        if (slots.Length == 2)
                            return SingleLeaf(slots[1 - i], hash, shift, 0);

                        var updatedSlots = new DataSlot<T>[slots.Length - 1];
                        slots.AsSpan(0, i).CopyTo(updatedSlots);
                        slots.AsSpan(i + 1).CopyTo(updatedSlots.AsSpan(i));
                        return new CollisionNode<T>(updatedSlots, hash);
                    }

            removed = false;
            return node;
        }

        var bit = (hash >> shift) & 0x1F;
        var bitpos = 1u << bit;
        var dataMap = (uint)node.Map;

        // Phase 2: Internal Node
        if (flags == NodeFlags.Internal)
        {
            var dataArray = NodeOps.GetDataArray<T>(node);
            var nodeMap = (uint)(node.Map >> 32);

            // Case 2a
            if ((dataMap & bitpos) != 0)
            {
                var dataIdx = BitOperations.PopCount(dataMap & (bitpos - 1));

                ref readonly var slot = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(dataArray!), dataIdx);

                if (comparer.Equals(slot.Key, key))
                {
                    removed = true;
                    var newMap = node.Map & ~(ulong)bitpos;
                    var childCap = NodeOps.GetCapacity(node.Meta);

                    if (dataArray!.Length == 1 && childCap == 0)
                        return null;

                    var newData = new DataSlot<T>[dataArray.Length - 1];
                    dataArray.AsSpan(0, dataIdx).CopyTo(newData);
                    dataArray.AsSpan(dataIdx + 1).CopyTo(newData.AsSpan(dataIdx));

                    if (childCap == 0)
                    {
                        var leaf = NodeOps.AllocateLeaf<T>((byte)newData.Length, NodeFlags.None, 0,
                            newMap);
                        newData.CopyTo(NodeOps.GetLeafDataSpan<T>(leaf));
                        return leaf;
                    }

                    var newNodeObj =
                        NodeOps.AllocateInternal<T>(childCap, NodeFlags.Internal, 0, newMap);
                    Unsafe.As<InternalNode1<T>>(newNodeObj).Data = newData;

                    // Sorry about all the unsafe stuff, folks, but that means I can move the getspan all the way down here
                    // where we really need it. It gives me about 5-20% speed increase.
                    NodeOps.GetChildSpan<T>(node).CopyTo(NodeOps.GetChildSpan<T>(newNodeObj));
                    return newNodeObj;
                }
            }

            // Case 2b
            if ((nodeMap & bitpos) != 0)
            {
                var nodeIdx = BitOperations.PopCount(nodeMap & (bitpos - 1));

                // I would love to be able to use the getxyzspan from nodeops
                // but this we don't always need the spans, and doing it like this means quite a substantial speedup.
                ref var firstChild =
                    ref Unsafe.As<NodeSlot1, NodeBase>(ref Unsafe.As<InternalNode1<T>>(node).Children);
                var childNode = Unsafe.Add(ref firstChild, nodeIdx);

                var newChild = Remove<T>(childNode, key, hash, shift + 5, comparer, out removed);

                if (!removed) return node;

                var childCap = NodeOps.GetCapacity(node.Meta);

                if (newChild == null)
                {
                    var newMap = node.Map & ~((ulong)bitpos << 32);

                    if (childCap == 1)
                    {
                        if (dataArray!.Length == 0) return null;

                        var leaf = NodeOps.AllocateLeaf<T>((byte)dataArray.Length, NodeFlags.None, 0, newMap);
                        dataArray.CopyTo(NodeOps.GetLeafDataSpan<T>(leaf));
                        return leaf;
                    }

                    var newNodeObj =
                        NodeOps.AllocateInternal<T>((byte)(childCap - 1), NodeFlags.Internal, 0, newMap);
                    Unsafe.As<InternalNode1<T>>(newNodeObj).Data = dataArray;

                    // Here we do a copy, and thus need spans. 
                    var childSpan = NodeOps.GetChildSpan<T>(node);
                    var newChildSpan = NodeOps.GetChildSpan<T>(newNodeObj);
                    childSpan[..nodeIdx].CopyTo(newChildSpan);
                    childSpan[(nodeIdx + 1)..].CopyTo(newChildSpan[nodeIdx..]);

                    return newNodeObj;
                }

                // CHAMP Compaction
                if (NodeOps.GetFlags(newChild.Meta) == NodeFlags.None)
                {
                    var nextChildCap = NodeOps.GetCapacity(newChild.Meta);
                    if (nextChildCap == 1)
                    {
                        var singleData = NodeOps.GetLeafDataSpan<T>(newChild)[0];

                        var newMap = (node.Map & ~((ulong)bitpos << 32)) | bitpos;
                        var newDataIdx = BitOperations.PopCount((uint)newMap & (bitpos - 1));

                        var newData = new DataSlot<T>[dataArray!.Length + 1];
                        dataArray.AsSpan(0, newDataIdx).CopyTo(newData);
                        newData[newDataIdx] = singleData;
                        dataArray.AsSpan(newDataIdx).CopyTo(newData.AsSpan(newDataIdx + 1));

                        if (childCap == 1)
                        {
                            var leaf = NodeOps.AllocateLeaf<T>((byte)newData.Length, NodeFlags.None, 0, newMap);
                            newData.CopyTo(NodeOps.GetLeafDataSpan<T>(leaf));
                            return leaf;
                        }

                        var newNodeObj =
                            NodeOps.AllocateInternal<T>((byte)(childCap - 1), NodeFlags.Internal, 0, newMap);
                        Unsafe.As<InternalNode1<T>>(newNodeObj).Data = newData;

                        var childSpan = NodeOps.GetChildSpan<T>(node);
                        var newChildSpan = NodeOps.GetChildSpan<T>(newNodeObj);
                        childSpan[..nodeIdx].CopyTo(newChildSpan);
                        childSpan[(nodeIdx + 1)..].CopyTo(newChildSpan[nodeIdx..]);

                        return newNodeObj;
                    }
                }

                // Normal sub-node replacement
                var updatedNodeObj = NodeOps.AllocateInternal<T>(childCap, NodeFlags.Internal, 0, node.Map);
                Unsafe.As<InternalNode1<T>>(updatedNodeObj).Data = dataArray;

                var childSpanOld = NodeOps.GetChildSpan<T>(node);
                var updatedChildSpan = NodeOps.GetChildSpan<T>(updatedNodeObj);
                childSpanOld.CopyTo(updatedChildSpan);
                updatedChildSpan[nodeIdx] = newChild;

                return updatedNodeObj;
            }

            removed = false;
            return node;
        }

        // Phase 3: Leaf Node
        if ((dataMap & bitpos) != 0)
        {
            var dataIdx = BitOperations.PopCount(dataMap & (bitpos - 1));
            var leafSpan = NodeOps.GetLeafDataSpan<T>(node);

            if (comparer.Equals(leafSpan[dataIdx].Key, key))
            {
                removed = true;
                if (leafSpan.Length == 1) return null;

                var newMap = node.Map & ~(ulong)bitpos;
                var updatedLeaf =
                    NodeOps.AllocateLeaf<T>((byte)(leafSpan.Length - 1), NodeFlags.None, 0, newMap);
                var newSpan = NodeOps.GetLeafDataSpan<T>(updatedLeaf);

                leafSpan[..dataIdx].CopyTo(newSpan);
                leafSpan[(dataIdx + 1)..].CopyTo(newSpan[dataIdx..]);

                return updatedLeaf;
            }
        }

        removed = false;
        return node;
    }

    // Elements in a subtree. Nodes store no counts, so this walks every node below `node`.
    internal static int CountElements<T>(NodeBase node)
    {
        var flags = NodeOps.GetFlags(node.Meta);
        if (flags == NodeFlags.Collision) return Unsafe.As<CollisionNode<T>>(node).Slots.Length;

        var count = BitOperations.PopCount((uint)node.Map);
        if (flags == NodeFlags.Internal)
            foreach (var child in NodeOps.GetChildSpan<T>(node))
                count += CountElements<T>(child);
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSingleLeaf(NodeBase node, NodeFlags flags)
    {
        return flags == NodeFlags.None && BitOperations.PopCount((uint)node.Map) == 1;
    }

    // Index of `key` in a collision node's slots, or -1.
    private static int IndexIn<T>(CollisionNode<T> col, T key, IEqualityComparer<T> comparer)
    {
        var slots = col.Slots;
        for (var i = 0; i < slots.Length; i++)
            if (comparer.Equals(slots[i].Key, key))
                return i;
        return -1;
    }

    private static CollisionNode<T> Append<T>(CollisionNode<T> col, DataSlot<T> slot)
    {
        var slots = new DataSlot<T>[col.Slots.Length + 1];
        col.Slots.AsSpan().CopyTo(slots);
        slots[^1] = slot;
        return new CollisionNode<T>(slots, col.Hash);
    }

    /// <summary>
    ///     Union of two subtrees rooted at `shift`. Of two equal elements the one from `node1` is kept.
    ///     `overlap` grows by the number of `node2` elements already in `node1`.
    ///     Returns `node1` itself when `node2` adds nothing to it.
    /// </summary>
    public static NodeBase? Merge<T>(
        NodeBase? node1,
        NodeBase? node2,
        int shift,
        IEqualityComparer<T> comparer,
        ref int overlap)
    {
        if (node1 == null) return node2;
        if (node2 == null) return node1;

        // A shared subtree is its own union, and all of its elements overlap.
        if (ReferenceEquals(node1, node2))
        {
            overlap += CountElements<T>(node1);
            return node1;
        }

        var flags1 = NodeOps.GetFlags(node1.Meta);
        var flags2 = NodeOps.GetFlags(node2.Meta);

        if (flags1 == NodeFlags.Collision)
        {
            var col1 = Unsafe.As<CollisionNode<T>>(node1);
            var h = col1.Hash;

            if (flags2 == NodeFlags.Collision && Unsafe.As<CollisionNode<T>>(node2).Hash == h)
            {
                var result = col1;
                foreach (var slot in Unsafe.As<CollisionNode<T>>(node2).Slots)
                    if (IndexIn(col1, slot.Key, comparer) >= 0) overlap++;
                    else result = Append(result, slot);
                return result;
            }

            // A one-element leaf, e.g. the micro-leaf made for an inline element below.
            if (IsSingleLeaf(node2, flags2))
            {
                var d2 = NodeOps.GetLeafDataSpan<T>(node2)[0];
                var h2 = comparer.GetHashCode(d2.Key!);
                if (h2 != h) return SplitCollision(col1, d2.Key, h2, shift, 0);
                if (IndexIn(col1, d2.Key, comparer) < 0) return Append(col1, d2);
                overlap++;
                return node1;
            }

            // Insert the collision node's elements one by one into the other side. Insert keeps an
            // equal element already there, which here is the right one, so that one is removed first.
            NodeBase? current = node2;
            foreach (var slot in col1.Slots)
            {
                if (ContainsAt(current, slot.Key, h, shift, comparer))
                {
                    overlap++;
                    current = Remove(current, slot.Key, h, shift, comparer, out _);
                }

                current = Insert(current, slot.Key, h, shift, comparer, out _);
            }

            return current;
        }

        if (flags2 == NodeFlags.Collision)
        {
            var col2 = Unsafe.As<CollisionNode<T>>(node2);
            var h = col2.Hash;

            if (IsSingleLeaf(node1, flags1))
            {
                var d1 = NodeOps.GetLeafDataSpan<T>(node1)[0];
                var h1 = comparer.GetHashCode(d1.Key!);
                if (h1 != h) return SplitCollision(col2, d1.Key, h1, shift, 0);

                var i = IndexIn(col2, d1.Key, comparer);
                if (i < 0) return Append(col2, d1);
                overlap++;
                var slots = (DataSlot<T>[])col2.Slots.Clone();
                slots[i] = d1;
                return new CollisionNode<T>(slots, h);
            }

            // Insert leaves an equal left element in place, and returns the same node if it adds nothing.
            var current = node1;
            foreach (var slot in col2.Slots)
            {
                current = Insert(current, slot.Key, h, shift, comparer, out var added);
                if (!added) overlap++;
            }

            return current;
        }

        // Fast path: Both are leaves with no overlapping bits
        if (flags1 == NodeFlags.None && flags2 == NodeFlags.None)
        {
            var map1 = (uint)node1.Map;
            var map2 = (uint)node2.Map;
            if ((map1 & map2) == 0)
            {
                var combinedMap = map1 | map2;
                var totalCount = BitOperations.PopCount(combinedMap);
                var newLeaf = NodeOps.AllocateLeaf<T>((byte)totalCount, NodeFlags.None, 0, combinedMap);
                var destSpan = NodeOps.GetLeafDataSpan<T>(newLeaf);
                var span = NodeOps.GetLeafDataSpan<T>(node1);
                var span0 = NodeOps.GetLeafDataSpan<T>(node2);

                int idx1 = 0, idx2 = 0, destIdx = 0;
                var tempMap = combinedMap;
                while (tempMap != 0)
                {
                    var bit = BitOperations.TrailingZeroCount(tempMap);
                    var bitpos = 1u << bit;
                    if ((map1 & bitpos) != 0)
                        destSpan[destIdx++] = span[idx1++];
                    else
                        destSpan[destIdx++] = span0[idx2++];
                    tempMap &= ~bitpos;
                }

                return newLeaf;
            }
        }

        var dataMap1 = (uint)node1.Map;
        var nodeMap1 = flags1 == NodeFlags.Internal ? (uint)(node1.Map >> 32) : 0;
        var span1 = flags1 == NodeFlags.Internal
            ? NodeOps.GetDataArray<T>(node1).AsSpan()
            : NodeOps.GetLeafDataSpan<T>(node1);
        var nodes1 = flags1 == NodeFlags.Internal
            ? NodeOps.GetChildSpan<T>(node1)
            : Span<NodeBase>.Empty;

        var dataMap2 = (uint)node2.Map;
        var nodeMap2 = flags2 == NodeFlags.Internal ? (uint)(node2.Map >> 32) : 0;
        var span2 = flags2 == NodeFlags.Internal
            ? NodeOps.GetDataArray<T>(node2).AsSpan()
            : NodeOps.GetLeafDataSpan<T>(node2);
        var nodes2 = flags2 == NodeFlags.Internal
            ? NodeOps.GetChildSpan<T>(node2)
            : Span<NodeBase>.Empty;
        var allBits = dataMap1 | nodeMap1 | dataMap2 | nodeMap2;

        var pooledData = ArrayPool<DataSlot<T>>.Shared.Rent(32);
        var pooledNodes = ArrayPool<NodeBase>.Shared.Rent(32);

        var dataCount = 0;
        var nodeCount = 0;
        ulong finalDataMap = 0;
        ulong finalNodeMap = 0;
        // Whether the result differs from node1; if not, node1 is returned instead of a copy.
        var changed = false;

        var tempBits = allBits;
        while (tempBits != 0)
        {
            var bit = BitOperations.TrailingZeroCount(tempBits);
            var bitpos = 1u << bit;
            tempBits &= ~bitpos;

            var hasData1 = (dataMap1 & bitpos) != 0;
            var hasNode1 = (nodeMap1 & bitpos) != 0;
            var hasData2 = (dataMap2 & bitpos) != 0;
            var hasNode2 = (nodeMap2 & bitpos) != 0;

            DataSlot<T> d1 = default;
            NodeBase? n1 = null;
            if (hasData1) d1 = span1[BitOperations.PopCount(dataMap1 & (bitpos - 1))];
            if (hasNode1) n1 = nodes1[BitOperations.PopCount(nodeMap1 & (bitpos - 1))];

            DataSlot<T> d2 = default;
            NodeBase? n2 = null;
            if (hasData2) d2 = span2[BitOperations.PopCount(dataMap2 & (bitpos - 1))];
            if (hasNode2) n2 = nodes2[BitOperations.PopCount(nodeMap2 & (bitpos - 1))];

            // Case 1: Populated exclusively in tree 1
            if ((hasData1 || hasNode1) && !(hasData2 || hasNode2))
            {
                if (hasData1)
                {
                    pooledData[dataCount++] = d1;
                    finalDataMap |= bitpos;
                }
                else
                {
                    pooledNodes[nodeCount++] = n1!;
                    finalNodeMap |= bitpos;
                }
            }
            // Case 2: Populated exclusively in tree 2
            else if (!(hasData1 || hasNode1) && (hasData2 || hasNode2))
            {
                changed = true;
                if (hasData2)
                {
                    pooledData[dataCount++] = d2;
                    finalDataMap |= bitpos;
                }
                else
                {
                    pooledNodes[nodeCount++] = n2!;
                    finalNodeMap |= bitpos;
                }
            }
            // Case 3: Both contain inline data slots
            else if (hasData1 && hasData2)
            {
                if (comparer.Equals(d1.Key, d2.Key))
                {
                    overlap++;
                    pooledData[dataCount++] = d1;
                    finalDataMap |= bitpos;
                }
                else
                {
                    changed = true;
                    var h2 = comparer.GetHashCode(d2.Key!);
                    var subNode = MergeDataSlots(d1, d2.Key, h2, shift + 5, comparer);
                    pooledNodes[nodeCount++] = subNode;
                    finalNodeMap |= bitpos;
                }
            }
            // Case 4: Both elements contain internal sub-nodes
            else if (hasNode1 && hasNode2)
            {
                var subNode = Merge(n1, n2, shift + 5, comparer, ref overlap)!;
                if (!ReferenceEquals(subNode, n1)) changed = true;
                pooledNodes[nodeCount++] = subNode;
                finalNodeMap |= bitpos;
            }
            // Case 5: Layer mismatch. Wrap slot into a micro-leaf and run pure Merge.
            else if (hasData1 && hasNode2)
            {
                changed = true;
                var h1 = comparer.GetHashCode(d1.Key!);
                var microLeaf = SingleLeaf(d1, h1, shift + 5, 0);
                pooledNodes[nodeCount++] = Merge(microLeaf, n2, shift + 5, comparer, ref overlap)!;
                finalNodeMap |= bitpos;
            }
            else if (hasNode1 && hasData2)
            {
                var h2 = comparer.GetHashCode(d2.Key!);
                var microLeaf = SingleLeaf(d2, h2, shift + 5, 0);
                var subNode = Merge(n1, microLeaf, shift + 5, comparer, ref overlap)!;
                if (!ReferenceEquals(subNode, n1)) changed = true;
                pooledNodes[nodeCount++] = subNode;
                finalNodeMap |= bitpos;
            }
        }

        if (!changed)
        {
            ArrayPool<DataSlot<T>>.Shared.Return(pooledData);
            ArrayPool<NodeBase>.Shared.Return(pooledNodes);
            return node1;
        }

        NodeBase resultNode;
        if (nodeCount == 0 && dataCount > 0)
        {
            resultNode = NodeOps.AllocateLeaf<T>((byte)dataCount, NodeFlags.None, 0, finalDataMap);
            pooledData.AsSpan(0, dataCount).CopyTo(NodeOps.GetLeafDataSpan<T>(resultNode));
        }
        else
        {
            var finalData = new DataSlot<T>[dataCount];
            pooledData.AsSpan(0, dataCount).CopyTo(finalData);

            resultNode = NodeOps.AllocateInternal<T>((byte)nodeCount, NodeFlags.Internal, 0,
                finalDataMap | (finalNodeMap << 32));
            Unsafe.As<InternalNode1<T>>(resultNode).Data = finalData;

            var resultNodesSpan = NodeOps.GetChildSpan<T>(resultNode);
            pooledNodes.AsSpan(0, nodeCount).CopyTo(resultNodesSpan);
        }

        ArrayPool<DataSlot<T>>.Shared.Return(pooledData);
        ArrayPool<NodeBase>.Shared.Return(pooledNodes);

        return resultNode;
    }

    public static bool Iter<T>(NodeBase? node, Func<T, bool> action)
    {
        if (node == null) return true;

        var flags = NodeOps.GetFlags(node.Meta);

        if (flags == NodeFlags.None)
        {
            var span = NodeOps.GetLeafDataSpan<T>(node);
            for (var i = 0; i < span.Length; i++)
                if (!action(span[i].Key))
                    return false;
            return true;
        }

        if (flags == NodeFlags.Internal)
        {
            var dataArray = NodeOps.GetDataArray<T>(node);
            if (dataArray != null)
                for (var i = 0; i < dataArray.Length; i++)
                    if (dataArray[i].Key != null && !action(dataArray[i].Key))
                        return false;

            var childSpan = NodeOps.GetChildSpan<T>(node);
            for (var i = 0; i < childSpan.Length; i++)
                if (!Iter(childSpan[i], action))
                    return false;

            return true;
        }

        // CollisionNode
        var colNode = Unsafe.As<CollisionNode<T>>(node);
        var slots = colNode.Slots;

        for (var i = 0; i < slots.Length; i++)
            if (!action(slots[i].Key))
                return false;


        return true;
    }
}
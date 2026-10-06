namespace Set;

// Holds elements that all share one full hash, stored in Hash so inserts can tell a real
// collision from an element that only shares the path so far.
internal sealed class CollisionNode<T> : NodeBase
{
    public DataSlot<T>[] Slots;
    public readonly int Hash;

    public CollisionNode(DataSlot<T>[] slots, int hash)
    {
        Slots = slots;
        Hash = hash;
        // capacity is not used by colissionnodes.
        Meta = NodeOps.PackMeta(0, NodeFlags.Collision, 0);
    }

    public CollisionNode(DataSlot<T>[] slots, int hash, ulong ownerId)
    {
        Slots = slots;
        Hash = hash;
        // capacity is not used by colissionnodes.
        Meta = NodeOps.PackMeta(0, NodeFlags.Collision, ownerId);
    }
}

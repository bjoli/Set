namespace Set;

internal sealed class CollisionNode<T> : NodeBase
{
    public DataSlot<T>[] Slots;

    public CollisionNode(DataSlot<T>[] slots)
    {
        Slots = slots;
        // capacity is not used by colissionnodes.
        Meta = NodeOps.PackMeta(0, NodeFlags.Collision, 0);
    }

    public CollisionNode(DataSlot<T>[] slots, ulong ownerId)
    {
        Slots = slots;
        // capacity is not used by colissionnodes.
        Meta = NodeOps.PackMeta(0, NodeFlags.Collision, ownerId);
    }
}
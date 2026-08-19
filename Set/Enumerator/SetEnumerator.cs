using System.Collections;
using System.Runtime.CompilerServices;

namespace Set;

internal struct StackFrame
{
    public NodeBase Node;
    public int DataIndex;
    public int NodeIndex;
}

[InlineArray(8)]
internal struct EnumeratorStack
{
    private StackFrame _element0;
}

public struct SetEnumerator<T> : IEnumerator<T>
{
    private EnumeratorStack _stack;
    private int _depth;
    private DataSlot<T> _current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SetEnumerator(NodeBase? root)
    {
        _current = default;
        if (root == null)
        {
            _depth = -1;
        }
        else
        {
            _depth = 0;
            _stack[0] = new StackFrame { Node = root, DataIndex = 0, NodeIndex = 0 };
        }
    }

    public readonly T Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current.Key;
    }

    readonly object IEnumerator.Current => Current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        while (_depth >= 0)
        {
            ref var frame = ref _stack[_depth];
            var flags = NodeOps.GetFlags(frame.Node.Meta);

            if (flags == NodeFlags.None)
            {
                var span = NodeOps.GetLeafDataSpan<T>(frame.Node);
                if (frame.DataIndex < span.Length)
                {
                    _current = span[frame.DataIndex++];
                    return true;
                }
            }
            else if (flags == NodeFlags.Internal)
            {
                var dataArray = NodeOps.GetDataArray<T>(frame.Node);

                if (dataArray != null && frame.DataIndex < dataArray.Length)
                {
                    _current = dataArray[frame.DataIndex++];
                    return true;
                }

                var childSpan = NodeOps.GetChildSpan<T>(frame.Node);
                if (frame.NodeIndex < childSpan.Length)
                {
                    var nextChild = childSpan[frame.NodeIndex++];
                    _depth++;
                    _stack[_depth] = new StackFrame { Node = nextChild, DataIndex = 0, NodeIndex = 0 };
                    continue;
                }
            }
            else // CollisionNode
            {
                var colNode = Unsafe.As<CollisionNode<T>>(frame.Node);
                if (frame.DataIndex < colNode.Slots.Length)
                {
                    _current = colNode.Slots[frame.DataIndex++];
                    return true;
                }
            }

            _depth--; // Pop the stack when the current node is exhausted
        }

        return false;
    }

    public readonly void Dispose()
    {
    }

    public void Reset()
    {
        throw new NotSupportedException();
    }
}
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace Set;

// So, the main idea of this was stolen from the vector implementation in scala, where every shift level
// has its own node type. This works less well in c#, but using unsafe casts to span, we can avoid dealing
// with the kind combinatorics explosion of actually having the runtime deal with all the types.
//
// The casting is done simply by looking at the length. 

[InlineArray(1)]
internal struct LeafSlot1<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(2)]
internal struct LeafSlot2<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(3)]
internal struct LeafSlot3<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(4)]
internal struct LeafSlot4<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(5)]
internal struct LeafSlot5<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(6)]
internal struct LeafSlot6<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(7)]
internal struct LeafSlot7<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(8)]
internal struct LeafSlot8<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(9)]
internal struct LeafSlot9<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(10)]
internal struct LeafSlot10<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(11)]
internal struct LeafSlot11<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(12)]
internal struct LeafSlot12<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(13)]
internal struct LeafSlot13<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(14)]
internal struct LeafSlot14<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(15)]
internal struct LeafSlot15<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(16)]
internal struct LeafSlot16<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(17)]
internal struct LeafSlot17<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(18)]
internal struct LeafSlot18<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(19)]
internal struct LeafSlot19<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(20)]
internal struct LeafSlot20<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(21)]
internal struct LeafSlot21<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(22)]
internal struct LeafSlot22<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(23)]
internal struct LeafSlot23<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(24)]
internal struct LeafSlot24<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(25)]
internal struct LeafSlot25<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(26)]
internal struct LeafSlot26<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(27)]
internal struct LeafSlot27<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(28)]
internal struct LeafSlot28<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(29)]
internal struct LeafSlot29<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(30)]
internal struct LeafSlot30<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(31)]
internal struct LeafSlot31<T>
{
    private DataSlot<T> _element0;
}

[InlineArray(32)]
internal struct LeafSlot32<T>
{
    private DataSlot<T> _element0;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node1<T> : NodeBase
{
    public LeafSlot1<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node2<T> : NodeBase
{
    public LeafSlot2<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node3<T> : NodeBase
{
    public LeafSlot3<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node4<T> : NodeBase
{
    public LeafSlot4<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node5<T> : NodeBase
{
    public LeafSlot5<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node6<T> : NodeBase
{
    public LeafSlot6<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node7<T> : NodeBase
{
    public LeafSlot7<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node8<T> : NodeBase
{
    public LeafSlot8<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node9<T> : NodeBase
{
    public LeafSlot9<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node10<T> : NodeBase
{
    public LeafSlot10<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node11<T> : NodeBase
{
    public LeafSlot11<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node12<T> : NodeBase
{
    public LeafSlot12<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node13<T> : NodeBase
{
    public LeafSlot13<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node14<T> : NodeBase
{
    public LeafSlot14<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node15<T> : NodeBase
{
    public LeafSlot15<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node16<T> : NodeBase
{
    public LeafSlot16<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node17<T> : NodeBase
{
    public LeafSlot17<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node18<T> : NodeBase
{
    public LeafSlot18<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node19<T> : NodeBase
{
    public LeafSlot19<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node20<T> : NodeBase
{
    public LeafSlot20<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node21<T> : NodeBase
{
    public LeafSlot21<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node22<T> : NodeBase
{
    public LeafSlot22<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node23<T> : NodeBase
{
    public LeafSlot23<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node24<T> : NodeBase
{
    public LeafSlot24<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node25<T> : NodeBase
{
    public LeafSlot25<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node26<T> : NodeBase
{
    public LeafSlot26<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node27<T> : NodeBase
{
    public LeafSlot27<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node28<T> : NodeBase
{
    public LeafSlot28<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node29<T> : NodeBase
{
    public LeafSlot29<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node30<T> : NodeBase
{
    public LeafSlot30<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node31<T> : NodeBase
{
    public LeafSlot31<T> Data;
}

[StructLayout(LayoutKind.Sequential)]
internal sealed class Node32<T> : NodeBase
{
    public LeafSlot32<T> Data;
}
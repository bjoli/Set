using System.Collections;
using System.Runtime.CompilerServices;

namespace Set;

public struct MapKeyEnumerator<T> : IEnumerator<T>
{
    private MapEnumerator<T> _inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal MapKeyEnumerator(NodeBase? root)
    {
        _inner = new MapEnumerator<T>(root);
    }

    public readonly T Current => _inner.Current.Key;

    readonly object? IEnumerator.Current => Current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return _inner.MoveNext();
    }

    public void Reset()
    {
        _inner.Reset();
    }

    public readonly void Dispose()
    {
    }
}

public struct MapValueEnumerator<T> : IEnumerator<TV>
{
    private MapEnumerator<T> _inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal MapValueEnumerator(NodeBase? root)
    {
        _inner = new MapEnumerator<T>(root);
    }

    public readonly TV Current => _inner.Current.Value;

    readonly object? IEnumerator.Current => Current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return _inner.MoveNext();
    }

    public void Reset()
    {
        _inner.Reset();
    }

    public readonly void Dispose()
    {
    }
}

public readonly struct MapKeyCollection<T> : IReadOnlyCollection<T>
{
    private readonly NodeBase? _root;

    internal MapKeyCollection(NodeBase? root, int count)
    {
        _root = root;
        Count = count;
    }

    public int Count { get; }

    public MapKeyEnumerator<T> GetEnumerator()
    {
        return new MapKeyEnumerator<T>(_root);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return new MapKeyEnumerator<T>(_root);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new MapKeyEnumerator<T>(_root);
    }
}

public readonly struct MapValueCollection<T> : IReadOnlyCollection<TV>
{
    private readonly NodeBase? _root;

    internal MapValueCollection(NodeBase? root, int count)
    {
        _root = root;
        Count = count;
    }

    public int Count { get; }

    public MapValueEnumerator<T> GetEnumerator()
    {
        return new MapValueEnumerator<T>(_root);
    }

    IEnumerator<TV> IEnumerable<TV>.GetEnumerator()
    {
        return new MapValueEnumerator<T>(_root);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new MapValueEnumerator<T>(_root);
    }
}
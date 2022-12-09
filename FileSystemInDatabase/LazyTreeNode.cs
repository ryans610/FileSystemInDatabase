using System.Linq;

namespace FileSystemInDatabase;

internal class LazyTreeNode<T> : IEnumerable<LazyTreeNode<T>>
{
    public LazyTreeNode(
        T data,
        Func<T, IEnumerable<LazyTreeNode<T>>> childrenNodeProvider)
    {
        Data = data;
        _childrenNodeProvider = childrenNodeProvider;
    }

    public LazyTreeNode(
        T data,
        Func<T, IEnumerable<LazyTreeNode<T>>> childrenNodeProvider,
        LazyTreeNode<T> parent)
        : this(data, childrenNodeProvider)
    {
        Parent = parent;
    }

    private readonly Func<T, IEnumerable<LazyTreeNode<T>>> _childrenNodeProvider;
    private ReadOnlyCollection<LazyTreeNode<T>> _children = null;

    // TODO: add reader writer lock for T that is not thread-safe to assign
    public T Data { get; set; }

    public LazyTreeNode<T> Parent { get; }

    public IEnumerable<LazyTreeNode<T>> Children => PrepareChildren();

    public bool IsRoot => Parent is null;

    private ReadOnlyCollection<LazyTreeNode<T>> PrepareChildren()
    {
        var children = _children; // defensive copy
        if (children is not null)
        {
            return children;
        }

        children = _childrenNodeProvider
            .Invoke(Data)
            .ToReadOnlyCollection();
        _children = children;

        return children;
    }

    public void ClearChildren()
    {
        _children = null;
    }

    public IEnumerator<LazyTreeNode<T>> GetEnumerator()
    {
        var children = Children
            .SelectMany(x => x)
            .ToReadOnlyCollection(); // defensive copy
        yield return this;
        foreach (var child in children)
        {
            yield return child;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

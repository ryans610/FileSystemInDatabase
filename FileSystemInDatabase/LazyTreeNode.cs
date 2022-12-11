using System.Linq;

namespace FileSystemInDatabase;

internal class LazyTreeNode<T> : IEnumerable<LazyTreeNode<T>>
{
    public LazyTreeNode(
        T data,
        Func<LazyTreeNode<T>, LazyTreeNode<T>> parentNodeProvider,
        Func<LazyTreeNode<T>, IEnumerable<LazyTreeNode<T>>> childrenNodeProvider)
    {
        Data = data;
        _parentNodeProvider = parentNodeProvider;
        _childrenNodeProvider = childrenNodeProvider;
    }

    private readonly Func<LazyTreeNode<T>, LazyTreeNode<T>> _parentNodeProvider;
    private readonly Func<LazyTreeNode<T>, IEnumerable<LazyTreeNode<T>>> _childrenNodeProvider;
    private LazyTreeNode<T> _parent = null;
    private ReadOnlyCollection<LazyTreeNode<T>> _children = null;

    // TODO: add reader writer lock for T that is not thread-safe to assign
    public T Data { get; set; }

    public LazyTreeNode<T> Parent => PrepareParent();

    public IEnumerable<LazyTreeNode<T>> Children => PrepareChildren();

    private LazyTreeNode<T> PrepareParent()
    {
        var parent = _parent;
        if (parent is not null)
        {
            return parent;
        }

        parent = _parentNodeProvider.Invoke(this);
        _parent = parent;
        return parent;
    }

    // ReSharper disable once ReturnTypeCanBeEnumerable.Local
    private ReadOnlyCollection<LazyTreeNode<T>> PrepareChildren()
    {
        var children = _children; // defensive copy
        if (children is not null)
        {
            return children;
        }

        children = _childrenNodeProvider
            .Invoke(this)
            .ToReadOnlyCollection();
        _children = children;

        return children;
    }

    public void ClearParent()
    {
        _parent = null;
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

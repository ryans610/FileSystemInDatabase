using System.Threading;
using Microsoft.Extensions.Hosting;

namespace FileSystemInDatabase;

internal class FileSystem : IFileSystem, IHostedService
{
    public FileSystem(FileSystemRepository repo)
    {
        _repo = repo;
        var startUpCompleteSource = new CancellationTokenSource();
        _startUpCompleteSource = startUpCompleteSource;
        _startUpCompleteToken = startUpCompleteSource.Token;
    }

    private readonly FileSystemRepository _repo;
    private readonly ConcurrentDictionary<Guid, LazyTreeNode<Node>> _nodes = new();

    private LazyTreeNode<Node> _root;

    private readonly CancellationTokenSource _startUpCompleteSource;
    private readonly CancellationToken _startUpCompleteToken;

    [PublicAPI]
    public Node GetNodeById(Guid id)
    {
        if (!IsStartUpComplete())
        {
            WaitingForStartUpCompleteAsync().GetAwaiter().GetResult();
        }

        return CollectionExtensions.GetValueOrDefault(_nodes, id)?.Data;
    }

    [PublicAPI]
    public IEnumerable<Node> GetNodesUnderFolder(Guid folderId)
    {
        if (!IsStartUpComplete())
        {
            WaitingForStartUpCompleteAsync().GetAwaiter().GetResult();
        }

        return GetFolderNodeOrThrow(folderId).Children.Select(x => x.Data);
    }

    [PublicAPI]
    public string GetFullPathOfNode(Guid nodeId)
    {
        if (!IsStartUpComplete())
        {
            WaitingForStartUpCompleteAsync().GetAwaiter().GetResult();
        }

        if (!_nodes.TryGetValue(nodeId, out var treeNode))
        {
            throw new InvalidOperationException(
                $"Node {nodeId} does not exists.");
        }

        var path = GetSelfAndAncestorsOfNode(treeNode)
            .Select(x => x.Data.Name)
            .Reverse()
            .JoinAsString('\\');
        var extension = treeNode.Data is FileNode file ? file.Extension : string.Empty;
        return $"{path}{extension}";
    }

    [PublicAPI]
    public IEnumerable<FolderNode> GetSubFoldersUnderFolder(Guid folderId)
    {
        if (!IsStartUpComplete())
        {
            WaitingForStartUpCompleteAsync().GetAwaiter().GetResult();
        }

        return GetFolderNodeOrThrow(folderId)
            .Children
            .Select(x => x.Data)
            .OfType<FolderNode>();
    }

    [PublicAPI]
    public IEnumerable<FileNode> GetFilesUnderFolder(Guid folderId)
    {
        if (!IsStartUpComplete())
        {
            WaitingForStartUpCompleteAsync().GetAwaiter().GetResult();
        }

        return GetFolderNodeOrThrow(folderId)
            .Children
            .Select(x => x.Data)
            .OfType<FileNode>();
    }

    [PublicAPI]
    public IEnumerable<FileNode> SearchFilesUnderFolderAndSubFolders(
        Guid folderId,
        Func<FileNode, bool> predicate)
    {
        if (!IsStartUpComplete())
        {
            WaitingForStartUpCompleteAsync().GetAwaiter().GetResult();
        }

        if (!_nodes.TryGetValue(folderId, out var folder))
        {
            return Enumerable.Empty<FileNode>();
        }

        return folder
            .Select(x => x.Data)
            .OfType<FileNode>()
            .Where(predicate);
    }

    [PublicAPI]
    public async Task AddNodeToFolderAsync(Node node, Guid folderId)
    {
        if (!IsStartUpComplete())
        {
            await WaitingForStartUpCompleteAsync();
        }

        node = node with { Id = Guid.NewGuid() };
        var folder = GetFolderNodeOrThrow(folderId);
        var treeNode = new LazyTreeNode<Node>(
            node,
            LazyTreeChildrenProvider,
            parent: folder);
        _nodes.TryAdd(node.Id, treeNode);
        TryClearNodeChildren(folderId);

        //TODO: database
    }

    [PublicAPI]
    public async Task MoveNodeToFolderAsync(Guid nodeId, Guid folderId)
    {
        if (!IsStartUpComplete())
        {
            await WaitingForStartUpCompleteAsync();
        }

        var folder = GetFolderNodeOrThrow(folderId);
        if (!_nodes.TryGetValue(nodeId, out var node))
        {
            throw new InvalidOperationException(
                $"Node {nodeId} does not exists.");
        }

        var oldParent = node.Parent;
        var newNode = node.Data with
        {
            ParentId = folderId,
        };
        if (_nodes.TryGetValue(nodeId, out var n))
        {
            n.Data = newNode;
        }

        TryClearNodeChildren(oldParent.Data.Id);
        TryClearNodeChildren(folderId);

        //TODO: database
    }

    [PublicAPI]
    public async Task DeleteFolderAsync(Guid folderId)
    {
        if (!IsStartUpComplete())
        {
            await WaitingForStartUpCompleteAsync();
        }

        if (!TryGetFolderNode(folderId, out var folderNode))
        {
            return;
        }

        if (folderNode.Data is not FolderNode folder)
        {
            throw new InvalidOperationException(
                $"Node {folderId} is not a folder.");
        }

        var nodesToBeDelete = folderNode.ToList();
        nodesToBeDelete.ForEach(x => _nodes.TryRemove(x.Data.Id, out _));
        TryClearNodeChildren(folderNode.Parent.Data.Id);

        var parentNodeIds = nodesToBeDelete
            .Select(x => x.Data.ParentId)
            .Where(x => x.HasValue)
            .Select(x => x.Value)
            .Prepend(folderId)
            .Distinct();
        await _repo.DeleteFolderNodeAsync(folderId, parentNodeIds);
    }

    [PublicAPI]
    public async Task DeleteFileAsync(Guid fileId)
    {
        if (!IsStartUpComplete())
        {
            await WaitingForStartUpCompleteAsync();
        }

        if (_nodes.TryRemove(fileId, out var fileNode))
        {
            if (fileNode.Data is not FileNode)
            {
                throw new InvalidOperationException(
                    $"Node {fileId} is not a file.");
            }

            TryClearNodeChildren(fileNode.Parent.Data.Id);

            await _repo.DeleteFileNodeAsync(fileId);
        }
    }

    private IEnumerable<LazyTreeNode<Node>> GetSelfAndAncestorsOfNode(LazyTreeNode<Node> treeNode)
    {
        yield return treeNode;
        while (treeNode.Data.Id != Guid.Empty)
        {
            treeNode = treeNode.Parent;
            yield return treeNode;
        }
    }

    private bool TryGetFolderNode(Guid folderId, out LazyTreeNode<Node> node)
    {
        return _nodes.TryGetValue(folderId, out node) &&
               node.Data.Type == Node.Types.Folder;
    }

    private LazyTreeNode<Node> GetFolderNodeOrThrow(Guid folderId)
    {
        if (!TryGetFolderNode(folderId, out var node))
        {
            throw new InvalidOperationException(
                $"{folderId} is not a folder or does not exists.");
        }

        return node;
    }

    private void TryClearNodeChildren(Guid nodeId)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.ClearChildren();
        }
    }

    private IEnumerable<LazyTreeNode<Node>> LazyTreeChildrenProvider(Node node)
    {
        return _nodes.Values.Where(x => x.Data.ParentId == node.Id);
    }

    private bool IsStartUpComplete() => _startUpCompleteToken.IsCancellationRequested;

    private async Task WaitingForStartUpCompleteAsync()
    {
        if (IsStartUpComplete())
        {
            return;
        }

        var tcs = new TaskCompletionSource();
        _startUpCompleteToken.Register(() => tcs.SetResult());
        await tcs.Task;
    }

    #region HostedService

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        //var nodes = await _repo.GetAllNodesAsync();

        _root = new LazyTreeNode<Node>(
            new FolderNode
            {
                Id = Guid.Empty,
                Name = "C:",
                ParentId = Guid.Empty,
            },
            LazyTreeChildrenProvider);
        _nodes.TryAdd(Guid.Empty, _root);

        var n = new LazyTreeNode<Node>(new FileNode
        {
            Id = Guid.NewGuid(),
            ParentId = Guid.Empty,
            Name = "Foo",
            Extension = ".jpg",
        }, LazyTreeChildrenProvider, _root);
        _nodes.TryAdd(n.Data.Id, n);

        _root.ClearChildren();

        _startUpCompleteSource.Cancel();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    #endregion HostedService
}

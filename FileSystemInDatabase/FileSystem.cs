namespace FileSystemInDatabase;

internal class FileSystem : IFileSystem, IHostedService
{
    public FileSystem(IOptions<FileSystemOptions> options, FileSystemRepository repo)
    {
        _options = options;
        _repo = repo;
        var startUpCompleteSource = new CancellationTokenSource();
        _startUpCompleteSource = startUpCompleteSource;
        _startUpCompleteToken = startUpCompleteSource.Token;
    }

    private readonly IOptions<FileSystemOptions> _options;
    private readonly FileSystemRepository _repo;

    private readonly CancellationTokenSource _startUpCompleteSource;
    private readonly CancellationToken _startUpCompleteToken;
    private readonly CancellationTokenSource _shutDownCancellationSource = new();
#pragma warning disable IDE0052 // 刪除未讀取的私用成員
    private Task _houseKeepingTask;
    private Task _trackingDatabaseChangeTask;
#pragma warning restore IDE0052 // 刪除未讀取的私用成員
    private TimeSpan _changeTrackingInterval = TimeSpan.Zero;
    private long _trackingDatabaseChangeVersion = 0L;

    private readonly ConcurrentDictionary<Guid, LazyTreeNode<Node>> _nodes = new();

    [PublicAPI]
    public Node GetNodeById(Guid id)
    {
        CheckAndWaitingForStartUpComplete();

        return CollectionExtensions.GetValueOrDefault(_nodes, id)?.Data;
    }

    [PublicAPI]
    public IEnumerable<Node> GetNodesUnderFolder(Guid folderId)
    {
        CheckAndWaitingForStartUpComplete();

        return GetFolderNodeOrThrow(folderId).Children.Select(x => x.Data);
    }

    [PublicAPI]
    public string GetFullPathOfNode(Guid nodeId)
    {
        CheckAndWaitingForStartUpComplete();

        if (!_nodes.TryGetValue(nodeId, out var treeNode))
        {
            throw new InvalidOperationException(
                $"Node {nodeId} does not exists.");
        }

        var path = GetSelfAndAncestorsOfNode(treeNode)
            .Select(x => x.Data.FullName)
            .Reverse()
            .JoinAsString('\\');
        return path;
    }

    [PublicAPI]
    public IEnumerable<FolderNode> GetSubFoldersUnderFolder(Guid folderId)
    {
        CheckAndWaitingForStartUpComplete();

        return GetFolderNodeOrThrow(folderId)
            .Children
            .Select(x => x.Data)
            .OfType<FolderNode>();
    }

    [PublicAPI]
    public IEnumerable<FileNode> GetFilesUnderFolder(Guid folderId)
    {
        CheckAndWaitingForStartUpComplete();

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
        CheckAndWaitingForStartUpComplete();

        return GetFolderNodeOrThrow(folderId)
            .Select(x => x.Data)
            .OfType<FileNode>()
            .Where(predicate);
    }

    [PublicAPI]
    public async Task<Guid> AddSubFolderToFolderAsync(string subFolderName, Guid folderId)
    {
        await CheckAndWaitingForStartUpCompleteAsync();

        var folder = GetFolderNodeOrThrow(folderId);

        if (folder.Children.Select(x => x.Data.Name).Contains(subFolderName))
        {
            throw new InvalidOperationException(
                $"Folder {folder.Data.Name} already contains node named {subFolderName}.");
        }

        var node = new FolderNode
        {
            Id = Guid.NewGuid(),
            IsRoot = false,
            ParentId = folderId,
            Name = subFolderName,
        };

        await _repo.InsertFolderNodeAsync(node);

        await UpdateSingleNodeFromDatabaseAsync(node.Id);

        return node.Id;
    }

    [PublicAPI]
    public async Task<Guid> AddFileToFolderAsync(string fileName, byte[] content, Guid folderId)
    {
        await CheckAndWaitingForStartUpCompleteAsync();

        var folder = GetFolderNodeOrThrow(folderId);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        if (folder.Children.Select(x => x.Data.Name).Contains(fileNameWithoutExtension))
        {
            throw new InvalidOperationException(
                $"Folder {folder.Data.Name} already contains node named {fileNameWithoutExtension}.");
        }

        var node = new FileNode
        {
            Id = Guid.NewGuid(),
            Name = fileNameWithoutExtension,
            ParentId = folderId,
            Extension = Path.GetExtension(fileName),
            Content = content,
        };

        await _repo.InsertFileNodeAsync(node);

        await UpdateSingleNodeFromDatabaseAsync(node.Id);

        return node.Id;
    }

    [PublicAPI]
    public async Task MoveNodeToFolderAsync(Guid nodeId, Guid folderId)
    {
        await CheckAndWaitingForStartUpCompleteAsync();

        var targetFolder = GetFolderNodeOrThrow(folderId);
        if (!_nodes.TryGetValue(nodeId, out var node))
        {
            throw new InvalidOperationException(
                $"Node {nodeId} does not exists.");
        }

        if (node.Data.ParentId == folderId)
        {
            throw new InvalidOperationException(
                $"Node {node.Data.FullName} already in folder {targetFolder.Data.FullName}.");
        }

        if (targetFolder.Children.Select(x => x.Data.Name).Contains(node.Data.Name))
        {
            throw new InvalidOperationException(
                $"Folder {targetFolder.Data.Name} already contains node named {node.Data.Name}.");
        }

        var oldParentId = node.Parent.Data.Id;

        var newParentId = await _repo.ChangeNodeParentAsync(nodeId, folderId);
        if (newParentId == oldParentId)
        {
            // move fail or move back
            return;
        }

        if (_nodes.TryGetValue(nodeId, out var n))
        {
            n.Data = node.Data with
            {
                ParentId = newParentId,
            };
            n.ClearParent();
        }

        TryClearNodeChildren(oldParentId);
        TryClearNodeChildren(newParentId);
    }

    [PublicAPI]
    public async Task DeleteFolderAsync(Guid folderId)
    {
        await CheckAndWaitingForStartUpCompleteAsync();

        if (!TryGetFolderNode(folderId, out var folderNode))
        {
            return;
        }

        if (folderNode.Data is not FolderNode)
        {
            throw new InvalidOperationException(
                $"Node {folderNode.Data.FullName} is not a folder.");
        }

        var nodesToBeDelete = folderNode.ToList();
        nodesToBeDelete.ForEach(x => _nodes.TryRemove(x.Data.Id, out _));
        TryClearNodeChildren(folderNode.Parent.Data.Id);

        var parentNodeIds = nodesToBeDelete
            .Select(x => x.Data.ParentId)
            .Prepend(folderId)
            .Distinct()
            .Where(x => x != folderNode.Data.ParentId);
        await _repo.DeleteFolderNodeAsync(folderId, parentNodeIds);
    }

    [PublicAPI]
    public async Task DeleteFileAsync(Guid fileId)
    {
        await CheckAndWaitingForStartUpCompleteAsync();

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
        while (treeNode.Data.Id != treeNode.Data.ParentId)
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

    private async Task UpdateSingleNodeFromDatabaseAsync(Guid nodeId)
    {
        var node = await _repo.GetNodeByIdAsync(nodeId);
        if (node is null)
        {
            if (_nodes.TryRemove(nodeId, out var deleteTreeNode))
            {
                TryClearNodeChildren(deleteTreeNode.Parent.Data.Id);
            }
        }
        else
        {
            UpdateSingleNode(node);
        }
    }

    private void UpdateSingleNode(Node node)
    {
        if (_nodes.TryGetValue(node.Id, out var treeNode))
        {
            var oldParentId = treeNode.Data.ParentId;
            var childrenIds = treeNode.Children.Select(x => x.Data.Id).ToList();
            treeNode.Data = node;
            TryClearNodeChildren(oldParentId);
            TryClearNodeChildren(node.Id);
            childrenIds.ForEach(x => TryClearNodeParent(x));
        }
        else
        {
            _nodes.TryAdd(node.Id, new LazyTreeNode<Node>(
                node,
                LazyTreeParentProvider,
                LazyTreeChildrenProvider));
            TryClearNodeChildren(node.ParentId);
        }
    }

    private void TryClearNodeParent(Guid nodeId)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.ClearParent();
        }
    }

    private void TryClearNodeChildren(Guid nodeId)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.ClearChildren();
        }
    }

    #region TreeNodeProvider

    private LazyTreeNode<Node> LazyTreeParentProvider(LazyTreeNode<Node> treeNode)
    {
        if (treeNode.Data.IsRoot)
        {
            return treeNode;
        }

        return _nodes.TryGetValue(treeNode.Data.ParentId, out var result) ? result : null;
    }

    private IEnumerable<LazyTreeNode<Node>> LazyTreeChildrenProvider(LazyTreeNode<Node> treeNode)
    {
        var id = treeNode.Data.Id;
        return _nodes.Values.Where(x => x.Data.Id != id && x.Data.ParentId == id);
    }

    #endregion TreeNodeProvider

    private void CheckAndWaitingForStartUpComplete()
    {
        if (_startUpCompleteToken.IsCancellationRequested)
        {
            return;
        }

        WaitingForStartUpCompleteAsync().GetAwaiter().GetResult();
    }

    private async Task CheckAndWaitingForStartUpCompleteAsync()
    {
        if (_startUpCompleteToken.IsCancellationRequested)
        {
            return;
        }

        await WaitingForStartUpCompleteAsync();
    }

    private async Task WaitingForStartUpCompleteAsync()
    {
        var tcs = new TaskCompletionSource();
        _startUpCompleteToken.Register(() => tcs.SetResult());
        await tcs.Task;
    }

    #region HostedService

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var nodes = await _repo.GetAllNodesAsync();
            foreach (var node in nodes)
            {
                _nodes.TryAdd(node.Id, new LazyTreeNode<Node>(
                    node,
                    LazyTreeParentProvider,
                    LazyTreeChildrenProvider));
            }

            _startUpCompleteSource.Cancel();

            if (_options.Value.HouseKeepingInterval != TimeSpan.Zero)
            {
                _houseKeepingTask = Task.Factory.StartNew(
                    HouseKeepingAsync,
                    TaskCreationOptions.LongRunning);
            }


            _changeTrackingInterval = _options.Value.TrackingChangeInterval;
            if (_changeTrackingInterval == TimeSpan.Zero)
            {
                _changeTrackingInterval = TimeSpan.FromSeconds(10);
            }

            var version = await _repo.GetInitialChangeTableVersion();
            Interlocked.Exchange(ref _trackingDatabaseChangeVersion, version);
            _trackingDatabaseChangeTask = Task.Factory.StartNew(
                TrackingDatabaseChangeAsync,
                TaskCreationOptions.LongRunning);
        }
        catch (Exception)
        {
            // TODO: Error Handling
            throw;
        }
        finally
        {
            _startUpCompleteSource.Cancel();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _shutDownCancellationSource.Cancel();
        _houseKeepingTask = null;
        _trackingDatabaseChangeTask = null;
        return Task.CompletedTask;
    }

    #endregion HostedService

    #region BackgroundTask

    private async Task HouseKeepingAsync()
    {
        var cancellationToken = _shutDownCancellationSource.Token;
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(_options.Value.HouseKeepingInterval, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await _repo.HouseKeepingOnceAsync();
            }
            catch (Exception)
            {
                // TODO: Error Handling
                //ignore
            }
        }
    }

    private async Task TrackingDatabaseChangeAsync()
    {
        var cancellationToken = _shutDownCancellationSource.Token;
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(_changeTrackingInterval, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                long version = Interlocked.Read(ref _trackingDatabaseChangeVersion);
                var changes = await _repo.FetchTableChanges(_trackingDatabaseChangeVersion);
                foreach (var change in changes)
                {
                    version = Math.Max(version, change.SYS_CHANGE_VERSION);
                    switch (change.SYS_CHANGE_OPERATION)
                    {
                        case 'I':
                        case 'U':
                            await UpdateSingleNodeFromDatabaseAsync(change.Id);
                            break;
                        case 'D':
                            if (_nodes.TryRemove(change.Id, out var deleteTreeNode))
                            {
                                TryClearNodeChildren(deleteTreeNode.Parent.Data.Id);
                            }

                            break;
                    }
                }

                Interlocked.Exchange(ref _trackingDatabaseChangeVersion, version);
            }
            catch (Exception)
            {
                // TODO: Error Handling
                //ignore
            }
        }
    }

    #endregion BackgroundTask
}

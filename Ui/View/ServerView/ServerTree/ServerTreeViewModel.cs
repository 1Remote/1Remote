using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
using _1RM.Utils;
using _1RM.View.Editor;
using _1RM.View.ServerList;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.View.ServerView.ServerTree
{
    public partial class ServerTreeViewModel : ServerPageViewModelBase
    {
        public const string FullPathSeparator = " ]=+=+=+=>[ ";
        public const string FolderNodePrefix = "%$TreeNode$%:";
        public class TreeNode : NotifyPropertyChangedBase
        {
            private string _name = "";
            public string Name
            {
                get => _name;
                private set => SetAndNotifyIfChanged(ref _name, value);
            }

            public bool IsRootFolder { get; private init; }
            public bool IsFolder { get; private init; }

            private int _customOrder;
            public int CustomOrder
            {
                get
                {
                    if (Server != null) return Server.CustomOrder;
                    return _customOrder;
                }
                set
                {
                    if (Server != null) Server.CustomOrder = value;
                    _customOrder = value;
                }
            }

            public string Id => Server?.Id ?? FolderNodePrefix + Name;
            public ProtocolBaseViewModel? Server { get; private init; }
            public TreeNode? ParentNode;
            public ObservableCollection<TreeNode> Children { get; } = new ObservableCollection<TreeNode>();

            /// <summary>
            /// The full path to this node in the tree (e.g., "LocalDataSource->Folder1->SubFolder")
            /// </summary>
            public string FullPath { get; set; } = "";

            internal bool _isExpanded = true; // Make internal so the parent class can access it
            public bool IsExpanded
            {
                get => _isExpanded;
                set
                {
                    if (SetAndNotifyIfChanged(ref _isExpanded, value))
                    {
                        // Save expansion state to local cache when node expansion changes
                        if (IsFolder && !string.IsNullOrEmpty(FullPath))
                        {
                            LocalityTreeViewService.Settings.TreeNodeExpansionStates[FullPath] = value;
                            LocalityTreeViewService.Save();
                        }
                    }
                }
            }

            private bool _isSelected = true;
            public bool IsSelected
            {
                get => false; // disable selection for tree nodes
                set => _isSelected = value;
            }


            private bool? _isCheckboxSelected = false;
            public bool? IsCheckboxSelected
            {
                get => _isCheckboxSelected;
                set
                {
                    if (SetAndNotifyIfChanged(ref _isCheckboxSelected, value))
                    {
                        if (Server != null)
                        {
                            Server.IsSelected = value == true;
                        }
                        if (value != null)
                        {
                            foreach (var child in Children)
                            {
                                // propagate selection to children
                                child.IsCheckboxSelected = value;
                            }
                        }
                    }
                }
            }

            public static TreeNode VirtualRoot { get; } = new TreeNode();
            public static TreeNode NewDatabase(string name)
            {
                var node = new TreeNode()
                {
                    Name = name,
                    IsFolder = true,
                    IsRootFolder = true
                };
                node.SetParent(VirtualRoot);
                return node;
            }

            public static TreeNode NewFolder(string name, TreeNode parentNode)
            {
                var node = new TreeNode()
                {
                    Name = name,
                    IsFolder = true,
                    IsRootFolder = false
                };
                node.SetParent(parentNode);
                return node;
            }
            public static TreeNode NewServer(ProtocolBaseViewModel server, TreeNode parentNode)
            {
                var node = new TreeNode
                {
                    Name = server.DisplayName,
                    IsFolder = false,
                    IsRootFolder = false,
                    Server = server
                };
                node.SetParent(parentNode);
                return node;
            }
            public void SetName(string name)
            {
                Name = name;
                if (IsRootFolder)
                {
                    FullPath = _name;
                }
                else
                {
                    Debug.Assert(ParentNode != null);
                    Debug.Assert(ParentNode.IsFolder == true);
                    FullPath = ParentNode.FullPath + FullPathSeparator + _name;
                }
            }
            public void SetParent(TreeNode parentNode)
            {
                if (IsRootFolder)
                {
                    FullPath = _name;
                }
                else
                {
                    Debug.Assert(parentNode != null);
                    Debug.Assert(parentNode.IsFolder == true);
                    FullPath = parentNode.FullPath + FullPathSeparator + _name;
                }
                if (!parentNode.Children.Contains(this))
                {
                    parentNode.Children.Add(this);
                }
                ParentNode = parentNode;
            }

            public void AddChild(TreeNode child, bool sort)
            {
                if (!IsFolder) throw new InvalidOperationException("Cannot add child to a non-folder node.");
                if (child.ParentNode != this)
                {
                    child.ParentNode?.Children.Remove(child);
                    child.SetParent(this);
                }
                if (!Children.Contains(child))
                    Children.Add(child);
                if (sort)
                {
                    SortNodes(Children, false);
                }
            }

            public List<string> GetFolderNames()
            {
                return Children.Where(x => x.IsFolder == true).Select(x => x.Name).ToList();
            }
            public List<TreeNode> GetChildNodes(bool includingSubFolder, bool needFolderNode = true, bool needItemNode = true)
            {
                var list = new List<TreeNode>();
                var foldersToUpdate = new Queue<TreeNode>();
                foldersToUpdate.Enqueue(this);
                while (foldersToUpdate.Count > 0)
                {
                    var currentFolder = foldersToUpdate.Dequeue();
                    foreach (var child in currentFolder.Children)
                    {
                        if (child.IsFolder && needFolderNode || !child.IsFolder && needItemNode)
                            list.Add(child);
                        if (child.IsFolder && includingSubFolder)
                            foldersToUpdate.Enqueue(child);
                    }
                }
                return list;
            }

            /// <summary>
            /// Find all parent nodes of a target node in the tree. if the target is a root node, return an empty list.
            /// </summary>
            public List<TreeNode> GetParents(bool includeRootNode = true, bool includedMyselfIamFolder = false)
            {
                var list = new List<TreeNode>();
                if (includedMyselfIamFolder && IsFolder)
                {
                    list.Add(this);
                }
                TreeNode? current = this;
                while (true)
                {
                    if (current?.ParentNode != null)
                    {
                        current = current.ParentNode;
                        if (!includeRootNode && current.IsRootFolder)
                        {
                            // If we don't include the root node, stop here
                            break;
                        }
                        list.Add(current);
                        if (current.IsRootFolder)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                list.Reverse();
                return list;
            }

            public TreeNode? GetDataBaseNode()
            {
                TreeNode? current = this;
                while (true)
                {
                    if (current == null || current.IsRootFolder == true)
                    {
                        break;
                    }
                    current = current.ParentNode;
                }
                return current;
            }

            /// <summary>
            /// Check if node is a descendant of mine
            /// </summary>
            public bool FindDescendant(TreeNode node)
            {
                var parentPaths = node.GetParents();
                return parentPaths.Contains(this);
            }
        }

        private ObservableCollection<TreeNode> _rootNodes = new ObservableCollection<TreeNode>();
        public ObservableCollection<TreeNode> RootNodes
        {
            get => _rootNodes;
            set => SetAndNotifyIfChanged(ref _rootNodes, value);
        }

        private TreeNode? _selectedNode;
        public TreeNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                SetAndNotifyIfChanged(ref _selectedNode, value);
                if (value?.Server != null)
                {
                    SelectedServerViewModel = value.Server;
                }
                else
                {
                    SelectedServerViewModel = null;
                }
            }
        }

        private ProtocolBaseViewModel? _selectedServerViewModel;
        public ProtocolBaseViewModel? SelectedServerViewModel
        {
            get => _selectedServerViewModel;
            set => SetAndNotifyIfChanged(ref _selectedServerViewModel, value);
        }

        public bool IsAnySelected => VmServerList.Any(x => x.IsSelected == true);
        public int SelectedCount => VmServerList.Count(x => x.IsSelected);
        public bool? IsSelectedAll
        {
            get
            {
                var items = VmServerList.Where(x => x.IsVisible);
                if (items.All(x => x.IsSelected))
                    return true;
                if (items.Any(x => x.IsSelected))
                    return null;
                return false;
            }
            set
            {
                if (value == false)
                {
                    foreach (var vmServerCard in VmServerList)
                    {
                        vmServerCard.IsSelected = false;
                    }
                }
                else
                {
                    foreach (var protocolBaseViewModel in VmServerList)
                    {
                        protocolBaseViewModel.IsSelected = protocolBaseViewModel.IsVisible;
                    }
                }
                RaisePropertyChanged();
            }
        }

        public ServerTreeViewModel(DataSourceService sourceService, GlobalData appData) : base(sourceService, appData)
        {
            IoC.Get<GlobalData>().OnReloadAll -= BuildView;
            IoC.Get<GlobalData>().OnReloadAll += BuildView;
            if (AppData.VmItemList.Count > 0)
            {
                // this view may be loaded after the data is loaded(when MainWindow start minimized)
                // so we need to rebuild the list here
                BuildView();
            }
        }

        private static void SortNodes(ObservableCollection<TreeNode> nodes, bool includeSubFolder = true)
        {
            if (nodes.Count <= 1)
            {
                if (nodes.FirstOrDefault()?.Children.Count > 0)
                    SortNodes(nodes.First().Children);
                return;
            }
            var orderBy = IoC.Get<MainWindowViewModel>().ServerOrderBy;
            List<TreeNode> sorted;
            switch (orderBy)
            {
                case EnumServerOrderBy.IdAsc:
                    sorted = nodes.OrderBy(n => !n.IsFolder).ThenBy(n => n.Server?.Server?.Id ?? "").ToList();
                    break;
                case EnumServerOrderBy.ProtocolAsc:
                    sorted = nodes.OrderBy(n => !n.IsFolder).ThenBy(n => n.Server?.Server?.Protocol ?? string.Empty).ToList();
                    break;
                case EnumServerOrderBy.ProtocolDesc:
                    sorted = nodes.OrderBy(n => !n.IsFolder).ThenByDescending(n => n.Server?.Server?.Protocol ?? string.Empty).ToList();
                    break;
                case EnumServerOrderBy.NameAsc:
                    sorted = nodes.OrderBy(n => !n.IsFolder).ThenBy(n => n.Name).ToList();
                    break;
                case EnumServerOrderBy.NameDesc:
                    sorted = nodes.OrderBy(n => !n.IsFolder).ThenByDescending(n => n.Name).ToList();
                    break;
                case EnumServerOrderBy.AddressAsc:
                    sorted = nodes.OrderBy(n => !n.IsFolder).ThenBy(n => n.Server?.Server?.SubTitle ?? string.Empty).ToList();
                    break;
                case EnumServerOrderBy.AddressDesc:
                    sorted = nodes.OrderBy(n => !n.IsFolder).ThenByDescending(n => n.Server?.Server?.SubTitle ?? string.Empty).ToList();
                    break;
                case EnumServerOrderBy.Custom:
                    sorted = nodes.OrderBy(n => n.CustomOrder).ToList();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            nodes.Clear();
            foreach (var node in sorted.ToList())
            {
                nodes.Add(node);
                if (includeSubFolder && node.Children.Count > 0)
                {
                    SortNodes(node.Children);
                }
            }
        }

        // Command to edit selected server
        private RelayCommand? _cmdMultiEditFolderServers;
        public RelayCommand CmdMultiEditFolderServers
        {
            get
            {
                return _cmdMultiEditFolderServers ??= new RelayCommand((o) =>
                {
                    if (o is TreeNode node)
                    {
                        if (!node.IsFolder)
                        {
                            if (node.Server?.Server == null) return;
                            GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(node.Server.Server, true);
                        }
                        else if (node.IsFolder)
                        {
                            var serversToEdit = node.GetChildNodes(includingSubFolder: true, needFolderNode: false, needItemNode: true).Select(n => n.Server?.Server).Where(s => s != null).ToList() as List<ProtocolBase>;
                            GlobalEventHelper.OnRequestGoToServerMultipleEditPage?.Invoke(serversToEdit);
                        }
                    }
                });
            }
        }

        #region Context Menu Commands

        // Command to create a new folder
        private RelayCommand? _cmdCreateFolder;
        public RelayCommand CmdCreateFolder
        {
            get
            {
                return _cmdCreateFolder ??= new RelayCommand(async (o) =>
                {
                    TreeNode? parentNode = null;
                    if (o is TreeNode node)
                    {
                        parentNode = node.IsFolder == false ? node.ParentNode : node;
                    }
                    if (parentNode == null) return;

                    var folderName = await InputBoxViewModel.GetValue("TXT: Enter folder name:",
                        (input) =>
                        {
                            if (parentNode.GetFolderNames().Any(x => x == input.Trim())) return IoC.Translate(LanguageService.XXX_IS_ALREADY_EXISTED, input);
                            return string.IsNullOrWhiteSpace(input) ? "TXT: Folder name cannot be empty" : "";
                        },
                        "New Folder",
                        IoC.Get<MainWindowViewModel>());

                    folderName = folderName?.Trim();
                    if (!string.IsNullOrWhiteSpace(folderName))
                    {
                        parentNode.AddChild(TreeNode.NewFolder(folderName, parentNode), true);
                        LocalityTreeViewService.SaveExpansionStates(RootNodes);
                    }
                });
            }
        }

        // Command to rename a node
        private RelayCommand? _cmdEditNode;
        public RelayCommand CmdEditNode
        {
            get
            {
                return _cmdEditNode ??= new RelayCommand(async (o) =>
                {
                    if (o is TreeNode node)
                    {
                        if (!node.IsFolder)
                        {
                            if (node.Server?.Server == null) return;
                            GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(node.Server.Server, true);
                        }
                        else if (node.IsFolder)
                        {
                            if (node.ParentNode == null) return;
                            // Rename folder
                            var oldName = node.Name;
                            var newName = await InputBoxViewModel.GetValue("TXT: Enter new folder name:",
                                (input) =>
                                {
                                    if (node.ParentNode.GetFolderNames().Any(x => x != oldName && x == input.Trim())) return IoC.Translate(LanguageService.XXX_IS_ALREADY_EXISTED, input);
                                    return string.IsNullOrWhiteSpace(input) ? "TXT: Folder name cannot be empty" : "";
                                },
                                node.Name,
                                IoC.Get<MainWindowViewModel>());
                            newName = newName?.Trim();


                            if (!string.IsNullOrWhiteSpace(newName) && newName != node.Name)
                            {
                                node.SetName(newName);
                                LocalityTreeViewService.SaveExpansionStates(RootNodes);
                                UpdateDbServerTreeNodesByCurrentTreeStructure(node);
                            }
                        }
                        else if (node.Server != null)
                        {
                            // Edit server
                            GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(node.Server.Server, true);
                        }
                    }
                });
            }
        }

        // Command to delete a node
        private RelayCommand? _cmdDeleteNode;
        public RelayCommand CmdDeleteNode
        {
            get
            {
                return _cmdDeleteNode ??= new RelayCommand((o) =>
                {
                    if (o is TreeNode node)
                    {
                        if (node.IsFolder)
                        {
                            // Delete folder and move servers to parent or root
                            if (MessageBoxHelper.Confirm($"TXT: Delete folder '{node.Name}' and move its contents to parent folder?"))
                            {
                                var serversToDelete = node.GetChildNodes(includingSubFolder: true, needFolderNode: false, needItemNode: true).Select(n => n.Server?.Server).Where(s => s != null).ToList() as List<ProtocolBase>;
                                node.ParentNode?.Children.Remove(node);
                                LocalityTreeViewService.SaveExpansionStates(RootNodes); // to remove old name cache
                                if (serversToDelete.Any())
                                {
                                    AppData.DeleteServer(serversToDelete);
                                }
                            }
                        }
                        else if (node.Server != null)
                        {
                            // Delete server
                            var server = node.Server.Server;
                            if (server.DataSource?.IsWritable == true)
                            {
                                if (MessageBoxHelper.Confirm(IoC.Translate("do_you_really_want_to_delete_x").Replace("{0}", server.DisplayName)))
                                {
                                    AppData.DeleteServer(new List<ProtocolBase> { server });
                                }
                            }
                        }
                    }
                });
            }
        }

        // Command to add a new server under selected node
        private RelayCommand? _cmdAddServerToNode;
        public RelayCommand CmdAddServerToNode
        {
            get
            {
                return _cmdAddServerToNode ??= new RelayCommand((o) =>
                {
                    List<string> treePath = new List<string>(); // Add to root by default
                    DataSourceBase? dataSource = null;
                    if (o is TreeNode node)
                    {
                        // Build the tree path for the new server
                        var parents = node.GetParents(includedMyselfIamFolder: true);
                        if (!string.IsNullOrEmpty(parents?.First().Name))
                        {
                            var dataSourceName = parents.First().Name;
                            dataSource = IoC.Get<DataSourceService>().GetDataSource(dataSourceName);
                        }
                        // remove the root node from the path if it exists
                        parents = parents?.Where(x => !x.IsRootFolder).ToList();
                        treePath = parents == null ? new List<string>() : parents.Select(x => x.Name).ToList();
                    }
                    // Navigate to add server page
                    GlobalEventHelper.OnGoToServerAddPage?.Invoke(preset: new ServerEditorPageViewModel.ParamsServerAddPreset()
                    {
                        DataSource = dataSource,
                        TreeNodes = treePath,
                    });
                });
            }
        }


        #endregion

        #region Helper Methods


        // Update all servers' TreeNode in the folderNode by current folder structure.
        private void UpdateDbServerTreeNodesByCurrentTreeStructure(TreeNode sourceNode)
        {
            if (sourceNode.IsFolder == false) return;
            var serversToUpdate = new List<ProtocolBase>();
            var foldersToUpdate = new Queue<TreeNode>();
            var foldersToUpdatePaths = new Queue<List<string>>();
            foldersToUpdate.Enqueue(sourceNode);
            foldersToUpdatePaths.Enqueue(sourceNode.GetParents(includeRootNode: false, includedMyselfIamFolder: true).Select(x => x.Name).ToList());
            while (foldersToUpdate.Count > 0)
            {
                var currentFolder = foldersToUpdate.Dequeue();
                var currentPath = foldersToUpdatePaths.Dequeue();
                foreach (var server in currentFolder.Children.Select(x => x.Server?.Server))
                {
                    if (server == null) continue;
                    server.TreeNodes = currentPath;
                    serversToUpdate.Add(server);
                }
                foreach (var child in currentFolder.Children.Where(x => x.IsFolder))
                {
                    var newPath = new List<string>(currentPath) { sourceNode.Name };
                    foldersToUpdate.Enqueue(child);
                    foldersToUpdatePaths.Enqueue(newPath);
                }
            }
            if (serversToUpdate.Count > 0)
            {
                IoC.Get<GlobalData>().UpdateServer(serversToUpdate);
            }
        }

        #endregion

        #region Drag and Drop Support



        /// <summary>
        /// Move a folder node to a target folder node within the same root database
        /// </summary>
        /// <param name="sourceNode">The folder node to move (IsFolder=True)</param>
        /// <param name="targetFolder">The target node (IsFolder=True or IsFolder=False)</param>
        /// <returns>True if the move was successful, false otherwise</returns>
        public bool FolderMoveToFolder(TreeNode sourceNode, TreeNode targetFolder)
        {
            // Cannot move to self
            if (sourceNode == targetFolder) return false;
            if (!sourceNode.IsFolder || sourceNode.IsRootFolder) return false;
            if (targetFolder?.IsFolder == false) targetFolder = targetFolder?.ParentNode!;
            if (targetFolder?.IsFolder != true) return false;
            if (sourceNode.ParentNode == targetFolder) return true; // Don't move if already in target location
            if (sourceNode.GetDataBaseNode() != targetFolder.GetDataBaseNode()) return false; // Only allow moving within the same data source
            if (sourceNode.FindDescendant(targetFolder)) return false; // Cannot move a folder into one of its own descendants
            // check db read-only state
            if (sourceNode.IsFolder)
            {
                var dbNode = sourceNode.GetDataBaseNode();
                var dataSource = IoC.Get<DataSourceService>().GetDataSource(dbNode?.Name ?? "");
                if (dataSource?.IsWritable != true) return false;
            }

            try
            {
                if (targetFolder.Children.Any(x => x.IsFolder && x.Name == sourceNode.Name))
                {
                    // A folder with the same name already exists in the target location, merge instead
                    var existingFolder = targetFolder.Children.First(x => x.IsFolder && x.Name == sourceNode.Name);
                    // Collect all servers in the folder to be moved
                    foreach (var child in sourceNode.Children)
                    {
                        existingFolder.AddChild(child, false);
                    }
                }
                else
                {
                    targetFolder.AddChild(sourceNode, false);
                }

                // Update all Nodes' parent in the moved folder and its subfolders
                {
                    sourceNode.SetParent(targetFolder);
                    var foldersToUpdate = new Queue<TreeNode>();
                    foldersToUpdate.Enqueue(sourceNode);
                    while (foldersToUpdate.Count > 0)
                    {
                        var currentFolder = foldersToUpdate.Dequeue();
                        foreach (var child in currentFolder.Children)
                        {
                            child.SetParent(currentFolder);
                            if (child.IsFolder)
                                foldersToUpdate.Enqueue(child);
                        }
                    }
                    LocalityTreeViewService.SaveExpansionStates(RootNodes);
                }
                SortNodes(targetFolder.Children, false);
                // Update all servers' TreeNode in the moved folder and its subfolders
                UpdateDbServerTreeNodesByCurrentTreeStructure(sourceNode);
                IoC.Get<GlobalData>().ReloadAll();
                return true;
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Warning($"Failed to move folder {sourceNode.Name}: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Move a server node to a target folder node within the same root database
        /// </summary>
        /// <param name="serverNode">The server node to move (IsFolder=False)</param>
        /// <param name="targetFolder">The target node (IsFolder=True or IsFolder=False)</param>
        /// <returns>True if the move was successful, false otherwise</returns>
        public bool ServerMoveToFolder(TreeNode serverNode, TreeNode targetFolder)
        {
            // Cannot move to self
            if (serverNode == targetFolder) return false;
            if (serverNode.IsFolder || serverNode.Server?.Server == null) return false;
            if (targetFolder?.IsFolder == false) targetFolder = targetFolder?.ParentNode!;
            if (targetFolder?.IsFolder != true) return false;
            if (serverNode.ParentNode == targetFolder) return true;   // already in the target folder
            if (serverNode.GetDataBaseNode() != targetFolder.GetDataBaseNode()) return false;   // not in the same root data source
            var server = serverNode.Server.Server;
            var dataSource = server.DataSource;
            if (dataSource?.IsWritable != true) return false;

            // Update the server's TreeNodes property
            try
            {
                // Remove from source parent and add to target folder
                targetFolder.AddChild(serverNode, true);
                // Update the server's TreeNodes
                var targetPaths = targetFolder.GetParents(false, true);
                server.TreeNodes = new List<string>((from t in targetPaths where !t.IsRootFolder select t.Name).ToList());
                IoC.Get<GlobalData>().UpdateServer(server);
                return true;
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Warning($"Failed to move server {server.DisplayName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Move sourceNode to the front or back of targetNode in the same folder
        /// </summary>
        public bool NodeMoveToReorderInSameFolder(TreeNode sourceNode, TreeNode targetNode, bool insertBefore = true)
        {
            if (sourceNode == targetNode) return true;
            if (IoC.Get<MainWindowViewModel>().ServerOrderBy != EnumServerOrderBy.Custom) return false; // Must be custom ordering
            if (sourceNode.ParentNode != targetNode.ParentNode) return false; // Must be in the same parent folder
            var parent = sourceNode.ParentNode!;
            var siblingServers = parent.Children.ToList();
            if (siblingServers.Count < 2) return true; // Nothing to reorder
            try
            {
                // Remove source from current position
                var sourceIndex = siblingServers.IndexOf(sourceNode);
                var targetIndex = siblingServers.IndexOf(targetNode);
                if (sourceIndex - targetIndex == 1 && insertBefore == false) return true;
                if (targetIndex - sourceIndex == 1 && insertBefore == true) return true;
                if (sourceIndex < 0 || targetIndex < 0) return false;

                // remove source and adjust target index if source was before target
                siblingServers.RemoveAt(sourceIndex);
                if (sourceIndex < targetIndex)
                    targetIndex--;

                // Insert at new position
                var insertIndex = insertBefore ? targetIndex : targetIndex + 1;
                if (insertIndex > siblingServers.Count)
                    insertIndex = siblingServers.Count;

                siblingServers.Insert(insertIndex, sourceNode);

                // Update custom order values
                UpdateCustomOrder(siblingServers);
                LocalityTreeViewService.SaveExpansionStates(RootNodes);
                SortNodes(parent.Children, false);
                return true;
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Warning($"Failed to reorder server {sourceNode.Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Update custom order for a list of server view models within the same folder
        /// </summary>
        private void UpdateCustomOrder(List<TreeNode> updatedNodes)
        {
            if (IoC.Get<MainWindowViewModel>().ServerOrderBy != EnumServerOrderBy.Custom) return; // Must be custom ordering
            try
            {
                if (updatedNodes.Count <= 1)
                    return;
                var allNodeOrder = AppData.VmItemList.ToDictionary(x => x.Id, x => 0); // Initialize with 0 order
                // first, update the current tree node order
                var currentTreeViewList = TreeNode.VirtualRoot.GetChildNodes(true);
                for (int i = 0; i < currentTreeViewList.Count; i++)
                {
                    currentTreeViewList[i].CustomOrder = i + 1;
                    allNodeOrder[currentTreeViewList[i].Id] = i + 1;
                }
                // Then, update the custom order for the servers in the folder
                for (int i = 0; i < updatedNodes.Count; i++)
                {
                    updatedNodes[i].CustomOrder = i + 1;
                    allNodeOrder[updatedNodes[i].Id] = i + 1;
                }
                // Save the complete ordered list
                LocalityTreeViewService.Settings.CustomNodeOrder = allNodeOrder;
                LocalityTreeViewService.Save();
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Warning($"Failed to update custom order: {ex.Message}");
            }
        }


        #endregion

        public sealed override void BuildView()
        {
            // Make sure this runs on UI thread
            Execute.OnUIThread(() =>
            {
                TreeNode.VirtualRoot.Children.Clear();
                VmServerList = new ObservableCollection<ProtocolBaseViewModel>(AppData.VmItemList);

                // Check if there are any servers to display
                if (VmServerList.Count == 0)
                {
                    RootNodes = TreeNode.VirtualRoot.Children;
                    return;
                }

                foreach (var vs in VmServerList)
                {
                    vs.IsSelected = false;
                    vs.PropertyChanged -= VmServerPropertyChanged;
                    vs.PropertyChanged += VmServerPropertyChanged;
                }
                VmServerList.CollectionChanged += (s, e) =>
                {
                    RaisePropertyChanged(nameof(IsAnySelected));
                    RaisePropertyChanged(nameof(IsSelectedAll));
                    RaisePropertyChanged(nameof(SelectedCount));
                };

                // add data sources to TreeNodes
                foreach (var ds in IoC.Get<DataSourceService>().AllSources)
                {
                    var node = TreeNode.NewDatabase(ds.DataSourceName);
                    node.SetParent(TreeNode.VirtualRoot);
                }
                // add servers to TreeNodes
                foreach (var server in VmServerList)
                {
                    if (IsServerVisible.ContainsKey(server) && IsServerVisible[server] == false) continue;
                    TreeNode? currentNode = null;
                    // This ensures first-time users see all servers at root level
                    currentNode = TreeNode.VirtualRoot.Children.FirstOrDefault(x => x.Name == server.DataSourceName);
                    if (currentNode == null)
                    {
                        currentNode = TreeNode.NewDatabase(server.DataSourceName);
                        currentNode.SetParent(TreeNode.VirtualRoot);
                    }
                    var treeNodeNames = new List<string>(server.Server.TreeNodes);
                    foreach (var name in treeNodeNames)
                    {
                        var children = currentNode.Children;
                        var parentNode = currentNode;
                        currentNode = children.FirstOrDefault(x => x.Name == name);
                        if (currentNode != null) continue;
                        currentNode = TreeNode.NewFolder(name, parentNode);
                    }
                    // Add the server to the last tree node
                    currentNode.AddChild(TreeNode.NewServer(server, currentNode), false);
                }

                // add folder to TreeNodes
                foreach (var kv in LocalityTreeViewService.Settings.TreeNodeExpansionStates)
                {
                    var treeNodeNames = kv.Key.Split(FullPathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (treeNodeNames.Count <= 1) continue;
                    var currentNode = TreeNode.VirtualRoot.Children.FirstOrDefault(x => x.Name == treeNodeNames.First());
                    if (currentNode == null) continue;
                    treeNodeNames.RemoveAt(0);
                    foreach (var name in treeNodeNames)
                    {
                        var children = currentNode.Children;
                        var parentNode = currentNode;
                        currentNode = children.FirstOrDefault(x => x.Name == name);
                        if (currentNode != null) continue;
                        currentNode = TreeNode.NewFolder(name, parentNode);
                    }
                }

                // Load tree node expansion states from local cache and rebuild cache with current nodes
                LoadLocalCaches(TreeNode.VirtualRoot.Children);

                // Sort the nodes
                SortNodes(TreeNode.VirtualRoot.Children);

                if (!string.IsNullOrEmpty(_lastKeyword))
                {
                    // search mode
                    // remove all empty folders
                    var folders = TreeNode.VirtualRoot.GetChildNodes(true, true, false)
                        .Where(x => x.Children.Count == 0 || x.Children.All(x => x.IsFolder)).ToList();
                    while (folders.Count > 0)
                    {
                        var empty = folders.Where(x => x.Children.Count == 0).ToList();
                        if (empty.Count == 0) break;
                        foreach (var folder in empty)
                        {
                            folder.ParentNode?.Children.Remove(folder);
                            folders.Remove(folder);
                        }
                    }
                    // expand all folders
                    foreach (var folder in TreeNode.VirtualRoot.GetChildNodes(true, true, false))
                    {
                        folder.IsExpanded = true;
                    }
                }

                // Update the UI
                RootNodes = TreeNode.VirtualRoot.Children;

                // Log some information for debugging
                SimpleLogHelper.Debug($"TreeView rebuilt with {TreeNode.VirtualRoot.Children.Count} root nodes and {AppData.VmItemList.Count} servers");

                RaisePropertyChanged(nameof(IsAnySelected));
                RaisePropertyChanged(nameof(IsSelectedAll));
                RaisePropertyChanged(nameof(SelectedCount));
            });
        }

        /// <summary>
        /// Load tree node expansion states from local cache (read only once) and rebuild the cache with current valid nodes
        /// </summary>
        /// <param name="roots">The collection of tree nodes to process</param>
        private void LoadLocalCaches(ObservableCollection<TreeNode> roots)
        {
            // Set custom order for each node based on server ID
            {
                var orders = LocalityTreeViewService.Settings.CustomNodeOrder;
                var nodes = roots.SelectMany(x => x.GetChildNodes(true)).ToList();
                foreach (var node in nodes)
                {
                    node.CustomOrder = orders.GetValueOrDefault(node.Id, int.MaxValue);
                }
            }
            LocalityTreeViewService.LoadExpansionStates(roots);
        }



        public override void ClearSelection()
        {
            if (VmServerList.Any(x => x.IsSelected))
            {
                var nodes = RootNodes.SelectMany(x => x.GetChildNodes(true)).ToList();
                foreach (var item in nodes)
                {
                    item.IsCheckboxSelected = false;
                }
            }
        }

        public void ApplySort()
        {
            SortNodes(RootNodes);
            //BuildView();
        }

        private void VmServerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProtocolBaseViewModel.IsSelected))
            {
                RaisePropertyChanged(nameof(IsAnySelected));
                RaisePropertyChanged(nameof(IsSelectedAll));
                RaisePropertyChanged(nameof(SelectedCount));
            }
        }
    }
}
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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

namespace _1RM.View.ServerTree
{
    public class ServerTreeViewModel : ServerPageBase
    {
        public const string FullPathSeparator = "]=+=+=+=>[";
        public const string FolderNodePrefix = "%$Node$%:";
        public class TreeNode : NotifyPropertyChangedBase
        {
            private string _name;

            public string Name
            {
                get => _name;
                set
                {
                    if (SetAndNotifyIfChanged(ref _name, value))
                    {
                        UpdateParent(ParentNode);
                    }
                }
            }

            public bool IsRootFolder { get; private set; }
            public bool IsFolder { get; private set; }

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

            public ProtocolBaseViewModel? Server { get; private set; }
            public TreeNode? ParentNode = null;
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

            // For folder/tree nodes
            public TreeNode(string name, TreeNode? parentNode)
            {
                _name = name;
                IsFolder = true;
                IsRootFolder = parentNode == null;
                // Note: Expansion state will be loaded and applied in BuildView's LoadLocalCaches method
                // to avoid multiple file reads. Default to expanded state here.
                _isExpanded = true;
                UpdateParent(parentNode);
            }

            public void UpdateParent(TreeNode? parentNode)
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
                ParentNode = parentNode;
            }

            public void AddChild(TreeNode child, bool sort)
            {
                if (!IsFolder) throw new InvalidOperationException("Cannot add child to a non-folder node.");
                if (child.ParentNode != this)
                {
                    child.ParentNode?.Children.Remove(child);
                    child.UpdateParent(this);
                }
                if (!Children.Contains(child))
                    Children.Add(child);
                if (sort)
                {
                    SortNodes(Children, false);
                }
            }


            // For server nodes
            public TreeNode(ProtocolBaseViewModel server, TreeNode parentNode)
            {
                _name = server.DisplayName;
                Server = server;
                IsFolder = false;
                IsRootFolder = false;
                UpdateParent(parentNode);
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
        }

        private TreeNode _rootNode = new TreeNode("", null);
        private ObservableCollection<TreeNode> _rootNodes = new ObservableCollection<TreeNode>();
        public ObservableCollection<TreeNode> RootNodes
        {
            get => _rootNodes;
            set
            {
                if (SetAndNotifyIfChanged(ref _rootNodes, value))
                {
                    _rootNode = new TreeNode("", null);
                    foreach (var node in value)
                    {
                        _rootNode.AddChild(node, false);
                    }
                }
            }
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
            var sortedFolder = nodes.Where(x => x.IsFolder).ToList();
            if (sortedFolder.FirstOrDefault()?.IsRootFolder == false)
            {
                sortedFolder = sortedFolder.OrderBy(x => x.Name).ToList();
            }
            var servers = nodes.Where(x => !x.IsFolder).ToList();
            var sortedServer = servers.OrderBy(n => !n.IsFolder);
            switch (orderBy)
            {
                case EnumServerOrderBy.IdAsc:
                    sortedServer = sortedServer.ThenBy(n => n.Server?.Server?.Id ?? "");
                    break;
                case EnumServerOrderBy.ProtocolAsc:
                    sortedServer = sortedServer.ThenBy(n => n.Server?.Server?.Protocol ?? string.Empty);
                    break;
                case EnumServerOrderBy.ProtocolDesc:
                    sortedServer = sortedServer.ThenByDescending(n => n.Server?.Server?.Protocol ?? string.Empty);
                    break;
                case EnumServerOrderBy.NameAsc:
                    sortedServer = sortedServer.ThenBy(n => n.Name);
                    break;
                case EnumServerOrderBy.NameDesc:
                    sortedServer = sortedServer.ThenByDescending(n => n.Name);
                    break;
                case EnumServerOrderBy.AddressAsc:
                    sortedServer = sortedServer.ThenBy(n => n.Server?.Server?.SubTitle ?? string.Empty);
                    break;
                case EnumServerOrderBy.AddressDesc:
                    sortedServer = sortedServer.ThenByDescending(n => n.Server?.Server?.SubTitle ?? string.Empty);
                    break;
                case EnumServerOrderBy.Custom:
                    sortedServer = sortedServer.ThenBy(n => n.Server?.CustomOrder ?? int.MaxValue);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            nodes.Clear();
            foreach (var node in sortedFolder.ToList())
            {
                nodes.Add(node);
                if (includeSubFolder && node.Children.Count > 0)
                {
                    SortNodes(node.Children);
                }
            }
            foreach (var node in sortedServer.ToList())
            {
                nodes.Add(node);
                if (includeSubFolder && node.Children.Count > 0)
                {
                    SortNodes(node.Children);
                }
            }
        }

        // Command to add a new server
        private RelayCommand? _cmdAdd;
        public RelayCommand CmdAdd
        {
            get
            {
                return _cmdAdd ??= new RelayCommand((o) =>
                {
                    if (View is ServerTreeView view)
                        view.CbPopForInExport.IsChecked = false;
                    GlobalEventHelper.OnGoToServerAddPage?.Invoke(TagFilters.Where<TagFilter>(x => x.IsIncluded == true).Select(x => x.TagName).ToList(), o as DataSourceBase);
                });
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
                            CollectServersInFolder(node, out var serversToEdit);
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
                        parentNode = node.IsFolder == false ? FindParent(node) : node;
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
                        parentNode.AddChild(new TreeNode(folderName, parentNode), true);
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
                            var parentNode = FindParent(node);
                            if (parentNode == null) return;
                            // Rename folder
                            var oldName = node.Name;
                            var newName = await InputBoxViewModel.GetValue("TXT: Enter new folder name:",
                                (input) =>
                                {
                                    if (parentNode.GetFolderNames().Any(x => x != oldName && x == input.Trim())) return IoC.Translate(LanguageService.XXX_IS_ALREADY_EXISTED, input);
                                    return string.IsNullOrWhiteSpace(input) ? "TXT: Folder name cannot be empty" : "";
                                },
                                node.Name,
                                IoC.Get<MainWindowViewModel>());
                            newName = newName?.Trim();


                            if (!string.IsNullOrWhiteSpace(newName) && newName != node.Name)
                            {
                                node.Name = newName;
                                LocalityTreeViewService.SaveExpansionStates(RootNodes);
                                UpdateDbServerTreeNodes(node);
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
                                CollectServersInFolder(node, out var serversToDelete);
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
                    List<string> treePath = []; // Add to root by default
                    DataSourceBase? dataSource = null;
                    if (o is TreeNode node)
                    {
                        // Build the tree path for the new server
                        var parents = node.GetParents(includedMyselfIamFolder: true);
                        if (!string.IsNullOrEmpty(parents?.First().Name))
                        {
                            var dataSourceName = parents.First().Name;
                            if (IoC.Get<DataSourceService>().LocalDataSource?.Name == dataSourceName)
                            {
                                dataSource = IoC.Get<DataSourceService>().LocalDataSource;
                            }
                            else
                            {
                                dataSource = IoC.Get<DataSourceService>().AdditionalSources.FirstOrDefault(x => x.Value.DataSourceName == dataSourceName).Value;
                            }
                        }
                        // remove the root node from the path if it exists
                        parents = parents?.Where(x => !x.IsRootFolder).ToList();
                        treePath = parents == null ? [] : parents.Select(x => x.Name).ToList();
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


        public TreeNode? FindParent(TreeNode target, TreeNode? root = null, IEnumerable<TreeNode>? children = null)
        {
            children ??= RootNodes;
            foreach (var node in children)
            {
                if (node == target)
                {
                    return root;
                }
                var ret = FindParent(target, node, node.Children);
                if (ret != null)
                {
                    return ret;
                }
            }
            return null;
        }


        ///// <summary>
        ///// Find all parent nodes of a target node in the tree. if the target is a root node, return an empty list.
        ///// </summary>
        //public List<TreeNode> GetParents(TreeNode target, bool includeRootNode = true, bool includedMyselfIamFolder = false)
        //{
        //    var list = new List<TreeNode>();
        //    if (includedMyselfIamFolder && target.IsFolder)
        //    {
        //        list.Add(target);
        //    }
        //    TreeNode? current = target;
        //    while (true)
        //    {
        //        if (current?.ParentNode != null)
        //        {
        //            current = current.ParentNode;
        //            if (!includeRootNode && current.IsRootFolder)
        //            {
        //                // If we don't include the root node, stop here
        //                break;
        //            }
        //            list.Add(current);
        //        }
        //        else
        //        {
        //            break;
        //        }
        //    }
        //    list.Reverse();
        //    return list;
        //}




        // Update all servers in a folder when the folder is renamed (iterative implementation)
        private void UpdateDbServerTreeNodes(TreeNode folderNode)
        {
            if (folderNode.IsFolder == false) return;

            // Use a stack to replace recursion
            var foldersToProcess = new Stack<TreeNode>();
            foldersToProcess.Push(folderNode);

            var serversToUpdate = new List<ProtocolBase>();
            while (foldersToProcess.Count > 0)
            {
                var currentFolder = foldersToProcess.Pop();
                var serverNodes = currentFolder.GetChildNodes(false, needFolderNode: false);
                if (serverNodes.Count > 0)
                {
                    // update the servers in this folder
                    var parents = currentFolder.GetParents(includeRootNode: false, includedMyselfIamFolder: true);
                    var folderPath = parents.Select(x => x.Name).ToList();
                    var dataSource = currentFolder.Children.FirstOrDefault(x => x.IsFolder == false)?.Server?.Server?.DataSource;
                    if (dataSource?.IsWritable == true)
                    {
                        var servers = (serverNodes.Select(x => x.Server?.Server).Where(x => x != null).ToList()) as List<ProtocolBase>;
                        foreach (var server in servers)
                        {
                            server.TreeNodes = new List<string>(folderPath);
                        }
                        serversToUpdate.AddRange(servers);
                    }
                }
                // Push all child folders onto the stack for processing
                foreach (var child in currentFolder.Children)
                {
                    if (child.IsFolder)
                    {
                        foldersToProcess.Push(child);
                    }
                }
            }
            IoC.Get<GlobalData>().UpdateServer(serversToUpdate);
        }

        /// <summary>
        /// Collect all servers under a folder node (iterative implementation)
        /// </summary>
        private void CollectServersInFolder(TreeNode folderNode, out List<ProtocolBase> servers)
        {
            servers = new List<ProtocolBase>();
            // Use a stack to replace recursion for depth-first traversal
            var nodesToProcess = new Stack<TreeNode>();
            nodesToProcess.Push(folderNode);
            while (nodesToProcess.Count > 0)
            {
                var currentNode = nodesToProcess.Pop();
                foreach (var child in currentNode.Children)
                {
                    if (child.IsFolder)
                    {
                        // Push folder nodes onto the stack for further processing
                        nodesToProcess.Push(child);
                    }
                    else if (child.Server != null)
                    {
                        // Directly add server nodes to the collection
                        servers.Add(child.Server.Server);
                    }
                }
            }
        }

        #endregion

        #region Drag and Drop Support

        /// <summary>
        /// Move a folder node to a target folder node within the same root database
        /// </summary>
        /// <param name="folderNode">The folder node to move (IsFolder=True)</param>
        /// <param name="targetFolder">The target node (IsFolder=True or IsFolder=False)</param>
        /// <returns>True if the move was successful, false otherwise</returns>
        public bool FolderMoveToFolder(TreeNode folderNode, TreeNode? targetFolder)
        {
            // Cannot move to self
            if (folderNode == targetFolder)
                return false;
            if (!folderNode.IsFolder || folderNode.IsRootFolder)
                return false;
            if (targetFolder?.IsFolder == false)
            {
                targetFolder = targetFolder.ParentNode;
            }
            if (targetFolder?.IsFolder != true) return false;

            // TODO check db read-only state

            // Cannot move a folder into one of its own descendants
            if (IsDescendantOf(targetFolder, folderNode))
                return false;


            var targetRoot = targetFolder;
            if (targetFolder.IsRootFolder == true)
            {
                // do nothing
            }
            else if (targetFolder.IsFolder == true)
            {
                var tmp = targetFolder.GetParents();
                if (tmp.Count == 0) return false;
                targetRoot = tmp[0];
            }
            else
            {
                SimpleLogHelper.Warning($"unhandled targetNode type: {targetFolder.Name}: IsFolder={targetFolder.IsFolder}, IsRootFolder={targetFolder.IsRootFolder}, Server={targetFolder.Server?.DisplayName ?? "null"}");
                return false;
            }


            {
                var sourcePaths = folderNode.GetParents();
                if (sourcePaths.Count == 0) return false;
                if (sourcePaths.First() != targetRoot) return false; // Only allow moving within the same data source
                if (sourcePaths.Last() == targetFolder) return false; // Don't move if already in target location
            }

            try
            {
                if (targetFolder.Children.Any(x => x.IsFolder && x.Name == folderNode.Name))
                {
                    // A folder with the same name already exists in the target location, merge instead
                    var existingFolder = targetFolder.Children.First(x => x.IsFolder && x.Name == folderNode.Name);
                    // Collect all servers in the folder to be moved
                    foreach (var child in folderNode.Children)
                    {
                        existingFolder.AddChild(child, false);
                    }
                }
                else
                {
                    targetFolder.AddChild(folderNode, false);
                }

                // Update all Node in the moved folder and its subfolders
                {
                    folderNode.UpdateParent(targetFolder);
                    var foldersToUpdate = new Queue<TreeNode>();
                    foldersToUpdate.Enqueue(folderNode);
                    while (foldersToUpdate.Count > 0)
                    {
                        var currentFolder = foldersToUpdate.Dequeue();
                        foreach (var child in currentFolder.Children)
                        {
                            child.UpdateParent(currentFolder);
                            if (child.IsFolder)
                                foldersToUpdate.Enqueue(child);
                        }
                    }
                    LocalityTreeViewService.SaveExpansionStates(RootNodes);
                }
                // Update all servers' TreeNode in the moved folder and its subfolders
                {
                    var serversToUpdate = new List<ProtocolBase>();
                    folderNode.UpdateParent(targetFolder);
                    var foldersToUpdate = new Queue<TreeNode>();
                    var foldersToUpdatePaths = new Queue<List<string>>();
                    foldersToUpdate.Enqueue(folderNode);
                    foldersToUpdatePaths.Enqueue(folderNode.GetParents(includeRootNode: false, includedMyselfIamFolder: true).Select(x => x.Name).ToList());
                    while (foldersToUpdate.Count > 0)
                    {
                        var currentFolder = foldersToUpdate.Dequeue();
                        var currentPath = foldersToUpdatePaths.Dequeue();
                        foreach (var server in currentFolder.Children.Select(x => x.Server?.Server))
                        {
                            if(server == null) continue;
                            server.TreeNodes = currentPath;
                            serversToUpdate.Add(server);
                        }
                        foreach (var child in currentFolder.Children.Where(x => x.IsFolder))
                        {
                            foldersToUpdate.Enqueue(child);
                            var newPath = new List<string>(currentPath) { folderNode.Name };
                            foldersToUpdatePaths.Enqueue(newPath);
                        }
                    }
                    if (serversToUpdate.Count > 0)
                    {
                        IoC.Get<GlobalData>().UpdateServer(serversToUpdate);
                    }
                }
                SortNodes(targetFolder.Children, false);
                IoC.Get<GlobalData>().ReloadAll();
                return true;
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Warning($"Failed to move folder {folderNode.Name}: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Move a server node to a target folder node within the same root database
        /// </summary>
        /// <param name="serverNode">The server node to move (IsFolder=False)</param>
        /// <param name="targetFolder">The target node (IsFolder=True or IsFolder=False)</param>
        /// <returns>True if the move was successful, false otherwise</returns>
        public bool ServerMoveToFolder(TreeNode serverNode, TreeNode? targetFolder)
        {
            // Cannot move to self
            if (serverNode == targetFolder)
                return false;
            if (serverNode.IsFolder || serverNode.Server?.Server == null)
                return false; 
            if (targetFolder?.IsFolder == false)
            {
                targetFolder = targetFolder.ParentNode;
            }
            if (targetFolder?.IsFolder != true) return false;

            var server = serverNode.Server.Server;
            var dataSource = server.DataSource;
            if (dataSource?.IsWritable != true)
                return false;



            var targetRoot = targetFolder;
            var targetPaths = new List<TreeNode>();
            if (targetFolder.IsRootFolder == true)
            {
                // do nothing
            }
            else if (targetFolder.IsFolder == true)
            {
                var tmp = targetFolder.GetParents();
                if (tmp.Count == 0) return false;
                targetRoot = tmp[0];
                targetPaths = tmp;
                targetPaths.RemoveAt(0); // remove the root node from the path
                targetPaths.Add(targetFolder);
            }
            else
            {
                SimpleLogHelper.Warning($"unhandled targetNode type: {targetFolder.Name}: IsFolder={targetFolder.IsFolder}, IsRootFolder={targetFolder.IsRootFolder}, Server={targetFolder.Server?.DisplayName ?? "null"}");
                return false;
            }



            var sourcePaths = serverNode.GetParents();
            if (sourcePaths.Count == 0) return false; // cannot find source path
            if (sourcePaths.FirstOrDefault() != targetRoot) return false;   // not in the same root data source
            if (sourcePaths.LastOrDefault() == targetFolder) return true;   // already in the target folder


            sourcePaths.RemoveAt(0); // remove the root node from the path

            // Update the server's TreeNodes property
            try
            {
                // Remove from source parent and add to target folder
                targetFolder.AddChild(serverNode, true);
                // Update the server's TreeNodes
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
        /// Check if a node is a descendant of another node
        /// </summary>
        /// <param name="potentialDescendant">The node that might be a descendant</param>
        /// <param name="ancestor">The potential ancestor node</param>
        /// <returns>True if potentialDescendant is a descendant of ancestor</returns>
        private bool IsDescendantOf(TreeNode potentialDescendant, TreeNode ancestor)
        {
            var parentPaths = potentialDescendant.GetParents();
            return parentPaths?.Contains(ancestor) == true;
        }

        /// <summary>
        /// Move sourceNode to the front or back of targetNode in the same folder
        /// </summary>
        public bool ReorderServersInSameFolder(TreeNode sourceNode, TreeNode targetNode, bool insertBefore = true)
        {
            if (sourceNode == targetNode) return true;
            if (IoC.Get<MainWindowViewModel>().ServerOrderBy != EnumServerOrderBy.Custom) return false; // Must be custom ordering
            var sourceParents = sourceNode.GetParents();
            var targetParents = sourceNode.GetParents();
            if (sourceParents.Count != targetParents.Count) return false; // Must be in the same parent folder
            if (sourceParents.LastOrDefault() != targetParents.LastOrDefault()) return false; // Must be in the same parent folder

            try
            {
                var parents
                var siblingServers = sourceParent.Children.Where(x => !x.IsFolder && x.Server != null).ToList() ?? new List<TreeNode>();

                if (siblingServers.Count < 2)
                    return true; // Nothing to reorder

                // Remove source from current position
                var sourceIndex = siblingServers.IndexOf(sourceNode);
                var targetIndex = siblingServers.IndexOf(targetNode);
                if (sourceIndex - targetIndex == 1)
                    insertBefore = true;
                if (targetIndex - sourceIndex == 1)
                    insertBefore = false;

                if (sourceIndex < 0 || targetIndex < 0)
                    return false;

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
                var serverViewModels = siblingServers.Select(x => x.Server!).ToList();
                UpdateCustomOrder(serverViewModels);
                LocalityTreeViewService.SaveExpansionStates(RootNodes);
                SortNodes(sourceParent.Children, false);
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
                LocalityTreeViewService.Settings.CustomNodeOrder.Clear();
                // Get current custom order mapping for all servers
                var allServers = AppData.VmItemList.ToList();
                // to dict
                var allNodeOrder = allServers.ToDictionary(x => x.Id, x => 0); // Initialize with 0 order
                // first, update the current tree node order
                var currentTreeViewList = RootNodes.SelectMany(x => x.GetChildNodes(true)).ToList();
                for (int i = 0; i < currentTreeViewList.Count; i++)
                {
                    currentTreeViewList[i].CustomOrder = i + 1; // Assign new custom order starting from 1
                    var id = currentTreeViewList[i].Server?.Id ?? FolderNodePrefix + currentTreeViewList[i].Name;
                    if (!string.IsNullOrEmpty(id))
                        allNodeOrder[id] = i + 1; // Assign new order starting from 1
                }
                // Then, update the custom order for the servers in the folder
                for (int i = 0; i < updatedNodes.Count; i++)
                {
                    updatedNodes[i].CustomOrder = i + 1;
                    var id = updatedNodes[i].Server?.Id ?? FolderNodePrefix + updatedNodes[i].Name;
                    allNodeOrder[id] = i + 1;
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

        /// <summary>
        /// Check if a move operation would be valid (same data source, no circular moves)
        /// </summary>
        /// <param name="sourceNode">The node being dragged</param>
        /// <param name="targetNode">The target node</param>
        /// <returns>True if the move would be valid</returns>
        public bool CanMoveNode(TreeNode sourceNode, TreeNode targetNode)
        {
            // Cannot move to self
            if (sourceNode == targetNode)
                return false;
            // Cannot move root folders
            if (sourceNode.IsRootFolder)
                return false;

            // Check data source write permissions for servers
            if (sourceNode.Server?.Server != null || targetNode.Server?.Server != null)
            {
                var server = sourceNode.Server?.Server ?? targetNode.Server!.Server;
                var dataSource = server.DataSource;
                if (dataSource?.IsWritable != true)
                {
                    SimpleLogHelper.Debug("Can not move node: " + sourceNode.Name + ", data source is not writable.");
                    return false;
                }
            }

            // For folder moves, prevent moving into descendants
            if (sourceNode.IsFolder && IsDescendantOf(targetNode, sourceNode))
            {
                SimpleLogHelper.Debug("Can not move node: " + sourceNode.Name + ", target is a descendant of source.");
                return false;
            }


            // Must be within the same data source
            var sourcePaths = sourceNode.GetParents();
            var targetPaths = targetNode.GetParents();

            // prevent moving into parent folder
            if (targetNode.IsFolder && targetNode == sourcePaths.LastOrDefault())
            {
                SimpleLogHelper.Debug("Can not move node: " + sourceNode.Name + ", target is the same as source parent.");
                return false;
            }
            if (LocalityTreeViewService.Settings.ServerOrderBy != EnumServerOrderBy.Custom)
            {
                if (!targetNode.IsFolder && targetPaths.LastOrDefault() == sourcePaths.LastOrDefault())
                {
                    SimpleLogHelper.Debug("Can not move node: " + sourceNode.Name + ", target parent is the same as source parent.");
                    return false;
                }
            }

            // Get source and target roots
            var sourceRoot = sourcePaths.FirstOrDefault();
            var targetRoot = targetPaths?.FirstOrDefault() ?? (targetNode.IsRootFolder ? targetNode : null);

            // Only allow moving within the same data source
            if (sourceRoot != targetRoot)
                return false;

            return true;
        }

        #endregion

        public sealed override void BuildView()
        {
            // Make sure this runs on UI thread
            Execute.OnUIThread(() =>
            {
                var newRoots = new ObservableCollection<TreeNode>();
                VmServerList = new ObservableCollection<ProtocolBaseViewModel>(AppData.VmItemList);

                // Check if there are any servers to display
                if (VmServerList.Count == 0)
                {
                    RootNodes = newRoots;
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


                // add servers to TreeNodes
                foreach (var server in VmServerList)
                {
                    // This ensures first-time users see all servers at root level
                    var treeNodeNames = new List<string> { server.DataSourceName }; // TODO: 如果 DataSourceName 改了怎么办？
                    treeNodeNames.AddRange(server.Server.TreeNodes);
                    // Build path based on TreeNodes
                    TreeNode? currentNode = null;
                    foreach (var name in treeNodeNames)
                    {
                        var children = currentNode?.Children ?? newRoots;
                        var parentNode = currentNode;
                        currentNode = children.FirstOrDefault(x => x.Name == name);
                        if (currentNode == null)
                        {
                            currentNode = new TreeNode(name, parentNode);
                            children.Add(currentNode);
                        }
                    }
                    // Add the server to the last tree node
                    if (currentNode != null)
                    {
                        currentNode.AddChild(new TreeNode(server, currentNode), false);
                    }
                }

                // add folder to TreeNodes
                foreach (var kv in LocalityTreeViewService.Settings.TreeNodeExpansionStates)
                {
                    var key = kv.Key;
                    var treeNodeNames = kv.Key.Split(TreeNode.FullPathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (treeNodeNames.Count > 1 && newRoots.Any(x => x.Name == treeNodeNames.First()))
                    {
                        // Build path based on TreeNodes
                        TreeNode? currentNode = null;
                        foreach (var name in treeNodeNames)
                        {
                            var children = currentNode?.Children ?? newRoots;
                            var parentNode = currentNode;
                            currentNode = children.FirstOrDefault(x => x.Name == name);
                            if (currentNode == null)
                            {
                                currentNode = new TreeNode(name, parentNode);
                                children.Add(currentNode);
                            }
                        }
                    }
                }


                // Load tree node expansion states from local cache and rebuild cache with current nodes
                LoadLocalCaches(newRoots);

                // Sort the nodes
                SortNodes(newRoots);

                // Update the UI
                RootNodes = newRoots;

                // Log some information for debugging
                SimpleLogHelper.Debug($"TreeView rebuilt with {newRoots.Count} root nodes and {AppData.VmItemList.Count} servers");

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
                    if (string.IsNullOrEmpty(node.Server?.Id)) continue; // Skip nodes without a server ID
                    node.CustomOrder = orders.GetValueOrDefault(node.Server.Id, int.MaxValue);
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
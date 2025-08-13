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
using System.Linq;
using System.Windows.Input;

namespace _1RM.View.ServerTree
{
    public class ServerTreeViewModel : ServerPageBase
    {
        public class TreeNode : NotifyPropertyChangedBase
        {
            private string _name;

            public string Name
            {
                get => _name;
                set => SetAndNotifyIfChanged(ref _name, value);
            }

            public bool IsRootFolder { get; private set; }
            public bool IsFolder { get; private set; }
            public ProtocolBaseViewModel? Server { get; private set; }
            public ObservableCollection<TreeNode> Children { get; } = new ObservableCollection<TreeNode>();

            /// <summary>
            /// The full path to this node in the tree (e.g., "LocalDataSource->Folder1->SubFolder")
            /// </summary>
            public string FullPath { get; private set; } = "";

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
                            LocalityTreeViewService.SetTreeNodeExpansionState(FullPath, value);
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
            public TreeNode(string name, bool isRoot, string fullPath = "")
            {
                _name = name;
                IsFolder = true;
                IsRootFolder = isRoot;
                FullPath = fullPath;
                
                // Note: Expansion state will be loaded and applied in BuildView's LoadAndRebuildTreeNodeExpansionStates method
                // to avoid multiple file reads. Default to expanded state here.
                _isExpanded = true;
            }

            // For server nodes
            public TreeNode(ProtocolBaseViewModel server)
            {
                _name = server.DisplayName;
                Server = server;
                IsFolder = false;
                IsRootFolder = false;
                FullPath = ""; // Server nodes don't need full path for expansion state
            }

            public List<string> GetFolderNames()
            {
                return Children.Where(x => x.IsFolder == true).Select(x => x.Name).ToList();
            }
            public List<TreeNode> GetChildNodeFolder()
            {
                return Children.Where(x => x.IsFolder == true).ToList();
            }
            public List<TreeNode> GetChildNodeItems()
            {
                return Children.Where(x => x.IsFolder != true).ToList();
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

        private void SortNodes(ObservableCollection<TreeNode> nodes)
        {
            var orderBy = IoC.Get<MainWindowViewModel>().ServerOrderBy;
            var sorted = nodes.OrderBy(n => !n.IsFolder);
            switch (orderBy)
            {
                case EnumServerOrderBy.IdAsc:
                    sorted = sorted.ThenBy(n => n.Server?.Server?.Id ?? "");
                    break;
                case EnumServerOrderBy.ProtocolAsc:
                    sorted = sorted.ThenBy(n => n.Server?.Server?.Protocol ?? string.Empty);
                    break;
                case EnumServerOrderBy.ProtocolDesc:
                    sorted = sorted.ThenByDescending(n => n.Server?.Server?.Protocol ?? string.Empty);
                    break;
                case EnumServerOrderBy.NameAsc:
                    sorted = sorted.ThenBy(n => n.Name);
                    break;
                case EnumServerOrderBy.NameDesc:
                    sorted = sorted.ThenByDescending(n => n.Name);
                    break;
                case EnumServerOrderBy.AddressAsc:
                    sorted = sorted.ThenBy(n => n.Server?.Server?.SubTitle ?? string.Empty);
                    break;
                case EnumServerOrderBy.AddressDesc:
                    sorted = sorted.ThenByDescending(n => n.Server?.Server?.SubTitle ?? string.Empty);
                    break;
                case EnumServerOrderBy.Custom:
                    sorted = sorted.ThenBy(n => n.Server?.CustomOrder ?? int.MaxValue);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var sortedNodes = sorted.ToList();
            nodes.Clear();
            foreach (var node in sortedNodes)
            {
                nodes.Add(node);
                if (node.Children.Count > 0)
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
                            var serversToEdit = new List<ProtocolBase>();
                            CollectServersInFolder(node, serversToEdit);
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
                        parentNode.Children.Add(new TreeNode(folderName, false));
                        SortNodes(parentNode.Children);
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
                                UpdateServerTreeNodes(node);
                                BuildView();
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
                                DeleteFolder(node);
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
                    if (o is TreeNode node)
                    {
                        // Build the tree path for the new server
                        var parents = FindParents(node, includeFirstNode: false, includedTargetIfIsFolder: true);
                        treePath = parents == null ? [] : parents.Select(x => x.Name).ToList();
                    }
                    // Navigate to add server page
                    GlobalEventHelper.OnGoToServerAddPage?.Invoke(preset: new ServerEditorPageViewModel.ParamsServerAddPreset()
                    {
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


        /// <summary>
        /// Find all parent nodes of a target node in the tree. if the target is a root node, return an empty list.
        /// </summary>
        public List<TreeNode>? FindParents(TreeNode target, TreeNode? root = null, IEnumerable<TreeNode>? children = null, bool includeFirstNode = true, bool includedTargetIfIsFolder = false)
        {
            children ??= RootNodes;
            foreach (var node in children)
            {
                if (node == target)
                {
                    if (includedTargetIfIsFolder && target.IsFolder)
                    {
                        var list = new List<TreeNode>();
                        if (root == null) { }
                        else if (!includeFirstNode && root.IsRootFolder) { }
                        else
                        {
                            list.Add(root);
                        }
                        list.Add(target);
                        return list;
                    }
                    return root == null ? [] : [root];
                }
                else
                {
                    var ret = FindParents(target, node, node.Children, includeFirstNode, includedTargetIfIsFolder);
                    if (ret == null) continue; // no match in this branch
                    if (root != null) // if we have a root, insert it at the beginning
                        ret.Insert(0, root);
                    return ret;
                }
            }
            return null;
        }



        // Delete a folder and move its contents to parent
        private void DeleteFolder(TreeNode folderNode)
        {
            var serversToDelete = new List<ProtocolBase>();
            CollectServersInFolder(folderNode, serversToDelete);
            AppData.DeleteServer(serversToDelete);
            // Rebuild the tree
            BuildView();
        }

        // Update all servers in a folder when the folder is renamed
        private void UpdateServerTreeNodes(TreeNode folderNode)
        {
            if(folderNode.IsFolder == false) return;
            var serverNodes = folderNode.GetChildNodeItems();
            if (serverNodes.Count > 0)
            {
                // update the servers in this folder
                var parents = FindParents(folderNode, includeFirstNode: false, includedTargetIfIsFolder: true);
                var folderPath = parents == null ? [] : parents.Select(x => x.Name).ToList();
                //IoC.Get<GlobalData>().UpdateServer()
                var dataSource = folderNode.Children.FirstOrDefault(x => x.IsFolder == false)?.Server?.Server?.DataSource;
                if (dataSource?.IsWritable != true) return;
                var servers = (serverNodes.Select(x => x.Server?.Server).Where(x => x != null).ToList()) as List<ProtocolBase>;
                foreach (var server in servers)
                {
                    server.TreeNodes = new List<string>(folderPath);
                }
                dataSource.Database_UpdateServer(servers);
            }

            var folderNodes = folderNode.GetChildNodeFolder();
            if (folderNodes.Count == 0) return;
            // Update all servers in subfolders
            foreach (var folder in folderNodes)
            {
                UpdateServerTreeNodes(folder);
            }
        }

        // Collect all servers under a folder node
        private void CollectServersInFolder(TreeNode folderNode, List<ProtocolBase> servers)
        {
            foreach (var child in folderNode.Children)
            {
                if (child.IsFolder)
                {
                    CollectServersInFolder(child, servers);
                }
                else if (child.Server != null)
                {
                    servers.Add(child.Server.Server);
                }
            }
        }

        #endregion

        #region Drag and Drop Support

        /// <summary>
        /// Move a server node to a target folder node within the same root database
        /// </summary>
        /// <param name="serverNode">The server node to move (IsFolder=False)</param>
        /// <param name="targetNode">The target node (IsFolder=True or IsFolder=False)</param>
        /// <returns>True if the move was successful, false otherwise</returns>
        public bool MoveServerToFolder(TreeNode serverNode, TreeNode targetNode)
        {
            // Cannot move to self
            if (serverNode == targetNode)
                return false;
            if (serverNode.IsFolder || serverNode.Server?.Server == null)
                return false;

            var server = serverNode.Server.Server;
            var dataSource = server.DataSource;
            if (dataSource?.IsWritable != true)
                return false;

            TreeNode targetRoot = targetNode;
            TreeNode targetFolder = targetNode;
            var targetPaths = new List<TreeNode>();
            if (targetNode.IsRootFolder == true)
            {
                // do nothing
            }
            else if (targetNode.IsFolder == true)
            {
                var tmp = FindParents(targetNode)!;
                if (tmp == null || tmp.Count == 0) return false;
                targetPaths = tmp;
                targetRoot = targetPaths[0];
                targetPaths.RemoveAt(0); // remove the root node from the path
                targetPaths.Add(targetNode);
                targetFolder = targetNode; // the target node is the folder itself
            }
            else if (targetNode is { IsFolder: false, Server: not null })
            {
                var tmp = FindParents(targetNode)!;
                if (tmp == null || tmp.Count == 0) return false;
                targetFolder = tmp.Last();
                targetPaths = tmp;
                targetRoot = targetPaths[0];
                targetPaths.RemoveAt(0); // remove the root node from the path
            }
            else
            {
                SimpleLogHelper.Warning($"unhandled targetNode type: {targetNode.Name}: IsFolder={targetNode.IsFolder}, IsRootFolder={targetNode.IsRootFolder}, Server={targetNode.Server?.DisplayName ?? "null"}");
                return false;
            }



            var sourcePaths = FindParents(serverNode);
            if (sourcePaths == null || sourcePaths.Count == 0) return false; // cannot find source path
            // Only allow moving within the same data source
            if (sourcePaths.FirstOrDefault() != targetRoot) return false;   // not in the same root data source
            var sourceParent = sourcePaths.Last();
            sourcePaths.RemoveAt(0); // remove the root node from the path
            if (sourcePaths.LastOrDefault() == targetPaths.LastOrDefault()) return false;   // already in the target folder

            // Get the new tree path for the server
            List<string> newTreePath = (from t in targetPaths where !t.IsRootFolder select t.Name).ToList();

            // Update the server's TreeNodes property
            try
            {
                // Update the server's tree path
                server.TreeNodes = new List<string>(newTreePath);
                dataSource.Database_UpdateServer(new List<ProtocolBase> { server });
                // Remove from source parent and add to target folder
                sourceParent.Children.Remove(serverNode);
                targetFolder.Children.Add(serverNode);
                if (IoC.Get<MainWindowViewModel>().ServerOrderBy != EnumServerOrderBy.Custom)
                    SortNodes(targetFolder.Children);
                return true;
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Warning($"Failed to move server {server.DisplayName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reorder servers within the same parent folder for custom ordering
        /// </summary>
        /// <param name="sourceNode">The server node being moved</param>
        /// <param name="targetNode">The target server node or position</param>
        /// <param name="insertBefore">Whether to insert before or after the target</param>
        /// <returns>True if the reorder was successful, false otherwise</returns>
        public bool ReorderServersInSameFolder(TreeNode sourceNode, TreeNode targetNode, bool insertBefore = true)
        {
            if (IoC.Get<MainWindowViewModel>().ServerOrderBy != EnumServerOrderBy.Custom) return false; // Must be custom ordering
            if (sourceNode.Server?.Server == null || targetNode.Server?.Server == null) return false;


            try
            {
                var sourceParent = FindParent(sourceNode);
                if (sourceParent == null) return false; // Cannot find parent
                if (sourceParent.Children.IndexOf(targetNode) < 0) return false; // Must be in the same parent folder

                // Get all server nodes in the same folder
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

                // Rebuild the view to reflect the changes
                BuildView();

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
        /// <param name="serverViewModels">List of servers in desired order within their folder</param>
        private void UpdateCustomOrder(List<ProtocolBaseViewModel> serverViewModels)
        {
            if (IoC.Get<MainWindowViewModel>().ServerOrderBy != EnumServerOrderBy.Custom) return; // Must be custom ordering
            try
            {
                if (serverViewModels.Count <= 1)
                    return;
                // Get current custom order mapping for all servers
                var allServers = AppData.VmItemList.ToList();
                var allServersOrder = allServers.Select(x => x.CustomOrder).ToList();

                // first, update the current tree view order
                var currentTreeViewList = GetChildrenServers();
                for (int i = 0; i < currentTreeViewList.Count; i++)
                {
                    var idx = allServers.IndexOf(currentTreeViewList[i]);
                    if (idx == -1) continue;
                    allServersOrder[idx] = i + 1; // Assign new order starting from 1
                }
                // Then, update the custom order for the servers in the folder
                    for (int i = 0; i < serverViewModels.Count; i++)
                    {
                        var idx = allServers.IndexOf(serverViewModels[i]);
                        if (idx == -1) continue;
                    allServersOrder[idx] = i + 1; // Assign new order starting from 1
                }
                // Save the complete ordered list
                _1RM.Service.Locality.LocalityListViewService.ServerCustomOrderSave(allServers, allServersOrder);
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Warning($"Failed to update custom order: {ex.Message}");
            }
        }


        public List<ProtocolBaseViewModel> GetChildrenServers(TreeNode? root = null)
        {
            var list = new List<ProtocolBaseViewModel>();
            var children = root?.Children ?? RootNodes;
            var folders = children.Where(x => x.IsFolder);
            foreach (var rootChild in folders)
            {
                list.AddRange(GetChildrenServers(rootChild));
            }
            list.AddRange(children.Where(x => x.IsFolder == false && x.Server != null).Select(x => x.Server)!);
            return list;
        }

        public List<TreeNode> GetChildrenNodes(TreeNode? root = null)
        {
            var list = new List<TreeNode>();
            if (root != null)
                list.Add(root);
            var children = root?.Children ?? RootNodes;
            var folders = children.Where(x => x.IsFolder);
            foreach (var rootChild in folders)
            {
                list.AddRange(GetChildrenNodes(rootChild));
            }
            list.AddRange(children.Where(x => x.IsFolder == false));
            return list;
        }

        /// <summary>
        /// Check if a move operation would be valid (same data source, server to folder)
        /// </summary>
        /// <param name="sourceNode">The node being dragged</param>
        /// <param name="targetNode">The target node</param>
        /// <returns>True if the move would be valid</returns>
        public bool CanMoveNode(TreeNode sourceNode, TreeNode targetNode)
        {
            // Cannot move to self
            if (sourceNode == targetNode)
                return false;
            if (sourceNode.Server?.Server != null)
            {
                var server = sourceNode.Server.Server;
                var dataSource = server.DataSource;
                if (dataSource?.IsWritable != true)
                    return false;
            }

            // Must be within the same data source
            var sourcePaths = FindParents(sourceNode);
            var targetPaths = FindParents(targetNode);
            if (sourcePaths == null) return false; // cannot find source path
            // Only allow moving within the same data source
            if (sourcePaths.FirstOrDefault() != targetPaths?.FirstOrDefault() // not in the same root data source
                && sourcePaths.FirstOrDefault() != targetNode) // not in the same root data source: in case targetNode is a root node
                return false;
            return true;
        }

        #endregion

        public sealed override void BuildView()
        {
            // Make sure this runs on UI thread
            Execute.OnUIThread(() =>
            {
                var newRoot = new ObservableCollection<TreeNode>();
                var nodeTreePaths = new Dictionary<string, TreeNode>();

                // Add a root node for each data source
                {
                    var rootName = IoC.Get<DataSourceService>().LocalDataSource!.Name;
                    var rootNode = new TreeNode(rootName, true, rootName);
                    newRoot.Add(rootNode);
                    nodeTreePaths[rootName] = rootNode;
                }
                foreach (var dataSource in IoC.Get<DataSourceService>().AdditionalSources)
                {
                    var rootName = dataSource.Value.DataSourceName;
                    var rootNode = new TreeNode(rootName, true, rootName);
                    newRoot.Add(rootNode);
                    nodeTreePaths[rootName] = rootNode;
                }

                VmServerList = new ObservableCollection<ProtocolBaseViewModel>(AppData.VmItemList);

                // Check if there are any servers to display
                if (VmServerList.Count == 0)
                {
                    RootNodes = newRoot;
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

                // Group servers by TreeNodes
                foreach (var server in VmServerList)
                {
                    // This ensures first-time users see all servers at root level
                    var treeNodes = new List<string> { server.DataSourceName };
                    treeNodes.AddRange(server.Server.TreeNodes);
                    // Build path based on TreeNodes
                    TreeNode? currentNode = null;
                    string currentPath = "";
                    for (var i = 0; i < treeNodes.Count; i++)
                    {
                        var nodeName = treeNodes[i];
                        currentPath = string.IsNullOrEmpty(currentPath) ? nodeName : $"{currentPath}->{nodeName}";
                        if (!nodeTreePaths.TryGetValue(currentPath, out var treeNode))
                        {
                            treeNode = new TreeNode(nodeName, i == 0, currentPath);
                            nodeTreePaths[currentPath] = treeNode;
                            if (currentNode == null)
                            {
                                // Add to root
                                newRoot.Add(treeNode);
                            }
                            else
                            {
                                currentNode.Children.Add(treeNode);
                            }
                        }

                        currentNode = treeNode;
                    }

                    // Add the server to the last tree node
                    if (currentNode != null)
                    {
                        currentNode.Children.Add(new TreeNode(server));
                    }
                }

                // Sort the nodes
                SortNodes(newRoot);

                // Load tree node expansion states from local cache and rebuild cache with current nodes
                LoadAndRebuildTreeNodeExpansionStates(newRoot);

                // Update the UI
                RootNodes = newRoot;

                // Log some information for debugging
                SimpleLogHelper.Debug($"TreeView rebuilt with {newRoot.Count} root nodes and {AppData.VmItemList.Count} servers");

                RaisePropertyChanged(nameof(IsAnySelected));
                RaisePropertyChanged(nameof(IsSelectedAll));
                RaisePropertyChanged(nameof(SelectedCount));
            });
        }

        /// <summary>
        /// Load tree node expansion states from local cache (read only once) and rebuild the cache with current valid nodes
        /// </summary>
        /// <param name="nodes">The collection of tree nodes to process</param>
        private void LoadAndRebuildTreeNodeExpansionStates(ObservableCollection<TreeNode> nodes)
        {
            // Read all cached expansion states once
            var cachedStates = LocalityTreeViewService.GetAllTreeNodeExpansionStates();
            var currentValidStates = new Dictionary<string, bool>();

            // Recursively apply cached states and collect current valid folder paths
            ApplyExpansionStatesRecursively(nodes, cachedStates, currentValidStates);

            // Rebuild the cache with only current valid states
            RebuildExpansionStateCache(currentValidStates);
        }

        /// <summary>
        /// Recursively apply expansion states to tree nodes and collect current valid folder paths
        /// </summary>
        /// <param name="nodes">The collection of tree nodes to process</param>
        /// <param name="cachedStates">All cached expansion states (read once)</param>
        /// <param name="currentValidStates">Dictionary to collect current valid folder paths and their states</param>
        private void ApplyExpansionStatesRecursively(ObservableCollection<TreeNode> nodes, 
            Dictionary<string, bool> cachedStates, 
            Dictionary<string, bool> currentValidStates)
        {
            foreach (var node in nodes)
            {
                if (node.IsFolder && !string.IsNullOrEmpty(node.FullPath))
                {
                    // Get cached expansion state or default to true
                    var savedExpansionState = cachedStates.GetValueOrDefault(node.FullPath, true);
                    
                    // Apply the state without triggering save
                    node._isExpanded = savedExpansionState;
                    node.RaisePropertyChanged(nameof(TreeNode.IsExpanded));

                    // Add to current valid states
                    currentValidStates[node.FullPath] = savedExpansionState;
                }

                // Recursively process child nodes
                if (node.Children.Count > 0)
                {
                    ApplyExpansionStatesRecursively(node.Children, cachedStates, currentValidStates);
                }
            }
        }

        /// <summary>
        /// Rebuild the expansion state cache with only current valid folder paths
        /// </summary>
        /// <param name="currentValidStates">Dictionary of current valid folder paths and their states</param>
        private void RebuildExpansionStateCache(Dictionary<string, bool> currentValidStates)
        {
            // Clear existing cache
            LocalityTreeViewService.ClearAllTreeNodeExpansionStates();

            // Save only current valid states
            foreach (var kvp in currentValidStates)
            {
                LocalityTreeViewService.SetTreeNodeExpansionState(kvp.Key, kvp.Value);
            }

            SimpleLogHelper.Debug($"TreeView expansion states cache rebuilt with {currentValidStates.Count} valid entries");
        }

        public override void ClearSelection()
        {
            if (VmServerList.Any(x => x.IsSelected))
            {
                var currentTreeNodes = GetChildrenNodes();
                foreach (var item in currentTreeNodes)
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
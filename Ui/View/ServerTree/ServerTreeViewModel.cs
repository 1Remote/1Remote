using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.View.ServerList;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace _1RM.View.ServerTree
{
    public class ServerTreeViewModel : ServerPageBase
    {
        public class TreeNode : NotifyPropertyChangedBase
        {
            public string Name { get; private set; }
            public bool IsTag { get; private set; }
            public ProtocolBaseViewModel? Server { get; private set; }
            public ObservableCollection<TreeNode> Children { get; } = new ObservableCollection<TreeNode>();
            public bool IsExpanded { get; set; } = true;
            public bool IsSelected { get; set; }

            // For folder/tree nodes
            public TreeNode(string name)
            {
                Name = name;
                IsTag = true; // Keep this name for compatibility, but now represents folder nodes
            }

            // For server nodes
            public TreeNode(ProtocolBaseViewModel server)
            {
                Name = server.DisplayName;
                Server = server;
                IsTag = false;
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

        private bool _isAnySelected;
        public bool IsAnySelected
        {
            get => _isAnySelected;
            set => SetAndNotifyIfChanged(ref _isAnySelected, value);
        }

        private int _selectedCount;
        public int SelectedCount
        {
            get => _selectedCount;
            set => SetAndNotifyIfChanged(ref _selectedCount, value);
        }

        public ServerTreeViewModel(DataSourceService sourceService, GlobalData appData) 
            : base(sourceService, appData)
        {
            // Observe changes to the server list
            AppData.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(GlobalData.VmItemList))
                {
                    BuildTreeView();
                }
            };

            BuildTreeView();
        }

        public void BuildTreeView()
        {
            // Make sure this runs on UI thread
            Execute.OnUIThread(() =>
            {
                var newRoot = new ObservableCollection<TreeNode>();
                var nodeTreePaths = new Dictionary<string, TreeNode>();
                var othersNode = new TreeNode("root"); // TODO: 根节点名称

                // Check if there are any servers to display
                if (AppData.VmItemList.Count == 0)
                {
                    RootNodes = newRoot;
                    return;
                }

                // Group servers by TreeNodes
                foreach (var server in AppData.VmItemList)
                {
                    // Don't automatically migrate tags to TreeNodes - let TreeNodes be empty initially
                    // This ensures first-time users see all servers at root level
                    
                    if (server.Server.TreeNodes.Count == 0)
                    {
                        othersNode.Children.Add(new TreeNode(server));
                    }
                    else
                    {
                        // Build path based on TreeNodes
                        TreeNode? currentNode = null;
                        string currentPath = "";
                        
                        foreach (var nodeName in server.Server.TreeNodes)
                        {
                            if (string.IsNullOrEmpty(currentPath))
                            {
                                currentPath = nodeName;
                            }
                            else
                            {
                                currentPath = $"{currentPath}->{nodeName}";
                            }

                            if (!nodeTreePaths.TryGetValue(currentPath, out var treeNode))
                            {
                                treeNode = new TreeNode(nodeName);
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
                }

                // Add "others" node if it has children
                if (othersNode.Children.Count > 0)
                {
                    newRoot.Add(othersNode);
                }

                // Sort the nodes
                SortNodes(newRoot);

                // Update the UI
                RootNodes = newRoot;

                // Log some information for debugging
                SimpleLogHelper.Debug($"TreeView rebuilt with {newRoot.Count} root nodes and {AppData.VmItemList.Count} servers");
            });
        }

        private void SortNodes(ObservableCollection<TreeNode> nodes)
        {
            // Sort tags first, then servers alphabetically
            var sorted = nodes.OrderBy(n => !n.IsTag).ThenBy(n => n.Name).ToList();
            nodes.Clear();
            foreach (var node in sorted)
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
                    if (View is ServerListPageView view)
                        view.CbPopForInExport.IsChecked = false;
                    GlobalEventHelper.OnGoToServerAddPage?.Invoke(TagFilters.Where<TagFilter>(x => x.IsIncluded == true).Select(x => x.TagName).ToList(), o as DataSourceBase);
                });
            }
        }

        // Command for when a server is selected in the tree
        public void CmdServerSelected(ProtocolBaseViewModel server)
        {
            SelectedServerViewModel = server;
            IsAnySelected = true;
            SelectedCount = 1;
        }

        // Command to cancel selection
        public void CmdCancelSelected()
        {
            SelectedNode = null;
            SelectedServerViewModel = null;
            IsAnySelected = false;
            SelectedCount = 0;
        }

        // Command to connect to selected server
        public void CmdConnectSelected()
        {
            if (SelectedServerViewModel != null)
            {
                GlobalEventHelper.OnRequestServerConnect?.Invoke(SelectedServerViewModel.Server, fromView: "ServerTreeView");
            }
        }

        // Command to edit selected server
        public void CmdMultiEditSelected()
        {
            if (SelectedServerViewModel != null)
            {
                GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(SelectedServerViewModel.Server, true);
            }
        }

        // Command to create desktop shortcut for selected server
        public void CmdCreateDesktopShortcut()
        {
            // Desktop shortcut functionality not implemented yet
            if (SelectedServerViewModel?.Server != null)
            {
                MessageBoxHelper.Info("Create desktop shortcut functionality is not implemented yet.");
            }
        }

        // Command to delete selected server
        public void CmdDeleteSelected()
        {
            if (SelectedServerViewModel != null)
            {
                var server = SelectedServerViewModel.Server;
                if (server.DataSource?.IsWritable == true)
                {
                    if (MessageBoxHelper.Confirm(IoC.Translate("do_you_really_want_to_delete_x").Replace("{0}", server.DisplayName)))
                    {
                        // Use the App's DeleteServer method
                        AppData.DeleteServer(new List<ProtocolBase> { server });
                        CmdCancelSelected();
                    }
                }
            }
        }

        // Command to export selected server to JSON
        public void CmdExportSelectedToJson()
        {
            if (SelectedServerViewModel != null)
            {
                // Export functionality not implemented yet
                MessageBoxHelper.Info("Export to JSON functionality is not implemented yet.");
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
                    var folderName = await InputBoxViewModel.GetValue("Enter folder name:", 
                        (input) => string.IsNullOrWhiteSpace(input) ? "Folder name cannot be empty" : "", 
                        "New Folder", 
                        IoC.Get<MainWindowViewModel>());
                    
                    if (!string.IsNullOrWhiteSpace(folderName))
                    {
                        CreateNewFolder(o as TreeNode, folderName);
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
                    List<string> treePath;
                    if (o is TreeNode node)
                    {
                        // Build the tree path for the new server
                        treePath = GetTreePath(node);
                    }
                    else
                    {
                        // Add to root
                        treePath = new List<string>();
                    }
                    
                    // Store the tree path for the new server
                    _pendingServerTreePath = treePath;
                    
                    // Navigate to add server page
                    GlobalEventHelper.OnGoToServerAddPage?.Invoke(new List<string>(), null);
                });
            }
        }

        // Store the tree path for servers being created
        private List<string>? _pendingServerTreePath;

        // Command to rename a node
        private RelayCommand? _cmdRenameNode;
        public RelayCommand CmdRenameNode
        {
            get
            {
                return _cmdRenameNode ??= new RelayCommand(async (o) =>
                {
                    if (o is TreeNode node)
                    {
                        if (node.IsTag)
                        {
                            // Rename folder
                            var newName = await InputBoxViewModel.GetValue("Enter new folder name:", 
                                (input) => string.IsNullOrWhiteSpace(input) ? "Folder name cannot be empty" : "", 
                                node.Name, 
                                IoC.Get<MainWindowViewModel>());
                            if (!string.IsNullOrWhiteSpace(newName) && newName != node.Name)
                            {
                                RenameFolder(node, newName);
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
                        if (node.IsTag)
                        {
                            // Delete folder and move servers to parent or root
                            if (MessageBoxHelper.Confirm($"Delete folder '{node.Name}' and move its contents to parent folder?"))
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

        #endregion

        #region Helper Methods

        // Get the tree path from root to the specified node
        private List<string> GetTreePath(TreeNode node)
        {
            var path = new List<string>();
            var current = node;
            
            // Find the path by searching through the tree
            FindNodePath(RootNodes, node, new List<string>(), path);
            
            return path;
        }

        private bool FindNodePath(ObservableCollection<TreeNode> nodes, TreeNode target, List<string> currentPath, List<string> result)
        {
            foreach (var node in nodes)
            {
                var newPath = new List<string>(currentPath);
                if (node.IsTag)
                {
                    newPath.Add(node.Name);
                }

                if (node == target)
                {
                    result.Clear();
                    result.AddRange(newPath);
                    return true;
                }

                if (FindNodePath(node.Children, target, newPath, result))
                {
                    return true;
                }
            }
            return false;
        }

        // Rename a folder and update all servers under it
        private void RenameFolder(TreeNode folderNode, string newName)
        {
            var oldPath = GetTreePath(folderNode);
            var newPath = new List<string>(oldPath);
            if (newPath.Count > 0)
            {
                newPath[newPath.Count - 1] = newName;
            }

            // Update all servers that are under this folder
            UpdateServersInFolder(folderNode, oldPath, newPath);
            
            // Rebuild the tree
            BuildTreeView();
        }

        // Delete a folder and move its contents to parent
        private void DeleteFolder(TreeNode folderNode)
        {
            var folderPath = GetTreePath(folderNode);
            var parentPath = folderPath.Take(folderPath.Count - 1).ToList();

            // Update all servers in this folder to move to parent
            var serversToUpdate = new List<ProtocolBase>();
            CollectServersInFolder(folderNode, serversToUpdate);

            foreach (var server in serversToUpdate)
            {
                server.TreeNodes = new List<string>(parentPath);
            }

            if (serversToUpdate.Count > 0)
            {
                AppData.UpdateServer(serversToUpdate);
            }

            // Rebuild the tree
            BuildTreeView();
        }

        // Update all servers in a folder when the folder is renamed
        private void UpdateServersInFolder(TreeNode folderNode, List<string> oldPath, List<string> newPath)
        {
            var serversToUpdate = new List<ProtocolBase>();
            CollectServersInFolder(folderNode, serversToUpdate);

            foreach (var server in serversToUpdate)
            {
                // Update the server's tree path
                var serverPath = new List<string>(server.TreeNodes);
                
                // Replace the old folder path with the new one
                if (serverPath.Count >= oldPath.Count)
                {
                    bool matches = true;
                    for (int i = 0; i < oldPath.Count; i++)
                    {
                        if (i >= serverPath.Count || serverPath[i] != oldPath[i])
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (matches)
                    {
                        // Replace the old path with new path
                        var updatedPath = new List<string>(newPath);
                        for (int i = oldPath.Count; i < serverPath.Count; i++)
                        {
                            updatedPath.Add(serverPath[i]);
                        }
                        server.TreeNodes = updatedPath;
                    }
                }
            }

            if (serversToUpdate.Count > 0)
            {
                AppData.UpdateServer(serversToUpdate);
            }
        }

        // Collect all servers under a folder node
        private void CollectServersInFolder(TreeNode folderNode, List<ProtocolBase> servers)
        {
            foreach (var child in folderNode.Children)
            {
                if (child.IsTag)
                {
                    CollectServersInFolder(child, servers);
                }
                else if (child.Server != null)
                {
                    servers.Add(child.Server.Server);
                }
            }
        }

        // Create a new folder under the specified parent node
        private void CreateNewFolder(TreeNode? parentNode, string folderName)
        {
            // Get the path for the new folder
            var parentPath = parentNode != null ? GetTreePath(parentNode) : new List<string>();
            var newFolderPath = new List<string>(parentPath) { folderName };

            // For now, just rebuild the tree view
            // The folder will appear when a server is added to it
            // This is a placeholder for future folder creation functionality
            MessageBoxHelper.Info($"Folder '{folderName}' will be created. Add servers to this path to make it visible.");
            
            // TODO: In the future, you might want to create an empty folder entry in the data source
            // or implement a different mechanism for folder management
        }

        #endregion
    }
}
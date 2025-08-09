using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.View.ServerList;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using _1RM.View.Editor;
using Org.BouncyCastle.Asn1.X509;
using _1RM.Service;

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

            public bool IsFolder { get; private set; }
            public ProtocolBaseViewModel? Server { get; private set; }
            public ObservableCollection<TreeNode> Children { get; } = new ObservableCollection<TreeNode>();
            public bool IsExpanded { get; set; } = true;
            public bool IsSelected { get; set; }

            // For folder/tree nodes
            public TreeNode(string name)
            {
                _name = name;
                IsFolder = true;
            }

            // For server nodes
            public TreeNode(ProtocolBaseViewModel server)
            {
                _name = server.DisplayName;
                Server = server;
                IsFolder = false;
            }

            public List<string> GetFolderNames()
            {
                return Children.Where(x => x.IsFolder == true).Select(x => x.Name).ToList();
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

                // Check if there are any servers to display
                if (AppData.VmItemList.Count == 0)
                {
                    RootNodes = newRoot;
                    return;
                }

                // Group servers by TreeNodes
                foreach (var server in AppData.VmItemList)
                {
                    // This ensures first-time users see all servers at root level
                    var treeNodes = new List<string> {server.DataSourceName};
                    treeNodes.AddRange(server.Server.TreeNodes);
                    // Build path based on TreeNodes
                    TreeNode? currentNode = null;
                    string currentPath = "";
                    foreach (var nodeName in treeNodes)
                    {
                        currentPath = string.IsNullOrEmpty(currentPath) ? nodeName : $"{currentPath}->{nodeName}";
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
            var sorted = nodes.OrderBy(n => !n.IsFolder).ThenBy(n => n.Name).ToList();
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
        public void SetSelectedServer(ProtocolBaseViewModel server)
        {
            SelectedServerViewModel = server;
            IsAnySelected = true;
            SelectedCount = 1;
        }

        private RelayCommand? _CmdCancelSelected;
        // Command to cancel selection
        public RelayCommand CmdCancelSelected
        {
            get
            {
                return _CmdCancelSelected ??= new RelayCommand((o) =>
                {
                    SelectedNode = null;
                    SelectedServerViewModel = null;
                    IsAnySelected = false;
                    SelectedCount = 0;
                });
            }
        }

        // Command to connect to selected server
        private RelayCommand? _cmdConnectSelected;
        public RelayCommand CmdConnectSelected
        {
            get
            {
                return _cmdConnectSelected ??= new RelayCommand((o) =>
                {
                    if (SelectedServerViewModel != null)
                    {
                        GlobalEventHelper.OnRequestServerConnect?.Invoke(SelectedServerViewModel.Server, fromView: "ServerTreeView");
                    }
                });
            }
        }

        // Command to edit selected server
        private RelayCommand? _cmdMultiEditSelected;
        public RelayCommand CmdMultiEditSelected
        {
            get
            {
                return _cmdMultiEditSelected ??= new RelayCommand((o) =>
                {
                    if (SelectedServerViewModel != null)
                    {
                        GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(SelectedServerViewModel.Server, true);
                    }
                });
            }
        }

        // Command to create desktop shortcut for selected server
        private RelayCommand? _cmdCreateDesktopShortcut;
        public RelayCommand CmdCreateDesktopShortcut
        {
            get
            {
                return _cmdCreateDesktopShortcut ??= new RelayCommand((o) =>
                {
                    // Desktop shortcut functionality not implemented yet
                    if (SelectedServerViewModel?.Server != null)
                    {
                        MessageBoxHelper.Info("Create desktop shortcut functionality is not implemented yet.");
                    }
                });
            }
        }

        // Command to delete selected server
        private RelayCommand? _cmdDeleteSelected;
        public RelayCommand CmdDeleteSelected
        {
            get
            {
                return _cmdDeleteSelected ??= new RelayCommand((o) =>
                {
                    if (SelectedServerViewModel != null)
                    {
                        var server = SelectedServerViewModel.Server;
                        if (server.DataSource?.IsWritable == true)
                        {
                            if (MessageBoxHelper.Confirm(IoC.Translate("do_you_really_want_to_delete_x")
                                    .Replace("{0}", server.DisplayName)))
                            {
                                // Use the App's DeleteServer method
                                AppData.DeleteServer(new List<ProtocolBase> { server });
                                CmdCancelSelected.Execute();
                            }
                        }
                    }
                });
            }
        }

        // Command to export selected server to JSON
        private RelayCommand? _cmdExportSelectedToJson;
        public RelayCommand CmdExportSelectedToJson
        {
            get
            {
                return _cmdExportSelectedToJson ??= new RelayCommand((o) =>
                {
                    if (SelectedServerViewModel != null)
                    {
                        // Export functionality not implemented yet
                        MessageBoxHelper.Info("TXT: Export to JSON functionality is not implemented yet.");
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
                        parentNode = node.IsFolder == false ? FindParent(null, RootNodes, node) : node;
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
                        parentNode.Children.Add(new TreeNode(folderName));
                        SortNodes(RootNodes);
                    }
                });
            }
        }

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
                        var parentNode = FindParent(null, RootNodes, node);
                        if (parentNode == null) return;

                        if (node.IsFolder)
                        {
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



                                var oldPath = GetTreePath(node);
                                var newPath = new List<string>(oldPath);
                                if (newPath.Count > 0)
                                {
                                    newPath[newPath.Count - 1] = newName;
                                }

                                // Update all servers that are under this folder
                                {
                                    UpdateServersInFolder(node, oldPath, newPath);

                                    var serverNodes = new List<TreeNode>();
                                    CollectServerNodesInFolder(node, serverNodes);

                                    foreach (var serverNode in serverNodes)
                                    {
                                        if(serverNode.IsFolder) continue;
                                        var server = serverNode.Server?.Server;
                                        if (server == null) continue;



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

                                // Rebuild the tree
                                BuildTreeView();
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

                    // since the root node is the data source, we need to remove it from the path
                    if (treePath.Count > 0)
                        treePath.RemoveAt(0);
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

        // Get the tree path from root to the specified node
        private List<string> GetTreePath(TreeNode node)
        {
            var path = new List<string>();
            // Find the path by searching through the tree
            FindNodePath(RootNodes, node, new List<string>(), path);
            return path;
        }

        private static TreeNode? FindParent(TreeNode? root, IEnumerable<TreeNode> children, TreeNode target)
        {
            foreach (var node in children)
            {
                if (node == target)
                {
                    return root;
                }
                var ret = FindParent(node, node.Children, target);
                if (ret != null)
                {
                    return ret;
                }
            }
            return null;
        }


        private static bool FindNodePath(ObservableCollection<TreeNode> nodes, TreeNode target, List<string> currentPath, List<string> result)
        {
            foreach (var node in nodes)
            {
                var newPath = new List<string>(currentPath);
                if (node.IsFolder)
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
        private void UpdateServersInFolder(TreeNode node, List<string> oldPath, List<string> newPath)
        {
            var serversToUpdate = new List<ProtocolBase>();
            CollectServersInFolder(node, serversToUpdate);

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
        // Collect all servers under a folder node
        private void CollectServerNodesInFolder(TreeNode folderNode, List<TreeNode> servers)
        {
            foreach (var child in folderNode.Children)
            {
                if (child.IsFolder)
                {
                    CollectServerNodesInFolder(child, servers);
                }
                else if (child.Server != null)
                {
                    servers.Add(child);
                }
            }
        }

        #endregion
    }
}
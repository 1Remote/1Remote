using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.View.Editor;
using _1RM.View.ServerList;
using _1RM.View.Utils;
using Org.BouncyCastle.Asn1.X509;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;
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
            public bool IsExpanded { get; set; } = true;
            public bool IsSelected { get; set; }

            // For folder/tree nodes
            public TreeNode(string name, bool isRoot)
            {
                _name = name;
                IsFolder = true;
                IsRootFolder = isRoot;
            }

            // For server nodes
            public TreeNode(ProtocolBaseViewModel server)
            {
                _name = server.DisplayName;
                Server = server;
                IsFolder = false;
                IsRootFolder = false;
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

                // Add a root node for each data source
                {
                    var rootNode = new TreeNode(IoC.Get<DataSourceService>().LocalDataSource!.Name, true);
                    newRoot.Add(rootNode);
                    nodeTreePaths[rootNode.Name] = rootNode;
                }
                foreach (var dataSource in IoC.Get<DataSourceService>().AdditionalSources)
                {
                    var rootNode = new TreeNode(dataSource.Value.DataSourceName, true);
                    newRoot.Add(rootNode);
                    nodeTreePaths[rootNode.Name] = rootNode;
                }

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
                    for (var i = 0; i < treeNodes.Count; i++)
                    {
                        var nodeName = treeNodes[i];
                        currentPath = string.IsNullOrEmpty(currentPath) ? nodeName : $"{currentPath}->{nodeName}";
                        if (!nodeTreePaths.TryGetValue(currentPath, out var treeNode))
                        {
                            treeNode = new TreeNode(nodeName, i == 0);
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
                        parentNode.Children.Add(new TreeNode(folderName, false));
                        SortNodes(RootNodes);
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
                            if(node.Server?.Server == null) return;
                            GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(node.Server.Server, true);
                        }
                        else if (node.IsFolder)
                        {
                            var parentNode = FindParent(null, RootNodes, node);
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
                    List<string> treePath;
                    if (o is TreeNode node)
                    {
                        // Build the tree path for the new server
                        treePath = GetTreePath(node, false);
                    }
                    else
                    {
                        // Add to root
                        treePath = new List<string>();
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

        // Get the tree path from root to the specified node, the first node is a dummy node representing the data source
        private List<string> GetTreePath(TreeNode node, bool includeFirstNode)
        {
            var path = new List<string>();
            // Find the path by searching through the tree
            FindNodePath(RootNodes, node, new List<string>(), path);
            if (!includeFirstNode && path.Count > 0)
            {
                // Remove the first node if not needed
                path.RemoveAt(0);
            }
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
            var serversToDelete = new List<ProtocolBase>();
            CollectServersInFolder(folderNode, serversToDelete);
            AppData.DeleteServer(serversToDelete);
            // Rebuild the tree
            BuildTreeView();
        }

        // Update all servers in a folder when the folder is renamed
        private void UpdateServerTreeNodes(TreeNode node)
        {
            var serverNodes = node.GetChildNodeItems();
            if (serverNodes.Count > 0)
            {
                // update the servers in this folder
                var folderPath = GetTreePath(node, false);
                //IoC.Get<GlobalData>().UpdateServer()
                var dataSource = node.Children.FirstOrDefault(x => x.IsFolder == false)?.Server?.Server?.DataSource;
                if (dataSource?.IsWritable != true) return;
                var servers = (serverNodes.Select(x => x.Server?.Server).Where(x => x != null).ToList()) as List<ProtocolBase>;
                foreach (var server in servers)
                {
                    server.TreeNodes = new List<string>(folderPath);
                }
                dataSource.Database_UpdateServer(servers);
            }

            var folderNodes = node.GetChildNodeFolder();
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
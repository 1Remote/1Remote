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
using Org.BouncyCastle.Asn1.X509;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        }


        protected override void OnViewLoaded()
        {
            AppData.OnReloadAll += BuildView;
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
                    sorted = sorted.ThenBy(n => n.Server?.Server?.SubTitle ?? string.Empty);
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
                    if (View is ServerListPageView view)
                        view.CbPopForInExport.IsChecked = false;
                    GlobalEventHelper.OnGoToServerAddPage?.Invoke(TagFilters.Where<TagFilter>(x => x.IsIncluded == true).Select(x => x.TagName).ToList(), o as DataSourceBase);
                });
            }
        }

        // Command for when a server is selected in the tree
        public void SetSelectedServer(ProtocolBaseViewModel? server)
        {
            SimpleLogHelper.Debug($"SelectedServerViewModel = {server?.DisplayName ?? "null"}");
            SelectedServerViewModel = server;
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
                            if (node.Server?.Server == null) return;
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
        public List<string> GetTreePath(TreeNode node, bool includeFirstNode)
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

        public TreeNode? FindParent(TreeNode target)
        {
            return FindParent(null, RootNodes, target);
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
            BuildView();
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

        public sealed override void BuildView()
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
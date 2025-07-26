using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.View.ServerList;
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
            public Tag? Tag { get; private set; }
            public ProtocolBaseViewModel? Server { get; private set; }
            public ObservableCollection<TreeNode> Children { get; } = new ObservableCollection<TreeNode>();
            public bool IsExpanded { get; set; } = true;
            public bool IsSelected { get; set; }

            // For tag nodes
            public TreeNode(string name, Tag tag)
            {
                Name = name;
                Tag = tag;
                IsTag = true;
            }

            // For server nodes
            public TreeNode(ProtocolBaseViewModel server)
            {
                Name = server.DisplayName;
                Server = server;
                IsTag = false;
            }

            // For "others" or root nodes
            public TreeNode(string name)
            {
                Name = name;
                IsTag = true;
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

            // Also observe changes to tags which could affect the tree structure
            AppData.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(GlobalData.TagList))
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
                var tagTreeNodes = new Dictionary<string, TreeNode>();
                var othersNode = new TreeNode("others");

                // Check if there are any servers to display
                if (AppData.VmItemList.Count == 0)
                {
                    RootNodes = newRoot;
                    return;
                }

                // Group servers by tags
                foreach (var server in AppData.VmItemList)
                {
                    if (server.Tags.Count == 0)
                    {
                        othersNode.Children.Add(new TreeNode(server));
                    }
                    else
                    {
                        // Build path based on tags
                        TreeNode? currentNode = null;
                        string currentPath = "";
                        
                        foreach (var tag in server.Tags)
                        {
                            if (string.IsNullOrEmpty(currentPath))
                            {
                                currentPath = tag.Name;
                            }
                            else
                            {
                                currentPath = $"{currentPath}->{tag.Name}";
                            }

                            if (!tagTreeNodes.TryGetValue(currentPath, out var tagNode))
                            {
                                tagNode = new TreeNode(tag.Name, tag);
                                tagTreeNodes[currentPath] = tagNode;

                                if (currentNode == null)
                                {
                                    // Add to root
                                    newRoot.Add(tagNode);
                                }
                                else
                                {
                                    currentNode.Children.Add(tagNode);
                                }
                            }

                            currentNode = tagNode;
                        }

                        // Add the server to the last tag node
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
    }
}
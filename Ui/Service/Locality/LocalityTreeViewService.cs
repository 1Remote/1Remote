using _1RM.Utils.Tracing;
using _1RM.View.ServerTree;
using Newtonsoft.Json;
using Shawn.Utils;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace _1RM.Service.Locality
{
    public class LocalityTreeViewSettings
    {
        public EnumServerOrderBy ServerOrderBy = EnumServerOrderBy.IdAsc;
        public Dictionary<string, int> CustomNodeOrder = new Dictionary<string, int>();
        /// <summary>
        /// Dictionary to store tree node expansion states
        /// Key: full path string of the tree folder node (e.g., "LocalDataSource->Folder1->SubFolder")
        /// Value: whether the node is expanded
        /// </summary>
        public Dictionary<string, bool> TreeNodeExpansionStates = new Dictionary<string, bool>();
    }

    public static class LocalityTreeViewService
    {
        public static string JsonPath => Path.Combine(AppPathHelper.Instance.LocalityDirPath, ".tree_view.json");

        private static LocalityTreeViewSettings? _settings;
        public static LocalityTreeViewSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    Load();
                }
                return _settings!;
            }
            private set => _settings = value;
        }


        public static void Load()
        {
            if (!File.Exists(JsonPath))
                _settings = new LocalityTreeViewSettings();
            try
            {
                var tmp = JsonConvert.DeserializeObject<LocalityTreeViewSettings>(File.ReadAllText(JsonPath));
                tmp ??= new LocalityTreeViewSettings();
                _settings = tmp;
            }
            catch
            {
                _settings = new LocalityTreeViewSettings();
            }
        }

        public static void Save()
        {
            AppPathHelper.CreateDirIfNotExist(AppPathHelper.Instance.LocalityDirPath, false);
            RetryHelper.Try(() => { File.WriteAllText(JsonPath, JsonConvert.SerializeObject(Settings, Formatting.Indented), Encoding.UTF8); }, actionOnError: exception => UnifyTracing.Error(exception));
        }

        public static void ServerOrderBySet(EnumServerOrderBy value)
        {
            if (Settings.ServerOrderBy == value) return;
            Settings.ServerOrderBy = value;
            Save();
        }

        public static void SaveExpansionStates(ObservableCollection<ServerTreeViewModel.TreeNode> roots)
        {
            var currentValidStates = new Dictionary<string, bool>();
            // Recursively apply cached states and collect current valid folder paths
            {
                var stack = new Stack<ServerTreeViewModel.TreeNode>();
                foreach (var node in roots)
                {
                    stack.Push(node);
                }
                while (stack.Count > 0)
                {
                    var currentNode = stack.Pop();
                    if (currentNode.IsFolder && !string.IsNullOrEmpty(currentNode.FullPath))
                    {
                        currentValidStates[currentNode.FullPath] = currentNode.IsExpanded;
                    }
                    // Push all child nodes onto the stack (in reverse order to maintain original traversal order)
                    if (currentNode.Children.Count > 0)
                    {
                        for (int i = currentNode.Children.Count - 1; i >= 0; i--)
                        {
                            stack.Push(currentNode.Children[i]);
                        }
                    }
                }
            }
            // Rebuild the cache
            Settings.TreeNodeExpansionStates = currentValidStates;
            Save();
        }


        public static void LoadExpansionStates(ObservableCollection<ServerTreeViewModel.TreeNode> roots)
        {
            var cachedStates = Settings.TreeNodeExpansionStates;
            var currentValidStates = new Dictionary<string, bool>();
            // Recursively apply cached states and collect current valid folder paths
            var stack = new Stack<ServerTreeViewModel.TreeNode>();
            foreach (var node in roots)
            {
                stack.Push(node);
            }
            while (stack.Count > 0)
            {
                var currentNode = stack.Pop();
                if (currentNode.IsFolder && !string.IsNullOrEmpty(currentNode.FullPath))
                {
                    // Get cached expansion state or default to true
                    var savedExpansionState = cachedStates.GetValueOrDefault(currentNode.FullPath, true);
                    // Apply the state without triggering save
                    currentNode._isExpanded = savedExpansionState;
                    currentNode.RaisePropertyChanged(nameof(ServerTreeViewModel.TreeNode.IsExpanded));
                    currentValidStates[currentNode.FullPath] = currentNode.IsExpanded;
                }

                // Push all child nodes onto the stack (in reverse order to maintain original traversal order)
                if (currentNode.Children.Count > 0)
                {
                    for (int i = currentNode.Children.Count - 1; i >= 0; i--)
                    {
                        stack.Push(currentNode.Children[i]);
                    }
                }
            }
            // Rebuild the cache
            Settings.TreeNodeExpansionStates = currentValidStates;
            Save();
        }
    }
}

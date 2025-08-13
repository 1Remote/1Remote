using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Shawn.Utils;
using _1RM.Service.DataSource;
using _1RM.Utils.Tracing;
using _1RM.View;

namespace _1RM.Service.Locality
{
    public class LocalityTreeViewSettings
    {
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
        private static LocalityTreeViewSettings _settings = new LocalityTreeViewSettings();

        private static bool _isLoaded = false;
        private static void Load()
        {
            lock (_settings)
            {
                if (_isLoaded) return;
                _isLoaded = true;
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
        }

        public static bool CanSave { get; private set; } = true;
        private static void Save()
        {
            if (!CanSave) return;
            lock (_settings)
            {
                if (!CanSave) return;
                CanSave = false;
                AppPathHelper.CreateDirIfNotExist(AppPathHelper.Instance.LocalityDirPath, false);
                RetryHelper.Try(() => { File.WriteAllText(JsonPath, JsonConvert.SerializeObject(_settings, Formatting.Indented), Encoding.UTF8); },
                    actionOnError: exception => UnifyTracing.Error(exception));
                CanSave = true;
            }
        }

        /// <summary>
        /// Get the expansion state of a tree node by its full path
        /// </summary>
        /// <param name="nodePath">Full path of the tree node (e.g., "LocalDataSource->Folder1->SubFolder")</param>
        /// <returns>True if expanded, false if collapsed. Default is true for new nodes.</returns>
        public static bool GetTreeNodeExpansionState(string nodePath)
        {
            Load();
            return _settings.TreeNodeExpansionStates.GetValueOrDefault(nodePath, true);
        }

        /// <summary>
        /// Set the expansion state of a tree node by its full path
        /// </summary>
        /// <param name="nodePath">Full path of the tree node (e.g., "LocalDataSource->Folder1->SubFolder")</param>
        /// <param name="isExpanded">Whether the node is expanded</param>
        public static void SetTreeNodeExpansionState(string nodePath, bool isExpanded)
        {
            Load();
            try
            {
                if (_settings.TreeNodeExpansionStates.ContainsKey(nodePath))
                    _settings.TreeNodeExpansionStates[nodePath] = isExpanded;
                else
                    _settings.TreeNodeExpansionStates.Add(nodePath, isExpanded);

                // Clean up outdated entries - remove entries for non-existent data sources
                var ds = IoC.TryGet<DataSourceService>();
                if (ds != null)
                {
                    var validDataSourceNames = new HashSet<string>();
                    if (ds.LocalDataSource != null)
                        validDataSourceNames.Add(ds.LocalDataSource.Name);
                    foreach (var additionalSource in ds.AdditionalSources)
                        validDataSourceNames.Add(additionalSource.Value.DataSourceName);

                    var keysToRemove = new List<string>();
                    foreach (var key in _settings.TreeNodeExpansionStates.Keys)
                    {
                        var rootName = key.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                        if (rootName != null && !validDataSourceNames.Contains(rootName))
                        {
                            keysToRemove.Add(key);
                        }
                    }

                    foreach (var key in keysToRemove)
                    {
                        _settings.TreeNodeExpansionStates.Remove(key);
                    }
                }
            }
            catch (Exception e)
            {
                UnifyTracing.Error(e);
                _settings.TreeNodeExpansionStates = new Dictionary<string, bool>();
            }
            Save();
        }

        /// <summary>
        /// Clear all tree node expansion states
        /// </summary>
        public static void ClearAllTreeNodeExpansionStates()
        {
            Load();
            _settings.TreeNodeExpansionStates.Clear();
            Save();
        }

        /// <summary>
        /// Get all stored tree node expansion states
        /// </summary>
        /// <returns>Dictionary of all stored expansion states</returns>
        public static Dictionary<string, bool> GetAllTreeNodeExpansionStates()
        {
            Load();
            return new Dictionary<string, bool>(_settings.TreeNodeExpansionStates);
        }
    }
}

using _1RM.Service.Locality;
using _1RM.View.ServerView;
using Newtonsoft.Json;
using Shawn.Utils;

namespace _1RM.Model
{
    /// <summary>
    /// the class is used on Tags page and in the header bar of ServerListPage and ProtocolListItem
    /// </summary>
    public class Tag : NotifyPropertyChangedBase
    {
        public Tag(string? name, bool isPinned, int customOrder)
        {
            _name = name?.ToLower() ?? ""; // json deserialization will set null to string
            _isPinned = isPinned;
            _customOrder = customOrder;
        }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetAndNotifyIfChanged(ref _name, value);
        }


        private int _itemsCount = 0;
        [JsonIgnore]
        public int ItemsCount
        {
            get => _itemsCount;
            set => SetAndNotifyIfChanged(ref _itemsCount, value);
        }

        private bool _isPinned = false;
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (SetAndNotifyIfChanged(ref _isPinned, value))
                {
                    if (_isPinned)
                    {
                        // move to the end of the list to keep new pinned tags at the end of the header bar.
                        IoC.Get<GlobalData>().TagList.Remove(this);
                        IoC.Get<GlobalData>().TagList.Add(this);
                        int i = 0;
                        foreach (var tag in IoC.Get<GlobalData>().TagList)
                        {
                            tag.CustomOrder = i++;
                        }
                    }
                    LocalityTagService.UpdateTags(IoC.Get<GlobalData>().TagList);       // save pinned state and order to .tags.json
                    IoC.Get<ServerListPageViewModel>().CalcTagFilterBarVisibility();    // update tag filter bar visibility
                    IoC.Get<GlobalData>().RaiseTagListChanged();                        // notify observers that TagList has changed to update UI bindings
                }
            }
        }


        private int _customOrder;
        /// <summary>
        /// Order of the tag on the header bar.
        /// </summary>
        public int CustomOrder
        {
            get => _customOrder;
            set => SetAndNotifyIfChanged(ref _customOrder, value);
        }
    }
}

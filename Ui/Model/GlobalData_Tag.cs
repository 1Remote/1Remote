using System;
using _1RM.Service.Locality;
using Shawn.Utils;
using System.Collections.Generic;
using System.Linq;

namespace _1RM.Model
{
    public partial class GlobalData : NotifyPropertyChangedBase
    {

        private List<Tag> _tagList = new List<Tag>();
        public List<Tag> TagList
        {
            get => _tagList;
            private set => SetAndNotifyIfChanged(ref _tagList, value);
        }


        /// <summary>
        /// Reload tags from servers, this will read all servers and update the TagList.
        /// </summary>
        public void ReloadTagsFromServers()
        {
            // get distinct tag from servers
            var tags = new List<Tag>();
            LocalityTagService.Load();
            foreach (var tagNames in VmItemList.Select(x => x.Server.Tags))
            {
                foreach (var tn in tagNames.Select(tagName => tagName.Trim().ToLower()))
                {
                    if (tags.All(x => !string.Equals(x.Name, tn, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        bool isPinned = LocalityTagService.GetIsPinned(tn);
                        int customOrder = LocalityTagService.GetCustomOrder(tn);
                        tags.Add(new Tag(tn, isPinned, customOrder) { ItemsCount = 1 });
                    }
                    else
                        tags.First(x => x.Name.ToLower() == tn).ItemsCount++;
                }
            }

            TagList = new List<Tag>(tags.OrderBy(x => x.CustomOrder).ThenBy(x => x.Name));
            foreach (var viewModel in VmItemList.Where(viewModel => viewModel.Server.Tags.Count > 0))
            {
                viewModel.ReLoadTags();
            }
        }
    }
}
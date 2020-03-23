using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using PRM.Core.Base;
using PRM.Core.Model;
using Shawn.Ulits;

namespace PRM.ViewModel
{
    public class VmSearchBox : NotifyPropertyChangedBase
    {
        public VmSearchBox()
        {
            UpdateDispList("");
        }



        private ObservableCollection<ServerAbstract> _dispServerlist = new ObservableCollection<ServerAbstract>();
        /// <summary>
        /// ServerDict data source for listbox
        /// </summary>
        public ObservableCollection<ServerAbstract> DispServerList
        {
            get => _dispServerlist;
            set
            {
                SetAndNotifyIfChanged(nameof(DispServerList), ref _dispServerlist, value);
            }
        }



        private int _selectedServerTextIndex;
        public int SelectedServerTextIndex
        {
            get => _selectedServerTextIndex;
            set => SetAndNotifyIfChanged(nameof(SelectedServerTextIndex), ref _selectedServerTextIndex, value);
        }



        private string _dispNameFilter;
        public string DispNameFilter
        {
            get => _dispNameFilter;
            set
            {
                SetAndNotifyIfChanged(nameof(DispNameFilter), ref _dispNameFilter, value);
                UpdateDispList(value);
            }
        }





        private bool _PopupIsOpen = true;
        public bool PopupIsOpen
        {
            get => _PopupIsOpen;
            set => SetAndNotifyIfChanged(nameof(PopupIsOpen), ref _PopupIsOpen, value);
        }









        private void UpdateDispList(string keyWord)
        {
            DispServerList.Clear();

            // 高级搜索
            foreach (var item in Global.GetInstance().ServerDict.Values
                .Where(x => x.GetType() != typeof(NoneServer)))
            {
                var f1 = KeyWordMatchHelper.IsMatchPinyinKeyWords(item.DispName, keyWord, out var m1);
                var f2 = KeyWordMatchHelper.IsMatchPinyinKeyWords(item.SubTitle, keyWord, out var m2);
                if (f1 || f2)
                {
                    DispServerList.Add(item);
                }
            }


            if (!DispServerList.Any())
                PopupIsOpen = false;
            else
            {
                SelectedServerTextIndex = 0;
                PopupIsOpen = true;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.Base;
using Shawn.Ulits;

namespace PRM.Core.ViewModel
{
    public class VmSearchBox : NotifyPropertyChangedBase
    {
        private readonly VmMain _vmMain = null;



        public VmSearchBox(VmMain vmMain)
        {
            Debug.Assert(vmMain != null);
            _vmMain = vmMain;
            UpdateDispList("");
        }



        private ObservableCollection<ServerAbstract> _dispServerlist = new ObservableCollection<ServerAbstract>();
        /// <summary>
        /// ServerList data source for listbox
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
            //foreach (var item in _vmMain.DispServerList
            //    .Where(x => x.Server.DispName.IndexOf(keyWord.Trim()) >= 0 && x.Server.GetType() != typeof(NoneServer))
            //    .Select(x => x.Server))
            //{
            //    DispServerList.Add(item);
            //}


            // 高级搜索
            foreach (var item in _vmMain.DispServerList
                .Where(x => x.Server.GetType() != typeof(NoneServer))
                .Select(x => x.Server))
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

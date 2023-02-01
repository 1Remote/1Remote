using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.Model
{
    public class MenuViewModel<T> : NotifyPropertyChangedBase
    {
        private readonly Action<T> _onClickItem;
        private readonly T? _valueItem;

        public MenuViewModel(string header,
            T? item,
            Action<T> onClickItem)
            : this(header, item, new List<MenuViewModel<T>>(), onClickItem)
        {

        }

        public MenuViewModel(string header,
            List<MenuViewModel<T>> subItems)
            : this(header, default(T), subItems, obj => { })
        {
        }


        private MenuViewModel(string header,
            T? item,
            List<MenuViewModel<T>> subItems,
            Action<T> onClickItem)
        {
            _header = header;
            _subItems = new ObservableCollection<MenuViewModel<T>>(subItems);
            _onClickItem = onClickItem;
            _valueItem = item;
        }

        private string _header;
        public string Header
        {
            get => _header;
            set => SetAndNotifyIfChanged(ref _header, value);
        }

        private ObservableCollection<MenuViewModel<T>> _subItems;
        public ObservableCollection<MenuViewModel<T>> SubItems
        {
            get => _subItems;
            set => SetAndNotifyIfChanged(ref _subItems, value);
        }

        private RelayCommand? _menuItemCommand;
        public RelayCommand MenuItemCommand
        {
            get
            {
                return _menuItemCommand ??= new RelayCommand((o) =>
                {
                    if (_valueItem != null) _onClickItem(_valueItem);
                });
            }
        }
    }
}

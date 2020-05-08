using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using PRM.Core;
using PRM.Core.Model;
using PRM.Core.Protocol;
using Shawn.Ulits;

namespace PRM.ViewModel
{
    public class ProtocolServerBaseInSearchBox : NotifyPropertyChangedBase
    {
        private ProtocolServerBase _server = null;
        public ProtocolServerBase Server
        {
            get => _server;
            set
            {
                SetAndNotifyIfChanged(nameof(Server), ref _server, value);
            }
        }

        public ProtocolServerBaseInSearchBox(ProtocolServerBase psb)
        {
            Server = psb;
            DispNameControl = (new TextBlock()
            {
                Text = psb.DispName,
            });
            SubTitleControl = (new TextBlock()
            {
                Text = psb.SubTitle,
            });
        }


        private object _dispNameControl = null;
        public object DispNameControl
        {
            get => _dispNameControl;
            set
            {
                SetAndNotifyIfChanged(nameof(DispNameControl), ref _dispNameControl, value);
            }
        }



        private object _subTitleControl = null;
        public object SubTitleControl
        {
            get => _subTitleControl;
            set
            {
                SetAndNotifyIfChanged(nameof(SubTitleControl), ref _subTitleControl, value);
            }
        }

    }




    public class VmSearchBox : NotifyPropertyChangedBase
    {
        public VmSearchBox()
        {
            UpdateDispList("");
        }



        private ObservableCollection<ProtocolServerBaseInSearchBox> _servers = new ObservableCollection<ProtocolServerBaseInSearchBox>();
        /// <summary>
        /// ServerList data source for listbox
        /// </summary>
        public ObservableCollection<ProtocolServerBaseInSearchBox> Servers
        {
            get => _servers;
            set
            {
                SetAndNotifyIfChanged(nameof(Servers), ref _servers, value);
            }
        }



        private int _selectedServerIndex;
        public int SelectedServerIndex
        {
            get => _selectedServerIndex;
            set => SetAndNotifyIfChanged(nameof(SelectedServerIndex), ref _selectedServerIndex, value);
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
            Servers.Clear();

            if (!string.IsNullOrEmpty(keyWord))
            {
                var keyWords = keyWord.Split(new string[]{" "}, StringSplitOptions.RemoveEmptyEntries);
                var keyWordIsMatch = new List<bool>(keyWords.Length);
                for (var i = 0; i < keyWords.Length; i++)
                    keyWordIsMatch.Add(false);

                // match keyword
                foreach (var item in Global.GetInstance().ServerList.Where(x => x.GetType() != typeof(ProtocolServerNone)))
                {
                    Debug.Assert(!string.IsNullOrEmpty(item.ClassVersion));
                    Debug.Assert(!string.IsNullOrEmpty(item.Protocol));

                    var mDispName = new List<List<bool>>();
                    var mSubTitle = new List<List<bool>>();
                    var dispName = item.DispName;
                    var subTitle = item.SubTitle;
                    for (var i = 0; i < keyWordIsMatch.Count; i++)
                    {
                        var f1 = KeyWordMatchHelper.IsMatchPinyinKeyWords(dispName, keyWords[i], out var m1);
                        var f2 = KeyWordMatchHelper.IsMatchPinyinKeyWords(subTitle, keyWords[i], out var m2);
                        mDispName.Add(m1);
                        mSubTitle.Add(m2);
                        keyWordIsMatch[i] = f1 || f2;
                    }

                    if (keyWordIsMatch.All(x => x == true))
                    {
                        var m1 = new List<bool>();
                        var m2 = new List<bool>();
                        for (var i = 0; i < dispName.Length; i++)
                            m1.Add(false);
                        for (var i = 0; i < subTitle.Length; i++)
                            m2.Add(false);
                        for (var i = 0; i < keyWordIsMatch.Count; i++)
                        {
                            if (mDispName[i] != null)
                                for (int j = 0; j < mDispName[i].Count; j++)
                                    m1[j] |= mDispName[i][j];
                            if (mSubTitle[i] != null)
                                for (int j = 0; j < mSubTitle[i].Count; j++)
                                    m2[j] |= mSubTitle[i][j];
                        }


                        var semite = new ProtocolServerBaseInSearchBox(item);
                        const bool enableHighLine = true;
                        // highline matched chars.
                        if (enableHighLine)
                        {
                            if (m1.Any(x => x == true))
                            {
                                var sp = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal };
                                for (int i = 0; i < m1.Count; i++)
                                {
                                    if (m1[i])
                                        sp.Children.Add(new TextBlock()
                                        {
                                            Text = dispName[i].ToString(),
                                            Background = new SolidColorBrush(Color.FromArgb(80, 239, 242, 132)),
                                        });
                                    else
                                        sp.Children.Add(new TextBlock()
                                        {
                                            Text = dispName[i].ToString(),
                                        });
                                }

                                semite.DispNameControl = sp;
                            }

                            if (m2.Any(x => x == true))
                            {
                                var sp = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal };
                                var subtitle = item.SubTitle;
                                for (int i = 0; i < m2.Count; i++)
                                {
                                    if (m2[i])
                                        sp.Children.Add(new TextBlock()
                                        {
                                            Text = subtitle[i].ToString(),
                                            Background = new SolidColorBrush(Color.FromArgb(120, 239, 242, 132)),
                                        });
                                    else
                                        sp.Children.Add(new TextBlock()
                                        {
                                            Text = subtitle[i].ToString(),
                                        });
                                }

                                semite.SubTitleControl = sp;
                            }
                        }

                        Servers.Add(semite);
                    }
                }
            }

            if (!Servers.Any())
                PopupIsOpen = false;
            else
            {
                SelectedServerIndex = 0;
                PopupIsOpen = true;
            }
        }
    }
}

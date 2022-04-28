using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using PRM.Model;
using PRM.Utils;

namespace PRM.View
{
    public class MainWindowSearchControlViewModel : NotifyPropertyChangedBaseScreen
    {
        private bool _isFocused = false;
        public bool IsFocused
        {
            get => _isFocused;
            set => SetAndNotifyIfChanged(ref _isFocused, value);
        }

        private int _tbFilterCaretIndex = 0;
        public int TbFilterCaretIndex
        {
            get => _tbFilterCaretIndex;
            set => SetAndNotifyIfChanged(ref _tbFilterCaretIndex, value);
        }

        private string _filterString = "";
        public string FilterString
        {
            get => _filterString;
            set
            {
                // can only be called by the Ui
                if (SetAndNotifyIfChanged(ref _filterString, value))
                {
                    Task.Factory.StartNew(() =>
                    {
                        var filter = FilterString;
                        Thread.Sleep(100);
                        if (filter == FilterString)
                        {
                            GlobalEventHelper.OnFilterChanged?.Invoke(FilterString);
                        }
                    });
                }
            }
        }

        public void SetFilterString(List<TagFilter>? tags, List<string>? keywords)
        {
            FilterString = TagAndKeywordEncodeHelper.EncodeKeyword(tags, keywords);
            TbFilterCaretIndex = FilterString?.Length ?? 0;
        }

        public void TbFilter_OnKeyUp(object sender, KeyEventArgs e)
        {
            // When press Esc, clear all of the search keywords, but keep selected tags;
            if (e.Key != Key.Escape || sender is TextBox textBox == false) return;
            var s = TagAndKeywordEncodeHelper.DecodeKeyword(FilterString);
            SetFilterString(s.Item1, null);
        }
        //public void TbFilter_PreviewKeyUpUp(object sender, KeyEventArgs e)
        //{
        //    e.Handled = true;
        //}
    }
}

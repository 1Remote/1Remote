using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using PRM.Model;
using PRM.Utils;

namespace Ui.View
{
    public class MainWindowSearchControlViewModel : NotifyPropertyChangedBaseScreen
    {
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

        public void SetFilterString(List<TagFilter> tags, List<string> keywords)
        {
            FilterString = TagAndKeywordEncodeHelper.EncodeKeyword(tags, keywords);
            RaisePropertyChanged("FilterString");
        }

        public void SetCaretIndexToEnd()
        {
            // 区分关键词的来源，若是从后端设置关键词（例如点击 tag 按钮，自动填充关键词到搜索栏），则需要把搜索框的 CaretIndex 设置到末尾，以方便用户输入其他关键词。 
            // if the keyword is set from the backend, we need to set the CaretIndex of the search box to the end to facilitate the user to enter other keywords.
            TbFilterCaretIndex = FilterString?.Length ?? 0;
        }

        public void TbFilter_OnKeyUp(object sender, KeyEventArgs e)
        {
            // When press Esc, clear all of the search keywords, but keep selected tags;
            if (e.Key != Key.Escape || sender is TextBox textBox == false) return;
            var s = TagAndKeywordEncodeHelper.DecodeKeyword(FilterString);
            var newString = TagAndKeywordEncodeHelper.EncodeKeyword(s.Item1);
            FilterString = newString;
            GlobalEventHelper.OnFilterChanged?.Invoke(FilterString);
            // Kill logical focus
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(textBox), null);
            // Kill keyboard focus
            Keyboard.ClearFocus();
        }
    }
}

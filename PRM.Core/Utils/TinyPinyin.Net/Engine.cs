using System;
using System.Collections.Generic;
using System.Text;

namespace TinyPinyin
{
    public static class Engine
    {
        public static string[] PinyinFromDict(String wordInDict, List<IPinyinDict> pinyinDictSet)
        {
            if (pinyinDictSet != null)
            {
                foreach (IPinyinDict dict in pinyinDictSet)
                {
                    if (dict != null && dict.Words() != null && dict.Words().Contains(wordInDict))
                    {
                        return dict.ToPinyin(wordInDict);
                    }
                }
            }
            throw new ArgumentException("No pinyin dict contains word: " + wordInDict);
        }
        public static string ToPinyin(string inputStr, string trie, List<IPinyinDict> pinyinDictList, string separator)
        {
            if (inputStr == null || inputStr.Length == 0)
            {
                return inputStr;
            }


            if (trie == null)
            {
                // 没有提供字典或选择器，按单字符转换输出
                var builder1 = new StringBuilder();
                for (int i = 0; i < inputStr.Length; i++)
                {
                    builder1.Append(PinyinHelper.GetPinyin(inputStr[i]));
                    if (i != inputStr.Length - 1)
                    {
                        builder1.Append(separator);
                    }
                }
                return builder1.ToString();
            }
            return null;

            //List<Emit> selectedEmits = selector.select(trie.parseText(inputStr));

            //Collections.sort(selectedEmits, EMIT_COMPARATOR);

            //StringBuffer resultPinyinStrBuf = new StringBuffer();

            //int nextHitIndex = 0;

            //for (int i = 0; i < inputStr.length();)
            //{
            //    // 首先确认是否有以第i个字符作为begin的hit
            //    if (nextHitIndex < selectedEmits.size() && i == selectedEmits.get(nextHitIndex).getStart())
            //    {
            //        // 有以第i个字符作为begin的hit
            //        String[] fromDicts = pinyinFromDict(selectedEmits.get(nextHitIndex).getKeyword(), pinyinDictList);
            //        for (int j = 0; j < fromDicts.length; j++)
            //        {
            //            resultPinyinStrBuf.append(fromDicts[j].toUpperCase());
            //            if (j != fromDicts.length - 1)
            //            {
            //                resultPinyinStrBuf.append(separator);
            //            }
            //        }

            //        i = i + selectedEmits.get(nextHitIndex).size();
            //        nextHitIndex++;
            //    }
            //    else
            //    {
            //        // 将第i个字符转为拼音
            //        resultPinyinStrBuf.append(Pinyin.toPinyin(inputStr.charAt(i)));
            //        i++;
            //    }

            //    if (i != inputStr.length())
            //    {
            //        resultPinyinStrBuf.append(separator);
            //    }
            //}

            //return resultPinyinStrBuf.toString();
        }
    }
}

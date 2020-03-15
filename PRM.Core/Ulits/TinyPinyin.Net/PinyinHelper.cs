using System.Collections.Generic;
using System.Linq;
using TinyPinyin.Data;

namespace TinyPinyin
{
    public static class PinyinHelper
    {
        /// <summary>
        /// 判断给定字符是否是中文
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static bool IsChinese(char c)
        {
            return (PinyinData.MIN_VALUE <= c && c <= PinyinData.MAX_VALUE && GetPinyinCode(c) > 0) || PinyinData.CHAR_12295 == c;
        }

        /// <summary>
        /// 获取单个字符的拼音
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static string GetPinyin(char c)
        {
            if (IsChinese(c))
            {
                if (c == PinyinData.CHAR_12295)
                {
                    return PinyinData.PINYIN_12295;
                }
                else
                {
                    return PinyinData.PINYIN_TABLE[GetPinyinCode(c)];
                }
            }
            else
            {
                return c.ToString();
            }
        }

        /// <summary>
        /// 获取文本的拼音
        /// </summary>
        /// <param name="str">要获取拼音的文本</param>
        /// <param name="separator">单个拼音分隔符</param>
        /// <returns></returns>
        public static string GetPinyin(string str, string separator = " ")
        {
            return Engine.ToPinyin(str, null, null, separator);
        }

        /// <summary>
        /// 获取拼音手字母
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string GetPinyinInitials(string str)
        {
            var result = GetPinyin(str, "|");
            return string.Join("", result.Split('|').Select(x => x.Substring(0, 1)).ToArray());
        }

        private static int GetPinyinCode(char c)
        {
            int offset = c - PinyinData.MIN_VALUE;
            if (0 <= offset && offset < PinyinData.PINYIN_CODE_1_OFFSET)
            {
                return decodeIndex(PinyinCode1.PINYIN_CODE_PADDING, PinyinCode1.PINYIN_CODE, offset);
            }
            else if (PinyinData.PINYIN_CODE_1_OFFSET <= offset
                  && offset < PinyinData.PINYIN_CODE_2_OFFSET)
            {
                return decodeIndex(PinyinCode2.PINYIN_CODE_PADDING, PinyinCode2.PINYIN_CODE,
                        offset - PinyinData.PINYIN_CODE_1_OFFSET);
            }
            else
            {
                return decodeIndex(PinyinCode3.PINYIN_CODE_PADDING, PinyinCode3.PINYIN_CODE,
                        offset - PinyinData.PINYIN_CODE_2_OFFSET);
            }
        }

        private static short decodeIndex(byte[] paddings, byte[] indexes, int offset)
        {
            //CHECKSTYLE:OFF
            int index1 = offset / 8;
            int index2 = offset % 8;
            short realIndex;
            realIndex = (short)(indexes[offset] & 0xff);
            //CHECKSTYLE:ON
            if ((paddings[index1] & PinyinData.BIT_MASKS[index2]) != 0)
            {
                realIndex = (short)(realIndex | PinyinData.PADDING_MASK);
            }
            return realIndex;
        }
    }
}

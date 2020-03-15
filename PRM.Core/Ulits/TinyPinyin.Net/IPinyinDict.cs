using System.Collections.Generic;

namespace TinyPinyin
{
    public interface IPinyinDict
    {
        /// <summary>
        /// 转换文本为到拼音
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        string[] ToPinyin(string word);

        List<string> Words();
    }
}

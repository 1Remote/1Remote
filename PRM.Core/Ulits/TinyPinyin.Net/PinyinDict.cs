using System.Collections.Generic;
using System.Linq;

namespace TinyPinyin
{
    public abstract class PinyinDict : IPinyinDict
    {
        public abstract Dictionary<string, string[]> Mapping();

        public string[] ToPinyin(string word)
        {
            var mappingResult = Mapping();
            if (mappingResult == null)
            {
                return null;
            }
            if (mappingResult.TryGetValue(word, out var result))
            {
                return result;
            }
            return null;
        }

        public List<string> Words()
        {
            var mappingResult = Mapping();
            if (mappingResult == null)
            {
                return null;
            }
            return mappingResult.Keys.Select(key => key).ToList();
        }
    }
}

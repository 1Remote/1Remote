using System;

namespace PRM.Model
{
    public class TagFilter
    {
        public enum FilterTagsControlAction
        {
            AppendIncludedFilter,
            AppendExcludedFilter,
            Remove,
            Set,
        }
        public enum FilterType
        {
            Included,
            Excluded,
        }
        protected TagFilter(string tagName, FilterType type)
        {
            TagName = tagName;
            IsExcluded = type == FilterType.Excluded;
        }

        public static TagFilter Create(string tagName, FilterType type)
        {
            return new TagFilter(tagName, type);
        }

        public string TagName { get; }
        public bool IsExcluded { get; }
        public bool IsIncluded => !IsExcluded;

        public override string ToString()
        {
            throw new NotSupportedException();
        }
    }
}

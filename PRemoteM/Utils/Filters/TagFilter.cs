using PRM.ViewModel;

namespace PRM.Utils.Filters
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
        public TagFilter(string tagName, FilterType type)
        {
            TagName = tagName;
            IsExcluded = type == FilterType.Excluded;
        }

        public string TagName { get; }
        public bool IsExcluded { get; }
        public bool IsIncluded => !IsExcluded;

        public override string ToString()
        {
            return TagName + (IsExcluded ? VmServerListPage.TagTypeSeparator + "Negative" : "");
        }
    }
}

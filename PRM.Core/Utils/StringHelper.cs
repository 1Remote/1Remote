using System;
using System.Text;

namespace Shawn.Utils
{
    public static class StringHelper
    {
        public static string ReplaceFirst(this string value, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(oldValue))
                return value;

            int idx = value.IndexOf(oldValue, StringComparison.Ordinal);
            if (idx == -1)
                return value;
            value = value.Remove(idx, oldValue.Length);
            return value.Insert(idx, newValue);
        }

        public static string ReplaceLast(this string value, string oldValue, string newValue)
        {
            int index = value.LastIndexOf(oldValue, StringComparison.Ordinal);
            if (index < 0)
            {
                return value;
            }
            else
            {
                var sb = new StringBuilder(value.Length - oldValue.Length + newValue.Length);
                sb.Append(value.Substring(0, index));
                sb.Append(newValue);
                sb.Append(value.Substring(index + oldValue.Length,
                    value.Length - index - oldValue.Length));

                return sb.ToString();
            }
        }

        public static string ReplaceStartWith(this string value, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(oldValue))
                return value;

            if (!value.StartsWith(oldValue))
                return value;

            int idx = value.IndexOf(oldValue, StringComparison.Ordinal);
            if (idx == -1)
                return value;
            value = value.Remove(idx, oldValue.Length);
            return value.Insert(idx, newValue);
        }

        public static string RemoveFirst(this string value, string find)
        {
            if (string.IsNullOrEmpty(find))
                return value;

            if (!value.StartsWith(find))
                return value;

            int idx = value.IndexOf(find, StringComparison.Ordinal);
            if (idx == -1)
                return value;
            value = value.Remove(idx, find.Length);
            return value;
        }
    }
}
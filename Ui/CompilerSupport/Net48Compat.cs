#if NETFRAMEWORK
// Polyfills for modern BCL APIs that do not exist on .NET Framework 4.8.
// Declared as extension methods in their official namespaces (matching the
// netstandard2.1 implementations) so existing `using` directives resolve them.
#nullable disable

namespace System.Collections.Generic
{
    internal static class Net48DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            return dictionary.TryGetValue(key, out var value) ? value : default;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        {
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary.ContainsKey(key))
                return false;
            dictionary.Add(key, value);
            return true;
        }
    }
}

namespace System.IO
{
    internal static class Net48StreamExtensions
    {
        public static void ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
        {
            var read = 0;
            while (read < count)
            {
                var n = stream.Read(buffer, offset + read, count - read);
                if (n <= 0)
                    throw new EndOfStreamException();
                read += n;
            }
        }
    }
}

namespace System
{
    internal static class Net48StringExtensions
    {
        public static string[] Split(this string source, string separator, StringSplitOptions options)
        {
            return source.Split(new[] { separator }, options);
        }
    }
}
#endif

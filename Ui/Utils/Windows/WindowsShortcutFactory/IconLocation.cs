using System;

namespace _1RM.Utils.Windows.WindowsShortcutFactory
{
    /// <summary>
    /// Contains the path of an icon and optionally an index.
    /// </summary>
    public readonly struct IconLocation : IEquatable<IconLocation>
    {
        /// <summary>
        /// The value that represents no icon.
        /// </summary>
        public static readonly IconLocation None = default;

        /// <summary>
        /// Initializes a new instance of the <see cref="IconLocation"/> struct.
        /// </summary>
        /// <param name="path">Path of the icon file or resource file.</param>
        /// <param name="index">Index of the icon if a resource file is specified.</param>
        public IconLocation(string path, int index = 0)
        {
            Path = path;
            Index = index;
        }

        public static implicit operator IconLocation(string path) => string.IsNullOrEmpty(path) ? default : new IconLocation(path);
        public static bool operator ==(IconLocation a, IconLocation b) => a.Equals(b);
        public static bool operator !=(IconLocation a, IconLocation b) => !a.Equals(b);

        /// <summary>
        /// Gets the path of the icon file or resource file.
        /// </summary>
        public string Path { get; }
        /// <summary>
        /// Gets the index of the icon if <see cref="Path"/> refers to a resource file.
        /// </summary>
        public int Index { get; }
        /// <summary>
        /// Gets a value indicating whether a path is specified.
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(Path);

        public override string ToString()
        {
            if (!IsValid)
                return "None";

            if (Index <= 0)
                return Path;

            return $"{Path};{Index}";
        }
        public bool Equals(IconLocation other)
        {
            if (IsValid && other.IsValid)
                return Path == other.Path && Index == other.Index;
            else
                return IsValid == other.IsValid;
        }
        public override bool Equals(object? obj) => obj is IconLocation i && Equals(i);
        public override int GetHashCode() => Path?.GetHashCode() ?? 0;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Shawn.Utils
{
    public class Ini
    {
        private readonly Dictionary<string, Dictionary<string, string>> _ini =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.InvariantCultureIgnoreCase);

        private readonly string _file;

        /// <summary>
        /// Initialize an INI file
        /// Load it if it exists
        /// </summary>
        /// <param name="file">Full path where the INI file has to be read from or written to</param>
        public Ini(string file)
        {
            this._file = file;

            if (!File.Exists(file))
                return;

            Load();
        }

        /// <summary>
        /// Load the INI file content
        /// </summary>
        public void Load()
        {
            var txt = File.ReadAllText(_file);

            var currentSection = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            _ini[""] = currentSection;

            foreach (var l in txt.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select((t, i) => new
                    {
                        idx = i,
                        text = t.Trim()
                    }))
            // .Where(t => !string.IsNullOrWhiteSpace(t) && !t.StartsWith(";")))
            {
                var line = l.text;

                if (line.StartsWith(";") || string.IsNullOrWhiteSpace(line))
                {
                    currentSection.Add(";" + l.idx.ToString(), line);
                    continue;
                }

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    _ini[line.Substring(1, line.Length - 2)] = currentSection;
                    continue;
                }

                var idx = line.IndexOf("=");
                if (idx == -1)
                    currentSection[line] = "";
                else
                    currentSection[line.Substring(0, idx)] = line.Substring(idx + 1);
            }
        }

        /// <summary>
        /// Get a parameter value at the root level
        /// </summary>
        /// <param name="key">parameter key</param>
        /// <returns></returns>
        public string GetValue(string key)
        {
            return GetValue(key, "", "");
        }

        /// <summary>
        /// Get a parameter value in the section
        /// </summary>
        /// <param name="key">parameter key</param>
        /// <param name="section">section</param>
        /// <returns></returns>
        public string GetValue(string key, string section)
        {
            return GetValue(key, section, "");
        }

        /// <summary>
        /// Returns a parameter value in the section, with a default value if not found
        /// </summary>
        /// <param name="key">parameter key</param>
        /// <param name="section">section</param>
        /// <param name="default">default value</param>
        /// <returns></returns>
        public string GetValue(string key, string section, string @default)
        {
            return ContainsKey(key, section) ? _ini[section][key] : @default;
        }

        /// <summary>
        /// Returns a parameter value in the section, with a default value if not found
        /// </summary>
        /// <param name="key">parameter key</param>
        /// <param name="section">section</param>
        /// <param name="default">default value</param>
        /// <returns></returns>
        public T GetValue<T>(string key, string section, T @default)
        {
            if (!ContainsKey(key, section))
                return @default;
            try
            {
                var r = Convert.ChangeType(_ini[section][key], typeof(T));
                return (T)r;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug(e);
                return @default;
            }
        }

        public bool ContainsKey(string key, string section)
        {
            return _ini.ContainsKey(section) && _ini[section].ContainsKey(key);
        }

        /// <summary>
        /// Save the INI file
        /// </summary>
        public void Save()
        {
            var sb = new StringBuilder();
            foreach (var section in _ini)
            {
                if (section.Key != "")
                {
                    sb.AppendFormat("[{0}]", section.Key);
                    sb.AppendLine();
                }

                foreach (var keyValue in section.Value)
                {
                    if (keyValue.Key.StartsWith(";"))
                    {
                        sb.Append(keyValue.Value);
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendFormat("{0}={1}", keyValue.Key, keyValue.Value);
                        sb.AppendLine();
                    }
                }

                if (!endWithCRLF(sb))
                    sb.AppendLine();
            }

            File.WriteAllText(_file, sb.ToString());
        }

        private bool endWithCRLF(StringBuilder sb)
        {
            if (sb.Length < 2)
                return false;
            else if (sb.Length < 4)
                return sb[sb.Length - 2] == '\r' &&
                       sb[sb.Length - 1] == '\n';
            else
                return sb[sb.Length - 4] == '\r' &&
                       sb[sb.Length - 3] == '\n' &&
                       sb[sb.Length - 2] == '\r' &&
                       sb[sb.Length - 1] == '\n';
        }

        /// <summary>
        /// Write a parameter value at the root level
        /// </summary>
        /// <param name="key">parameter key</param>
        /// <param name="value">parameter value</param>
        public void WriteValue(string key, string value)
        {
            WriteValue(key, "", value);
        }

        /// <summary>
        /// Write a parameter value in a section
        /// </summary>
        /// <param name="key">parameter key</param>
        /// <param name="section">section</param>
        /// <param name="value">parameter value</param>
        public void WriteValue(string key, string section, string value)
        {
            Dictionary<string, string> currentSection;
            if (!_ini.ContainsKey(section))
            {
                currentSection = new Dictionary<string, string>();
                _ini.Add(section, currentSection);
            }
            else
                currentSection = _ini[section];
            currentSection[key] = value;
        }

        /// <summary>
        /// Get all the keys names in a section
        /// </summary>
        /// <param name="section">section</param>
        /// <returns></returns>
        public string[] GetKeys(string section)
        {
            if (!_ini.ContainsKey(section))
                return new string[0];

            return _ini[section].Keys.ToArray();
        }

        /// <summary>
        /// Get all the section names of the INI file
        /// </summary>
        /// <returns></returns>
        public string[] GetSections()
        {
            return _ini.Keys.Where(t => t != "").ToArray();
        }
    }
}
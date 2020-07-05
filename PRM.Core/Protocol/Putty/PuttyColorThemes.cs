using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PRM.Core.Protocol.Putty
{
    public static class PuttyColorThemes
    {
        public const string ThemeRegFileFolder = "PuttyThemes";

        public static List<PuttyRegOptionItem> ColorThemesFromRegFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null;
            var content = File.ReadAllText(filePath);
            var regex = new Regex(@"\""(.*?)\"".*?=.*?\""(.*?)\""", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var matchCollection = regex.Matches(content);

            var options = new List<PuttyRegOptionItem>();
            foreach (var m in matchCollection)
            {
                var key = Regex.Match(m.ToString(), @"Colour\d+");
                var value = Regex.Match(m.ToString(), @"\""\d{1,3}.*?,\d{1,3}.*?,\d{1,3}.*?\""");
                if (Enum.TryParse(key.ToString(), out PuttyRegOptionKey ek))
                {
                    options.Add(PuttyRegOptionItem.Create(ek, value.ToString().Replace(@"""", "").ToString()));
                }
            }
            if (options.Count > 0)
                return options;
            return null;
        }


        public static FileInfo ToRegFile(this List<PuttyRegOptionItem> options, string filePath)
        {
            if (options.Count == 0)
                return null;
            if (File.Exists(filePath))
                File.Delete(filePath);
            var sb = new StringBuilder(@"Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\Software\SimonTatham\PuTTY\Sessions\Default%20Settings]");
            foreach (var option in options)
            {
                sb.AppendLine($@"""{option.Key}""=""{option.Value}""");
            }

            File.WriteAllText(filePath, sb.ToString());
            if (File.Exists(filePath))
                return new FileInfo(filePath);
            return null;
        }


        public static Dictionary<string, List<PuttyRegOptionItem>> GetThemes()
        {
            var themes = new Dictionary<string, List<PuttyRegOptionItem>>();
            // default
            {
                var theme = Get00__Default();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            var tmp = new Dictionary<string, List<PuttyRegOptionItem>>();
            // customize theme
            var di = new DirectoryInfo(ThemeRegFileFolder);
            if (di.Exists)
            {
                var regs = di.GetFiles("*.reg");
                foreach (var reg in regs)
                {
                    var t = ColorThemesFromRegFile(reg.FullName);
                    if (t != null
                    && !tmp.ContainsKey(reg.Name.Replace(reg.Extension, "")))
                    {
                        tmp.Add(reg.Name.Replace(reg.Extension, ""), t);
                    }
                }
            }
            // existed theme
            AddStaticThemes(tmp);

            // Order
            var ordered = tmp.OrderBy(x => x.Key);
            foreach (var kv in ordered)
            {
                if (!themes.ContainsKey(kv.Key))
                    themes.Add(kv.Key, kv.Value);
                else
                    themes[kv.Key] = kv.Value;
            }
            return themes;
        }

        /*
        public static void GenStaticThemeCode()
        {
            var themes = new Dictionary<string, List<PuttyRegOptionItem>>();
            var di = new DirectoryInfo("PuttyThemes");
            if (di.Exists)
            {
                var regs = di.GetFiles("*.reg");
                foreach (var reg in regs)
                {
                    var t = ColorThemesFromRegFile(reg.FullName);
                    if (t != null)
                    {
                        themes.Add(reg.Name.Replace(reg.Extension, ""), t);
                    }
                }
            }

            var sb1 = new StringBuilder(@"
");
            
            var sb2 = new StringBuilder(@"

        private static void AddStaticThemes(Dictionary<string, List<PuttyRegOptionItem>> themes)
        {
");
            var invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            foreach (var theme in themes)
            {
                var funcName = "Get" + theme.Key.Replace(".", "_").Replace(" ", "_");
                foreach (var c in invalidChars)
                {
                    funcName = funcName.Replace(c.ToString(), "_");
                }
                var func = @"
        private static Tuple<string, List<PuttyRegOptionItem>> FUNC_NAME()
        {
            var options = new List<PuttyRegOptionItem>();
FUNC_CONTENT
        }
";
                var sbContent = new StringBuilder();
                foreach (var option in theme.Value)
                {
                    sbContent.AppendLine($@"            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.{option.Key}, ""{option.Value}""));");
                }
                sbContent.Append($@"            return new Tuple<string, List<PuttyRegOptionItem>>(""{theme.Key}"", options);");

                func = func.Replace("FUNC_NAME", funcName).Replace("FUNC_CONTENT", sbContent.ToString());
                sb1.AppendLine(func);
                sb2.AppendLine("            {");
                sb2.AppendLine($@"                var theme = {funcName}();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);");
                sb2.AppendLine("            }");
            }

            sb2.AppendLine("        }");
            File.WriteAllText("putty theme code.cs",$@"

        #region Static Themes

{sb2}

{sb1}

        #endregion

");
        }
        */

        #region Static Themes



        private static void AddStaticThemes(Dictionary<string, List<PuttyRegOptionItem>> themes)
        {
            {
                var theme = Get01__Apple_Terminal();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get02__Argonaut();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get03__Birds_Of_Paradise();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get04__Blazer();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get05__Chalkboard();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get06__Ciapre();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get07__Dark_Pastel();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get08__Desert();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get09__Espresso();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get10__Fish_Of_Paradise();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get11__Fish_Tank();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get12__github();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get13__Grass();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get14__Highway();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get15__Homebrew();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get16__Hurtado();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get17__Ic_Green_Ppl();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get18__Idletoes();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get19__Igvita_Desert();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get20__Igvita_Light();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get21__Invisibone();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get22__Kibble();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get23__Liquid_Carbon();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get24__Liquid_Carbon_Transparent();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get25__Liquid_Carbon_Transparent_Inverse();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get26__Man_Page();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get27__Monokai_Soda();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get28__Monokai_Dimmed();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get29__Monokai_Stevelosh();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get30__Neopolitan();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get31__Novel();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get32__Ocean();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get33__Papirus_Dark();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get34__Pro();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get35__Red_Sands();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get36__Seafoam_Pastel();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get37__Solarized_Dark();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get38__Solarized_Light();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get39__Solarized_Darcula();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get40__Sundried();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get41__Sympfonic();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get42__Teerb();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get43__Terminal_Basic();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get44__Thayer();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get45__Tomorrow();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get46__Tomorrow_Night();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get47__Twilight();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get48__Vaughn();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get49__X_Dotshare();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get50__Zenburn();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
            {
                var theme = Get51__Mariana();
                if (!themes.ContainsKey(theme.Item1))
                    themes.Add(theme.Item1, theme.Item2);
            }
        }




        public static Tuple<string, List<PuttyRegOptionItem>> Get00__Default()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "187,187,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "85,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "0,255,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "85,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "187,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "255,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "0,187,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "85,255,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "187,187,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "255,255,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,0,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "85,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "187,0,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "255,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "0,187,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "85,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "187,187,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("Default", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get01__Apple_Terminal()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "230,230,230"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "187,187,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "100,102,102"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "187,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "255,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "0,187,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "100,210,56"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "187,187,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "255,255,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,0,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "52,66,241"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "187,0,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "255,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "0,187,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "85,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "224,224,224"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("01. Apple Terminal", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get02__Argonaut()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "255,250,244"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "158,156,154"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "14,16,25"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "14,16,25"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,0,24"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "35,35,35"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "68,68,68"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "255,0,15"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "255,39,64"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "140,225,11"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "171,225,91"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "255,185,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "255,210,66"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,141,248"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "0,146,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "109,67,166"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "154,95,235"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "0,216,235"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "103,255,240"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("02. Argonaut", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get03__Birds_Of_Paradise()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "224,219,183"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,248,216"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "42,31,29"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "42,31,29"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "87,61,38"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "87,61,38"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "87,61,38"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "155,108,74"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "190,45,38"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "232,70,39"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "107,161,138"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "149,216,186"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "233,157,42"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "208,209,80"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "90,134,173"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "184,211,237"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "172,128,166"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "209,158,203"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "116,166,173"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "147,207,215"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "224,219,183"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,249,213"));
            return new Tuple<string, List<PuttyRegOptionItem>>("03. Birds Of Paradise", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get04__Blazer()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "217,230,242"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "217,230,242"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "13,25,38"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "13,25,38"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "217,230,242"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "217,230,242"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "38,38,38"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "184,122,122"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "219,189,189"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "122,184,122"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "189,219,189"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "184,184,122"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "219,219,189"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "122,122,184"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "189,189,219"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "184,122,184"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "219,189,219"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "122,184,184"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "189,219,219"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "217,217,217"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("04. Blazer", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get05__Chalkboard()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "217,230,242"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "217,111,95"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "41,38,47"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "41,38,47"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "217,230,242"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "217,230,242"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "50,50,50"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "195,115,114"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "219,170,170"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "114,195,115"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "170,219,170"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "194,195,114"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "218,219,170"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "115,114,195"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "170,170,219"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "195,114,194"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "219,170,218"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "114,194,195"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "170,218,219"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "217,217,217"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("05. Chalkboard", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get06__Ciapre()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "174,164,122"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "244,244,244"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "25,28,39"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "25,28,39"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "146,128,91"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "174,164,122"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "24,24,24"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "85,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "129,0,9"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "172,56,53"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "72,81,59"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "166,167,93"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "204,139,63"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "220,223,124"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "87,109,140"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "48,151,198"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "114,77,124"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "211,48,97"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "92,79,75"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "243,219,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "174,164,127"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "244,244,244"));
            return new Tuple<string, List<PuttyRegOptionItem>>("06. Ciapre", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get07__Dark_Pastel()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,94,125"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "187,187,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "85,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "255,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "255,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "85,255,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "85,255,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "255,255,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "255,255,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "85,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "85,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "255,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "255,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "85,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "85,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "187,187,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("07. Dark Pastel", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get08__Desert()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,215,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "51,51,51"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "51,51,51"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "0,255,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "77,77,77"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "85,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "255,43,43"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "255,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "152,251,152"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "85,255,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "240,230,140"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "255,255,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "205,133,63"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "135,206,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "255,222,173"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "255,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "255,160,160"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "255,215,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "245,222,179"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("08. Desert", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get09__Espresso()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "50,50,50"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "50,50,50"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "214,214,214"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "53,53,53"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "83,83,83"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "210,82,82"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "240,12,12"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "165,194,97"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "194,224,117"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "255,198,109"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "225,228,139"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "108,153,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "138,183,217"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "209,151,217"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "239,181,247"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "190,214,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "220,244,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "238,238,236"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("09. Espresso", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get10__Fish_Of_Paradise()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "224,219,183"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,248,216"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "35,35,44"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "35,35,44"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "87,61,38"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "87,61,38"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "87,61,38"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "155,108,74"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "190,45,38"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "232,70,39"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "107,161,138"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "149,216,186"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "233,157,42"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "208,209,80"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "90,134,173"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "184,211,237"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "172,128,166"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "209,158,203"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "116,166,173"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "147,207,215"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "224,219,183"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,249,213"));
            return new Tuple<string, List<PuttyRegOptionItem>>("10. Fish Of Paradise", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get11__Fish_Tank()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "236,240,254"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "246,255,235"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "35,37,55"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "35,37,55"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "254,205,94"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "236,240,254"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "3,7,60"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "108,91,48"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "198,0,74"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "218,75,138"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "172,241,87"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "219,255,169"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "254,205,94"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "254,230,169"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "82,95,184"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "178,190,250"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "152,111,130"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "253,165,205"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "150,135,99"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "165,189,134"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "236,240,252"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "246,255,236"));
            return new Tuple<string, List<PuttyRegOptionItem>>("11. Fish Tank", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get12__github()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "62,62,62"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "201,85,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "244,244,244"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "244,244,244"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "63,63,63"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "62,62,62"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "62,62,62"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "102,102,102"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "151,11,22"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "222,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "7,150,42"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "135,213,162"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "248,238,199"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "241,208,7"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,62,138"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "46,108,186"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "233,70,145"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "255,162,159"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "137,209,236"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "28,250,254"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("12. github", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get13__Grass()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "255,240,165"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,176,59"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "19,119,61"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "19,119,61"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "140,40,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "85,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "187,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "187,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "0,187,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "0,187,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "231,176,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "231,176,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,0,163"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "0,0,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "149,0,98"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "255,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "0,187,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "85,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "187,187,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("13. Grass", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get14__Highway()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "237,237,237"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,248,216"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "34,34,37"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "34,34,37"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "34,34,37"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "237,237,237"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "93,80,74"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "208,14,24"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "240,126,24"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "19,128,52"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "177,209,48"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "255,203,62"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "255,241,32"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,107,179"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "79,194,253"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "107,39,117"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "222,0,113"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "56,69,100"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "93,80,74"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "237,237,237"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("14. Highway", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get15__Homebrew()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "0,255,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "0,255,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "35,255,24"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,0,24"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "102,102,102"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "153,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "229,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "0,166,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "0,217,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "153,153,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "229,229,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,0,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "0,0,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "178,0,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "229,0,229"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "0,166,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "0,229,229"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "191,191,191"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "229,229,229"));
            return new Tuple<string, List<PuttyRegOptionItem>>("15. Homebrew", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get16__Hurtado()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "219,219,219"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "187,187,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "87,87,87"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "38,38,38"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "255,27,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "213,29,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "165,224,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "165,223,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "251,231,74"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "251,232,74"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "73,100,135"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "137,190,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "253,95,241"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "192,1,193"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "134,233,254"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "134,234,254"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "203,204,203"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "219,219,219"));
            return new Tuple<string, List<PuttyRegOptionItem>>("16. Hurtado", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get17__Ic_Green_Ppl()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "217,239,211"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "159,255,109"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "58,61,63"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "58,61,63"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "66,255,88"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "217,239,211"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "31,31,31"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "3,39,16"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "251,0,42"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "167,255,63"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "51,156,36"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "159,255,109"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "101,155,37"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "210,255,109"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "20,155,69"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "114,255,181"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "83,184,44"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "80,255,62"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "44,184,104"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "34,255,113"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "224,255,239"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "218,239,208"));
            return new Tuple<string, List<PuttyRegOptionItem>>("17. Ic Green Ppl", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get18__Idletoes()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,169"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "50,50,50"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "50,50,50"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "214,214,214"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "50,50,50"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "83,83,83"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "210,82,82"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "240,112,112"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "127,225,115"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "157,255,145"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "255,198,109"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "255,228,139"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "64,153,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "94,183,247"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "246,128,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "255,157,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "190,214,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "220,244,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "238,238,236"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("18. Idletoes", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get19__Igvita_Desert()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "51,51,51"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "85,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "0,255,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "77,77,77"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "85,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "255,43,43"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "255,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "152,251,152"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "85,255,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "240,230,140"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "255,255,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "205,133,63"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "135,206,235"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "255,222,173"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "255,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "255,160,160"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "255,215,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "245,222,179"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("19. Igvita Desert", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get20__Igvita_Light()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "187,187,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "85,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "0,255,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "85,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "187,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "255,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "152,251,152"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "85,255,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "187,187,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "255,255,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "3,92,190"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "85,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "187,0,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "255,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "255,160,160"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "85,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "187,187,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("20. Igvita Light", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get21__Invisibone()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "160,160,160"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "207,207,207"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "35,35,35"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "35,35,35"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "160,160,160"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "160,160,160"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "48,48,48"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "104,104,104"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "211,112,163"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "255,167,218"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "109,158,63"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "163,213,114"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "181,136,88"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "239,189,139"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "96,149,197"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "152,203,254"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "172,123,222"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "229,176,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "59,162,117"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "117,218,169"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "207,207,207"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("21. Invisibone", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get22__Kibble()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "247,247,247"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "202,99,30"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "14,16,10"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "14,16,10"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "159,218,156"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "247,247,247"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "77,77,77"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "90,90,90"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "199,0,49"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "240,21,120"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "41,207,19"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "108,224,92"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "216,227,14"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "243,247,158"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "52,73,209"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "151,164,247"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "132,0,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "196,149,240"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "7,152,171"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "104,242,224"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "226,209,227"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("22. Kibble", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get23__Liquid_Carbon()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "175,194,194"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "48,48,48"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "48,48,48"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "48,48,48"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "175,194,194"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "255,48,48"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "255,48,48"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "85,154,112"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "85,154,112"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "204,172,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "204,172,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,153,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "0,153,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "204,105,200"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "204,105,200"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "122,196,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "122,196,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "188,204,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "188,204,204"));
            return new Tuple<string, List<PuttyRegOptionItem>>("23. Liquid Carbon", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get24__Liquid_Carbon_Transparent()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "175,194,194"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "175,194,194"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "255,48,48"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "255,48,48"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "85,154,112"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "85,154,112"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "204,172,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "204,172,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,153,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "0,153,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "204,105,200"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "204,105,200"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "122,196,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "122,196,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "188,204,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "188,204,204"));
            return new Tuple<string, List<PuttyRegOptionItem>>("24. Liquid Carbon Transparent", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get25__Liquid_Carbon_Transparent_Inverse()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "175,194,194"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "175,194,194"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "188,204,205"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "255,48,48"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "255,48,48"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "85,154,112"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "85,154,112"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "204,172,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "204,172,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,153,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "0,153,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "204,105,200"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "204,105,200"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "122,196,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "122,196,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "0,0,0"));
            return new Tuple<string, List<PuttyRegOptionItem>>("25. Liquid Carbon Transparent Inverse", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get26__Man_Page()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "254,244,156"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "254,244,156"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "127,127,127"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "102,102,102"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "204,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "229,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "0,166,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "0,217,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "153,153,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "229,229,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,0,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "0,0,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "178,0,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "229,0,229"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "0,166,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "0,229,229"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "204,204,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "229,229,229"));
            return new Tuple<string, List<PuttyRegOptionItem>>("26. Man Page", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get27__Monokai_Soda()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "196,197,181"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "196,197,181"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "26,26,26"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "26,26,26"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "246,247,236"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "196,197,181"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "26,26,26"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "98,94,76"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "244,0,95"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "244,0,95"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "152,224,36"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "152,224,36"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "250,132,25"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "224,213,97"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "157,101,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "157,101,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "244,0,95"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "244,0,95"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "88,209,235"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "88,209,235"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "196,197,181"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "246,246,239"));
            return new Tuple<string, List<PuttyRegOptionItem>>("27. Monokai Soda", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get28__Monokai_Dimmed()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "185,188,186"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "254,255,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "31,31,31"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "31,31,31"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "248,62,25"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "185,188,186"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "58,61,67"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "136,137,135"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "190,63,72"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "251,0,31"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "135,154,59"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "15,114,47"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "197,166,53"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "196,112,51"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "79,118,161"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "24,109,227"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "133,92,141"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "251,0,103"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "87,143,164"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "46,112,109"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "185,188,186"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "253,255,185"));
            return new Tuple<string, List<PuttyRegOptionItem>>("28. Monokai Dimmed", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get29__Monokai_Stevelosh()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "219,219,219"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "187,187,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "85,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "229,34,34"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "255,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "166,227,45"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "85,255,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "252,149,30"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "255,255,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "196,141,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "85,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "250,37,115"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "255,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "103,217,240"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "85,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "242,242,242"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("29. Monokai Stevelosh", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get30__Neopolitan()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "23,19,16"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "23,19,16"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "23,19,16"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "23,19,16"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "128,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "128,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "97,206,60"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "97,206,60"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "251,222,45"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "251,222,45"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "37,59,118"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "37,59,118"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "255,0,128"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "255,0,128"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "141,166,206"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "141,166,206"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "248,248,248"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "248,248,248"));
            return new Tuple<string, List<PuttyRegOptionItem>>("30. Neopolitan", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get31__Novel()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "59,35,34"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "142,42,25"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "223,219,195"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "223,219,195"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "115,99,90"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "128,128,128"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "204,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "204,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "0,150,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "0,150,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "208,107,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "208,107,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,0,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "0,0,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "204,0,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "204,0,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "0,135,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "0,135,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "204,204,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("31. Novel", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get32__Ocean()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "34,79,188"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "34,79,188"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "127,127,127"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "102,102,102"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "153,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "229,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "0,166,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "0,217,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "153,153,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "229,229,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,0,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "0,0,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "178,0,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "229,0,229"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "0,166,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "0,229,229"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "191,191,191"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "229,229,229"));
            return new Tuple<string, List<PuttyRegOptionItem>>("32. Ocean", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get33__Papirus_Dark()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "209,195,184"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "209,195,184"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "46,44,40"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "46,44,40"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "209,195,184"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "209,195,184"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "113,115,112"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "46,44,40"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "140,115,90"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "153,138,122"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "174,196,149"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "180,204,153"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "191,187,153"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "230,227,202"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "136,161,186"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "136,161,186"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "109,104,113"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "148,141,153"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "92,83,70"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "122,135,53"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "200,185,114"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "228,221,150"));
            return new Tuple<string, List<PuttyRegOptionItem>>("33. Papirus Dark", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get34__Pro()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "242,242,242"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "77,77,77"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "102,102,102"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "153,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "229,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "0,166,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "0,217,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "153,153,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "229,229,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "32,9,219"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "0,0,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "178,0,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "229,0,229"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "0,166,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "0,229,229"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "191,191,191"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "229,229,229"));
            return new Tuple<string, List<PuttyRegOptionItem>>("34. Pro", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get35__Red_Sands()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "215,201,167"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "223,189,34"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "122,37,30"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "122,37,30"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "85,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "255,63,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "187,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "0,187,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "0,187,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "231,176,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "231,176,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,114,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "0,114,174"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "187,0,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "255,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "0,187,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "85,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "187,187,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("35. Red Sands", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get36__Seafoam_Pastel()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "212,231,212"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "100,136,144"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "36,52,53"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "36,52,53"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "87,100,122"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "212,231,212"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "117,117,117"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "138,138,138"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "130,93,77"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "207,147,122"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "114,140,98"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "152,217,170"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "173,161,109"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "250,231,157"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "77,123,130"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "122,195,207"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "138,114,103"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "214,178,161"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "114,148,148"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "173,224,224"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "224,224,224"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "224,224,224"));
            return new Tuple<string, List<PuttyRegOptionItem>>("36. Seafoam Pastel", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get37__Solarized_Dark()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "131,148,150"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "147,161,161"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "0,43,54"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "7,54,66"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "0,43,54"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "238,232,213"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "7,54,66"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "0,43,54"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "220,50,47"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "203,75,22"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "133,153,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "88,110,117"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "181,137,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "101,123,131"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "38,139,210"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "131,148,150"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "211,54,130"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "108,113,196"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "42,161,152"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "147,161,161"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "238,232,213"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "253,246,227"));
            return new Tuple<string, List<PuttyRegOptionItem>>("37. Solarized Dark", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get38__Solarized_Light()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "101,123,131"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "88,110,117"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "253,246,227"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "238,232,213"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "238,232,213"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "101,123,131"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "7,54,66"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "0,43,54"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "220,50,47"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "203,75,22"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "133,153,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "88,110,117"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "181,137,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "101,123,131"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "38,139,210"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "131,148,150"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "211,54,130"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "108,113,196"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "42,161,152"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "147,161,161"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "238,232,213"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "253,246,227"));
            return new Tuple<string, List<PuttyRegOptionItem>>("38. Solarized Light", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get39__Solarized_Darcula()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "210,216,217"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "236,236,236"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "61,63,65"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "61,63,65"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "112,130,132"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "0,40,49"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "37,41,42"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "37,41,42"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "242,72,64"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "242,72,64"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "98,150,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "98,150,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "182,136,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "182,136,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "32,117,199"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "32,117,199"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "121,127,212"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "121,127,212"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "21,150,141"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "21,150,141"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "210,216,217"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "210,216,217"));
            return new Tuple<string, List<PuttyRegOptionItem>>("39. Solarized Darcula", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get40__Sundried()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "201,201,201"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "26,24,24"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "26,24,24"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "201,201,201"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "48,43,42"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "77,78,72"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "167,70,61"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "170,0,12"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "88,119,68"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "18,140,33"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "157,96,42"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "252,106,33"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "72,91,152"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "121,153,247"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "134,70,81"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "253,138,161"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "156,129,79"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "250,212,132"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "201,201,201"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("40. Sundried", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get41__Sympfonic()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,132,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "220,50,47"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "27,29,33"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "220,50,47"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "220,50,47"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "86,219,58"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "86,219,58"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "255,132,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "255,132,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,132,212"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "0,132,212"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "183,41,217"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "183,41,217"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "204,204,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "204,204,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("41. Sympfonic", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get42__Teerb()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "208,208,208"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "229,229,229"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "38,38,38"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "38,38,38"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "228,201,175"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "208,208,208"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "28,28,28"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "28,28,28"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "214,134,134"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "214,134,134"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "174,214,134"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "174,214,134"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "215,175,135"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "228,201,175"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "134,174,214"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "134,174,214"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "214,174,214"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "214,174,214"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "138,219,180"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "177,231,221"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "208,208,208"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "239,239,239"));
            return new Tuple<string, List<PuttyRegOptionItem>>("42. Teerb", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get43__Terminal_Basic()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "127,127,127"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "102,102,102"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "153,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "229,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "0,166,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "0,217,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "153,153,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "229,229,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,0,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "0,0,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "178,0,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "229,0,229"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "0,166,178"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "0,229,229"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "191,191,191"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "229,229,229"));
            return new Tuple<string, List<PuttyRegOptionItem>>("43. Terminal Basic", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get44__Thayer()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "160,160,160"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "27,29,30"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "27,29,30"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "252,151,31"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "27,29,30"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "80,83,84"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "249,38,114"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "255,89,149"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "130,180,20"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "182,227,84"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "253,151,31"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "254,237,108"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "86,194,214"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "140,237,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "140,84,254"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "158,111,254"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "64,128,128"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "85,170,170"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "204,204,198"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "248,248,242"));
            return new Tuple<string, List<PuttyRegOptionItem>>("44. Thayer", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get45__Tomorrow()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "77,77,76"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "234,234,234"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "250,250,250"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "214,214,214"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "77,77,76"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "142,144,140"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "255,51,52"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "200,40,41"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "158,196,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "113,140,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "234,183,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "245,135,31"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "87,149,230"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "66,113,174"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "183,119,224"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "137,89,168"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "84,206,214"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "62,153,159"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "239,239,239"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "214,214,214"));
            return new Tuple<string, List<PuttyRegOptionItem>>("45. Tomorrow", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get46__Tomorrow_Night()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "197,200,198"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "234,234,234"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "29,31,33"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "197,200,198"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "197,200,198"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "29,31,33"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "204,102,102"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "213,78,83"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "181,189,104"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "185,202,74"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "240,198,116"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "231,197,71"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "129,162,190"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "122,166,218"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "178,148,187"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "195,151,216"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "138,190,183"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "112,192,177"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "192,200,198"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "234,234,234"));
            return new Tuple<string, List<PuttyRegOptionItem>>("46. Tomorrow Night", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get47__Twilight()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "255,255,212"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,212"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "20,20,20"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "20,20,20"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,255,212"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "20,20,20"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "38,38,38"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "192,109,68"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "222,124,76"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "175,185,122"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "204,216,140"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "194,168,108"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "226,196,126"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "68,71,74"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "90,94,98"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "180,190,124"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "208,220,142"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "119,131,133"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "138,152,155"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "255,255,212"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,212"));
            return new Tuple<string, List<PuttyRegOptionItem>>("47. Twilight", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get48__Vaughn()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "220,220,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,94,125"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "37,35,79"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "37,35,79"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "255,85,85"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "37,35,79"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "112,144,128"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "112,80,80"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "220,163,163"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "96,180,138"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "96,180,138"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "223,175,143"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "240,223,175"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "85,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "85,85,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "240,140,195"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "236,147,211"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "140,208,211"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "147,224,227"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "112,144,128"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("48. Vaughn", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get49__X_Dotshare()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "215,208,199"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "21,21,21"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "21,21,21"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "255,137,57"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "215,208,199"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "16,16,16"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "64,64,64"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "232,79,79"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "210,61,61"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "184,214,140"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "160,207,93"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "225,170,93"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "243,157,33"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "125,193,207"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "78,159,177"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "155,100,251"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "133,66,255"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "109,135,141"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "66,113,123"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "221,221,221"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "221,221,221"));
            return new Tuple<string, List<PuttyRegOptionItem>>("49. X Dotshare", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get50__Zenburn()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "220,220,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "220,220,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "63,63,63"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "63,63,63"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "115,99,90"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "0,0,0"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "77,77,77"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "112,144,128"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "112,80,80"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "220,163,163"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "96,180,138"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "195,191,159"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "240,223,175"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "224,207,159"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "80,96,112"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "148,191,243"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "220,140,195"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "236,147,211"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "140,208,211"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "147,224,227"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "220,220,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            return new Tuple<string, List<PuttyRegOptionItem>>("50. Zenburn", options);
        }


        private static Tuple<string, List<PuttyRegOptionItem>> Get51__Mariana()
        {
            var options = new List<PuttyRegOptionItem>();
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "216,222,233"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "147,161,161"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "52,61,70"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "79,91,102"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "0,43,54"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "216,222,233"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "79,91,102"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "0,43,56"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "236,95,103"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "203,75,22"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "153,199,148"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "88,110,117"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "249,174,88"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "101,123,131"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "102,153,204"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "216,222,233"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "197,148,197"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "108,113,196"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "95,179,179"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "147,161,161"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "216,222,233"));
            options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "253,246,227"));
            return new Tuple<string, List<PuttyRegOptionItem>>("51. Mariana", options);
        }



        #endregion
    }
}

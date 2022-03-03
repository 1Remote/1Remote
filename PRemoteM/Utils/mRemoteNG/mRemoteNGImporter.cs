using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using PRM.Model.Protocol.Base;
using PRM.Model.Protocol.Putty;
using PRM.Model.Protocol.RDP;
using PRM.Model.Protocol.VNC;
using Shawn.Utils.Wpf.Image;

namespace PRM.Utils.mRemoteNG
{
    public static class MRemoteNgImporter
    {
        private const string NodeTypeContainer = "Container";
        private const string NodeTypeConnection = "Connection";

        #region MRemoteNgItem
        internal class MRemoteNgItem
        {
            public string Name = "",
                Id = "",
                Parent = "",
                NodeType = "",
                Description = "",
                Icon = "",
                Panel = "",
                Username = "",
                Password = "",
                Domain = "",
                Hostname = "",
                VmId = "",
                Protocol = "",
                PuttySession = "",
                Port = "",
                ConnectToConsole = "",
                UseCredSsp = "",
                UseVmId = "",
                RenderingEngine = "",
                ICAEncryptionStrength = "",
                RDPAuthenticationLevel = "",
                LoadBalanceInfo = "",
                Colors = "",
                Resolution = "",
                AutomaticResize = "",
                DisplayWallpaper = "",
                DisplayThemes = "",
                EnableFontSmoothing = "",
                EnableDesktopComposition = "",
                CacheBitmaps = "",
                RedirectDiskDrives = "",
                RedirectPorts = "",
                RedirectPrinters = "",
                RedirectClipboard = "",
                RedirectSmartCards = "",
                RedirectSound = "",
                RedirectKeys = "",
                PreExtApp = "",
                PostExtApp = "",
                MacAddress = "",
                UserField = "",
                ExtApp = "",
                Favorite = "",
                VNCCompression = "",
                VNCEncoding = "",
                VNCAuthMode = "",
                VNCProxyType = "",
                VNCProxyIP = "",
                VNCProxyPort = "",
                VNCProxyUsername = "",
                VNCProxyPassword = "",
                VNCColors = "",
                VNCSmartSizeMode = "",
                VNCViewOnly = "",
                RDGatewayUsageMethod = "",
                RDGatewayHostname = "",
                RDGatewayUseConnectionCredentials = "",
                RDGatewayUsername = "",
                RDGatewayPassword = "",
                RDGatewayDomain = "",
                RedirectAudioCapture = "",
                RdpVersion = "",
                InheritCacheBitmaps = "",
                InheritColors = "",
                InheritDescription = "",
                InheritDisplayThemes = "",
                InheritDisplayWallpaper = "",
                InheritEnableFontSmoothing = "",
                InheritEnableDesktopComposition = "",
                InheritDomain = "",
                InheritIcon = "",
                InheritPanel = "",
                InheritPassword = "",
                InheritPort = "",
                InheritProtocol = "",
                InheritPuttySession = "",
                InheritRedirectDiskDrives = "",
                InheritRedirectKeys = "",
                InheritRedirectPorts = "",
                InheritRedirectPrinters = "",
                InheritRedirectClipboard = "",
                InheritRedirectSmartCards = "",
                InheritRedirectSound = "",
                InheritResolution = "",
                InheritAutomaticResize = "",
                InheritUseConsoleSession = "",
                InheritUseCredSsp = "",
                InheritUseVmId = "",
                InheritVmId = "",
                InheritRenderingEngine = "",
                InheritUsername = "",
                InheritICAEncryptionStrength = "",
                InheritRDPAuthenticationLevel = "",
                InheritLoadBalanceInfo = "",
                InheritPreExtApp = "",
                InheritPostExtApp = "",
                InheritMacAddress = "",
                InheritUserField = "",
                InheritFavorite = "",
                InheritExtApp = "",
                InheritVNCCompression = "",
                InheritVNCEncoding = "",
                InheritVNCAuthMode = "",
                InheritVNCProxyType = "",
                InheritVNCProxyIP = "",
                InheritVNCProxyPort = "",
                InheritVNCProxyUsername = "",
                InheritVNCProxyPassword = "",
                InheritVNCColors = "",
                InheritVNCSmartSizeMode = "",
                InheritVNCViewOnly = "",
                InheritRDGatewayUsageMethod = "",
                InheritRDGatewayHostname = "",
                InheritRDGatewayUseConnectionCredentials = "",
                InheritRDGatewayUsername = "",
                InheritRDGatewayPassword = "",
                InheritRDGatewayDomain = "",
                InheritRDPAlertIdleTimeout = "",
                InheritRDPMinutesToIdleTimeout = "",
                InheritSoundQuality = "",
                InheritRedirectAudioCapture = "",
                InheritRdpVersion = "";
        }
        #endregion

        private static string GetValue(List<string> keyList, string[] valueList, string fieldName)
        {
            var i = keyList.IndexOf(fieldName.ToLower());
            if (i >= 0)
            {
                var val = valueList[i];
                return val.Trim();
            }
            return "";
        }

        private static List<string> GetTitles(string firstLine)
        {
            if (string.IsNullOrWhiteSpace(firstLine))
                return null;
            // split title line by ';'
            var titles = firstLine.ToLower().Split(';').ToList();
            if (titles.Count == 0)
                return null;
            return titles;
        }

        private static Dictionary<string, MRemoteNgItem> GetMRemoteNgItems(ref string[] csvLines)
        {
            if (csvLines.Length == 0)
                return null;

            // split title line by ';'
            var titles = GetTitles(csvLines[0]);
            if (titles.Count == 0)
                return null;

            // read the server name from csv, map server id -> server name
            var id2MRemoteNgItem = new Dictionary<string, MRemoteNgItem>(); // id -> item

            var t = typeof(MRemoteNgItem);
            var fields = t.GetFields();

            for (var i = 1; i < csvLines.Length; i++)
            {
                var line = csvLines[i];
                var arr = line.Split(';');
                if (arr.Length < 7) continue;

                var item = new MRemoteNgItem();
                foreach (var field in fields)
                {
                    var value = GetValue(titles, arr, field.Name);
                    field.SetValue(item, value);
                }

                id2MRemoteNgItem.Add(item.Id, item);
            }

            // if it has a parent, then find the parents, set server name to: "parent name" - "server name"
            foreach (var kv in id2MRemoteNgItem.ToArray())
            {
                var item = kv.Value;
                if (item.NodeType != NodeTypeConnection) continue;
                var pid = item.Parent;
                while (id2MRemoteNgItem.ContainsKey(pid))
                {
                    item.Name = $"{id2MRemoteNgItem[pid].Name} - {item.Name}";
                    pid = id2MRemoteNgItem[pid].Parent;
                }
            }

            return id2MRemoteNgItem;
        }

        private static void Inherit(ref Dictionary<string, MRemoteNgItem> items)
        {
            var t = typeof(MRemoteNgItem);
            var fields = t.GetFields();

            foreach (var kv in items)
            {
                var item = kv.Value;
                if (item.NodeType != NodeTypeConnection)
                    continue;

                foreach (var field in fields)
                {
                    if (string.IsNullOrEmpty(field.GetValue(item)?.ToString()) == false) continue;

                    // if any field == string.Empty then find it's parent
                    var pid = item.Parent;
                    while (items.ContainsKey(pid) && string.IsNullOrWhiteSpace(field.GetValue(items[pid])?.ToString()))
                    {
                        pid = items[pid].Parent;
                    }

                    if (items.ContainsKey(pid) && string.IsNullOrWhiteSpace(field.GetValue(items[pid])?.ToString()) == false)
                    {
                        field.SetValue(item, field.GetValue(items[pid]));
                    }
                }
            }
        }

        public static List<ProtocolServerBase> FromCsv(string csvPath, List<BitmapSource> icons)
        {
            if (!File.Exists(csvPath))
                return null;

            var csvLines = File.ReadAllLines(csvPath, Encoding.UTF8);
            if (csvLines.Length == 0)
                return null;

            var id2MRemoteNgItem = GetMRemoteNgItems(ref csvLines);
            if (id2MRemoteNgItem == null || id2MRemoteNgItem.Count == 0)
                return null;

            Inherit(ref id2MRemoteNgItem);

            var list = new List<ProtocolServerBase>();
            var r = new Random();
            foreach (var kv in id2MRemoteNgItem)
            {
                var item = kv.Value;
                if (item.NodeType != NodeTypeConnection)
                    continue;

                ProtocolServerBase server = null;
                List<string> tags = new List<string>();
                //if (id2MRemoteNgItem.ContainsKey(item.Parent))
                //{
                //    string tag = id2MRemoteNgItem[item.Parent].Name;
                //    var pid = id2MRemoteNgItem[item.Parent].Parent;
                //    while (id2MRemoteNgItem.ContainsKey(pid))
                //    {
                //        tag = $"{id2MRemoteNgItem[pid].Name} - {tag}";
                //        pid = id2MRemoteNgItem[pid].Parent;
                //    }
                //}
                if (id2MRemoteNgItem.ContainsKey(item.Parent))
                    tags = new List<string>() { id2MRemoteNgItem[item.Parent].Name };

                switch (item.Protocol.ToLower())
                {
                    case "rdp":
                        server = new ProtocolServerRDP()
                        {
                            DisplayName = item.Name,
                            Tags = tags,
                            Address = item.Hostname,
                            UserName = item.Username,
                            Password = item.Password,
                            Port = item.Port,
                            Domain = item.Domain,
                            LoadBalanceInfo = item.LoadBalanceInfo,
                            RdpWindowResizeMode = ERdpWindowResizeMode.AutoResize, // string.Equals( getValue(title, arr, "AutomaticResize"), "TRUE", StringComparison.CurrentCultureIgnoreCase) ? ERdpWindowResizeMode.AutoResize : ERdpWindowResizeMode.Fixed,
                            IsConnWithFullScreen = string.Equals(item.Resolution, "Fullscreen", StringComparison.CurrentCultureIgnoreCase),
                            RdpFullScreenFlag = ERdpFullScreenFlag.EnableFullScreen,
                            DisplayPerformance = item.Colors.IndexOf("32", StringComparison.Ordinal) >= 0 ? EDisplayPerformance.High : EDisplayPerformance.Auto,
                            IsAdministrativePurposes = string.Equals(item.ConnectToConsole, "TRUE", StringComparison.CurrentCultureIgnoreCase),
                            EnableClipboard = string.Equals(item.RedirectClipboard, "TRUE", StringComparison.CurrentCultureIgnoreCase),
                            EnableDiskDrives = string.Equals(item.RedirectDiskDrives, "TRUE", StringComparison.CurrentCultureIgnoreCase),
                            EnableKeyCombinations = string.Equals(item.RedirectKeys, "TRUE", StringComparison.CurrentCultureIgnoreCase),
                            AudioRedirectionMode = string.Equals(item.RedirectSound, "BringToThisComputer", StringComparison.CurrentCultureIgnoreCase) ? EAudioRedirectionMode.RedirectToLocal : (string.Equals(item.RedirectSound, "LeaveAtRemoteComputer", StringComparison.CurrentCultureIgnoreCase) ? EAudioRedirectionMode.LeaveOnRemote : EAudioRedirectionMode.Disabled),
                            EnableAudioCapture = string.Equals(item.RedirectAudioCapture, "TRUE", StringComparison.CurrentCultureIgnoreCase),
                            EnablePorts = string.Equals(item.RedirectPorts, "TRUE", StringComparison.CurrentCultureIgnoreCase),
                            EnablePrinters = string.Equals(item.RedirectPrinters, "TRUE", StringComparison.CurrentCultureIgnoreCase),
                            EnableSmartCardsAndWinHello = string.Equals(item.RedirectSmartCards, "TRUE", StringComparison.CurrentCultureIgnoreCase),
                            GatewayMode = string.Equals(item.RDGatewayUsageMethod, "Never", StringComparison.CurrentCultureIgnoreCase) ? EGatewayMode.DoNotUseGateway :
                                (string.Equals(item.RDGatewayUsageMethod, "Detect", StringComparison.CurrentCultureIgnoreCase) ? EGatewayMode.AutomaticallyDetectGatewayServerSettings : EGatewayMode.UseTheseGatewayServerSettings),
                            GatewayHostName = item.RDGatewayHostname,
                            GatewayPassword = item.RDGatewayPassword,
                        };

                        break;

                    case "ssh1":
                        server = new ProtocolServerSSH()
                        {
                            DisplayName = item.Name,
                            Tags = tags,
                            Address = item.Hostname,
                            UserName = item.Username,
                            Password = item.Password,
                            Port = item.Port,
                            SshVersion = 1
                        };
                        break;

                    case "ssh2":
                        server = new ProtocolServerSSH()
                        {
                            DisplayName = item.Name,
                            Tags = tags,
                            Address = item.Hostname,
                            UserName = item.Username,
                            Password = item.Password,
                            Port = item.Port,
                            SshVersion = 2
                        };
                        break;

                    case "vnc":
                        server = new ProtocolServerVNC()
                        {
                            DisplayName = item.Name,
                            Tags = tags,
                            Address = item.Hostname,
                            Password = item.Password,
                            Port = item.Port,
                        };
                        break;

                    case "telnet":
                        server = new ProtocolServerTelnet()
                        {
                            DisplayName = item.Name,
                            Tags = tags,
                            Address = item.Hostname,
                            Port = item.Port,
                        };
                        break;
                }

                if (server != null && icons.Count > 0)
                {
                    server.IconBase64 = icons[r.Next(0, icons.Count)].ToBase64();
                    list.Add(server);
                }
            }

            return list;
        }
    }
}

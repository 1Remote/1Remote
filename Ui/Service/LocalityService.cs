using System;
using System.IO;
using System.Text;
using System.Windows;
using Newtonsoft.Json;
using Shawn.Utils;

namespace PRM.Service
{
    public enum EnumServerOrderBy
    {
        IdAsc = -1,
        ProtocolAsc = 0,
        ProtocolDesc = 1,
        NameAsc = 2,
        NameDesc = 3,
        //TagAsc = 4,
        //TagDesc = 5,
        AddressAsc = 6,
        AddressDesc = 7,
    }

    internal class LocalitySettings
    {
        public double MainWindowWidth = 800;
        public double MainWindowHeight = 530;
        public double TabWindowWidth = 800;
        public double TabWindowHeight = 600;
        public WindowState TabWindowState = WindowState.Normal;
        public EnumServerOrderBy ServerOrderBy = EnumServerOrderBy.IdAsc;
    }

    public sealed class LocalityService
    {
        public LocalityService()
        {
            _localitySettings = new LocalitySettings();
            try
            {
                var tmp = JsonConvert.DeserializeObject<LocalitySettings>(File.ReadAllText(AppPathHelper.Instance.LocalityJsonPath));
                if (tmp != null)
                    _localitySettings = tmp;
            }
            catch
            {
                // ignored
            }
        }

        public double MainWindowWidth
        {
            get => _localitySettings.MainWindowWidth;
            set
            {
                if (Math.Abs(_localitySettings.MainWindowWidth - value) > 0.001)
                {
                    _localitySettings.MainWindowWidth = value;
                    Save();
                }
            }
        }

        public double MainWindowHeight
        {
            get => _localitySettings.MainWindowHeight;
            set
            {
                if (Math.Abs(_localitySettings.MainWindowHeight - value) > 0.001)
                {
                    _localitySettings.MainWindowHeight = value;
                    Save();
                }
            }
        }

        public double TabWindowWidth
        {
            get => _localitySettings.TabWindowWidth;
            set
            {
                if (Math.Abs(_localitySettings.TabWindowWidth - value) > 0.001)
                {
                    _localitySettings.TabWindowWidth = value;
                    Save();
                }
            }
        }

        public double TabWindowHeight
        {
            get => _localitySettings.TabWindowHeight;
            set
            {
                if (Math.Abs(_localitySettings.TabWindowHeight - value) > 0.001)
                {
                    _localitySettings.TabWindowHeight = value;
                    Save();
                }
            }
        }

        public WindowState TabWindowState
        {
            get => _localitySettings.TabWindowState;
            set
            {
                if (_localitySettings.TabWindowState != value)
                {
                    _localitySettings.TabWindowState = value;
                    Save();
                }
            }
        }
        public EnumServerOrderBy ServerOrderBy
        {
            get => _localitySettings.ServerOrderBy;
            set
            {
                if (_localitySettings.ServerOrderBy != value)
                {
                    _localitySettings.ServerOrderBy = value;
                    Save();
                }
            }
        }


        private readonly LocalitySettings _localitySettings;

        #region Interface

        public bool CanSave = true;
        private void Save()
        {
            if (!CanSave) return;
            lock (this)
            {
                if (!CanSave) return;
                CanSave = false;
                var fi = new FileInfo(AppPathHelper.Instance.LocalityJsonPath);
                if (fi?.Directory?.Exists == false)
                    fi.Directory.Create();
                File.WriteAllText(AppPathHelper.Instance.LocalityJsonPath, JsonConvert.SerializeObject(this._localitySettings, Formatting.Indented), Encoding.UTF8);
                CanSave = true;
            }
        }

        #endregion Interface
    }
}
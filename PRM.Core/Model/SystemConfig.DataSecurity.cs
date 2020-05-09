using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Shawn.Ulits;

namespace PRM.Core.Model
{
    public sealed class SystemConfigDataSecurity : SystemConfigBase
    {
        public SystemConfigDataSecurity(Ini ini) : base(ini)
        {
            Load();
        }

        private string _dbPath = "./PRemoteM.db";
        public string DbPath
        {
            get => _dbPath;
            set => SetAndNotifyIfChanged(nameof(DbPath), ref _dbPath, value);
        }



        private string _rsaPublicKey = null;
        public string RsaPublicKey
        {
            get => _rsaPublicKey;
            set
            {
                // TODO 同步更新数据库
                SetAndNotifyIfChanged(nameof(RsaPublicKey), ref _rsaPublicKey, value);
            }
        }



        private string _rsaPrivateKeyPath = null;
        public string RsaPrivateKeyPath
        {
            get => _rsaPrivateKeyPath;
            set
            {
                // TODO 同步更新数据库
                SetAndNotifyIfChanged(nameof(RsaPrivateKeyPath), ref _rsaPrivateKeyPath, value);
            }
        }


        #region Interface
        private const string _sectionName = "DataSecurity";
        public override void Save()
        {
            _ini.WriteValue(nameof(DbPath).ToLower(), _sectionName, DbPath);
            _ini.Save();
        }

        public override void Load()
        {
            //TODO thread not safe
            StopAutoSave = true;
            DbPath = _ini.GetValue(nameof(DbPath).ToLower(), _sectionName, DbPath);
            StopAutoSave = false;
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigDataSecurity));
            Save();
        }

        #endregion
    }
}

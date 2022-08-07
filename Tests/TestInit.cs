using System;
using System.IO;
using _1RM;
using _1RM.Model;
using _1RM.Service;
using Shawn.Utils.Interface;

namespace Tests
{
    public static class TestInit
    {
        public static void Init()
        {
            AppPathHelper.Instance = new AppPathHelper(Environment.CurrentDirectory);
            if (File.Exists(AppPathHelper.Instance.ProfileJsonPath))
                File.Delete(AppPathHelper.Instance.ProfileJsonPath);
            if (File.Exists(AppPathHelper.Instance.SqliteDbDefaultPath))
                File.Delete(AppPathHelper.Instance.SqliteDbDefaultPath);



            IoC.GetByType = (type, key) =>
            {
                if (type == typeof(IDataService) || type == typeof(DataService))
                    return new DataService();
                if (type == typeof(ILanguageService) || type == typeof(LanguageService) || type == typeof(MockLanguageService))
                    return new MockLanguageService();
                if (type == typeof(_1RM.Service.Configuration))
                    return new _1RM.Service.Configuration();
                if (type == typeof(_1RM.Service.ConfigurationService))
                    return new ConfigurationService(new Configuration(), new KeywordMatchService());
                if (type == typeof(PrmContext))
                    return new PrmContext(new ProtocolConfigurationService(), new GlobalData(new ConfigurationService(new _1RM.Service.Configuration(), new KeywordMatchService())));
                return null;
            };
        }
    }
}

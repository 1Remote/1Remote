using System;
using System.IO;
using PRM.Service;

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
        }
    }
}

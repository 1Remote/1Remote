using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.Model;

namespace PRemoteM.Tests
{
    public static class TestInit
    {
        public static void Init()
        {
            if (File.Exists("test.json"))
                File.Delete("test.json");
            if (File.Exists("test.db"))
                File.Delete("test.db");
        }
    }
}

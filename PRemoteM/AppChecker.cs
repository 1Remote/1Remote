using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.DB;
using PRM.Core.Model;

namespace PRM
{
    public static class AppChecker
    {
        public static bool CheckDbExisted()
        {
            try
            {
                Server.Init();
            }
            catch (Exception exception)
            {
                return false;
            }
            return true;
        }
        public static Tuple<bool, string> CheckDbEncrypted()
        {

            var ret = SystemConfig.GetInstance().DataSecurity.ValidateRsa();
            switch (ret)
            {
                case SystemConfigDataSecurity.ERsaStatues.Ok:
                    break;
                default:
                    switch (ret)
                    {
                        case SystemConfigDataSecurity.ERsaStatues.CanNotFindPrivateKey:
                            return new Tuple<bool, string>(false, SystemConfig.GetInstance().Language.GetText("system_options_data_security_error_rsa_private_key_not_found"));
                        case SystemConfigDataSecurity.ERsaStatues.PrivateKeyContentError:
                        case SystemConfigDataSecurity.ERsaStatues.PrivateKeyIsNotMatch:
                            return new Tuple<bool, string>(false, SystemConfig.GetInstance().Language.GetText("system_options_data_security_error_rsa_private_key_not_match"));
                    }
                    break;
            }
            return new Tuple<bool, string>(true, "");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Shawn.Utils
{
    public static class IOPermissionHelper
    {
        public static bool HasWritePermissionOnDir(string path)
        {
            var writeAllow = false;
            var writeDeny = false;
            var accessControlList = Directory.GetAccessControl(path);
            var accessRules = accessControlList?.GetAccessRules(true, true, 
                typeof(System.Security.Principal.SecurityIdentifier));
            if (accessRules == null)
                return false;

            foreach (FileSystemAccessRule rule in accessRules)
            {
                if ((FileSystemRights.Write & rule.FileSystemRights) != FileSystemRights.Write) 
                    continue;
                if (rule.AccessControlType == AccessControlType.Allow)
                    writeAllow = true;
                else if (rule.AccessControlType == AccessControlType.Deny)
                    writeDeny = true;
            }

            return writeAllow && !writeDeny;
        }
        public static bool HasWritePermissionOnFile(string filePath)
        {
            try
            {
                FileSystemSecurity security;
#if NETCOREAPP
                // nuget import System.IO.FileSystem.AccessControl
                if (File.Exists(filePath))
                {
                    security = new FileSecurity(filePath, AccessControlSections.Owner | 
                                                          AccessControlSections.Group |
                                                          AccessControlSections.Access);
                }
                else
                {
                    security = new DirectorySecurity(filePath,AccessControlSections.Owner | 
                                                              AccessControlSections.Group |
                                                              AccessControlSections.Access);
                }
#else
                if (File.Exists(filePath))
                {
                    security = File.GetAccessControl(filePath);
                }
                else
                {
                    security = Directory.GetAccessControl(Path.GetDirectoryName(filePath));
                }
#endif
                var rules = security.GetAccessRules(true, true, typeof(NTAccount));
                var currentUser = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                bool result = false;
                foreach (FileSystemAccessRule rule in rules)
                {
                    if (0 == (rule.FileSystemRights &
                              (FileSystemRights.WriteData | FileSystemRights.Write)))
                    {
                        continue;
                    }

                    if (rule.IdentityReference.Value.StartsWith("S-1-"))
                    {
                        var sid = new SecurityIdentifier(rule.IdentityReference.Value);
                        if (!currentUser.IsInRole(sid))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (!currentUser.IsInRole(rule.IdentityReference.Value))
                        {
                            continue;
                        }
                    }

                    if (rule.AccessControlType == AccessControlType.Deny)
                        return false;
                    if (rule.AccessControlType == AccessControlType.Allow)
                        result = true;
                }
                return result;
            }
            catch
            {
                return false;
            }
        }
    }
}

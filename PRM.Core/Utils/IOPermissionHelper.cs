using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

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

        public static bool IsFileInUse(string fileName)
        {
            if (!File.Exists(fileName))
                return false;

            bool inUse = true;
            FileStream fs = null;
            try
            {
                fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.None);
                inUse = false;
            }
            catch(Exception e)
            {
                // ignored
            }
            finally
            {
                fs?.Close();
            }
            return inUse; //true表示正在使用,false没有使用
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

                if (result)
                {
                    result = !IsFileInUse(filePath);
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
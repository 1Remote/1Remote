# Troubleshooting RDP Authentication Error 0x80080005

## Problem Description

If you're experiencing an RDP authentication error with message:
```
An authentication error has occurred (Code: 0x80080005)
```

This error is caused by **Windows Update KB5074109** (released January 13, 2026), which introduced a bug in Remote Desktop authentication. This issue affects multiple RDP applications system-wide, not just 1Remote.

## Solution

### ✅ Recommended Fix: Install Microsoft's Patch

Microsoft has released an out-of-band update **KB5077744** (January 17, 2026) that fixes this issue.

#### Installation Steps:

1. **Download KB5077744**:
   - Visit the [Microsoft Update Catalog](https://www.catalog.update.microsoft.com/Search.aspx?q=KB5077744)
   - Select the version matching your Windows build
   - Download and install the update

2. **Restart your computer** after installation

3. **Test your RDP connection** in 1Remote

#### Note About Windows Update
This is an out-of-band (OOB) update with limited scope, so it may **not appear automatically** in Windows Update. You need to install it manually from the Catalog.

## Alternative Temporary Workarounds

If you cannot install KB5077744 immediately:

### Option 1: RDP Server-Side Configuration
If you have access to the RDP server:
1. Temporarily adjust authentication requirements
2. Use less restrictive Network Level Authentication (NLA) settings
3. ⚠️ **Security Warning**: Only use this temporarily, as it reduces security

### Option 2: Use Alternative Connection Protocol
If the target server supports it:
1. Try using VNC instead of RDP temporarily
2. Use SSH tunneling for your RDP connection

### Option 3: Wait for Regular Windows Update
Microsoft may include KB5077744 in future monthly update cycles, though this may take time.

## Verification Steps

After applying the fix:

1. **Check Windows Update history**:
   - Open Settings → Windows Update → Update history
   - Verify KB5077744 is installed

2. **Test RDP connection**:
   - Open 1Remote
   - Attempt to connect to an RDP session
   - Connection should succeed without authentication error

## Additional Information

### Why This Happened
- Windows security update KB5074109 introduced a bug in CredSSP (Credential Security Support Provider)
- CredSSP is the authentication protocol used by RDP clients
- The bug affects RDP authentication across various applications

### Why Different Users See Different Behavior
- Some users may not have KB5074109 installed yet
- Different Windows configurations may exhibit varying symptoms
- System-specific factors (domain membership, group policies, etc.) can affect manifestation

## References

- **Original Issue**: [#1056](https://github.com/1Remote/1Remote/issues/1056)
- **Microsoft KB5074109**: [January 2026 Security Update](https://support.microsoft.com/en-us/topic/january-13-2026-kb5074109)
- **Microsoft KB5077744 (FIX)**: [Out-of-Band Update](https://support.microsoft.com/en-us/topic/january-17-2026-kb5077744-os-builds-26200-7627-and-26100-7627-out-of-band-27015658-9686-4467-ab5f-d713b617e3e4)

## Still Having Issues?

If you've installed KB5077744 and still experience problems:

1. **Verify the update is installed**:
   ```powershell
   Get-HotFix | Where-Object {$_.HotFixID -eq "KB5077744"}
   ```

2. **Check Windows version**:
   - KB5077744 is available for Windows 11 version 24H2 and 23H2
   - Ensure your Windows version is supported

3. **Report the issue**:
   - If problems persist after installing KB5077744, this may be a different issue
   - Open a new issue on GitHub with detailed information:
     - Windows version and build number
     - KB5077744 installation confirmed
     - Full error message and logs

## For System Administrators

### Group Policy Considerations
If managing multiple systems:
1. Deploy KB5077744 via WSUS or Configuration Manager
2. Test in a staging environment first
3. Monitor authentication logs for issues

### Security Recommendations
- **Do not disable CredSSP** as a workaround (security risk)
- **Do not downgrade** to older Windows updates (security risk)
- **Install KB5077744** as soon as possible to maintain both security and functionality

---

**Last Updated**: January 22, 2026  
**Applies To**: 1Remote all versions, Windows 11 with KB5074109 installed

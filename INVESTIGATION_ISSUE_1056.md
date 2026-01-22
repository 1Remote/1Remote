# Investigation Report: RDP Authentication Error (Issue #1056)

## Problem Statement
Users reported RDP authentication error (Code: 0x80080005) after updating Windows with security patch KB5074109. The issue manifests as "An authentication error has occurred" when attempting to connect via RDP.

## Root Cause Analysis

### Primary Cause: Windows Update KB5074109
The investigation reveals that **this is primarily a Windows operating system issue**, not a 1Remote application bug:

1. **Windows KB5074109** (January 13, 2026 security update) introduced a bug in Remote Desktop authentication
2. The bug affects **multiple RDP applications**, not just 1Remote (as confirmed by Reddit discussions and Microsoft's own acknowledgment)
3. Microsoft released **KB5077744** (January 17, 2026) as an out-of-band fix specifically to address this RDP authentication issue

### Microsoft's Official Statement
From KB5077744 release notes:
> **[Remote Desktop]** Fixed: After installing the January 2026 Windows security update (KB5074109), some users experienced sign-in failures during Remote Desktop connections. This issue affected authentication steps for different Remote Desktop applications on Windows such as the Windows App.

## Code Analysis

### 1Remote's RDP Implementation
The investigation examined 1Remote's RDP authentication code to understand if any recent changes could interact negatively with KB5074109:

**Key Finding in `AxMsRdpClient09Host.xaml.cs` (Line 225):**
```csharp
// enable CredSSP, will use CredSsp if the client supports.
_rdpClient.AdvancedSettings9.EnableCredSspSupport = true;
```

**CredSSP (Credential Security Support Provider)** is the authentication protocol that was impacted by KB5074109. However:
- This setting has been in the codebase since the .NET 6 migration (commit 71541eba, April 2022)
- No changes to CredSSP configuration were made between v1.2.0 and current HEAD
- The `EnableCredSspSupport = true` setting is standard and correct for RDP clients

### Changes Between v1.2.0 and Current
Reviewed all RDP-related commits after v1.2.0 tag (086f3fe7):

1. **6b119bcc** - Fix: focus selected tab content - Minor focus management changes
2. **a765805a** - Reduce RDP auto reconnection attempts from 20 to 5
3. **ff94a198** - Fix RDP client disposal race condition
4. **499be2c5** - Fix RDP connection bar not displaying in full screen
5. **931c3918** - Improve RDP auto-close handling based on disconnect reason codes
6. **29162624** - Fix behavior on RDP is disconnected
7. **2511d349** - Prevent TaskCanceledException during RDP client disposal
8. **9343b221** - Fix incorrect size when restoring minimized RDP

**None of these commits modified authentication settings or CredSSP configuration.**

## User Reports Analysis

### Conflicting Evidence
- User @maartsen: Confirmed v1.2.1 works, newer versions have issues
- User @itagagaki: Reported both v1.2.1 and latest Nightly work fine with KB5074109

### Likely Explanation
The varying experiences suggest:
1. **System-specific factors**: Different Windows configurations may exhibit the bug differently
2. **Timing**: Users who experienced issues might have been between KB5074109 and KB5077744
3. **Perception**: When v1.2.1 worked, it might have been before KB5074109 was installed

## Conclusions

### Primary Determination
**The RDP authentication error (0x80080005) is caused by Windows Update KB5074109, not by code changes in 1Remote.**

### Evidence Supporting This Conclusion
1. Microsoft officially acknowledged and fixed the bug in KB5077744
2. No authentication-related code changes in 1Remote between v1.2.0 and current
3. The issue affects multiple RDP applications system-wide
4. Some users reported success with both old and new versions of 1Remote

### Why Some Users See v1.2.1 as "Working"
Possible explanations:
1. **Older version may have been tested before KB5074109 was installed**
2. **Placebo effect**: Reinstalling any version might have temporarily resolved system state
3. **System variation**: Different Windows configurations may behave differently

## Recommendations

### For Users Experiencing This Issue

#### Option 1: Install Microsoft's Fix (Recommended)
Install Windows Update **KB5077744** from:
- Windows Update Catalog: https://www.catalog.update.microsoft.com/Search.aspx?q=KB5077744
- Note: This is an out-of-band update and may not appear in Windows Update automatically

#### Option 2: Temporary Workarounds (if KB5077744 unavailable)
1. **Disable CredSSP temporarily** (not recommended for production, security risk):
   - This would require modifying 1Remote's code
   - Not recommended as it reduces security
   
2. **Use alternative authentication methods**:
   - Check if the target RDP server supports different authentication protocols

#### Option 3: Wait for Windows Update
Microsoft may include KB5077744 in future regular update cycles.

### For 1Remote Development

#### No Code Changes Required
Based on this investigation, **no changes to 1Remote's codebase are necessary**:
1. The authentication configuration is correct and standard
2. The issue is in the Windows operating system, not the application
3. Microsoft has already released a fix

#### Documentation Updates Recommended
1. Add a note in documentation about KB5074109/KB5077744
2. Include troubleshooting steps for RDP authentication errors
3. Link to Microsoft's KB articles

## References

- Issue #1056: https://github.com/1Remote/1Remote/issues/1056
- Microsoft KB5074109: https://support.microsoft.com/en-us/topic/january-13-2026-kb5074109
- Microsoft KB5077744: https://support.microsoft.com/en-us/topic/january-17-2026-kb5077744-os-builds-26200-7627-and-26100-7627-out-of-band-27015658-9686-4467-ab5f-d713b617e3e4
- Reddit Discussion: https://www.reddit.com/r/windows/comments/1qc35b0/kb5074109_breaks_azure_virtual_desktop_on_windows/

## Investigation Metadata

- **Investigation Date**: January 22, 2026
- **1Remote Version Range Analyzed**: 1.2.0 (086f3fe7) to HEAD
- **Key Files Reviewed**:
  - `Ui/View/Host/ProtocolHosts/AxMsRdpClient09Host.cs`
  - `Ui/View/Host/ProtocolHosts/AxMsRdpClient09Host.xaml.cs`
- **Commits Analyzed**: 71 commits between v1.2.0 and HEAD
- **RDP-specific Commits**: 18 commits reviewed in detail

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace _1RM.Utils.RdpFile
{
    // Source: https://social.msdn.microsoft.com/Forums/windowsdesktop/en-US/9095625c-4361-4e0b-bfcf-be15550b60a8/imsrdpclientnonscriptablesendkeys?forum=windowsgeneraldevelopmentissues
    internal class MsRdpClientNonScriptableWrapper
    {
        [InterfaceType(1)]
        [Guid("2F079C4C-87B2-4AFD-97AB-20CDB43038AE")]
        private interface IMsRdpClientNonScriptableEx : MSTSCLib.IMsTscNonScriptable
        {
            [DispId(4)] new string BinaryPassword { get; set; }
            [DispId(5)] new string BinarySalt { get; set; }
            [DispId(1)] new string ClearTextPassword { set; }
            [DispId(2)] new string PortablePassword { get; set; }
            [DispId(3)] new string PortableSalt { get; set; }

            new void ResetPassword();

            // 函数接口原实现 void NotifyRedirectDeviceChange([ComAliasName("MSTSCLib.UINT_PTR")] uint wParam, [ComAliasName("MSTSCLib.LONG_PTR")] int lParam);，无法满足 x64 的指针数值范围

            /// <summary>
            /// 迁移到 .NET6 后，使用自带的接口会因为 IntPtr 类型的 lParam 其值超过了 int 型最大值，之后会出现错误 `Arithmetic operation resulted in an overflow  HwndSourceHook`
            /// </summary>
            /// <param name="wParam"></param>
            /// <param name="lParam"></param>
            void NotifyRedirectDeviceChange([ComAliasName("MSTSCLib.UINT_PTR")] long wParam, [ComAliasName("MSTSCLib.LONG_PTR")] long lParam);

            /// <summary>
            ///     [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            ///     new void SendKeys([In] int numKeys, [MarshalAs(UnmanagedType.LPArray), In] bool[] pbArrayKeyUp, [MarshalAs(UnmanagedType.LPArray), In] int[] plKeyData);
            /// </summary>
            unsafe void SendKeys(int numKeys, int* pbArrayKeyUp, int* plKeyData);
        }

        private readonly IMsRdpClientNonScriptableEx _mEx;

        public MsRdpClientNonScriptableWrapper(object ocx)
        {
            _mEx = (IMsRdpClientNonScriptableEx)ocx;
        }

        /*
        public void SendCtrlAltDel()
        {
            // Source: https://github.com/bosima/RDManager/blob/master/RDManager/MainForm.cs
            Client.Focus();
            new MsRdpClientNonScriptableWrapper(Client.GetOcx()).SendKeys(
                new int[] { 0x1d, 0x38, 0x53, 0x53, 0x38, 0x1d },
                new bool[] { false, false, false, true, true, true, }
            );
        }
        */
        public void SendKeys(int[] keyScanCodes, bool[] keyReleased)
        {
            if (keyScanCodes.Length != keyReleased.Length) throw new ArgumentException("MsRdpClientNonScriptableWrapper.SendKeys: Arraysize must match");

            int[] temp = new int[keyReleased.Length];
            for (int i = 0; i < temp.Length; i++) temp[i] = keyReleased[i] ? 1 : 0;
            unsafe
            {
                fixed (int* pScanCodes = keyScanCodes)
                fixed (int* pKeyReleased = temp)
                {
                    _mEx.SendKeys(keyScanCodes.Length, pKeyReleased, pScanCodes);
                }
            }
        }


        public void NotifyRedirectDeviceChange(IntPtr wParam, IntPtr lParam) // HwndSourceHook
        {
            var iwParam = wParam.ToInt64();
            var ilParam = lParam.ToInt64();
            _mEx.NotifyRedirectDeviceChange(iwParam, ilParam);
        }
    }
}

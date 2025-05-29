using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using _1RM.View;
using IPAddress = System.Net.IPAddress;

namespace _1RM.Utils
{
    public class SubTitleSortByNaturalIp : IComparer
    {
        private readonly bool _orderIsAsc = true;
        private int return1 => _orderIsAsc ? 1 : -1;
        private int return_1 => _orderIsAsc ? -1 : 1;

        public SubTitleSortByNaturalIp(bool orderIsAsc)
        {
            _orderIsAsc = orderIsAsc;
        }


        public int Compare(object? x, object? y)
        {
            if (x is not ProtocolBaseViewModel px || y is not ProtocolBaseViewModel py)
            {
                return return_1;
            }
            if (x == null || y == null)
            {
                throw new ArgumentNullException("Neither x nor y can be null");
            }

            string strX = px.SubTitle;
            string strY = py.SubTitle;

            // 根据 : 分割拆分
            var (ipX, portX) = SplitIpAndPort(strX);
            var (ipY, portY) = SplitIpAndPort(strY);

            // 尝试解析 IP 地址
            {
                bool isXIpV4 = IsValidIPv4(ipX, out var xa4);
                bool isYIpV4 = IsValidIPv4(ipY, out var ya4);
                if (isXIpV4 && isYIpV4)
                {
                    var keyX = GetIpV4Key(xa4, portX);
                    var keyY = GetIpV4Key(ya4, portY);
                    return string.Compare(keyX, keyY, StringComparison.Ordinal);
                }
                // 优先级排序：IPv4 > IPv6 > 其他
                if (isXIpV4 && !isYIpV4) return return_1; // x 是 IPv4，y 不是
                if (!isXIpV4 && isYIpV4) return return1; // y 是 IPv4，x 不是
            }

            {
                bool isXIpV6 = IsValidIPv6(ipX, out var xa6);
                bool isYIpV6 = IsValidIPv6(ipY, out var ya6);
                if (isXIpV6 && isYIpV6)
                {
                    var xkey = GetIpV6Key(xa6, portX);
                    var ykey = GetIpV6Key(ya6, portY);
                    return string.Compare(xkey, ykey, StringComparison.Ordinal);
                }
                if (isXIpV6 && !isYIpV6) return return_1; // x 是 IPv6，y 不是
                if (!isXIpV6 && isYIpV6) return return1; // y 是 IPv6，x 不是
            }

            // 进行字面值比较
            var ret = string.Compare(strX, strY, StringComparison.Ordinal);
            if (!_orderIsAsc)
            {
                ret = -ret;
            }
            return ret;
        }

        private (string, string) SplitIpAndPort(string str)
        {
            int i = str.IndexOf(":", StringComparison.Ordinal);
            if (i <= 0)
                return (str, "");
            var ip = str.Substring(0, i);
            var port = str.Substring(i + 1);
            return (ip, port);
        }

        private bool IsValidIPv4(string ip, out IPAddress? address)
        {
            address = null;
            if (ip.Split('.').Length != 4)
            {
                return false;
            }
            return IPAddress.TryParse(ip, out address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        }

        private bool IsValidIPv6(string ip, out IPAddress? address)
        {
            address = null;
            if (ip.Split(':').Length != 8)
            {
                return false;
            }
            return IPAddress.TryParse(ip, out address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
        }

        private string GetIpV4Key(IPAddress address, string port)
        {
            var parts = address.GetAddressBytes();
            return string.Join(".", parts.Select(p => p.ToString("D3"))) + ":" + port;
        }

        private string GetIpV6Key(IPAddress address, string port)
        {
            var parts = address.GetAddressBytes();
            return string.Join(":", parts.Select(p => p.ToString("D5"))) + ":" + port;
        }
    }
}

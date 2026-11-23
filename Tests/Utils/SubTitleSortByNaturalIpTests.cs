using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using _1RM.Model.Protocol;
using _1RM.Utils;
using _1RM.View;
using Xunit;

namespace Tests.Utils
{
    /// <summary>
    /// The main window "Address" column sorts through <see cref="SubTitleSortByNaturalIp"/> as a
    /// <see cref="ListCollectionView.CustomSort"/>. The direction flag used to be dropped on every
    /// branch that compared two IP addresses, so the descending click produced the ascending order.
    /// </summary>
    public class SubTitleSortByNaturalIpTests
    {
        private static ProtocolBaseViewModel Vm(string address, string port = "22")
        {
            return new ProtocolBaseViewModel(new SSH { Address = address, Port = port, UserName = "root" });
        }

        /// <summary>Sorts through the same view the list page installs the comparer on.</summary>
        private static List<string> SortThroughListView(IEnumerable<ProtocolBaseViewModel> servers, bool orderIsAsc)
        {
            var view = new ListCollectionView(new ObservableCollection<ProtocolBaseViewModel>(servers))
            {
                CustomSort = new SubTitleSortByNaturalIp(orderIsAsc)
            };
            return view.Cast<ProtocolBaseViewModel>().Select(x => x.SubTitle).ToList();
        }

        [Fact]
        public void AddressDescending_MustReverseTheIpv4Order()
        {
            var servers = new List<ProtocolBaseViewModel>
            {
                Vm("192.168.0.10"),
                Vm("192.168.0.2"),
                Vm("192.168.0.33"),
            };

            var descending = SortThroughListView(servers, orderIsAsc: false);

            Assert.Equal(new[]
            {
                "192.168.0.33:22 (root)",
                "192.168.0.10:22 (root)",
                "192.168.0.2:22 (root)",
            }, descending);
        }

        [Fact]
        public void AddressAscending_MustKeepNaturalIpv4Order()
        {
            var servers = new List<ProtocolBaseViewModel>
            {
                Vm("192.168.0.10"),
                Vm("192.168.0.2"),
                Vm("192.168.0.33"),
            };

            var ascending = SortThroughListView(servers, orderIsAsc: true);

            // 192.168.0.2 sorts before 192.168.0.10: the comparer is natural, not lexicographic.
            Assert.Equal(new[]
            {
                "192.168.0.2:22 (root)",
                "192.168.0.10:22 (root)",
                "192.168.0.33:22 (root)",
            }, ascending);
        }

        [Fact]
        public void AddressDescending_MustReverseNonIpSubtitles()
        {
            var servers = new List<ProtocolBaseViewModel>
            {
                Vm("alpha", ""),
                Vm("charlie", ""),
                Vm("bravo", ""),
            };

            var descending = SortThroughListView(servers, orderIsAsc: false);

            Assert.Equal(new[]
            {
                "charlie: (root)",
                "bravo: (root)",
                "alpha: (root)",
            }, descending);
        }

        [Fact]
        public void AddressDescending_MustReverseThePortWithinOneHost()
        {
            var servers = new List<ProtocolBaseViewModel>
            {
                Vm("10.0.0.1", "22"),
                Vm("10.0.0.1", "3389"),
            };

            var descending = SortThroughListView(servers, orderIsAsc: false);

            Assert.Equal(new[]
            {
                "10.0.0.1:3389 (root)",
                "10.0.0.1:22 (root)",
            }, descending);
        }
    }
}

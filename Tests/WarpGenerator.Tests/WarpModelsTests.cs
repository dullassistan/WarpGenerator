using System.Linq;
using WarpGenerator.Models;
using Xunit;

namespace WarpGenerator.Tests
{
    public class WarpModelsTests
    {
        [Fact]
        public void DnsOption_All_ContainsExpectedPredefinedServers()
        {
            var all = DnsOption.All;
            Assert.Equal(6, all.Count);

            var ids = all.Select(d => d.Id).ToList();
            Assert.Contains("cf", ids);
            Assert.Contains("google", ids);
            Assert.Contains("malw", ids);
            Assert.Contains("xbox", ids);
            Assert.Contains("geohide", ids);
            Assert.Contains("comss", ids);

            // All IDs must be unique
            Assert.Equal(all.Count, ids.Distinct().Count());

            // Each option must have valid display name and non-empty DNS server list
            foreach (var opt in all)
            {
                Assert.False(string.IsNullOrWhiteSpace(opt.Id));
                Assert.False(string.IsNullOrWhiteSpace(opt.DisplayName));
                Assert.False(string.IsNullOrWhiteSpace(opt.DnsServers));
            }
        }

        [Fact]
        public void WarpConfigSettings_DefaultValues_AreConsistent()
        {
            var settings = new WarpConfigSettings();

            Assert.Equal("AmneziaWG", settings.SelectedClient);
            Assert.Equal("awg", settings.ClashMode);
            Assert.Equal("cf", settings.SelectedDnsId);
            Assert.Equal("engage.cloudflareclient.com:2408", settings.Endpoint);
            Assert.Equal("1420", settings.Mtu);
            Assert.False(settings.IsAwg1);
            Assert.True(settings.IsAwg2);
            Assert.False(settings.IsAwg3);
            Assert.False(settings.IsAwg31);
            Assert.False(settings.ExcludeLan);
            Assert.False(settings.EnableIpv6);
            Assert.False(settings.PersistentKeepaliveEnabled);
            Assert.Equal("25", settings.PersistentKeepaliveValue);
        }

        [Fact]
        public void WarpApiResponse_Properties_HoldAssignedValues()
        {
            var res = new WarpApiResponse
            {
                Success = true,
                PrivKey = "priv",
                PeerPub = "pub",
                ClientIpv4 = "172.16.0.2",
                ClientIpv6 = "2606::1"
            };

            Assert.True(res.Success);
            Assert.Equal("priv", res.PrivKey);
            Assert.Equal("pub", res.PeerPub);
            Assert.Equal("172.16.0.2", res.ClientIpv4);
            Assert.Equal("2606::1", res.ClientIpv6);
        }
    }
}

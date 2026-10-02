using System;
using System.Linq;
using WarpGenerator.Models;
using WarpGenerator.Services;
using Xunit;

namespace WarpGenerator.Tests
{
    public class WarpConfigBuilderServiceTests
    {
        private readonly WarpConfigBuilderService _builder = new();

        private static WarpApiResponse CreateTestApiData(string privKey = "aGVsbG93b3JsZDEyMzQ1Njc4OTA=", string peerPub = "Y2xvdWRmbGFyZXdhcnBwdWJrZXk=")
        {
            return new WarpApiResponse
            {
                Success = true,
                PrivKey = privKey,
                PeerPub = peerPub,
                ClientIpv4 = "172.16.0.2",
                ClientIpv6 = "2606:4700:110:8f81:85b1:9c29:da2e:b302"
            };
        }

        [Fact]
        public void BuildConfigString_NullApiData_ReturnsEmptyString()
        {
            var result = _builder.BuildConfigString(null!, new WarpConfigSettings());
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void BuildConfigString_DefaultAmneziaWg_BuildsValidConf()
        {
            var apiData = CreateTestApiData();
            var settings = new WarpConfigSettings
            {
                SelectedClient = "AmneziaWG",
                IsAwg1 = false,
                IsAwg2 = false
            };

            string conf = _builder.BuildConfigString(apiData, settings);

            Assert.Contains("[Interface]", conf);
            Assert.Contains($"PrivateKey = {apiData.PrivKey}", conf);
            Assert.Contains($"Address = {apiData.ClientIpv4}/32", conf);
            Assert.Contains("MTU = 1420", conf);
            Assert.Contains("[Peer]", conf);
            Assert.Contains($"PublicKey = {apiData.PeerPub}", conf);
            Assert.Contains("Endpoint = engage.cloudflareclient.com:2408", conf);
            Assert.Contains("AllowedIPs = 0.0.0.0/0", conf);
            // IPv6 is disabled by default -> no IPv6 address in interface address
            Assert.DoesNotContain(apiData.ClientIpv6, conf);
        }

        [Fact]
        public void BuildConfigString_Awg1Presets_RenderCorrectJunkParameters()
        {
            var apiData = CreateTestApiData();

            // Preset 1
            var s1 = new WarpConfigSettings { SelectedClient = "AmneziaWG", IsAwg1 = true, Awg1JunkPreset = 1, IsAwg2 = false };
            string c1 = _builder.BuildConfigString(apiData, s1);
            Assert.Contains("Jc = 4", c1);
            Assert.Contains("Jmin = 40", c1);
            Assert.Contains("Jmax = 70", c1);
            Assert.Contains("H1 = 1", c1);

            // Preset 2
            var s2 = new WarpConfigSettings { SelectedClient = "AmneziaWG", IsAwg1 = true, Awg1JunkPreset = 2, IsAwg2 = false };
            string c2 = _builder.BuildConfigString(apiData, s2);
            Assert.Contains("Jc = 30", c2);
            Assert.Contains("Jmin = 10", c2);
            Assert.Contains("Jmax = 30", c2);

            // Preset 3 (custom)
            var s3 = new WarpConfigSettings
            {
                SelectedClient = "AmneziaWG",
                IsAwg1 = true,
                Awg1JunkPreset = 3,
                Awg1Jc = "15",
                Awg1Jmin = "100",
                Awg1Jmax = "200",
                IsAwg2 = false
            };
            string c3 = _builder.BuildConfigString(apiData, s3);
            Assert.Contains("Jc = 15", c3);
            Assert.Contains("Jmin = 100", c3);
            Assert.Contains("Jmax = 200", c3);
        }

        [Fact]
        public void BuildConfigString_WireSock_RendersIdIpIb()
        {
            var apiData = CreateTestApiData();
            var settings = new WarpConfigSettings
            {
                SelectedClient = "WireSock",
                IsAwg2 = true,
                WireSockId = "myhost.com",
                WireSockIp = "quic",
                WireSockIb = "curl"
            };

            string conf = _builder.BuildConfigString(apiData, settings);

            Assert.Contains("Id = myhost.com", conf);
            Assert.Contains("Ip = quic", conf);
            Assert.Contains("Ib = curl", conf);
        }

        [Fact]
        public void BuildConfigString_Awg2Options_RenderI1ThroughI5()
        {
            var apiData = CreateTestApiData();
            var settings = new WarpConfigSettings
            {
                SelectedClient = "AmneziaWG",
                IsAwg2 = true,
                Awg2I1 = "<b 0x010203>",
                Awg2I2 = "<b 0x0405>",
                Awg2I3 = "<b 0x0607>",
                Awg2I4 = "<b 0x0809>",
                Awg2I5 = "<b 0x0a0b>"
            };

            string conf = _builder.BuildConfigString(apiData, settings);

            Assert.Contains("I1 = <b 0x010203>", conf);
            Assert.Contains("I2 = <b 0x0405>", conf);
            Assert.Contains("I3 = <b 0x0607>", conf);
            Assert.Contains("I4 = <b 0x0809>", conf);
            Assert.Contains("I5 = <b 0x0a0b>", conf);
        }

        [Fact]
        public void BuildConfigString_Awg3And31_RenderExpectedParameters()
        {
            var apiData = CreateTestApiData();
            var settings = new WarpConfigSettings
            {
                SelectedClient = "AmneziaWG",
                IsAwg2 = false,
                IsAwg3 = true,
                Awg3Cpa = "50",
                Awg3Rkat = "60",
                Awg3Rt = "10",
                Awg3Rat = "120",
                Awg3Kt = "15",
                Awg3Mha = "5",
                IsAwg31 = true,
                Awg31RandomTrailers = true,
                Awg31DisableCookies = true
            };

            string conf = _builder.BuildConfigString(apiData, settings);

            Assert.Contains("ContentPaddingAddition = 50", conf);
            Assert.Contains("RekeyAfterTime = 60", conf);
            Assert.Contains("RekeyTimeout = 10", conf);
            Assert.Contains("RejectAfterTime = 120", conf);
            Assert.Contains("KeepaliveTimeout = 15", conf);
            Assert.Contains("MaxHandshakeAttempts = 5", conf);
            Assert.Contains("RandomTrailers = on", conf);
            Assert.Contains("DisableCookies = on", conf);
        }

        [Fact]
        public void BuildConfigString_Ipv6Disabled_FiltersIpv6DnsServers()
        {
            var apiData = CreateTestApiData();
            var settings = new WarpConfigSettings
            {
                SelectedClient = "AmneziaWG",
                SelectedDnsId = "cf", // 1.1.1.1, 1.0.0.1, 2606:4700:4700::1111, 2606:4700:4700::1001
                EnableIpv6 = false,
                IsAwg2 = false
            };

            string conf = _builder.BuildConfigString(apiData, settings);

            Assert.Contains("DNS = 1.1.1.1, 1.0.0.1", conf);
            Assert.DoesNotContain("2606:", conf);
        }

        [Fact]
        public void BuildConfigString_Ipv6Enabled_IncludesIpv6DnsAndAddress()
        {
            var apiData = CreateTestApiData();
            var settings = new WarpConfigSettings
            {
                SelectedClient = "AmneziaWG",
                SelectedDnsId = "cf",
                EnableIpv6 = true,
                IsAwg2 = false
            };

            string conf = _builder.BuildConfigString(apiData, settings);

            Assert.Contains("2606:4700:4700::1111", conf);
            Assert.Contains($"{apiData.ClientIpv4}/32, {apiData.ClientIpv6}/128", conf);
            Assert.Contains("AllowedIPs = 0.0.0.0/0, ::/0", conf);
        }

        [Fact]
        public void BuildConfigString_ExcludeLan_ReplacesAllowedIPs()
        {
            var apiData = CreateTestApiData();
            var settings = new WarpConfigSettings
            {
                SelectedClient = "AmneziaWG",
                ExcludeLan = true,
                IsAwg2 = false
            };

            string conf = _builder.BuildConfigString(apiData, settings);

            Assert.Contains($"AllowedIPs = {WarpConfigBuilderService.ExcludeLanSubnets}", conf);
        }

        [Fact]
        public void BuildConfigString_PersistentKeepalive_RendersWhenEnabled()
        {
            var apiData = CreateTestApiData();
            var settings = new WarpConfigSettings
            {
                SelectedClient = "AmneziaWG",
                PersistentKeepaliveEnabled = true,
                PersistentKeepaliveValue = "30",
                IsAwg2 = false
            };

            string conf = _builder.BuildConfigString(apiData, settings);
            Assert.Contains("PersistentKeepalive = 30", conf);

            settings.PersistentKeepaliveEnabled = false;
            string confWithout = _builder.BuildConfigString(apiData, settings);
            Assert.DoesNotContain("PersistentKeepalive", confWithout);
        }

        [Fact]
        public void BuildConfigString_ClashStandard_BuildsValidYaml()
        {
            var apiData = CreateTestApiData();
            var settings = new WarpConfigSettings
            {
                SelectedClient = "Clash",
                ClashMode = "awg",
                Endpoint = "162.159.192.1:2408",
                IsAwg1 = true,
                IsAwg2 = false
            };

            string yaml = _builder.BuildConfigString(apiData, settings);

            Assert.Contains("warp-common: &warp-common", yaml);
            Assert.Contains("type: wireguard", yaml);
            Assert.Contains("amnezia-wg-option:", yaml);
            Assert.Contains("jc: 4", yaml);
            Assert.Contains("server: 162.159.192.1", yaml);
            Assert.Contains("port: 2408", yaml);
            Assert.Contains("proxy-groups:", yaml);
            Assert.Contains("- MATCH,WARP", yaml);
        }

        [Fact]
        public void BuildConfigString_ClashMasque_BuildsMasqueProxy()
        {
            var apiData = CreateTestApiData();
            var settings = new WarpConfigSettings
            {
                SelectedClient = "Clash",
                ClashMode = "masque"
            };

            string yaml = _builder.BuildConfigString(apiData, settings);

            Assert.Contains("type: masque", yaml);
            Assert.Contains("server: 162.159.198.1", yaml);
            Assert.Contains("port: 443", yaml);
            Assert.Contains("sni: consumer-masque.cloudflareclient.com", yaml);
            Assert.Contains("\"WARP-MASQUE\"", yaml);
        }

        [Fact]
        public void BuildConfigString_ClashHybrid_BuildsFallbackGroup()
        {
            var apiData = CreateTestApiData();
            var settings = new WarpConfigSettings
            {
                SelectedClient = "Clash",
                ClashMode = "hybrid"
            };

            string yaml = _builder.BuildConfigString(apiData, settings);

            Assert.Contains("type: fallback", yaml);
            Assert.Contains("- \"WARP-AWG\"", yaml);
            Assert.Contains("- \"WARP-MASQUE\"", yaml);
        }

        [Theory]
        [InlineData("Clash", "yaml", "Clash YAML (*.yaml)|*.yaml")]
        [InlineData("clash", "yaml", "Clash YAML (*.yaml)|*.yaml")]
        [InlineData("AmneziaWG", "conf", "WireGuard Config (*.conf)|*.conf")]
        [InlineData("WireSock", "conf", "WireGuard Config (*.conf)|*.conf")]
        [InlineData(null, "conf", "WireGuard Config (*.conf)|*.conf")]
        public void GetConfigFileFormat_ReturnsExpectedFormat(string? client, string expectedExt, string expectedFilter)
        {
            var (ext, filter) = WarpConfigBuilderService.GetConfigFileFormat(client);
            Assert.Equal(expectedExt, ext);
            Assert.Equal(expectedFilter, filter);
        }

        [Theory]
        [InlineData(true, "AmneziaWG", 0, "WARP_EURO.conf")]
        [InlineData(true, "WireSock", 2, "WARP_EURO2.conf")]
        [InlineData(false, "AmneziaWG", 0, "WARP.conf")]
        [InlineData(false, "Clash", 0, "WARP.yaml")]
        [InlineData(false, "clash", 3, "WARP3.yaml")]
        public void GetConfigFileName_ReturnsExpectedName(bool isEurope, string? client, int count, string expected)
        {
            string name = WarpConfigBuilderService.GetConfigFileName(isEurope, client, count);
            Assert.Equal(expected, name);
        }
    }
}

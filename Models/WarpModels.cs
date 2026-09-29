using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WarpGenerator.Models
{
    public class WarpApiResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("privKey")]
        public string PrivKey { get; set; } = string.Empty;

        [JsonPropertyName("peer_pub")]
        public string PeerPub { get; set; } = string.Empty;

        [JsonPropertyName("client_ipv4")]
        public string ClientIpv4 { get; set; } = string.Empty;

        [JsonPropertyName("client_ipv6")]
        public string ClientIpv6 { get; set; } = string.Empty;
    }

    public class DnsOption
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string DnsServers { get; set; } = string.Empty;

        public static readonly List<DnsOption> All = new()
        {
            new DnsOption { Id = "cf", DisplayName = "1.1.1.1", DnsServers = "1.1.1.1, 1.0.0.1, 2606:4700:4700::1111, 2606:4700:4700::1001" },
            new DnsOption { Id = "google", DisplayName = "8.8.8.8", DnsServers = "8.8.8.8, 8.8.4.4, 2001:4860:4860::8888, 2001:4860:4860::8844" },
            new DnsOption { Id = "malw", DisplayName = "dns.malw.link", DnsServers = "95.216.204.218, 80.253.249.40, 2a01:4f9:c014:6dac::1, 2a12:bec4:1460:5b7::2" },
            new DnsOption { Id = "xbox", DisplayName = "xbox-dns.ru", DnsServers = "111.88.96.50, 111.88.96.51, 2a00:ab00:1233:26::50, 2a00:ab00:1233:26::51" },
            new DnsOption { Id = "geohide", DisplayName = "geohide.ru", DnsServers = "45.155.204.190, 37.230.192.51, 193.233.112.67, 193.233.112.68" },
            new DnsOption { Id = "comss", DisplayName = "dns.comss.one", DnsServers = "83.220.169.155, 212.109.195.93, 195.133.25.16, 2a01:230:4:915::2, 2a01:230:4:306::2" }
        };
    }

    public class WarpConfigSettings
    {
        public string SelectedClient { get; set; } = "AmneziaWG";
        public string ClashMode { get; set; } = "awg"; // "awg", "masque", "hybrid"
        public string SelectedDnsId { get; set; } = "cf";
        public string Endpoint { get; set; } = "engage.cloudflareclient.com:2408";
        public string Mtu { get; set; } = "1420";

        // AWG 1.0
        public bool IsAwg1 { get; set; } = false;
        public int Awg1JunkPreset { get; set; } = 1;
        public string Awg1Jc { get; set; } = "4";
        public string Awg1Jmin { get; set; } = "40";
        public string Awg1Jmax { get; set; } = "70";

        // AWG 2.0
        public bool IsAwg2 { get; set; } = true;
        public string Awg2I1 { get; set; } = "";
        public string Awg2I2 { get; set; } = "";
        public string Awg2I3 { get; set; } = "";
        public string Awg2I4 { get; set; } = "";
        public string Awg2I5 { get; set; } = "";

        // WireSock
        public string WireSockId { get; set; } = "apteka.ru";
        public string WireSockIp { get; set; } = "quic";
        public string WireSockIb { get; set; } = "curl";

        // AWG 3.0
        public bool IsAwg3 { get; set; } = false;
        public string Awg3Cpa { get; set; } = "";
        public string Awg3Mha { get; set; } = "";
        public string Awg3Kt { get; set; } = "";
        public string Awg3Rat { get; set; } = "";
        public string Awg3Rkat { get; set; } = "";
        public string Awg3Rt { get; set; } = "";

        // AWG 3.1
        public bool IsAwg31 { get; set; } = false;
        public bool Awg31RandomTrailers { get; set; } = true;
        public bool Awg31DisableCookies { get; set; } = true;

        // Split Tunneling, IPv6 & Keepalive
        public bool ExcludeLan { get; set; } = false;
        public bool EnableIpv6 { get; set; } = false;
        public bool PersistentKeepaliveEnabled { get; set; } = false;
        public string PersistentKeepaliveValue { get; set; } = "25";
    }

    public class WarpEndpointItem
    {
        public string Endpoint { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public int Port { get; set; } = 2408;
        public long PingMs { get; set; }
        public string Node { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public bool IsForeign { get; set; } = true;
    }
}


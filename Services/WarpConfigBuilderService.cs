using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WarpGenerator.Models;

namespace WarpGenerator.Services
{
    public class WarpConfigBuilderService
    {
        public const string ExcludeLanSubnets =
            "1.0.0.0/8, 2.0.0.0/7, 4.0.0.0/6, 8.0.0.0/7, 11.0.0.0/8, 12.0.0.0/6, 16.0.0.0/4, 32.0.0.0/3, 64.0.0.0/3, 96.0.0.0/4, 112.0.0.0/5, 120.0.0.0/6, 124.0.0.0/7, 126.0.0.0/8, 128.0.0.0/3, 160.0.0.0/5, 168.0.0.0/8, 169.0.0.0/9, 169.128.0.0/10, 169.192.0.0/11, 169.224.0.0/12, 169.240.0.0/13, 169.248.0.0/14, 169.252.0.0/15, 169.255.0.0/16, 170.0.0.0/7, 172.0.0.0/12, 172.32.0.0/11, 172.64.0.0/10, 172.128.0.0/9, 173.0.0.0/8, 174.0.0.0/7, 176.0.0.0/4, 192.0.0.0/9, 192.128.0.0/11, 192.160.0.0/13, 192.169.0.0/16, 192.170.0.0/15, 192.172.0.0/14, 192.176.0.0/12, 192.192.0.0/10, 193.0.0.0/8, 194.0.0.0/7, 196.0.0.0/6, 200.0.0.0/5, 208.0.0.0/4, 224.0.0.0/4, ::/1, 8000::/2, c000::/3, e000::/4, f000::/5, f800::/6, fe00::/9, fec0::/10, ff00::/8";

        public string BuildConfigString(WarpApiResponse apiData, WarpConfigSettings settings)
        {
            if (apiData == null) return string.Empty;

            bool isClash = settings.SelectedClient.Equals("Clash", StringComparison.OrdinalIgnoreCase);
            bool isWiresock = settings.SelectedClient.Equals("WireSock", StringComparison.OrdinalIgnoreCase);
            string mtuVal = string.IsNullOrWhiteSpace(settings.Mtu) ? "1420" : settings.Mtu.Trim();

            // DNS servers
            var dnsOpt = DnsOption.All.FirstOrDefault(d => d.Id.Equals(settings.SelectedDnsId, StringComparison.OrdinalIgnoreCase))
                         ?? DnsOption.All[0];
            string dnsServers = dnsOpt.DnsServers;
            if (!settings.EnableIpv6)
            {
                var dnsList = dnsServers.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(ip => !ip.Contains(':'));
                dnsServers = string.Join(", ", dnsList);
            }

            // Address
            string address = settings.EnableIpv6 && !string.IsNullOrWhiteSpace(apiData.ClientIpv6)
                ? $"{apiData.ClientIpv4}/32, {apiData.ClientIpv6}/128"
                : $"{apiData.ClientIpv4}/32";

            // Endpoint (2408 standard working port)
            string endpoint = string.IsNullOrWhiteSpace(settings.Endpoint)
                ? "engage.cloudflareclient.com:2408"
                : settings.Endpoint.Trim();

            // --- AWG 1.0 ---
            string jc = "4", jmin = "40", jmax = "70";
            var interfaceOptions = new StringBuilder();

            if (settings.IsAwg1)
            {
                if (settings.Awg1JunkPreset == 1)
                {
                    jc = "4"; jmin = "40"; jmax = "70";
                }
                else if (settings.Awg1JunkPreset == 2)
                {
                    jc = "30"; jmin = "10"; jmax = "30";
                }
                else if (settings.Awg1JunkPreset == 3)
                {
                    jc = string.IsNullOrWhiteSpace(settings.Awg1Jc) ? "128" : settings.Awg1Jc.Trim();
                    jmin = string.IsNullOrWhiteSpace(settings.Awg1Jmin) ? "1279" : settings.Awg1Jmin.Trim();
                    jmax = string.IsNullOrWhiteSpace(settings.Awg1Jmax) ? "1280" : settings.Awg1Jmax.Trim();
                }

                interfaceOptions.Append($"\nS1 = 0\nS2 = 0\nS3 = 0\nS4 = 0\nJc = {jc}\nJmin = {jmin}\nJmax = {jmax}\nH1 = 1\nH2 = 2\nH3 = 3\nH4 = 4");
            }

            // --- AWG 2.0 ---
            if (settings.IsAwg2)
            {
                if (!isWiresock)
                {
                    string i1 = string.IsNullOrWhiteSpace(settings.Awg2I1) ? CryptoService.GetRandomAwg2I1() : settings.Awg2I1.Trim();
                    interfaceOptions.Append($"\nI1 = {i1}");
                    if (!string.IsNullOrWhiteSpace(settings.Awg2I2)) interfaceOptions.Append($"\nI2 = {settings.Awg2I2.Trim()}");
                    if (!string.IsNullOrWhiteSpace(settings.Awg2I3)) interfaceOptions.Append($"\nI3 = {settings.Awg2I3.Trim()}");
                    if (!string.IsNullOrWhiteSpace(settings.Awg2I4)) interfaceOptions.Append($"\nI4 = {settings.Awg2I4.Trim()}");
                    if (!string.IsNullOrWhiteSpace(settings.Awg2I5)) interfaceOptions.Append($"\nI5 = {settings.Awg2I5.Trim()}");
                }
                else
                {
                    string idVal = string.IsNullOrWhiteSpace(settings.WireSockId) ? "apteka.ru" : settings.WireSockId.Trim();
                    string ipVal = string.IsNullOrWhiteSpace(settings.WireSockIp) ? "quic" : settings.WireSockIp.Trim();
                    string ibVal = string.IsNullOrWhiteSpace(settings.WireSockIb) ? "curl" : settings.WireSockIb.Trim();

                    interfaceOptions.Append($"\nId = {idVal}");
                    interfaceOptions.Append($"\nIp = {ipVal}");
                    interfaceOptions.Append($"\nIb = {ibVal}");
                }
            }

            // --- AWG 3.0 ---
            string cpa = settings.Awg3Cpa?.Trim() ?? "";
            string rkat = settings.Awg3Rkat?.Trim() ?? "";
            string rt = settings.Awg3Rt?.Trim() ?? "";
            string rat = settings.Awg3Rat?.Trim() ?? "";
            string kt = settings.Awg3Kt?.Trim() ?? "";
            string mha = settings.Awg3Mha?.Trim() ?? "";

            if (settings.IsAwg3)
            {
                if (!string.IsNullOrEmpty(cpa)) interfaceOptions.Append($"\nContentPaddingAddition = {cpa}");
                if (!string.IsNullOrEmpty(rkat)) interfaceOptions.Append($"\nRekeyAfterTime = {rkat}");
                if (!string.IsNullOrEmpty(rt)) interfaceOptions.Append($"\nRekeyTimeout = {rt}");
                if (!string.IsNullOrEmpty(rat)) interfaceOptions.Append($"\nRejectAfterTime = {rat}");
                if (!string.IsNullOrEmpty(kt)) interfaceOptions.Append($"\nKeepaliveTimeout = {kt}");
                if (!string.IsNullOrEmpty(mha)) interfaceOptions.Append($"\nMaxHandshakeAttempts = {mha}");
            }

            // --- AWG 3.1 ---
            if (settings.IsAwg31)
            {
                if (settings.Awg31RandomTrailers) interfaceOptions.Append("\nRandomTrailers = on");
                if (settings.Awg31DisableCookies) interfaceOptions.Append("\nDisableCookies = on");
            }

            // --- CLASH (YAML) ---
            if (isClash)
            {
                string host = "engage.cloudflareclient.com";
                string port = "2408";
                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    int colonIndex = endpoint.LastIndexOf(':');
                    if (colonIndex > 0)
                    {
                        host = endpoint[..colonIndex];
                        port = endpoint[(colonIndex + 1)..];
                    }
                    else
                    {
                        host = endpoint;
                    }
                }

                var awgOptionsYaml = new StringBuilder();
                if (settings.IsAwg1)
                {
                    awgOptionsYaml.Append($"\n   s1: 0\n   s2: 0\n   s3: 0\n   s4: 0\n   jc: {jc}\n   jmin: {jmin}\n   jmax: {jmax}\n   h1: 1\n   h2: 2\n   h3: 3\n   h4: 4");
                }
                if (settings.IsAwg2)
                {
                    string i1 = string.IsNullOrWhiteSpace(settings.Awg2I1) ? CryptoService.GetRandomAwg2I1() : settings.Awg2I1.Trim();
                    awgOptionsYaml.Append($"\n   i1: \"{i1}\"");
                    if (!string.IsNullOrWhiteSpace(settings.Awg2I2)) awgOptionsYaml.Append($"\n   i2: \"{settings.Awg2I2.Trim()}\"");
                    if (!string.IsNullOrWhiteSpace(settings.Awg2I3)) awgOptionsYaml.Append($"\n   i3: \"{settings.Awg2I3.Trim()}\"");
                    if (!string.IsNullOrWhiteSpace(settings.Awg2I4)) awgOptionsYaml.Append($"\n   i4: \"{settings.Awg2I4.Trim()}\"");
                    if (!string.IsNullOrWhiteSpace(settings.Awg2I5)) awgOptionsYaml.Append($"\n   i5: \"{settings.Awg2I5.Trim()}\"");
                }
                if (settings.IsAwg3)
                {
                    if (!string.IsNullOrEmpty(cpa)) awgOptionsYaml.Append($"\n   content-padding-addition: {cpa}");
                    if (!string.IsNullOrEmpty(rkat)) awgOptionsYaml.Append($"\n   rekey-after-time: {rkat}");
                    if (!string.IsNullOrEmpty(rt)) awgOptionsYaml.Append($"\n   rekey-timeout: {rt}");
                    if (!string.IsNullOrEmpty(rat)) awgOptionsYaml.Append($"\n   reject-after-time: {rat}");
                    if (!string.IsNullOrEmpty(kt)) awgOptionsYaml.Append($"\n   keepalive-timeout: {kt}");
                    if (!string.IsNullOrEmpty(mha)) awgOptionsYaml.Append($"\n   max-handshake-attempts: {mha}");
                }
                if (settings.IsAwg31)
                {
                    if (settings.Awg31RandomTrailers) awgOptionsYaml.Append("\n   random-trailers: true");
                    if (settings.Awg31DisableCookies) awgOptionsYaml.Append("\n   disable-cookies: true");
                }

                string amneziaBlock = awgOptionsYaml.Length > 0 ? $"\n  amnezia-wg-option:{awgOptionsYaml}" : "";

                var dnsItems = dnsServers.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string dnsYaml = string.Join(", ", dnsItems);

                string allowedIpsYaml = settings.ExcludeLan
                    ? "[" + string.Join(", ", ExcludeLanSubnets.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(s => $"'{s}'")) + "]"
                    : (settings.EnableIpv6 ? "['0.0.0.0/0', '::/0']" : "['0.0.0.0/0']");

                string ipv6Line = settings.EnableIpv6 && !string.IsNullOrWhiteSpace(apiData.ClientIpv6)
                    ? $"\n  ipv6: {apiData.ClientIpv6}"
                    : "";

                if (settings.ClashMode == "masque")
                {
                    return $@"proxies:
- name: ""WARP-MASQUE""
  type: masque
  server: 162.159.198.1
  port: 443
  sni: consumer-masque.cloudflareclient.com
  ip: {apiData.ClientIpv4}{ipv6Line}
  private-key: {apiData.PrivKey}
  public-key: {apiData.PeerPub}
  udp: true
  mtu: {mtuVal}
  remote-dns-resolve: true
  dns: [{dnsYaml}]

proxy-groups:
- name: WARP
  type: select
  icon: https://www.vectorlogo.zone/logos/cloudflare/cloudflare-icon.svg
  proxies:
    - ""WARP-MASQUE""
  url: 'http://speed.cloudflare.com/'
  interval: 300
rules:
- MATCH,WARP";
                }

                if (settings.ClashMode == "hybrid")
                {
                    return $@"warp-common: &warp-common
  type: wireguard
  ip: {apiData.ClientIpv4}{ipv6Line}
  private-key: {apiData.PrivKey}
  public-key: {apiData.PeerPub}
  allowed-ips: {allowedIpsYaml}
  udp: true
  mtu: {mtuVal}
  remote-dns-resolve: true
  dns: [{dnsYaml}]{amneziaBlock}

proxies:
- name: ""WARP-AWG""
  <<: *warp-common
  server: {host}
  port: {port}

- name: ""WARP-MASQUE""
  type: masque
  server: 162.159.198.1
  port: 443
  sni: consumer-masque.cloudflareclient.com
  ip: {apiData.ClientIpv4}{ipv6Line}
  private-key: {apiData.PrivKey}
  public-key: {apiData.PeerPub}
  udp: true
  mtu: {mtuVal}
  remote-dns-resolve: true
  dns: [{dnsYaml}]

proxy-groups:
- name: WARP
  type: fallback
  icon: https://www.vectorlogo.zone/logos/cloudflare/cloudflare-icon.svg
  proxies:
    - ""WARP-AWG""
    - ""WARP-MASQUE""
  url: 'http://speed.cloudflare.com/'
  interval: 300
rules:
- MATCH,WARP";
                }

                return $@"warp-common: &warp-common
  type: wireguard
  ip: {apiData.ClientIpv4}{ipv6Line}
  private-key: {apiData.PrivKey}
  public-key: {apiData.PeerPub}
  allowed-ips: {allowedIpsYaml}
  udp: true
  mtu: {mtuVal}
  remote-dns-resolve: true
  dns: [{dnsYaml}]{amneziaBlock}

proxies:
- name: ""WARP""
  <<: *warp-common
  server: {host}
  port: {port}

proxy-groups:
- name: WARP
  type: select
  icon: https://www.vectorlogo.zone/logos/cloudflare/cloudflare-icon.svg
  proxies:
    - ""WARP""
  url: 'http://speed.cloudflare.com/'
  interval: 300
rules:
- MATCH,WARP";
            }

            // --- STANDARD WIREGUARD / AMNEZIAWG / WIRESOCK (.conf) ---
            string allowedIPs;
            if (settings.ExcludeLan)
            {
                allowedIPs = ExcludeLanSubnets;
            }
            else
            {
                allowedIPs = settings.EnableIpv6 ? "0.0.0.0/0, ::/0" : "0.0.0.0/0";
            }

            string peerOptions = "";
            if (settings.PersistentKeepaliveEnabled)
            {
                string pkVal = string.IsNullOrWhiteSpace(settings.PersistentKeepaliveValue) ? "25" : settings.PersistentKeepaliveValue.Trim();
                peerOptions = $"\nPersistentKeepalive = {pkVal}";
            }

            return $@"[Interface]
PrivateKey = {apiData.PrivKey}
Address = {address}
DNS = {dnsServers}
MTU = {mtuVal}{interfaceOptions}

[Peer]
# Cloudflare WARP
PublicKey = {apiData.PeerPub}
Endpoint = {endpoint}
AllowedIPs = {allowedIPs}{peerOptions}";
        }
    }
}

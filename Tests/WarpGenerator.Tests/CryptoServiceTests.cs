using System;
using System.Linq;
using WarpGenerator.Services;
using Xunit;

namespace WarpGenerator.Tests
{
    public class CryptoServiceTests
    {
        [Fact]
        public void GenerateRandomAwg1_ProducesParametersInValidRanges()
        {
            for (int i = 0; i < 50; i++)
            {
                var (jc, jmin, jmax) = CryptoService.GenerateRandomAwg1();
                Assert.InRange(jc, 1, 100);
                Assert.InRange(jmin, 1, 200);
                Assert.True(jmax > jmin, $"jmax ({jmax}) must be greater than jmin ({jmin})");
                Assert.InRange(jmax, 2, 201);
            }
        }

        [Fact]
        public void GenerateRandomAwg3_ProducesValidRangeStrings()
        {
            void AssertRange(string rangeStr, int expectedMinLow, int expectedMaxHigh)
            {
                Assert.Contains('-', rangeStr);
                var parts = rangeStr.Split('-');
                Assert.Equal(2, parts.Length);
                Assert.True(int.TryParse(parts[0], out int minVal));
                Assert.True(int.TryParse(parts[1], out int maxVal));
                Assert.True(minVal <= maxVal, $"minVal {minVal} must be <= maxVal {maxVal}");
                Assert.InRange(minVal, expectedMinLow, expectedMaxHigh);
                Assert.InRange(maxVal, expectedMinLow, expectedMaxHigh);
            }

            for (int i = 0; i < 50; i++)
            {
                var (cpa, mha, kt, rat, rkat, rt) = CryptoService.GenerateRandomAwg3();

                AssertRange(cpa, 5, 110);
                AssertRange(mha, 5, 40);
                AssertRange(kt, 5, 25);
                AssertRange(rat, 50, 200);
                AssertRange(rkat, 50, 150);
                AssertRange(rt, 3, 15);
            }
        }

        [Fact]
        public void GenerateRandomEuropeEndpoint_ReturnsValidIpAndPort()
        {
            for (int i = 0; i < 30; i++)
            {
                string ep = CryptoService.GenerateRandomEuropeEndpoint();
                Assert.NotNull(ep);
                Assert.Contains(':', ep);

                var parts = ep.Split(':');
                Assert.Equal(2, parts.Length);

                string ip = parts[0];
                int port = int.Parse(parts[1]);

                bool valid = CryptoService.EuropePrefixes.Any(p => ip.StartsWith(p)) ||
                             CryptoService.EuropeEndpoints.Any(e => e.StartsWith(ip));
                Assert.True(valid, $"Endpoint {ep} is not from European list or prefixes");

                Assert.Contains(port, CryptoService.WarpPorts.Concat(CryptoService.EuropePorts));
            }
        }

        [Fact]
        public void GenerateRandomAnycastEndpoint_ReturnsValidFormatAndPort()
        {
            for (int i = 0; i < 30; i++)
            {
                string ep = CryptoService.GenerateRandomAnycastEndpoint();
                Assert.NotNull(ep);
                Assert.Contains(':', ep);

                var parts = ep.Split(':');
                Assert.Equal(2, parts.Length);

                string ip = parts[0];
                int port = int.Parse(parts[1]);

                bool valid = CryptoService.AnycastPrefixes.Any(p => ip.StartsWith(p)) ||
                             CryptoService.StandardEndpoints.Any(e => e.StartsWith(ip)) ||
                             ip.Contains("cloudflareclient.com");
                Assert.True(valid, $"Endpoint {ep} is not Anycast");

                Assert.Contains(port, CryptoService.WarpPorts);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GenerateRandomEndpoint_WithEuropeFlag_ReturnsNonEmpty(bool europe)
        {
            string ep = CryptoService.GenerateRandomEndpoint(europe);
            Assert.False(string.IsNullOrWhiteSpace(ep));
            Assert.Contains(':', ep);
        }

        [Fact]
        public void GetRandomAwg2I1_ReturnsOneOfPredefined()
        {
            for (int i = 0; i < 20; i++)
            {
                string i1 = CryptoService.GetRandomAwg2I1();
                Assert.Contains(i1, CryptoService.PredefinedAwg2I1);
            }
        }

        [Fact]
        public void GetRandomWireSockDomain_ReturnsKnownDomain()
        {
            for (int i = 0; i < 50; i++)
            {
                string domain = CryptoService.GetRandomWireSockDomain();
                Assert.Contains(domain, CryptoService.WireSockDomains);
            }
        }

        [Fact]
        public void PredefinedVariants_StartAndEndWithCorrectByteNotation()
        {
            var variants = new[]
            {
                CryptoService.Variant1Quic,
                CryptoService.Variant2Tls,
                CryptoService.Variant3SipI1,
                CryptoService.Variant3SipI2
            };

            foreach (var v in variants)
            {
                Assert.False(string.IsNullOrWhiteSpace(v));
                Assert.StartsWith("<b 0x", v);
                Assert.EndsWith(">", v);
            }
        }
    }
}

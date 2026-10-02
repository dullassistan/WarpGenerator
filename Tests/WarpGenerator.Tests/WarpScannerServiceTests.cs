using System.Collections.Generic;
using WarpGenerator.Models;
using WarpGenerator.Services;
using Xunit;

namespace WarpGenerator.Tests
{
    public class WarpScannerServiceTests
    {
        [Theory]
        [InlineData("Starting Phase 1 scan...", "⏳ Фаза 1: поиск доступных портов WARP...")]
        [InlineData("Running Phase 2 tunnel check", "⏳ Фаза 2: проверка туннелей (реальное соединение)...")]
        [InlineData("Some random output line", null)]
        public void MapProgress_MapsExpectedPhrases(string input, string? expected)
        {
            string? result = WarpScannerService.MapProgress(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ParseTable_ParsesWarpscoutOutputCorrectly()
        {
            var lines = new List<string>
            {
                "┌─────┬──────────────────────────┬──────────┬─────────┬──────────┬──────────┐",
                "│ #   │ Endpoint                 │ Ping     │ Country │ City     │ ISP      │",
                "├─────┼──────────────────────────┼──────────┼─────────┼──────────┼──────────┤",
                "│ 1   │ 162.159.192.1:2408       │ 35 ms    │ DE      │ FRA      │ CF       │",
                "│ 2   │ 162.159.193.10:500       │ 42 ms    │ RU      │ MOW      │ Rostel   │",
                "│ 3   │ 162.159.195.5:1000       │ 65 ms    │ FI      │ HEL      │ Telia    │",
                "└─────┴──────────────────────────┴──────────┴─────────┴──────────┴──────────┘"
            };

            List<WarpEndpointItem> items = WarpScannerService.ParseTable(lines);

            Assert.Equal(3, items.Count);

            // Item 1
            Assert.Equal("162.159.192.1:2408", items[0].Endpoint);
            Assert.Equal("162.159.192.1", items[0].Ip);
            Assert.Equal(2408, items[0].Port);
            Assert.Equal(35, items[0].PingMs);
            Assert.True(items[0].IsForeign);
            Assert.Equal("FRA (CF)", items[0].Location);

            // Item 2 (RU)
            Assert.Equal("162.159.193.10:500", items[1].Endpoint);
            Assert.Equal(42, items[1].PingMs);
            Assert.False(items[1].IsForeign);

            // Item 3 (FI)
            Assert.Equal("162.159.195.5:1000", items[2].Endpoint);
            Assert.Equal(65, items[2].PingMs);
            Assert.True(items[2].IsForeign);
        }

        [Fact]
        public void ParseTable_IgnoresInvalidOrHeaderRows()
        {
            var lines = new List<string>
            {
                "Random text without borders",
                "│ Header line │ Not an endpoint │ invalid ping │",
                "│ 1 │ not-an-ip:9999 │ 50 ms │ DE │ FRA │ CF │"
            };

            List<WarpEndpointItem> items = WarpScannerService.ParseTable(lines);
            Assert.Empty(items);
        }
    }
}

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Tests.Ingestion;

public sealed class AdSpendReportParserTests
{
    private const string Report = """
        [{"date":"2026-10-01","advertisedSku":" A-1 ","advertisedAsin":"B01","campaignId":310000000001,"campaignName":"Auto",
          "impressions":120,"clicks":3,"cost":1.47,"sales7d":29.99,"purchases7d":1,"unitsSoldClicks7d":1,"campaignBudgetCurrencyCode":"usd"}]
        """;

    [Fact]
    public async Task ParseAsync_GzipReport_ReadsEveryColumn()
    {
        var rows = await AdSpendReportParser.ParseAsync(Gzip(Report), CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(
            new AdSpendRow(new DateOnly(2026, 10, 1), "A-1", "B01", "310000000001", "Auto", 120, 3, 1.47m, 29.99m, 1, 1, "USD"),
            row);
    }

    [Fact]
    public async Task ParseAsync_PlainJson_ReadsTheSame()
    {
        var rows = await AdSpendReportParser.ParseAsync(new MemoryStream(Encoding.UTF8.GetBytes(Report)), CancellationToken.None);

        Assert.Equal("A-1", Assert.Single(rows).Sku);
    }

    [Fact]
    public async Task ParseAsync_AdGroupsOfOneCampaign_AreSummedPerSkuAndDay()
    {
        var json = """
            [{"date":"2026-10-01","advertisedSku":"A-1","campaignId":"31","clicks":2,"cost":"1.00","sales7d":10},
             {"date":"2026-10-01","advertisedSku":"a-1","campaignId":"31","clicks":1,"cost":0.50,"sales7d":null},
             {"date":"2026-10-01","advertisedSku":"A-1","campaignId":"32","clicks":4,"cost":2.00},
             {"date":"2026-10-02","advertisedSku":"A-1","campaignId":"31","clicks":1,"cost":0.25}]
            """;

        var rows = await AdSpendReportParser.ParseAsync(Gzip(json), CancellationToken.None);

        Assert.Equal(3, rows.Count);
        var merged = Assert.Single(rows, r => r.Date == new DateOnly(2026, 10, 1) && r.CampaignId == "31");
        Assert.Equal((3L, 1.50m, 10m), (merged.Clicks, merged.Cost, merged.AttributedSales));
    }

    [Fact]
    public async Task ParseAsync_RowsWithoutSkuDateOrCampaign_AreDropped()
    {
        var json = """
            [{"date":"2026-10-01","campaignId":31,"cost":1},
             {"date":"not a date","advertisedSku":"A-1","campaignId":31,"cost":1},
             {"date":"2026-10-01","advertisedSku":"A-1","cost":1},
             {"date":"2026-10-01","advertisedSku":"A-2","campaignId":31,"cost":1}]
            """;

        var rows = await AdSpendReportParser.ParseAsync(Gzip(json), CancellationToken.None);

        Assert.Equal("A-2", Assert.Single(rows).Sku);
    }

    [Fact]
    public async Task ParseAsync_NotAnArray_Throws()
    {
        await Assert.ThrowsAsync<JsonException>(() => AdSpendReportParser.ParseAsync(Gzip("""{"code":"error"}"""), CancellationToken.None));
    }

    private static MemoryStream Gzip(string json)
    {
        var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes(json));
        }

        buffer.Position = 0;
        return buffer;
    }
}

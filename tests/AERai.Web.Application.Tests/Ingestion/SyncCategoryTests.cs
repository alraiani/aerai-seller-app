using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Tests.Ingestion;

public sealed class SyncCategoryTests
{
    [Theory]
    [InlineData(AmazonReportType.Orders, SyncCategory.Orders)]
    [InlineData(AmazonReportType.OrdersApi, SyncCategory.Orders)]
    [InlineData(AmazonReportType.FbaInventory, SyncCategory.Inventory)]
    [InlineData(AmazonReportType.FbaReservedInventory, SyncCategory.Inventory)]
    [InlineData(AmazonReportType.RestockRecommendations, SyncCategory.Inventory)]
    [InlineData(AmazonReportType.AwdInventory, SyncCategory.Inventory)]
    [InlineData(AmazonReportType.InboundShipments, SyncCategory.Inventory)]
    [InlineData(AmazonReportType.Settlements, SyncCategory.SettlementsAndPayments)]
    [InlineData(AmazonReportType.DeferredTransactions, SyncCategory.SettlementsAndPayments)]
    [InlineData(AmazonReportType.AdsSpend, SyncCategory.Advertising)]
    public void Category_GroupsEachReportByItsData(AmazonReportType type, SyncCategory expected) =>
        Assert.Equal(expected, type.Category());

    [Fact]
    public void Category_EveryReportTypeHasADefinedCategory() =>
        Assert.All(Enum.GetValues<AmazonReportType>(), t => Assert.True(Enum.IsDefined(t.Category())));
}

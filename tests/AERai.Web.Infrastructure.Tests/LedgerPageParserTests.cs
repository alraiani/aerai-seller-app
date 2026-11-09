using AERai.Web.Application.Ingestion;
using AERai.Web.Infrastructure.SpApi;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>Flattening Amazon's <c>listTransactions</c> pages into ledger lines that add up to each total.</summary>
public sealed class LedgerPageParserTests
{
    private const string Page = """
        {
          "payload": {
            "nextToken": "abc",
            "transactions": [
              {
                "transactionId": "T-1",
                "transactionType": "Shipment",
                "transactionStatus": "DEFERRED",
                "description": "Order Payment",
                "postedDate": "2026-10-05T14:30:00Z",
                "totalAmount": { "currencyCode": "USD", "currencyAmount": 13.30 },
                "relatedIdentifiers": [
                  { "relatedIdentifierName": "ORDER_ID", "relatedIdentifierValue": "111-1" },
                  { "relatedIdentifierName": "SETTLEMENT_ID", "relatedIdentifierValue": "2400" }
                ],
                "breakdowns": [ { "breakdownType": "Sales", "breakdownAmount": { "currencyCode": "USD", "currencyAmount": 99 } } ],
                "items": [
                  {
                    "totalAmount": { "currencyCode": "USD", "currencyAmount": 13.30 },
                    "breakdowns": [
                      { "breakdownType": "ProductCharges", "breakdownAmount": { "currencyCode": "USD", "currencyAmount": 20.00 },
                        "breakdowns": [
                          { "breakdownType": "Principal", "breakdownAmount": { "currencyCode": "USD", "currencyAmount": 19.00 } },
                          { "breakdownType": "Tax", "breakdownAmount": { "currencyCode": "USD", "currencyAmount": 0.50 } }
                        ] },
                      { "breakdownType": "AmazonFees", "breakdownAmount": { "currencyCode": "USD", "currencyAmount": -6.20 },
                        "breakdowns": { "breakdowns": [
                          { "breakdownType": "Commission", "breakdownAmount": { "currencyCode": "USD", "currencyAmount": -3.00 } },
                          { "breakdownType": "FBAPerUnitFulfillmentFee", "breakdownAmount": { "currencyCode": "USD", "currencyAmount": -3.20 } }
                        ] } }
                    ],
                    "contexts": [ { "contextType": "ProductContext", "sku": " SKU-1 ", "asin": "B0001", "quantityShipped": 2 } ]
                  }
                ]
              },
              {
                "transactionId": "T-2",
                "transactionType": "ServiceFee",
                "postedDate": "2026-10-06T00:00:00Z",
                "totalAmount": { "currencyCode": "usd", "currencyAmount": -39.99 }
              },
              { "transactionId": "T-3", "totalAmount": { "currencyCode": "USD", "currencyAmount": 1 } }
            ]
          }
        }
        """;

    [Fact]
    public void Parse_NestedBreakdowns_KeepsLeavesWithTheirProductAndTheRestUnitemized()
    {
        var page = LedgerPageParser.Parse(Page);

        Assert.Equal("abc", page.NextToken);
        var shipment = page.Items[0];
        Assert.Equal(("T-1", "DEFERRED", "111-1", "2400", 13.30m, "USD"), (shipment.TransactionId, shipment.Status, shipment.AmazonOrderId, shipment.SettlementId, shipment.Amount, shipment.Currency));

        // The transaction-level "Sales" repeats its items, so items win; ProductCharges' children are 0.50 short of it,
        // and the item's leaves are 0.70 short of its total: both kept, so the lines equal the total.
        Assert.Equal(
            [
                ("SKU-1", 2, "ProductCharges/Principal", 19.00m),
                ("SKU-1", 0, "ProductCharges/Tax", 0.50m),
                ("SKU-1", 0, "ProductCharges", 0.50m),
                ("SKU-1", 0, "AmazonFees/Commission", -3.00m),
                ("SKU-1", 0, "AmazonFees/FBAPerUnitFulfillmentFee", -3.20m),
                ("SKU-1", 0, AmazonFinancialLine.Unitemized, -0.50m),
            ],
            shipment.Lines.Select(l => (l.Sku, l.Units, l.BreakdownPath, l.Amount)));
        Assert.Equal(shipment.Amount, shipment.Lines.Sum(l => l.Amount));
        Assert.All(shipment.Lines, l => Assert.Equal("B0001", l.Asin));
    }

    [Fact]
    public void Parse_TotalWithoutBreakdowns_IsOneUnitemizedLine()
    {
        var fee = LedgerPageParser.Parse(Page).Items[1];

        Assert.Equal("USD", fee.Currency);
        var line = Assert.Single(fee.Lines);
        Assert.Equal((null, AmazonFinancialLine.Unitemized, -39.99m), (line.Sku, line.BreakdownPath, line.Amount));
    }

    [Fact]
    public void Parse_TransactionWithoutAPostedDate_IsSkipped()
    {
        Assert.DoesNotContain(LedgerPageParser.Parse(Page).Items, t => t.TransactionId == "T-3");
    }
}

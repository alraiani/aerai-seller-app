using AERai.Web.Application.Inventory;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class RestockRulesTests
{
    private static readonly RestockRules Handkerchiefs = new(5, 10, 50, 100, 10);

    [Theory]
    [InlineData(12, 500, 10, 10)] // Nearest 5.
    [InlineData(13, 500, 15, 15)]
    [InlineData(2, 500, 5, 5)] // At least one multiple when anything is wanted.
    [InlineData(54, 500, 50, 50)] // From 50 up, nearest 10.
    [InlineData(56, 500, 60, 60)]
    [InlineData(0, 500, 0, 0)]
    public void RoundSend_RoundsToHowItShips(int wanted, int atHome, int send, int rounded) =>
        Assert.Equal((send, rounded), Handkerchiefs.RoundSend(wanted, atHome));

    [Theory]
    [InlineData(60, 57, 50)] // Fits home stock, keeping 10s from 50 up.
    [InlineData(40, 23, 20)] // Below 50, 5s.
    [InlineData(10, 3, 3)] // Less than one multiple at home: all of it.
    public void RoundSend_CappedByHomeStock_RoundsDown(int wanted, int atHome, int send) =>
        Assert.Equal(send, Handkerchiefs.RoundSend(wanted, atHome).Send);

    [Theory]
    [InlineData(37, 100)] // The minimum batch.
    [InlineData(143, 150)] // Up to the next 10.
    [InlineData(0, 0)]
    public void RoundOrder_AtLeastTheMinimumInMultiples(int units, int expected) =>
        Assert.Equal(expected, Handkerchiefs.RoundOrder(units));

    [Fact]
    public void None_LeavesQuantitiesAlone()
    {
        Assert.Equal((13, 13), RestockRules.None.RoundSend(13, 100));
        Assert.Equal((7, 13), RestockRules.None.RoundSend(13, 7));
        Assert.Equal(37, RestockRules.None.RoundOrder(37));
    }
}

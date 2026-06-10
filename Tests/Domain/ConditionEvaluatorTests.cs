using System.Collections.Generic;
using Xunit;
using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Tests.Domain;

public class ConditionEvaluatorTests
{
    [Theory]
    [InlineData(null, 100, new int[] { }, true)]
    [InlineData("", 100, new int[] { }, true)]
    [InlineData("<TOTAL_ORDER> > 50", 100, new int[] { }, true)]
    [InlineData("<TOTAL_ORDER> > 50", 30, new int[] { }, false)]
    [InlineData("<GAME_CATEGORY> = 10", 100, new int[] { 10 }, true)]
    [InlineData("<GAME_CATEGORY> = 10", 100, new int[] { 5, 8 }, false)]
    [InlineData("<TOTAL_ORDER> > 50 AND <GAME_CATEGORY> = 10", 75, new int[] { 10 }, true)]
    [InlineData("<TOTAL_ORDER> > 50 AND <GAME_CATEGORY> = 10", 40, new int[] { 10 }, false)]
    [InlineData("(<TOTAL_ORDER> > 50 AND <GAME_CATEGORY> = 10) OR <TOTAL_ORDER> > 200", 250, new int[] { 5 }, true)]
    [InlineData("<TOTAL_ORDER> >= 50 AND <TOTAL_ORDER> <= 150", 100, new int[] { }, true)]
    [InlineData("<TOTAL_ORDER> >= 50 AND <TOTAL_ORDER> <= 150", 49.99, new int[] { }, false)]
    [InlineData("<TOTAL_ORDER> > 50.12.34", 100, new int[] { }, false)]
    [InlineData("(<TOTAL_ORDER> > 50", 100, new int[] { }, false)]
    [InlineData("<MY_TAG> = 10", 100, new int[] { 10 }, false)]
    [InlineData("<TOTAL_ORDER> > 50 )", 100, new int[] { }, false)]
    [InlineData("<GAME_CATEGORY> > 10", 100, new int[] { 10 }, false)]
    [InlineData("<TOTAL_ORDER> > 50 @", 100, new int[] { }, false)]
    [InlineData("<TOTAL_ORDER", 100, new int[] { }, false)]
    public void Evaluate_ShouldMatchExpectedResult(string? condicao, decimal totalPedido, int[] categorias, bool expected)
    {
        var cats = new List<int>(categorias);
        bool result = ConditionEvaluator.Evaluate(condicao, totalPedido, cats);
        Assert.Equal(expected, result);
    }
}

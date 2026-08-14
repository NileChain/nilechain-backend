using NileChain.Domain.Common;

namespace NileChain.Tests;

public class ContractTermDatesTests
{
    [Fact]
    public void FillPlaceholders_ReplacesArabicBrackets()
    {
        var text =
            """
            تاريخ بدء العقد: [تاريخ بدء العقد]
            تاريخ انتهاء العقد: [تاريخ انتهاء العقد]
            """;

        var start = new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
        var filled = ContractTermDates.FillPlaceholders(text, start, end)!;

        Assert.DoesNotContain("[تاريخ بدء العقد]", filled, StringComparison.Ordinal);
        Assert.DoesNotContain("[تاريخ انتهاء العقد]", filled, StringComparison.Ordinal);
        Assert.Contains("تاريخ بدء العقد:", filled, StringComparison.Ordinal);
        Assert.Contains("تاريخ انتهاء العقد:", filled, StringComparison.Ordinal);
        Assert.Contains(ContractTermDates.FormatDate(start), filled, StringComparison.Ordinal);
        Assert.Contains(ContractTermDates.FormatDate(end), filled, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyOnFullSign_SetsStartToSignedAtAndEndToDelivery()
    {
        var start = new DateTime(2026, 8, 13, 15, 30, 0, DateTimeKind.Utc);
        var delivery = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        var contract = new NileChain.Domain.Entities.Contract
        {
            FactorySignedAt = start.AddHours(-1),
            FarmSignedAt = start,
            GeneratedText = "تاريخ بدء العقد: [تاريخ بدء العقد]\nتاريخ انتهاء العقد: [تاريخ انتهاء العقد]"
        };
        contract.RefreshSignatureStatus();

        ContractTermDates.ApplyOnFullSign(contract, delivery);

        Assert.Equal(start, contract.SignedAt);
        Assert.Equal(ContractTermDates.NormalizeDate(start), contract.StartsAt);
        Assert.Equal(ContractTermDates.NormalizeDate(delivery), contract.EndsAt);
        Assert.DoesNotContain("[تاريخ", contract.GeneratedText, StringComparison.Ordinal);
    }
}

using NileChain.AI.Contracts;
using NileChain.AI.Models;
using NileChain.AI.Plugins;

namespace NileChain.Tests;

public class StructuredContractDraftTests
{
    private static readonly AgentRequest Deal = new()
    {
        CropType = "قمح",
        QuantityTons = 80,
        PricePerTon = 12500,
        QualitySpecs = "Moisture ≤ 12%",
        DeliveryDate = new DateTime(2026, 9, 11),
        DeliveryPoint = "FactoryGate",
        FreightPayer = "Farm",
        TransitRisk = "Farm"
    };

    private static ContractFacts Facts() =>
        ContractFacts.From(Deal, "مزرعة النيل", "مصنع الدلتا");

    private static string ClauseJson(string body) =>
        $$"""
        {
          "subject": "{{body}}",
          "obligations": "{{body}}"
        }
        """;

    // A clean, digit-free clause of acceptable length.
    private const string GoodBody =
        "يتعهد المورد بتوريد المحصول محل هذا العقد وفقاً للمواصفات المتفق عليها، ويتعهد المشتري بقبول التوريد المطابق وسداد الثمن في مواعيده.";

    [Fact]
    public void Compose_WithoutClauses_CarriesEveryAgreedFigure()
    {
        var facts = Facts();
        var text = ContractComposer.Compose(facts);

        Assert.Contains("مزرعة النيل", text);
        Assert.Contains("مصنع الدلتا", text);
        Assert.Contains("80 طن متري", text);
        Assert.Contains("12500 جنيه مصري", text);
        Assert.Contains("1000000 جنيه مصري", text);
        Assert.Contains(facts.DeliveryDateArabic, text);
        Assert.Contains("30٪", text);
    }

    [Fact]
    public void Compose_WithClauses_KeepsFactLinesAndUsesModelProse()
    {
        var facts = Facts();
        Assert.True(ContractClauseDraft.TryParse(ClauseJson(GoodBody), out var clauses, out _));

        var text = ContractComposer.Compose(facts, clauses);

        Assert.Contains(GoodBody, text);
        Assert.Contains("80 طن متري", text);
        Assert.Contains("12500 جنيه مصري", text);
        Assert.Contains(facts.DeliveryDateArabic, text);
    }

    [Fact]
    public void Compose_ArticlesNotAuthoredByModel_KeepDeterministicProse()
    {
        Assert.True(ContractClauseDraft.TryParse(ClauseJson(GoodBody), out var clauses, out _));

        var text = ContractComposer.Compose(Facts(), clauses);

        Assert.Contains("محاكم القاهرة الاقتصادية", text);
        Assert.Contains("قوة قاهرة", text);
        Assert.Equal(2, ContractComposer.AuthoredClauseCount(clauses));
    }

    [Fact]
    public void Compose_AlwaysHasTheNineArticlesInOrder()
    {
        var text = ContractComposer.Compose(Facts());
        var order = new[]
        {
            "المادة الأولى", "المادة الثانية", "المادة الثالثة", "المادة الرابعة",
            "المادة الخامسة", "المادة السادسة", "المادة السابعة", "المادة الثامنة",
            "المادة التاسعة"
        };

        var lastIndex = -1;
        foreach (var heading in order)
        {
            var index = text.IndexOf(heading, StringComparison.Ordinal);
            Assert.True(index > lastIndex, $"{heading} is missing or out of order");
            lastIndex = index;
        }
    }

    [Fact]
    public void Compose_OmitsHandwrittenSignatureBlocks()
    {
        var text = ContractComposer.Compose(Facts());

        Assert.DoesNotContain("التوقيع:", text);
        Assert.DoesNotContain("الشهود", text);
        Assert.Contains("التوقيع الإلكتروني", text);
    }

    [Theory]
    // A fabricated quantity, price, or date can only contradict the fact line above it.
    [InlineData("يتعهد المورد بتوريد 90 طناً من المحصول وفقاً للمواصفات المتفق عليها بين الطرفين دون أي تغيير.")]
    [InlineData("يتعهد المورد بتوريد الكمية بسعر ١٥٠٠٠ جنيه للطن وفقاً للمواصفات المتفق عليها بين الطرفين.")]
    [InlineData("تسري أحكام هذا العقد حتى تاريخ 2027/01/01 وفقاً لما اتفق عليه الطرفان دون أي تغيير لاحق.")]
    public void TryParse_ClauseWithAnyNumber_IsRejected(string body)
    {
        var parsed = ContractClauseDraft.TryParse(ClauseJson(body), out var draft, out var reason);

        Assert.False(parsed);
        Assert.True(draft.IsEmpty);
        Assert.Contains("numeric guard", reason);
    }

    [Theory]
    [InlineData("يلتزم الطرفان بما ورد في [تاريخ بدء العقد] وفقاً للمواصفات المتفق عليها بين الطرفين دون تغيير.")]
    [InlineData("يلتزم الطرفان بتوقيع العقد أمام الشهود وفقاً للمواصفات المتفق عليها بين الطرفين دون أي تغيير.")]
    public void TryParse_PlaceholdersOrHandwrittenSignatures_AreRejected(string body)
    {
        Assert.False(ContractClauseDraft.TryParse(ClauseJson(body), out var draft, out _));
        Assert.True(draft.IsEmpty);
    }

    [Fact]
    public void TryParse_MixedDraft_KeepsCleanClausesAndReportsTheRest()
    {
        var json = $$"""
            {
              "subject": "{{GoodBody}}",
              "priceTerms": "يتم السداد على أقساط قيمتها 500 جنيه وفقاً للمواصفات المتفق عليها بين الطرفين."
            }
            """;

        Assert.True(ContractClauseDraft.TryParse(json, out var draft, out var reason));

        Assert.Equal(GoodBody, draft.Body("subject"));
        Assert.Null(draft.Body("priceTerms"));
        Assert.Contains("priceTerms", reason);
    }

    [Fact]
    public void TryParse_AcceptsFencedJsonAndSurroundingChatter()
    {
        var raw = $"""
            تمام، هذه البنود:
            ```json
            {ClauseJson(GoodBody)}
            ```
            """;

        Assert.True(ContractClauseDraft.TryParse(raw, out var draft, out _));
        Assert.Equal(GoodBody, draft.Body("subject"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("عذراً، لا أستطيع صياغة العقد.")]
    [InlineData("{ not json")]
    [InlineData("""{"unknownKey":"نص طويل بما يكفي لتجاوز الحد الأدنى المطلوب من الطول في هذا الاختبار."}""")]
    public void TryParse_UnusableAnswers_Fail(string? raw)
    {
        Assert.False(ContractClauseDraft.TryParse(raw, out var draft, out var reason));
        Assert.True(draft.IsEmpty);
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void TryParse_TooShortOrTooLongProse_IsRejected()
    {
        Assert.False(ContractClauseDraft.TryParse(ClauseJson("قصير جداً"), out _, out _));
        Assert.False(
            ContractClauseDraft.TryParse(
                ClauseJson(new string('ن', 1_300)),
                out _,
                out _));
    }

    [Fact]
    public void Guard_ComposedDraft_Passes()
    {
        var text = ContractComposer.Compose(Facts());

        Assert.True(
            ContractTermsGuard.Validate(text, Deal, "مزرعة النيل", "مصنع الدلتا", out var error),
            error);
    }

    [Fact]
    public void Guard_AlteredQuantity_IsRejected()
    {
        // The old check passed on the mere presence of "طن"; an altered figure must now fail.
        var tampered = ContractComposer.Compose(Facts())
            .Replace("80 طن متري", "90 طن متري", StringComparison.Ordinal);

        Assert.False(
            ContractTermsGuard.Validate(tampered, Deal, "مزرعة النيل", "مصنع الدلتا", out var error));
        Assert.Contains("quantity", error);
    }

    [Fact]
    public void Guard_DroppedDeliveryDate_IsRejected()
    {
        var facts = Facts();
        var tampered = ContractComposer.Compose(facts)
            .Replace(facts.DeliveryDateArabic, "لاحقاً", StringComparison.Ordinal);

        Assert.False(
            ContractTermsGuard.Validate(tampered, Deal, "مزرعة النيل", "مصنع الدلتا", out var error));
        Assert.Contains("delivery date", error);
    }

    [Fact]
    public void Guard_WrongFarm_IsRejected()
    {
        var text = ContractComposer.Compose(Facts());

        Assert.False(
            ContractTermsGuard.Validate(text, Deal, "مزرعة أخرى", "مصنع الدلتا", out var error));
        Assert.Contains("parties/farm", error);
    }

    [Fact]
    public void StructuredPrompt_AsksForEveryArticleKeyAndBansNumbers()
    {
        var prompt = new ContractPlugin().BuildStructuredClausePrompt(
            farmName: "مزرعة النيل",
            factoryName: "مصنع الدلتا",
            cropType: "قمح",
            quantityTons: 80,
            pricePerTon: 12500,
            deliveryDate: "11 September 2026",
            qualitySpecs: "Moisture ≤ 12%",
            ragContext: "-");

        foreach (var key in ContractArticles.Keys)
            Assert.Contains($"\"{key}\"", prompt);

        Assert.Contains("ممنوع تماماً كتابة أي رقم", prompt);
    }
}

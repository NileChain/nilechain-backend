using NileChain.Application.Dtos.Contracts;
using NileChain.Application.Services;
using NileChain.AI;

namespace NileChain.Tests;

public class ContractBodyParserTests
{
    [Fact]
    public void ParseSections_SplitsArabicArticles()
    {
        var text = ContractDraftTemplate.Build(
            "مزرعة النيل",
            "مصنع الدلتا",
            "قمح",
            80,
            12500,
            new DateTime(2026, 9, 11),
            "Moisture ≤ 12%");

        var sections = ContractBodyParser.ParseSections(text);

        Assert.NotEmpty(sections);
        Assert.Contains(sections, s => s.Title.Contains("المادة", StringComparison.Ordinal));
        Assert.All(sections, s => Assert.NotEmpty(s.Paragraphs));
    }

    [Fact]
    public void ParseSections_DoesNotTreatContractTitleAsClause()
    {
        var text = ContractDraftTemplate.Build(
            "مزرعة النيل",
            "مصنع الدلتا",
            "قمح",
            80,
            12500,
            new DateTime(2026, 9, 11),
            null);

        var sections = ContractBodyParser.ParseSections(text);

        Assert.DoesNotContain(sections, s =>
            s.Title.Contains("عقد توريد", StringComparison.Ordinal)
            || s.Title.Contains("بسم الله", StringComparison.Ordinal));
        Assert.True(string.IsNullOrWhiteSpace(sections[0].Title));
        Assert.Contains("تم الاتفاق", sections[0].Paragraphs[0], StringComparison.Ordinal);
    }

    [Fact]
    public void IsMostlyArabic_DetectsArabicBody()
    {
        Assert.True(ContractBodyParser.IsMostlyArabic("عقد توريد زراعي بين الطرفين"));
        Assert.False(ContractBodyParser.IsMostlyArabic("Agricultural supply agreement between parties"));
    }

    [Fact]
    public void ExtractPaymentTermsHint_FindsPaymentLine_DoesNotInvent()
    {
        var withPayment =
            "المادة الثالثة — الثمن وشروط السداد\n" +
            "يتم السداد بنسبة 30٪ مقدماً، و70٪ عند الاستلام والقبول.\n";
        var withoutPayment = "المادة الأولى — موضوع العقد\nيتعهد المورد بالتوريد وفق المواصفات.";

        Assert.Contains("30٪", ContractBodyParser.ExtractPaymentTermsHint(withPayment)!);
        Assert.Null(ContractBodyParser.ExtractPaymentTermsHint(withoutPayment));
        Assert.Null(ContractBodyParser.ExtractPaymentTermsHint(null));
    }
}

public class ContractPdfStructuredTests
{
    [Fact]
    public void GeneratePdf_StructuredArabicModel_ProducesPdfBytes()
    {
        var text = ContractDraftTemplate.Build(
            "مزرعة النيل",
            "مصنع الدلتا",
            "قمح",
            80,
            12500,
            new DateTime(2026, 9, 11),
            "Moisture ≤ 12%");

        var pdf = new ContractPdfService();
        var bytes = pdf.GeneratePdf(new ContractPdfModel
        {
            ContractId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Title = "عقد توريد زراعي",
            Status = "PendingSignature",
            CreatedAt = new DateTime(2026, 8, 14),
            FactoryName = "مصنع الدلتا",
            FactoryLocation = "الجيزة",
            FarmName = "مزرعة النيل",
            FarmLocation = "المنيا",
            CropName = "قمح",
            QuantityTons = 80,
            PricePerTon = 12500,
            DeliveryDate = new DateTime(2026, 9, 11),
            DeliveryLocation = "الجيزة",
            QualityRequirements = "Moisture ≤ 12%",
            GeneratedText = text,
            FactorySigned = false,
            FarmSigned = false
        });

        Assert.True(bytes.Length > 500);
        Assert.Equal(0x25, bytes[0]); // %
        Assert.Equal(0x50, bytes[1]); // P
        Assert.Equal(0x44, bytes[2]); // D
        Assert.Equal(0x46, bytes[3]); // F
    }

    [Fact]
    public void GeneratePdf_SignatureStates_RemainDistinct()
    {
        var pdf = new ContractPdfService();
        var neither = pdf.GeneratePdf(new ContractPdfModel
        {
            ContractId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FactoryName = "Nile Mill",
            FarmName = "Green Farm",
            CropName = "Wheat",
            QuantityTons = 10,
            PricePerTon = 1000,
            GeneratedText = "Article 1 — Subject\nThe supplier shall deliver wheat.",
            FactorySigned = false,
            FarmSigned = false
        });
        var factoryOnly = pdf.GeneratePdf(new ContractPdfModel
        {
            ContractId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FactoryName = "Nile Mill",
            FarmName = "Green Farm",
            CropName = "Wheat",
            QuantityTons = 10,
            PricePerTon = 1000,
            GeneratedText = "Article 1 — Subject\nThe supplier shall deliver wheat.",
            FactorySigned = true,
            FarmSigned = false,
            FactorySignedAt = DateTime.UtcNow
        });
        var both = pdf.GeneratePdf(new ContractPdfModel
        {
            ContractId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Status = "Signed",
            FactoryName = "Nile Mill",
            FarmName = "Green Farm",
            CropName = "Wheat",
            QuantityTons = 10,
            PricePerTon = 1000,
            GeneratedText = "Article 1 — Subject\nThe supplier shall deliver wheat.",
            FactorySigned = true,
            FarmSigned = true,
            FactorySignedAt = DateTime.UtcNow.AddMinutes(-1),
            FarmSignedAt = DateTime.UtcNow
        });

        Assert.False(neither.SequenceEqual(factoryOnly));
        Assert.False(factoryOnly.SequenceEqual(both));
    }

    [Fact]
    public void GeneratePdf_WritesInspectableSampleFiles()
    {
        var outDir = Path.Combine(Path.GetTempPath(), "nilechain-contract-pdf-samples");
        Directory.CreateDirectory(outDir);

        var arabicText = ContractDraftTemplate.Build(
            "مزرعة الدلتا المتوسطة",
            "مصنع دلتا ريتش فودز",
            "قمح",
            80,
            12500,
            new DateTime(2026, 9, 11),
            "Moisture ≤ 12%");

        var pdf = new ContractPdfService();
        var ar = pdf.GeneratePdf(new ContractPdfModel
        {
            ContractId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Status = "PendingFarmSignature",
            CreatedAt = new DateTime(2026, 8, 10),
            FactoryName = "مصنع دلتا ريتش فودز",
            FactoryLocation = "الجيزة، مصر",
            FarmName = "مزرعة الدلتا المتوسطة",
            FarmLocation = "الجيزة، مصر",
            CropName = "قمح",
            QuantityTons = 80,
            PricePerTon = 12500,
            DeliveryDate = new DateTime(2026, 9, 11),
            DeliveryLocation = "الجيزة",
            QualityRequirements = "Moisture ≤ 12%",
            GeneratedText = arabicText,
            FactorySigned = true,
            FarmSigned = false,
            FactorySignedAt = DateTime.UtcNow.AddDays(-1)
        });

        var en = pdf.GeneratePdf(new ContractPdfModel
        {
            ContractId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Status = "Signed",
            CreatedAt = new DateTime(2026, 8, 10),
            FactoryName = "Delta Rich Foods",
            FactoryLocation = "Giza, Egypt",
            FarmName = "Average Delta Farm",
            FarmLocation = "Giza, Egypt",
            CropName = "Wheat",
            QuantityTons = 80,
            PricePerTon = 12500,
            DeliveryDate = new DateTime(2026, 9, 11),
            DeliveryLocation = "Giza",
            PaymentTerms = "30% advance, 70% upon acceptance",
            GeneratedText =
                "Agricultural Supply Agreement\n\n" +
                "This Agreement is entered into between the parties.\n\n" +
                "Article 1 — Subject\nThe Supplier shall deliver Wheat.\n\n" +
                "Article 2 — Quantity and Specifications\nQuantity: 80 MT.\n\n" +
                "Article 3 — Price and Payment\nUnit price 12500 EGP/MT.\n\n" +
                "Article 4 — Delivery\nDelivery date 11 September 2026.",
            FactorySigned = true,
            FarmSigned = true,
            FactorySignedAt = DateTime.UtcNow.AddDays(-2),
            FarmSignedAt = DateTime.UtcNow.AddDays(-1)
        });

        var arPath = Path.Combine(outDir, "sample-contract-ar.pdf");
        var enPath = Path.Combine(outDir, "sample-contract-en.pdf");
        File.WriteAllBytes(arPath, ar);
        File.WriteAllBytes(enPath, en);

        Assert.True(ar.Length > 2000);
        Assert.True(en.Length > 1500);

        var repoSamples = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "pdf-samples"));
        try
        {
            Directory.CreateDirectory(repoSamples);
            File.Copy(arPath, Path.Combine(repoSamples, "sample-contract-ar.pdf"), true);
            File.Copy(enPath, Path.Combine(repoSamples, "sample-contract-en.pdf"), true);
        }
        catch
        {
            // Temp samples above are enough when the repo path is unavailable.
        }
    }

    [Fact]
    public void GeneratePdf_WritesPreviewPngPages_ForVisualInspection()
    {
        var arabicText = ContractDraftTemplate.Build(
            "مزرعة الدلتا المتوسطة",
            "مصنع دلتا ريتش فودز",
            "قمح",
            80,
            12500,
            new DateTime(2026, 9, 11),
            "Moisture ≤ 12%");

        var model = new ContractPdfModel
        {
            ContractId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Status = "PendingFarmSignature",
            CreatedAt = new DateTime(2026, 8, 10),
            FactoryName = "مصنع دلتا ريتش فودز",
            FactoryLocation = "الجيزة، مصر",
            FarmName = "مزرعة الدلتا المتوسطة",
            FarmLocation = "الجيزة، مصر",
            CropName = "قمح",
            QuantityTons = 80,
            PricePerTon = 12500,
            DeliveryDate = new DateTime(2026, 9, 11),
            DeliveryLocation = "الجيزة",
            QualityRequirements = "Moisture ≤ 12%",
            GeneratedText = arabicText,
            FactorySigned = true,
            FarmSigned = false,
            FactorySignedAt = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc)
        };

        var outDir = Path.Combine(
            Path.GetTempPath(),
            "nilechain-contract-pdf-previews");
        Directory.CreateDirectory(outDir);
        foreach (var stale in Directory.EnumerateFiles(outDir, "ar-page-*.png"))
            File.Delete(stale);

        var pages = ContractPdfService.GeneratePreviewImages(model).ToList();
        Assert.NotEmpty(pages);
        for (var i = 0; i < pages.Count; i++)
        {
            var path = Path.Combine(outDir, $"ar-page-{i + 1}.png");
            File.WriteAllBytes(path, pages[i]);
        }

        var repoSamples = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "pdf-samples"));
        try
        {
            Directory.CreateDirectory(repoSamples);
            for (var i = 0; i < Math.Min(pages.Count, 3); i++)
                File.WriteAllBytes(Path.Combine(repoSamples, $"preview-ar-page-{i + 1}.png"), pages[i]);
            if (pages.Count > 0)
                File.WriteAllBytes(
                    Path.Combine(repoSamples, $"preview-ar-page-last.png"),
                    pages[^1]);
        }
        catch
        {
            // Temp previews above are enough when the repo path is unavailable.
        }
    }
}

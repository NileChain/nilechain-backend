using System.Globalization;
using System.Reflection;
using NileChain.Application.Dtos.Contracts;
using NileChain.Application.Interfaces;
using NileChain.Domain.Common;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NileChain.Application.Services;

/// <summary>
/// Dedicated A4 editorial corporate-contract PDF renderer (Word / legal-stationery style).
/// Independent from the web SaaS UI — never includes navbar, cards, buttons, or dashboard chrome.
/// Legal wording comes only from generated/approved contract text; structured fields supply commercial data.
/// Requires legal/business-owner review before production use.
/// </summary>
public class ContractPdfService : IContractPdfService
{
    private static readonly object FontLock = new();
    private static bool _fontsRegistered;

    // Editorial ink + restrained NileChain accent (corporate stationery, not SaaS chrome)
    private static readonly Color Ink = Color.FromHex("#1A1A1A");
    private static readonly Color Charcoal = Color.FromHex("#2C2C2C");
    private static readonly Color Muted = Color.FromHex("#555555");
    private static readonly Color Rule = Color.FromHex("#D8D8D8");
    private static readonly Color TableHeaderBg = Color.FromHex("#F3F3F3");
    private static readonly Color Accent = Color.FromHex("#1B5E20");
    private static readonly Color SignedGreen = Color.FromHex("#1B5E20");
    private static readonly Color PendingAmber = Color.FromHex("#B45309");

    private const string PrimaryFont = "Noto Sans Arabic";
    private const string FallbackFont = "Noto Sans";

    static ContractPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false;
        EnsureFontsRegistered();
    }

    public byte[] GeneratePdf(
        string title,
        string contractText,
        string farmName,
        string factoryName,
        bool factorySigned = false,
        bool farmSigned = false,
        DateTime? factorySignedAt = null,
        DateTime? farmSignedAt = null)
    {
        return GeneratePdf(new ContractPdfModel
        {
            ContractId = Guid.Empty,
            Title = title,
            Status = factorySigned && farmSigned
                ? "Signed"
                : factorySigned
                    ? "PendingFarmSignature"
                    : farmSigned
                        ? "PendingFactorySignature"
                        : "PendingSignature",
            GeneratedText = contractText,
            FarmName = farmName,
            FactoryName = factoryName,
            FactorySigned = factorySigned,
            FarmSigned = farmSigned,
            FactorySignedAt = factorySignedAt,
            FarmSignedAt = farmSignedAt,
            CreatedAt = DateTime.UtcNow,
            CropName = string.Empty,
            QuantityTons = 0
        });
    }

    public byte[] GeneratePdf(ContractPdfModel model)
    {
        EnsureFontsRegistered();
        return BuildDocument(model).GeneratePdf();
    }

    /// <summary>
    /// Renders page preview PNGs for visual QA (tests / design review). Not used in production download.
    /// </summary>
    public static IEnumerable<byte[]> GeneratePreviewImages(ContractPdfModel model)
    {
        EnsureFontsRegistered();
        return new ContractPdfService().BuildDocument(model).GenerateImages();
    }

    private Document BuildDocument(ContractPdfModel model)
    {
        var bodyText = ContractSignatureText.StripHandwrittenBlocks(model.GeneratedText);
        var rtl = ContractBodyParser.IsMostlyArabic(bodyText);
        var sections = ContractBodyParser.ParseSections(bodyText).ToList();
        var bismillah = ContractBodyParser.ExtractBismillah(bodyText)
                        ?? (rtl ? "بسم الله الرحمن الرحيم" : null);

        // Untitled leading block = introductory / preamble prose (do not invent headings).
        var preambleParas = new List<string>();
        if (sections.Count > 0 && string.IsNullOrWhiteSpace(sections[0].Title))
        {
            preambleParas.AddRange(sections[0].Paragraphs.Where(p =>
                !string.IsNullOrWhiteSpace(p)
                && !BismillahLooksLike(p)
                && !ContractTitleLooksLike(p)));
            sections.RemoveAt(0);
        }

        // Party lines already mirrored by structured party blocks — avoid double listing.
        var introParas = preambleParas
            .Where(p => !LooksLikePartyLine(p))
            .ToList();
        var whereasParas = introParas.Where(LooksLikeWhereas).ToList();
        var openingParas = introParas.Where(p => !LooksLikeWhereas(p)).ToList();

        var contractNo = model.ContractId == Guid.Empty
            ? "—"
            : $"NC-{model.CreatedAt.Year}-{model.ContractId.ToString("N")[..8].ToUpperInvariant()}";
        var version = string.IsNullOrWhiteSpace(model.DocumentVersion) ? "1.0" : model.DocumentVersion!;
        var logo = TryLoadLogo();
        var labels = rtl ? Labels.Arabic : Labels.English;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(0);
                page.MarginBottom(42);
                page.MarginHorizontal(0);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x
                    .FontFamily(PrimaryFont, FallbackFont)
                    .FontSize(10.5f)
                    .FontColor(Ink)
                    .LineHeight(1.55f));

                if (rtl)
                    page.ContentFromRightToLeft();
                else
                    page.ContentFromLeftToRight();

                page.Header().Column(h =>
                {
                    h.Item().ShowOnce().Element(e =>
                        FirstPageLetterhead(e, labels, logo, rtl));
                    h.Item().SkipOnce().PaddingHorizontal(54).PaddingTop(10).Element(e =>
                        ContinuingLetterhead(e, labels, contractNo, logo));
                });

                page.Content().PaddingHorizontal(54).PaddingTop(10).Column(col =>
                {
                    if (model.FactorySigned && model.FarmSigned)
                    {
                        col.Item().PaddingBottom(8).BorderBottom(0.6f).BorderColor(Rule)
                            .PaddingBottom(6)
                            .Text(labels.FullySignedBanner)
                            .FontSize(9).Bold().FontColor(Ink)
                            .FontFamily(PrimaryFont, FallbackFont);
                    }
                    else if (model.FactorySigned)
                    {
                        col.Item().PaddingBottom(8).BorderBottom(0.6f).BorderColor(Rule)
                            .PaddingBottom(6)
                            .Text(labels.FactorySignedBanner)
                            .FontSize(9).Bold().FontColor(Muted)
                            .FontFamily(PrimaryFont, FallbackFont);
                    }
                    else if (model.FarmSigned)
                    {
                        col.Item().PaddingBottom(8).BorderBottom(0.6f).BorderColor(Rule)
                            .PaddingBottom(6)
                            .Text(labels.FarmSignedBanner)
                            .FontSize(9).Bold().FontColor(Muted)
                            .FontFamily(PrimaryFont, FallbackFont);
                    }

                    if (!string.IsNullOrWhiteSpace(bismillah))
                    {
                        col.Item().AlignCenter().Text(bismillah!)
                            .FontSize(11.5f).Bold().FontColor(Ink)
                            .FontFamily(PrimaryFont, FallbackFont);
                    }

                    var title = string.IsNullOrWhiteSpace(model.Title)
                        ? labels.AgreementTitle
                        : model.Title;
                    col.Item().PaddingTop(10).AlignCenter().Text(title)
                        .FontSize(18).Bold().FontColor(Ink)
                        .FontFamily(PrimaryFont, FallbackFont);

                    col.Item().PaddingTop(16).Element(e =>
                        RomanHeading(e, 1, labels.IntroHeading));

                    var introToRender = openingParas.Count > 0
                        ? openingParas
                        : new List<string> { BuildOpeningSentence(labels, model, rtl) };
                    foreach (var para in introToRender)
                    {
                        col.Item().PaddingTop(6).Text(para)
                            .FontSize(10.5f).LineHeight(1.7f).Justify()
                            .FontFamily(PrimaryFont, FallbackFont);
                    }

                    col.Item().PaddingTop(14).Element(e =>
                        RomanHeading(e, 2, labels.SummaryHeading));
                    col.Item().PaddingTop(4).Text(labels.SummaryLead)
                        .FontSize(10.5f).LineHeight(1.6f)
                        .FontFamily(PrimaryFont, FallbackFont);
                    col.Item().PaddingTop(8).Element(e =>
                        SummaryTable(e, labels, model, rtl));
                    col.Item().PaddingTop(8).Text(labels.SummaryClose)
                        .FontSize(10.5f).LineHeight(1.6f)
                        .FontFamily(PrimaryFont, FallbackFont);

                    if (whereasParas.Count > 0)
                    {
                        col.Item().PaddingTop(16).Element(e =>
                            ClauseHeading(e, labels.Preamble, numbered: false));
                        foreach (var para in whereasParas)
                        {
                            col.Item().PaddingTop(6).Text(para)
                                .FontSize(10.5f).LineHeight(1.65f).Justify()
                                .FontFamily(PrimaryFont, FallbackFont);
                        }
                    }

                    if (sections.Count == 0 && openingParas.Count == 0 && whereasParas.Count == 0)
                    {
                        if (!string.IsNullOrWhiteSpace(bodyText) && introToRender.Count == 0)
                        {
                            col.Item().PaddingTop(16).Element(e =>
                                ClauseHeading(e, labels.Terms, numbered: false));
                            foreach (var para in SplitLooseParagraphs(bodyText))
                            {
                                col.Item().PaddingTop(6).Text(para)
                                    .FontSize(10.5f).LineHeight(1.65f).Justify()
                                    .FontFamily(PrimaryFont, FallbackFont);
                            }
                        }
                    }
                    else
                    {
                        foreach (var section in sections)
                        {
                            var heading = string.IsNullOrWhiteSpace(section.Title)
                                ? labels.Terms
                                : CleanHeading(section.Title);

                            col.Item().PaddingTop(14).Element(block =>
                            {
                                block.EnsureSpace(78).Column(sec =>
                                {
                                    sec.Item().Element(e =>
                                        ClauseHeading(e, heading, numbered: false));
                                    if (section.Paragraphs.Count > 0)
                                    {
                                        sec.Item().PaddingTop(6).Text(section.Paragraphs[0])
                                            .FontSize(10.5f).LineHeight(1.65f).Justify()
                                            .FontFamily(PrimaryFont, FallbackFont);
                                    }
                                });
                            });

                            for (var i = 1; i < section.Paragraphs.Count; i++)
                            {
                                col.Item().PaddingTop(6).Text(section.Paragraphs[i])
                                    .FontSize(10.5f).LineHeight(1.65f).Justify()
                                    .FontFamily(PrimaryFont, FallbackFont);
                            }
                        }
                    }

                    col.Item().PaddingTop(20).Element(e =>
                        ClauseHeading(e, labels.Signatures, numbered: false));
                    col.Item().PaddingTop(4).Text(labels.SignaturesHint)
                        .FontSize(9).FontColor(Muted).LineHeight(1.5f)
                        .FontFamily(PrimaryFont, FallbackFont);
                    col.Item().PaddingTop(12).Element(e =>
                        FormalSignatures(e, labels, model, rtl));
                });

                page.Footer().PaddingHorizontal(54).Element(f =>
                    DocumentFooter(f, labels, contractNo, version, model, rtl));
            });
        });
    }

    // ─── Letterheads ─────────────────────────────────────────────────────────

    private static void FirstPageLetterhead(
        IContainer container,
        Labels labels,
        byte[]? logo,
        bool rtl)
    {
        container.Column(col =>
        {
            col.Item().Height(18).Svg(
                """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 595 18" preserveAspectRatio="none">
                  <path fill="#2C2C2C" d="M0 0h595v18H324L297.5 6.8 271 18H0z"/>
                </svg>
                """);

            col.Item().PaddingHorizontal(54).PaddingTop(12).PaddingBottom(8).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Row(brand =>
                {
                    brand.ConstantItem(48).AlignMiddle().Element(mark =>
                    {
                        if (logo is { Length: > 0 })
                            mark.Background(Colors.White).Height(44).Width(44).Image(logo).FitArea();
                        else
                            mark.Background(Colors.White).Height(44).Width(44)
                                .Border(1.1f).BorderColor(Accent)
                                .AlignCenter().AlignMiddle()
                                .Text("N").FontSize(18).Bold().FontColor(Accent)
                                .FontFamily(PrimaryFont, FallbackFont);
                    });
                    brand.RelativeItem().PaddingHorizontal(8).AlignMiddle().Column(text =>
                    {
                        text.Item().Text("NileChain")
                            .FontSize(14).Bold().FontColor(Accent)
                            .FontFamily(PrimaryFont, FallbackFont);
                        text.Item().PaddingTop(1).Text(labels.MarketplaceTag)
                            .FontSize(7.5f).FontColor(Muted)
                            .FontFamily(PrimaryFont, FallbackFont);
                    });
                });

                var contact = row.RelativeItem().AlignMiddle();
                contact = rtl ? contact.AlignLeft() : contact.AlignRight();
                contact.Column(c =>
                {
                    c.Item().Text(labels.ContactEmail)
                        .FontSize(8).FontColor(Muted)
                        .FontFamily(PrimaryFont, FallbackFont);
                    c.Item().PaddingTop(1).Text(labels.ContactAddress)
                        .FontSize(8).FontColor(Muted)
                        .FontFamily(PrimaryFont, FallbackFont);
                    c.Item().PaddingTop(1).Text(labels.ContactWeb)
                        .FontSize(8).FontColor(Muted)
                        .FontFamily(PrimaryFont, FallbackFont);
                });
            });

            col.Item().PaddingHorizontal(54).Height(2.8f).Background(Accent);
            col.Item().PaddingHorizontal(54).PaddingTop(1.6f).Height(1.2f).Background(Charcoal);
        });
    }

    private static void ContinuingLetterhead(
        IContainer container,
        Labels labels,
        string contractNo,
        byte[]? logo)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(4).Row(row =>
            {
                row.RelativeItem().Row(brand =>
                {
                    brand.ConstantItem(18).Element(mark =>
                    {
                        if (logo is { Length: > 0 })
                            mark.Height(16).Width(16).Image(logo).FitArea();
                        else
                            mark.Height(16).Width(16)
                                .Border(0.6f).BorderColor(Accent)
                                .AlignCenter().AlignMiddle()
                                .Text("N").FontSize(8).Bold().FontColor(Accent)
                                .FontFamily(PrimaryFont, FallbackFont);
                    });
                    brand.RelativeItem().PaddingLeft(6).AlignMiddle().Text("NileChain")
                        .FontSize(9).Bold().FontColor(Accent)
                        .FontFamily(PrimaryFont, FallbackFont);
                });

                row.RelativeItem().AlignRight().AlignMiddle()
                    .Text($"{labels.ContractNo}: {contractNo}")
                    .FontSize(8).FontColor(Muted)
                    .FontFamily(PrimaryFont, FallbackFont);
            });

            col.Item().LineHorizontal(0.7f).LineColor(Rule);
        });
    }

    // ─── Parties / commercial / signatures ───────────────────────────────────

    private static void SummaryTable(
        IContainer container,
        Labels labels,
        ContractPdfModel model,
        bool rtl)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.2f);
                c.RelativeColumn(2.15f);
            });

            table.Header(header =>
            {
                header.Cell().Background(TableHeaderBg)
                    .BorderBottom(0.6f).BorderColor(Rule)
                    .PaddingVertical(6).PaddingHorizontal(8)
                    .Text(labels.ColDescription).Bold().FontSize(9).FontColor(Ink)
                    .FontFamily(PrimaryFont, FallbackFont);
                header.Cell().Background(TableHeaderBg)
                    .BorderBottom(0.6f).BorderColor(Rule)
                    .PaddingVertical(6).PaddingHorizontal(8)
                    .Text(labels.ColDetails).Bold().FontSize(9).FontColor(Ink)
                    .FontFamily(PrimaryFont, FallbackFont);
            });

            void Row(string label, string value)
            {
                table.Cell()
                    .BorderBottom(0.5f).BorderColor(Rule)
                    .PaddingVertical(5).PaddingHorizontal(8)
                    .Text(label).Bold().FontSize(9.5f).FontColor(Ink)
                    .FontFamily(PrimaryFont, FallbackFont);
                table.Cell()
                    .BorderBottom(0.5f).BorderColor(Rule)
                    .PaddingVertical(5).PaddingHorizontal(8)
                    .Text(value).FontSize(9.5f).FontColor(Ink)
                    .FontFamily(PrimaryFont, FallbackFont);
            }

            var factory = string.IsNullOrWhiteSpace(model.FactoryLocation)
                ? model.FactoryName
                : $"{model.FactoryName} · {model.FactoryLocation}";
            var farm = string.IsNullOrWhiteSpace(model.FarmLocation)
                ? model.FarmName
                : $"{model.FarmName} · {model.FarmLocation}";

            Row(labels.BuyerShort, factory);
            Row(labels.SupplierShort, farm);
            Row(labels.Product, string.IsNullOrWhiteSpace(model.CropName) ? "—" : model.CropName);
            Row(labels.Quantity, $"{FormatNumber(model.QuantityTons)} {labels.Ton}");
            Row(
                labels.Price,
                model.PricePerTon is { } p
                    ? $"{FormatNumber(p)} {labels.Egp}/{labels.Ton}"
                    : "—");
            Row(
                labels.StartsAt,
                model.StartsAt.HasValue
                    ? FormatDate(model.StartsAt.Value, rtl)
                    : model.FactorySigned && model.FarmSigned
                        ? FormatDate(
                            Later(model.FactorySignedAt, model.FarmSignedAt) ?? model.CreatedAt,
                            rtl)
                        : labels.StartsAtPending);
            Row(
                labels.EndsAt,
                model.EndsAt.HasValue
                    ? FormatDate(model.EndsAt.Value, rtl)
                    : model.DeliveryDate.HasValue
                        ? FormatDate(model.DeliveryDate.Value, rtl)
                        : "—");
        });
    }

    private static void FormalSignatures(
        IContainer container,
        Labels labels,
        ContractPdfModel model,
        bool rtl)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(e =>
                SignatureColumn(
                    e,
                    labels.BuyerShort,
                    model.FactoryName,
                    model.FactorySigned,
                    model.FactorySignedAt,
                    labels,
                    rtl));
            row.ConstantItem(28);
            row.RelativeItem().Element(e =>
                SignatureColumn(
                    e,
                    labels.SupplierShort,
                    model.FarmName,
                    model.FarmSigned,
                    model.FarmSignedAt,
                    labels,
                    rtl));
        });
    }

    private static void SignatureColumn(
        IContainer container,
        string role,
        string name,
        bool signed,
        DateTime? signedAt,
        Labels labels,
        bool rtl)
    {
        container.EnsureSpace(110).Column(col =>
        {
            col.Item().Text(role)
                .FontSize(8).Bold().FontColor(Muted)
                .FontFamily(PrimaryFont, FallbackFont);

            col.Item().PaddingTop(4).Text(name)
                .FontSize(11).Bold().FontColor(Ink)
                .FontFamily(PrimaryFont, FallbackFont);

            col.Item().PaddingTop(14).LineHorizontal(0.9f)
                .LineColor(signed ? Accent : Ink);

            col.Item().PaddingTop(6).Text(
                    signed
                        ? signedAt.HasValue
                            ? $"{labels.Signed} · {FormatDateTime(signedAt.Value, rtl)}"
                            : labels.Signed
                        : labels.Unsigned)
                .FontSize(8.5f).Bold()
                .FontColor(signed ? SignedGreen : PendingAmber)
                .FontFamily(PrimaryFont, FallbackFont);
        });
    }

    private static void RomanHeading(IContainer container, int number, string title)
    {
        ClauseHeading(container, $"{ToRoman(number)}.  {title}", numbered: false);
    }

    private static string ToRoman(int number)
    {
        var n = Math.Max(1, number);
        var map = new (int Value, string Numeral)[]
        {
            (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
        };
        var sb = new System.Text.StringBuilder();
        foreach (var (value, numeral) in map)
        {
            while (n >= value)
            {
                sb.Append(numeral);
                n -= value;
            }
        }
        return sb.ToString();
    }

    private static void ClauseHeading(
        IContainer container,
        string title,
        string? number = null,
        bool numbered = true)
    {
        var text = numbered && !string.IsNullOrWhiteSpace(number)
            ? $"{number}.  {title}"
            : title;

        container.Column(col =>
        {
            col.Item().Text(text)
                .Bold().FontSize(11).FontColor(Ink)
                .FontFamily(PrimaryFont, FallbackFont);
        });
    }

    private static void DocumentFooter(
        IContainer container,
        Labels labels,
        string contractNo,
        string version,
        ContractPdfModel model,
        bool rtl)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.65f).LineColor(Rule);
            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Muted)
                        .FontFamily(PrimaryFont, FallbackFont));
                    text.Span("NileChain");
                    text.Span("  ·  ");
                    text.Span(contractNo);
                    if (model.FactorySigned && model.FarmSigned)
                    {
                        var at = Later(model.FactorySignedAt, model.FarmSignedAt);
                        if (at.HasValue)
                        {
                            text.Span("  ·  ");
                            text.Span($"{labels.SignedAtLabel}: {FormatDateTime(at.Value, rtl)}");
                        }
                    }
                });
                var pages = row.RelativeItem();
                pages = rtl ? pages.AlignLeft() : pages.AlignRight();
                pages.Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Muted)
                        .FontFamily(PrimaryFont, FallbackFont));
                    text.Span($"{labels.Version}: {version}");
                    text.Span("  ·  ");
                    text.Span($"{labels.Page} ");
                    text.CurrentPageNumber();
                    text.Span($" {labels.PageOf} ");
                    text.TotalPages();
                });
            });
        });
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static DateTime? Later(DateTime? a, DateTime? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return a.Value >= b.Value ? a : b;
    }

    private static string BuildOpeningSentence(Labels labels, ContractPdfModel model, bool _) =>
        string.Format(
            CultureInfo.InvariantCulture,
            labels.OpeningTemplate,
            model.FactoryName,
            model.FarmName);

    private static bool LooksLikePartyLine(string text)
    {
        var t = text.Trim();
        return t.StartsWith("الطرف الأول", StringComparison.Ordinal)
               || t.StartsWith("الطرف الثاني", StringComparison.Ordinal)
               || t.StartsWith("أولاً", StringComparison.Ordinal)
               || t.StartsWith("ثانياً", StringComparison.Ordinal)
               || t.StartsWith("First Party", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("Second Party", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeWhereas(string text)
    {
        var t = text.Trim();
        return t.StartsWith("وحيث", StringComparison.Ordinal)
               || t.StartsWith("وبناءً", StringComparison.Ordinal)
               || t.StartsWith("وبناء على", StringComparison.Ordinal)
               || t.StartsWith("Whereas", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("NOW, THEREFORE", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDate(DateTime value, bool rtl)
    {
        var utc = value.ToUniversalTime();
        return rtl
            ? utc.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : utc.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture);
    }

    private static string FormatDateTime(DateTime value, bool rtl = false)
    {
        var utc = value.ToUniversalTime();
        return rtl
            ? utc.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC"
            : utc.ToString("dd MMMM yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC";
    }

    private static string FormatNumber(decimal value) =>
        value.ToString("#,##0.##", CultureInfo.InvariantCulture);

    private static string CleanHeading(string title) =>
        title.Replace("—", "—").Trim();

    private static bool BismillahLooksLike(string text) =>
        text.TrimStart().StartsWith("بسم الله", StringComparison.Ordinal);

    private static bool ContractTitleLooksLike(string text)
    {
        var t = text.Trim();
        return t.StartsWith("عقد توريد", StringComparison.Ordinal)
               || t.Equals("Agricultural Supply Agreement", StringComparison.OrdinalIgnoreCase)
               || t.Equals("Agricultural Supply Contract", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SplitLooseParagraphs(string text)
    {
        return text.Replace("\r\n", "\n")
            .Split(new[] { "\n\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0 && !BismillahLooksLike(p) && !ContractTitleLooksLike(p));
    }

    private static void EnsureFontsRegistered()
    {
        if (_fontsRegistered)
            return;

        lock (FontLock)
        {
            if (_fontsRegistered)
                return;

            var assembly = typeof(ContractPdfService).Assembly;
            foreach (var (resource, family) in new[]
                     {
                         ("NileChain.Application.Resources.Fonts.NotoSansArabic-Regular.ttf", "Noto Sans Arabic"),
                         ("NileChain.Application.Resources.Fonts.NotoSansArabic-Bold.ttf", "Noto Sans Arabic"),
                         ("NileChain.Application.Resources.Fonts.NotoSans-Regular.ttf", "Noto Sans"),
                         ("NileChain.Application.Resources.Fonts.NotoSans-Bold.ttf", "Noto Sans")
                     })
            {
                using var stream = assembly.GetManifestResourceStream(resource)
                    ?? OpenFontFileFallback(resource);
                if (stream is null)
                    continue;

                FontManager.RegisterFontWithCustomName(family, stream);
            }

            _fontsRegistered = true;
        }
    }

    private static Stream? OpenFontFileFallback(string resourceName)
    {
        var parts = resourceName.Split('.');
        if (parts.Length < 2)
            return null;
        var file = parts[^2] + "." + parts[^1];
        var path = ResolveResourcePath(Path.Combine("Fonts", file));
        return path is not null && File.Exists(path) ? File.OpenRead(path) : null;
    }

    private static byte[]? TryLoadLogo()
    {
        var assembly = typeof(ContractPdfService).Assembly;
        foreach (var resource in new[]
                 {
                     "NileChain.Application.Resources.nilechain-mark-light.png",
                     "NileChain.Application.Resources.nilechain-mark.png"
                 })
        {
            using var embedded = assembly.GetManifestResourceStream(resource);
            if (embedded is not null)
            {
                using var ms = new MemoryStream();
                embedded.CopyTo(ms);
                return ms.ToArray();
            }
        }

        foreach (var file in new[] { "nilechain-mark-light.png", "nilechain-mark.png" })
        {
            var path = ResolveResourcePath(file);
            if (path is not null && File.Exists(path))
                return File.ReadAllBytes(path);
        }

        return null;
    }

    private static string? ResolveResourcePath(string relative)
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(assemblyDir))
        {
            var candidate = Path.Combine(assemblyDir, "Resources", relative);
            if (File.Exists(candidate))
                return candidate;
        }

        var cwd = Path.Combine(AppContext.BaseDirectory, "Resources", relative);
        return File.Exists(cwd) ? cwd : null;
    }

    private sealed class Labels
    {
        public required string MarketplaceAr { get; init; }
        public required string MarketplaceEn { get; init; }
        public required string AgreementTitle { get; init; }
        public required string ContractNo { get; init; }
        public required string Created { get; init; }
        public required string Issued { get; init; }
        public required string Preamble { get; init; }
        public required string BuyerFirst { get; init; }
        public required string SupplierSecond { get; init; }
        public required string BuyerShort { get; init; }
        public required string SupplierShort { get; init; }
        public required string Name { get; init; }
        public required string Address { get; init; }
        public required string Representative { get; init; }
        public required string Capacity { get; init; }
        public required string CommercialTerms { get; init; }
        public required string Product { get; init; }
        public required string Quantity { get; init; }
        public required string Price { get; init; }
        public required string Total { get; init; }
        public required string DeliveryDate { get; init; }
        public required string DeliveryLocation { get; init; }
        public required string PaymentTerms { get; init; }
        public required string Quality { get; init; }
        public required string Terms { get; init; }
        public required string MissingBody { get; init; }
        public required string Signatures { get; init; }
        public required string InWitness { get; init; }
        public required string SignatureLine { get; init; }
        public required string SignedElectronically { get; init; }
        public required string AwaitingElectronicSignature { get; init; }
        public required string Date { get; init; }
        public required string Version { get; init; }
        public required string Page { get; init; }
        public required string PageOf { get; init; }
        public required string Ton { get; init; }
        public required string Egp { get; init; }
        public required string OpeningTemplate { get; init; }
        public required string IntroHeading { get; init; }
        public required string SummaryHeading { get; init; }
        public required string SummaryLead { get; init; }
        public required string SummaryClose { get; init; }
        public required string ColDescription { get; init; }
        public required string ColDetails { get; init; }
        public required string ContactEmail { get; init; }
        public required string ContactAddress { get; init; }
        public required string ContactWeb { get; init; }
        public required string StartsAt { get; init; }
        public required string StartsAtPending { get; init; }
        public required string EndsAt { get; init; }
        public required string MarketplaceTag { get; init; }
        public required string SignaturesHint { get; init; }
        public required string Signed { get; init; }
        public required string Unsigned { get; init; }
        public required string FullySignedBanner { get; init; }
        public required string FactorySignedBanner { get; init; }
        public required string FarmSignedBanner { get; init; }
        public required string SignedAtLabel { get; init; }
        public required string Status { get; init; }

        public static Labels English { get; } = new()
        {
            MarketplaceAr = "سوق الأعمال الزراعية بين الشركات",
            MarketplaceEn = "B2B Agricultural Marketplace",
            MarketplaceTag = "B2B Agricultural Marketplace",
            AgreementTitle = "Agricultural Supply Agreement",
            ContractNo = "Contract No.",
            Created = "Contract date",
            Issued = "Issue date",
            Preamble = "Preamble",
            BuyerFirst = "First: First Party — Buyer / Factory",
            SupplierSecond = "Second: Second Party — Supplier / Farm",
            BuyerShort = "Buyer — Factory",
            SupplierShort = "Supplier — Farm",
            Name = "Name",
            Address = "Address",
            Representative = "Represented by",
            Capacity = "Capacity",
            CommercialTerms = "Crop, quantities and specifications",
            Product = "Product / Crop",
            Quantity = "Quantity",
            Price = "Price per unit",
            Total = "Total value",
            DeliveryDate = "Delivery date",
            DeliveryLocation = "Delivery location",
            PaymentTerms = "Payment terms",
            Quality = "Quality specifications",
            Terms = "Terms and conditions",
            MissingBody = "Full legal text is not available for this contract.",
            Signatures = "Electronic signatures",
            SignaturesHint =
                "Signing happens electronically on the platform. This section shows each party’s signature status — handwritten blanks are not part of the clause text.",
            Signed = "Signed",
            Unsigned = "Pending signature",
            FullySignedBanner = "This contract has been fully signed by both parties.",
            FactorySignedBanner = "Signed by factory — awaiting farm signature",
            FarmSignedBanner = "Signed by farm — awaiting factory signature",
            SignedAtLabel = "Signed at",
            InWitness =
                "In witness whereof, the parties have executed this Agreement electronically via the NileChain platform on the dates indicated below.",
            SignatureLine = "Signature",
            SignedElectronically = "Signed electronically",
            AwaitingElectronicSignature = "Awaiting electronic signature",
            Date = "Date",
            Version = "Document version",
            Page = "Page",
            PageOf = "of",
            Ton = "ton",
            Egp = "EGP",
            OpeningTemplate =
                "This agricultural supply agreement is entered into between {0} (Buyer / Factory) and {1} (Supplier / Farm) via the NileChain platform, and becomes binding once both parties complete electronic signature.",
            IntroHeading = "Introduction",
            SummaryHeading = "Supply Agreement Summary",
            SummaryLead = "The following outlines the key terms of the agreement between the parties:",
            SummaryClose =
                "The agreement runs from the start date above through the end / delivery date, unless later amended by both parties on the platform.",
            ColDescription = "Description",
            ColDetails = "Details",
            ContactEmail = "hello@nilechain.dev",
            ContactAddress = "Cairo, Arab Republic of Egypt",
            ContactWeb = "nilechain.dev",
            StartsAt = "Contract start date",
            StartsAtPending = "Set when both parties finish signing",
            EndsAt = "Contract end date",
            Status = "Status"
        };

        public static Labels Arabic { get; } = new()
        {
            MarketplaceAr = "سوق الأعمال الزراعية بين الشركات",
            MarketplaceEn = "B2B Agricultural Marketplace",
            MarketplaceTag = "سوق الأعمال الزراعية بين الشركات",
            AgreementTitle = "عقد توريد زراعي",
            ContractNo = "رقم العقد",
            Created = "تاريخ العقد",
            Issued = "تاريخ الإصدار",
            Preamble = "تمهيد",
            BuyerFirst = "أولاً: الطرف الأول — المشتري / المصنع",
            SupplierSecond = "ثانياً: الطرف الثاني — المورد / المزرعة",
            BuyerShort = "المشتري — المصنع",
            SupplierShort = "المورد — المزرعة",
            Name = "الاسم",
            Address = "العنوان",
            Representative = "ويمثله",
            Capacity = "الصفة",
            CommercialTerms = "بيانات المحصول والكميات والمواصفات",
            Product = "المحصول / المنتج",
            Quantity = "الكمية",
            Price = "السعر للوحدة",
            Total = "إجمالي القيمة",
            DeliveryDate = "تاريخ التسليم",
            DeliveryLocation = "مكان التسليم",
            PaymentTerms = "شروط الدفع",
            Quality = "مواصفات الجودة",
            Terms = "الأحكام والشروط",
            MissingBody = "النص القانوني الكامل غير متوفر لهذا العقد.",
            Signatures = "التوقيعات الإلكترونية",
            SignaturesHint =
                "يتم التوقيع إلكترونياً عبر المنصة، ويُعرض هنا حالة توقيع كل طرف دون خانات خطية داخل البنود.",
            Signed = "تم التوقيع",
            Unsigned = "بانتظار التوقيع",
            FullySignedBanner = "تم توقيع هذا العقد بالكامل من الطرفين.",
            FactorySignedBanner = "تم توقيع العقد من المصنع — بانتظار توقيع المزرعة",
            FarmSignedBanner = "تم توقيع العقد من المزرعة — بانتظار توقيع المصنع",
            SignedAtLabel = "تاريخ التوقيع",
            InWitness =
                "وإثباتًا لما تقدم، فقد وقع الطرفان على هذا العقد إلكترونيًا عبر منصة NileChain في التواريخ المبينة أدناه.",
            SignatureLine = "التوقيع",
            SignedElectronically = "تم التوقيع إلكترونياً",
            AwaitingElectronicSignature = "في انتظار التوقيع الإلكتروني",
            Date = "التاريخ",
            Version = "إصدار المستند",
            Page = "صفحة",
            PageOf = "من",
            Ton = "طن",
            Egp = "EGP",
            OpeningTemplate =
                "أُبرم عقد التوريد الزراعي هذا بين {0} (المشتري / المصنع) و{1} (المورد / المزرعة) عبر منصة NileChain، ويُعدّ ملزماً للطرفين بعد اكتمال التوقيع الإلكتروني.",
            IntroHeading = "التمهيد",
            SummaryHeading = "ملخص عقد التوريد",
            SummaryLead = "يبين الجدول التالي البنود الرئيسية المتفق عليها بين الطرفين:",
            SummaryClose =
                "يسري العقد من تاريخ البدء المبين أعلاه حتى تاريخ الانتهاء / التسليم، ما لم يُعدَّل باتفاق الطرفين عبر المنصة.",
            ColDescription = "البند",
            ColDetails = "التفاصيل",
            ContactEmail = "hello@nilechain.dev",
            ContactAddress = "القاهرة، جمهورية مصر العربية",
            ContactWeb = "nilechain.dev",
            StartsAt = "تاريخ بدء العقد",
            StartsAtPending = "يُحدَّد عند اكتمال توقيع الطرفين",
            EndsAt = "تاريخ انتهاء العقد",
            Status = "الحالة"
        };
    }
}

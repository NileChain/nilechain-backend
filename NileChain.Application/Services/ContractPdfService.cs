using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NileChain.Application.Interfaces;

namespace NileChain.Application.Services;

public class ContractPdfService : IContractPdfService
{
    static ContractPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GeneratePdf(
        string title,
        string contractText,
        string farmName,
        string factoryName,
        bool signed = false)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(48);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken4).LineHeight(1.45f));
                page.PageColor(Colors.White);

                if (signed)
                {
                    page.Background()
                        .AlignCenter()
                        .AlignMiddle()
                        .Rotate(-28)
                        .Text("SIGNED")
                        .FontSize(72)
                        .Bold()
                        .FontColor(Colors.Green.Lighten3);
                }

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(brand =>
                        {
                            brand.Item().Text("NileChain").Bold().FontSize(18).FontColor(Colors.Green.Darken3);
                            brand.Item().Text("AI-generated agricultural supply contract")
                                .FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(120).AlignRight().Text(text =>
                        {
                            text.Span(signed ? "SIGNED" : "PENDING")
                                .SemiBold()
                                .FontSize(10)
                                .FontColor(signed ? Colors.Green.Darken2 : Colors.Orange.Darken2);
                        });
                    });

                    col.Item().PaddingTop(10).Text(title).FontSize(16).SemiBold();
                    col.Item().PaddingTop(4).Text($"{factoryName}  ↔  {farmName}")
                        .FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(8).Column(col =>
                {
                    col.Item().Text("Contract Terms").SemiBold().FontSize(12);
                    col.Item().PaddingTop(8).Text(contractText).FontSize(11).LineHeight(1.5f);
                    col.Item().PaddingTop(24).Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(sig =>
                        {
                            sig.Item().Text("Factory").FontSize(9).FontColor(Colors.Grey.Darken1);
                            sig.Item().Text(factoryName).SemiBold();
                            sig.Item().PaddingTop(6).Text(signed ? "✔ Signed" : "Awaiting signature")
                                .FontSize(10)
                                .FontColor(signed ? Colors.Green.Darken2 : Colors.Orange.Darken2);
                        });
                        row.ConstantItem(12);
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(sig =>
                        {
                            sig.Item().Text("Farm").FontSize(9).FontColor(Colors.Grey.Darken1);
                            sig.Item().Text(farmName).SemiBold();
                            sig.Item().PaddingTop(6).Text(signed ? "✔ Signed" : "Pending Signature")
                                .FontSize(10)
                                .FontColor(signed ? Colors.Green.Darken2 : Colors.Orange.Darken2);
                        });
                    });
                });

                page.Footer().AlignCenter().DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken1)).Text(text =>
                {
                    text.Span("Created by NileChain AI · Document version 1.0 · Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}

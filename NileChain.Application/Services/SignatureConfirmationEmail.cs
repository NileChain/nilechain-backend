using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NileChain.Application.Dtos.Email;
using NileChain.Application.Interfaces;
using NileChain.Domain.Entities;
using System.Text.Encodings.Web;

namespace NileChain.Application.Services;

public static class SignatureConfirmationEmail
{
    public static async Task TrySendToBothPartiesAsync(
        IEmailService email,
        ILogger logger,
        Contract contract)
    {
        var farmEmail = contract.FarmMatch?.Farm?.User?.Email;
        var factoryEmail = contract.FarmMatch?.SupplyRequest?.Factory?.User?.Email;
        var crop = contract.FarmMatch?.SupplyRequest?.CropType?.Name ?? "عقد";
        var farm = contract.FarmMatch?.Farm?.Name ?? "";
        var factory = contract.FarmMatch?.SupplyRequest?.Factory?.Name ?? "";
        var title = $"{crop} — {farm} / {factory}";
        var enc = HtmlEncoder.Default;
        var body =
            "<div dir=\"rtl\" style=\"font-family:Arial,sans-serif\">" +
            "<p>تم توقيع العقد بالكامل على منصة NileChain.</p>" +
            "<p><b>" + enc.Encode(title) + "</b></p>" +
            "</div>";

        foreach (var to in new[] { farmEmail, factoryEmail })
        {
            if (string.IsNullOrWhiteSpace(to))
                continue;
            try
            {
                await email.SendAsync(new EmailMessage
                {
                    To = to,
                    Subject = "NileChain — تم توقيع العقد",
                    Body = body,
                    IsHtml = true
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send fully-signed confirmation for contract {ContractId}", contract.ContractId);
            }
        }
    }
}

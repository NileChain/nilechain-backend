using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Channel;
using NileChain.Application.Interfaces;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

/// <summary>Writes an outbox row and logs. Never opens an HTTP connection to a provider.</summary>
public sealed class LoggingOutboundChannel : IOutboundChannel
{
    private readonly IChannelMessageRepository _messages;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser>? _users;
    private readonly ILogger<LoggingOutboundChannel> _logger;

    public LoggingOutboundChannel(
        IChannelMessageRepository messages,
        IUnitOfWork unitOfWork,
        ILogger<LoggingOutboundChannel> logger,
        UserManager<ApplicationUser>? users = null)
    {
        _messages = messages;
        _unitOfWork = unitOfWork;
        _users = users;
        _logger = logger;
    }

    public async Task EnqueueWhatsAppAsync(
        Guid? userId,
        string? toPhone,
        string templateKey,
        string body,
        string? relatedEntityType,
        Guid? relatedEntityId)
    {
        try
        {
            var phone = toPhone?.Trim();
            if (string.IsNullOrWhiteSpace(phone)
                && userId is Guid uid
                && uid != Guid.Empty
                && _users is not null)
            {
                var user = await _users.FindByIdAsync(uid.ToString());
                phone = user?.PhoneNumber?.Trim();
            }

            var missingPhone = string.IsNullOrWhiteSpace(phone);
            var row = new ChannelMessage
            {
                ChannelMessageId = Guid.NewGuid(),
                Channel = ChannelTemplates.WhatsApp,
                ToPhone = missingPhone ? "unknown" : phone!,
                UserId = userId is Guid id && id != Guid.Empty ? id : null,
                TemplateKey = string.IsNullOrWhiteSpace(templateKey) ? "Unknown" : templateKey.Trim(),
                Body = string.IsNullOrWhiteSpace(body) ? "(empty)" : body.Trim(),
                Status = missingPhone ? ChannelMessageStatus.Failed : ChannelMessageStatus.Logged,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                FailReason = missingPhone ? "No Egyptian phone on file" : null,
                CreatedAt = DateTime.UtcNow
            };

            await _messages.AddAsync(row);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "WhatsApp stub logged Template={Template} UserId={UserId} Status={Status} Related={Type}/{Id}",
                row.TemplateKey,
                row.UserId,
                row.Status,
                row.RelatedEntityType,
                row.RelatedEntityId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WhatsApp stub enqueue failed Template={Template}", templateKey);
        }
    }

    public async Task<Result<ChannelMessageListDto>> ListForAdminAsync(string? status, int take = 100)
    {
        ChannelMessageStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<ChannelMessageStatus>(status, ignoreCase: true, out var s))
        {
            parsed = s;
        }

        var rows = await _messages.ListRecentAsync(parsed, take);
        return Result<ChannelMessageListDto>.Success(new ChannelMessageListDto
        {
            Items = rows.Select(m => new ChannelMessageDto
            {
                ChannelMessageId = m.ChannelMessageId,
                Channel = m.Channel,
                ToPhone = m.ToPhone,
                UserId = m.UserId,
                TemplateKey = m.TemplateKey,
                Body = m.Body,
                Status = m.Status.ToString(),
                RelatedEntityType = m.RelatedEntityType,
                RelatedEntityId = m.RelatedEntityId,
                FailReason = m.FailReason,
                CreatedAt = m.CreatedAt
            }).ToList()
        });
    }
}

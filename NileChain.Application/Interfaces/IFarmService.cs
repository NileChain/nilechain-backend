using NileChain.Application.Common;
using NileChain.Application.Dtos.Farm;
using Microsoft.AspNetCore.Http;

namespace NileChain.Application.Interfaces;

public interface IFarmService
{
    Task<Guid> RegisterFarmAsync(Guid userId, string name, string governorate, decimal sizeInFeddans);
    Task<Result<FarmProfileResponse>> GetProfileAsync(Guid userId);
    Task<Result<FarmDashboardResponse>> GetDashboardAsync(Guid userId);
    Task<Result> UpdateProfileAsync(Guid userId, UpdateFarmProfileRequest request);
    Task<Result<FarmDocumentDto>> AddDocumentAsync(Guid userId, IFormFile file);
    Task<Result> DeleteDocumentAsync(Guid userId, Guid documentId);
    Task<Result> AddCropAsync(Guid userId, Guid cropTypeId);
    Task<Result> DeleteCropAsync(Guid userId, Guid cropTypeId);
    Task<Result<List<FarmMatchItemDto>>> GetMatchesAsync(Guid userId, string? status, Guid? cropTypeId);
    Task<Result> RespondToMatchAsync(Guid userId, Guid matchId, string action);
    Task<Result<List<FarmContractDto>>> GetContractsAsync(Guid userId);
    Task<Result<List<ConversationDto>>> GetConversationsAsync(Guid userId);
    Task<Result<List<MessageDto>>> GetMessagesAsync(Guid userId, Guid matchId);
    Task<Result> SendMessageAsync(Guid userId, Guid matchId, string content);
    Task<Result<List<FarmNotificationDto>>> GetNotificationsAsync(Guid userId);
    Task<Result> MarkNotificationAsReadAsync(Guid userId, Guid notificationId);
}

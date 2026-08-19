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
    Task<Result<FarmDocumentDto>> AddDocumentAsync(Guid userId, IFormFile file, string? kybKind);
    Task<Result<List<FarmDocumentDto>>> GetDocumentsAsync(Guid userId);
    Task<Result> DeleteDocumentAsync(Guid userId, Guid documentId);
    Task<Result> AddCropAsync(Guid userId, AddCropRequest request);
    Task<Result> UpdateCropAsync(Guid userId, Guid cropTypeId, UpdateFarmCropRequest request);
    Task<Result> DeleteCropAsync(Guid userId, Guid cropTypeId);
    Task<Result<FarmImageDto>> AddImageAsync(Guid userId, IFormFile file);
    Task<Result> DeleteImageAsync(Guid userId, Guid imageId);
    Task<Result> CounterOfferAsync(Guid userId, Guid matchId, CounterOfferRequest request);
    Task<Result> AcceptCounterOfferAsync(Guid userId, Guid matchId);
    Task<Result<List<FarmCertificationDto>>> GetCertificationsAsync(Guid userId);
    Task<Result> AddCertificationAsync(Guid userId, AddFarmCertificationRequest request);
    Task<Result> DeleteCertificationAsync(Guid userId, Guid certificationId);
    Task<Result<FarmMatchesListResponse>> GetMatchesAsync(
        Guid userId,
        string? status,
        Guid? cropTypeId,
        string? sort = null,
        string? search = null,
        int? days = null,
        int page = 1,
        int pageSize = 20);
    Task<Result> RespondToMatchAsync(Guid userId, Guid matchId, string action);
    Task<Result<FarmContractDto>> GetOrCreateContractForMatchAsync(Guid userId, Guid matchId);
    Task<Result<List<FarmContractDto>>> GetContractsAsync(Guid userId);
    Task<Result<FarmContractDto>> GetContractAsync(Guid userId, Guid contractId);
    Task<Result<FarmContractDto>> ApproveContractAsync(
        Guid userId,
        Guid contractId,
        string? otpCode,
        string? ipAddress,
        string? userAgent,
        string? consentText);
    Task<Result<FarmContractDto>> RejectContractAsync(Guid userId, Guid contractId);
    Task<Result<(byte[] PdfBytes, string FileName)>> GetContractPdfAsync(Guid userId, Guid contractId);
    Task<Result<List<ConversationDto>>> GetConversationsAsync(Guid userId);
    Task<Result<List<MessageDto>>> GetMessagesAsync(Guid userId, Guid matchId);
    Task<Result> SendMessageAsync(Guid userId, Guid matchId, string content);
    Task<Result<FactoryPublicProfileDto>> GetMatchedFactoryPublicProfileAsync(Guid userId, Guid factoryId);
    Task<Result<List<FarmNotificationDto>>> GetNotificationsAsync(Guid userId);
    Task<Result> MarkNotificationAsReadAsync(Guid userId, Guid notificationId);
}

using NileChain.Application.Common;
using NileChain.Application.Dtos.Admin;
using NileChain.Application.Dtos.Factory;
using NileChain.Application.Dtos.Farm;

namespace NileChain.Application.Interfaces;

public interface IFactoryService
{
    Task<Guid> RegisterFactoryAsync(Guid userId, string name, string governorate);
    Task<Result<FactoryProfileResponse>> GetProfileAsync(Guid userId);
    Task<Result> UpdateProfileAsync(Guid userId, UpdateFactoryProfileRequest request);
    Task<Result<List<FactoryDocumentDto>>> GetDocumentsAsync(Guid userId);
    Task<Result<FactoryDocumentDto>> AddDocumentAsync(Guid userId, Microsoft.AspNetCore.Http.IFormFile file, string? kybKind);
    Task<Result> DeleteDocumentAsync(Guid userId, Guid documentId);
    Task<Result<CreateSupplyRequestResponse>> CreateRequestAsync(
        Guid userId,
        CreateSupplyRequestRequest request,
        string? idempotencyKey = null);
    Task<Result<PagedResult<FactorySupplyRequestListItemDto>>> GetRequestsAsync(
        Guid userId,
        int page = 1,
        int pageSize = 10,
        string? status = null);
    Task<Result<FactorySupplyRequestDetailDto>> GetRequestAsync(Guid userId, Guid requestId);
    Task<Result<FactorySupplyRequestDetailDto>> UpdateRequestDeliveryTermsAsync(
        Guid userId,
        Guid requestId,
        UpdateSupplyRequestDeliveryTermsRequest request);
    Task<Result<FactorySupplyRequestDetailDto>> UpdateRequestGeoScopeAsync(
        Guid userId,
        Guid requestId,
        UpdateSupplyRequestGeoScopeRequest request);
    Task<Result<FactorySupplyRequestDetailDto>> ExpandGeoAsync(Guid userId, Guid requestId);
    Task<Result<FactorySupplyRequestDetailDto>> ShowMoreMatchesAsync(Guid userId, Guid requestId);
    Task<Result> CancelRequestAsync(Guid userId, Guid requestId);
    Task<Result<FactoryDashboardResponse>> GetDashboardAsync(Guid userId);
    Task<Result<FactorySupplierScorecardDto>> GetSupplierScorecardAsync(Guid userId, Guid farmId);
    Task<Result<List<FactoryMatchItemDto>>> GetRequestMatchesAsync(Guid userId, Guid requestId, string? sort = null);
    Task<Result> ExcludeMatchAsync(Guid userId, Guid matchId);
    Task<Result> AcceptCounterOfferAsync(Guid userId, Guid matchId);
    Task<Result> RejectCounterOfferAsync(Guid userId, Guid matchId);
    Task<Result> CounterOfferAsync(Guid userId, Guid matchId, CounterOfferRequest request);
    Task<Result<List<FactoryMatchedFarmDto>>> GetMatchedFarmsAsync(Guid userId);
    Task<Result<List<FarmListingDto>>> GetPublishedListingsAsync(Guid? cropTypeId = null, string? governorate = null);

    Task<Result<List<FactoryNotificationDto>>> GetNotificationsAsync(Guid userId);
    Task<Result> MarkNotificationAsReadAsync(Guid userId, Guid notificationId);

    Task<Result<List<FactoryConversationDto>>> GetConversationsAsync(Guid userId);
    Task<Result<FactoryActiveMatchDto>> GetActiveMatchWithFarmAsync(Guid userId, Guid farmId);
    Task<Result<List<FactoryMessageDto>>> GetMessagesAsync(Guid userId, Guid matchId);
    Task<Result> SendMessageAsync(Guid userId, Guid matchId, string content);

    Task<Result<PersistContractResponse>> PersistContractAsync(Guid userId, PersistContractRequest request);
    Task<Result<List<FactoryContractDto>>> GetContractsAsync(Guid userId);
    Task<Result<FactoryContractDto>> GetContractAsync(Guid userId, Guid contractId);
    Task<Result<FactoryContractDto>> ApproveContractAsync(
        Guid userId,
        Guid contractId,
        string? otpCode,
        string? ipAddress,
        string? userAgent,
        string? consentText);
    Task<Result<FactoryContractDto>> RejectContractAsync(Guid userId, Guid contractId);
    Task<Result<(byte[] PdfBytes, string FileName)>> GetContractPdfAsync(Guid userId, Guid contractId);
}

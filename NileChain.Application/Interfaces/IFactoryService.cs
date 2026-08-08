using NileChain.Application.Common;
using NileChain.Application.Dtos.Factory;
using NileChain.Application.Dtos.Farm;

namespace NileChain.Application.Interfaces;

public interface IFactoryService
{
    Task<Guid> RegisterFactoryAsync(Guid userId, string name, string governorate);
    Task<Result<FactoryProfileResponse>> GetProfileAsync(Guid userId);
    Task<Result> UpdateProfileAsync(Guid userId, UpdateFactoryProfileRequest request);
    Task<Result<CreateSupplyRequestResponse>> CreateRequestAsync(Guid userId, CreateSupplyRequestRequest request);
    Task<Result<List<FactoryMatchItemDto>>> GetRequestMatchesAsync(Guid userId, Guid requestId);
    Task<Result<List<FactoryMatchedFarmDto>>> GetMatchedFarmsAsync(Guid userId);

    Task<Result<List<FactoryNotificationDto>>> GetNotificationsAsync(Guid userId);
    Task<Result> MarkNotificationAsReadAsync(Guid userId, Guid notificationId);

    Task<Result<List<FactoryConversationDto>>> GetConversationsAsync(Guid userId);
    Task<Result<List<FactoryMessageDto>>> GetMessagesAsync(Guid userId, Guid matchId);
    Task<Result> SendMessageAsync(Guid userId, Guid matchId, string content);

    Task<Result<PersistContractResponse>> PersistContractAsync(Guid userId, PersistContractRequest request);
    Task<Result<List<FactoryContractDto>>> GetContractsAsync(Guid userId);
    Task<Result<FactoryContractDto>> GetContractAsync(Guid userId, Guid contractId);
    Task<Result<FactoryContractDto>> ApproveContractAsync(Guid userId, Guid contractId);
    Task<Result<FactoryContractDto>> RejectContractAsync(Guid userId, Guid contractId);
    Task<Result<(byte[] PdfBytes, string FileName)>> GetContractPdfAsync(Guid userId, Guid contractId);
}

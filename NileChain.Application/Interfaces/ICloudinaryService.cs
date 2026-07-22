using Microsoft.AspNetCore.Http;

namespace NileChain.Application.Interfaces;

public interface ICloudinaryService
{
    Task<(string Url, string PublicId)> UploadAsync(IFormFile file);
    Task DeleteAsync(string publicId);
}

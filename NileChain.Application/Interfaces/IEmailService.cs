using NileChain.Application.Dtos.Email;

namespace NileChain.Application.Interfaces
{
    public interface IEmailService
    {
        Task SendAsync(EmailMessage message);
    }
}

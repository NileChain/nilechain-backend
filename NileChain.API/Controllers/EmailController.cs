using NileChain.Application.Dtos.Email;
using NileChain.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace NileChain.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmailController : ControllerBase
    {
        private readonly IEmailService _emailService;

        public EmailController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        [HttpPost("test")]
        public async Task<IActionResult> Test()
        {
            await _emailService.SendAsync(
                new EmailMessage
                {
                    To = "assemomar202@gmail.com",
                    Subject = "NileChain SMTP Test",
                    Body = """
                <h2>Congratulations 🎉</h2>

                <p>Your SMTP configuration is working successfully.</p>
                """
                });

            return Ok("Email Sent Successfully.");
        }
    }
}

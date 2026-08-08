using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NileChain.Application.Dtos.Email;
using NileChain.Application.Interfaces;

namespace NileChain.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
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

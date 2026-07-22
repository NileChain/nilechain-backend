using NileChain.Application.Interfaces;

namespace NileChain.Infrastructure.Email
{
    public class TemplateRendererService : ITemplateRenderer
    {
        public async Task<string> RenderAsync(
            string templateName,
            Dictionary<string, string> placeholders)
        {
            var templatePath = Path.Combine(
                AppContext.BaseDirectory,
                "Email",
                "Templates",
                templateName);

            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException(
                    $"Email template '{templateName}' was not found.",
                    templatePath);
            }

            var html = await File.ReadAllTextAsync(templatePath);

            foreach (var placeholder in placeholders)
            {
                html = html.Replace(
                    $"{{{{{placeholder.Key}}}}}",
                    placeholder.Value);
            }

            return html;
        }
    }
}

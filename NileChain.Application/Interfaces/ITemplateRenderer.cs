namespace NileChain.Application.Interfaces
{
    public interface ITemplateRenderer
    {
        Task<string> RenderAsync(
            string templateName,
            Dictionary<string, string> placeholders);
    }
}

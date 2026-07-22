namespace NileChain.Infrastructure.Email
{
    public static class TemplateRenderer
    {
        public static string Render(
            string template,
            Dictionary<string, string> values)
        {
            foreach (var item in values)
            {
                template = template.Replace(
                    "{{" + item.Key + "}}",
                    item.Value);
            }

            return template;
        }
    }
}

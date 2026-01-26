using Scriban;

namespace SummarizationService.Prompting;

public sealed class PromptTemplateStore
{
    private readonly string _basePath;

    public PromptTemplateStore(IConfiguration configuration)
    {
        _basePath = configuration["PROMPT_TEMPLATE_PATH"]
            ?? Path.Combine(AppContext.BaseDirectory, "prompts");
    }

    public string Render(string templatePath, object model)
    {
        var fullPath = Path.Combine(_basePath, templatePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Prompt template not found: {templatePath}");
        }

        var content = File.ReadAllText(fullPath);
        var template = Template.Parse(content);
        if (template.HasErrors)
        {
            var errors = string.Join("; ", template.Messages.Select(x => x.Message));
            throw new InvalidOperationException($"Prompt template parse failed: {errors}");
        }

        return template.Render(model, member => member.Name);
    }

    public string RenderContent(string content, object model)
    {
        var template = Template.Parse(content);
        if (template.HasErrors)
        {
            var errors = string.Join("; ", template.Messages.Select(x => x.Message));
            throw new InvalidOperationException($"Prompt template parse failed: {errors}");
        }

        return template.Render(model, member => member.Name);
    }
}

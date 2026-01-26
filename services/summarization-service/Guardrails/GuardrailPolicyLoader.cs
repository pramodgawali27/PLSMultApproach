using System.Text.Json;

namespace SummarizationService.Guardrails;

public static class GuardrailPolicyLoader
{
    public static GuardrailPolicy Load(IConfiguration configuration)
    {
        var basePath = configuration["GUARDRAIL_POLICY_PATH"]
            ?? Path.Combine(AppContext.BaseDirectory, "policies", "guardrails.default.json");

        if (!File.Exists(basePath))
        {
            return new GuardrailPolicy();
        }

        var json = File.ReadAllText(basePath);
        var policy = JsonSerializer.Deserialize<GuardrailPolicy>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return policy ?? new GuardrailPolicy();
    }
}

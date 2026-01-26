namespace SummarizationService.Guardrails;

public sealed class GuardrailPolicy
{
    public GuardrailRule PiiPhi { get; set; } = new();
    public GuardrailRule Jailbreak { get; set; } = new();
    public ToxicityRule Toxicity { get; set; } = new();
    public GroundednessRule Groundedness { get; set; } = new();
}

public sealed class GuardrailRule
{
    public bool Enabled { get; set; }
    public string Mode { get; set; } = "warn";
    public List<string> Patterns { get; set; } = new();
}

public sealed class ToxicityRule
{
    public bool Enabled { get; set; }
    public string Mode { get; set; } = "warn";
    public List<string> Keywords { get; set; } = new();
}

public sealed class GroundednessRule
{
    public bool Enabled { get; set; }
    public string Mode { get; set; } = "warn";
    public List<string> RequiredForLanes { get; set; } = new();
}

public sealed record GuardrailFinding(string Name, string Decision, string Reason);

public sealed record GuardrailResult(string FinalDecision, IReadOnlyList<GuardrailFinding> Findings);

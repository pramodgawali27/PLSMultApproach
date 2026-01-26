using System.Text.RegularExpressions;
using PLS.Contracts;

namespace SummarizationService.Guardrails;

public sealed class GuardrailEvaluator
{
    private readonly GuardrailPolicy _policy;

    public GuardrailEvaluator(GuardrailPolicy policy)
    {
        _policy = policy;
    }

    public GuardrailResult Evaluate(SummaryLane lane, string input)
    {
        var findings = new List<GuardrailFinding>();
        var finalDecision = "pass";

        if (_policy.PiiPhi.Enabled)
        {
            foreach (var pattern in _policy.PiiPhi.Patterns)
            {
                if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                {
                    var decision = _policy.PiiPhi.Mode;
                    findings.Add(new GuardrailFinding("pii_phi", decision, "Sensitive data pattern detected."));
                    finalDecision = Merge(finalDecision, decision);
                    break;
                }
            }
        }

        if (_policy.Jailbreak.Enabled)
        {
            foreach (var pattern in _policy.Jailbreak.Patterns)
            {
                if (input.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    var decision = _policy.Jailbreak.Mode;
                    findings.Add(new GuardrailFinding("jailbreak", decision, "Potential jailbreak phrase detected."));
                    finalDecision = Merge(finalDecision, decision);
                    break;
                }
            }
        }

        if (_policy.Toxicity.Enabled)
        {
            foreach (var keyword in _policy.Toxicity.Keywords)
            {
                if (input.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    var decision = _policy.Toxicity.Mode;
                    findings.Add(new GuardrailFinding("toxicity", decision, "Potential toxic keyword detected."));
                    finalDecision = Merge(finalDecision, decision);
                    break;
                }
            }
        }

        if (_policy.Groundedness.Enabled && _policy.Groundedness.RequiredForLanes.Contains(lane.ToString()))
        {
            var decision = _policy.Groundedness.Mode;
            findings.Add(new GuardrailFinding("groundedness", decision, "Groundedness required for this lane."));
            finalDecision = Merge(finalDecision, decision);
        }

        return new GuardrailResult(finalDecision, findings);
    }

    private static string Merge(string current, string next)
    {
        if (current == "block" || next == "block") return "block";
        if (current == "warn" || next == "warn") return "warn";
        return "pass";
    }
}

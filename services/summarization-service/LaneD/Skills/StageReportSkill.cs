namespace SummarizationService.LaneD.Skills;

/// <summary>
/// Thin wrapper around LaneDStageReporter exposed as an IAgentSkill.
/// Allows the orchestrator to report stage transitions through the skill registry.
/// </summary>
public sealed class StageReportSkill : IAgentSkill
{
    public string Name => "StageReportSkill";

    private readonly LaneDStageReporter _reporter;

    public StageReportSkill(LaneDStageReporter reporter)
    {
        _reporter = reporter;
    }

    public Task ReportAsync(string stageId, string message, CancellationToken cancellationToken)
        => _reporter.ReportAsync(stageId, message, cancellationToken);
}

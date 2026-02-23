namespace SummarizationService.LaneD.Skills;

/// <summary>
/// Marker interface for Lane D agent skills.
/// Skills are registered in SkillRegistry and may be invoked by name during orchestration.
/// </summary>
public interface IAgentSkill
{
    string Name { get; }
}

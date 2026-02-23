using SummarizationService.LaneD.Skills;

namespace SummarizationService.LaneD;

/// <summary>
/// Registry of available agent skills. Skills are looked up by name at runtime.
/// Register skills via RegisterSkill(); retrieve via Get{T}().
/// </summary>
public sealed class SkillRegistry
{
    private readonly Dictionary<string, IAgentSkill> _skills = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterSkill(IAgentSkill skill)
        => _skills[skill.Name] = skill;

    public T Get<T>(string name) where T : class, IAgentSkill
    {
        if (!_skills.TryGetValue(name, out var skill))
            throw new KeyNotFoundException($"Skill '{name}' is not registered.");
        if (skill is not T typed)
            throw new InvalidCastException($"Skill '{name}' is not of type {typeof(T).Name}.");
        return typed;
    }

    public bool TryGet<T>(string name, out T? skill) where T : class, IAgentSkill
    {
        skill = null;
        if (!_skills.TryGetValue(name, out var raw) || raw is not T typed)
            return false;
        skill = typed;
        return true;
    }
}

namespace SummarizationService.LaneD;

/// <summary>Request sent to an agent stage.</summary>
public sealed record AgentRequest(
    string AgentName,
    string SystemPrompt,
    string UserPrompt,
    double Temperature,
    int MaxTokens,
    IReadOnlyList<AgentTool>? Tools);

/// <summary>Response from an agent stage.</summary>
public sealed record AgentResponse(string Text, int TokensUsed, string AgentName);

/// <summary>A function tool available to an agent.</summary>
public sealed record AgentTool(string Name, string Description, object InputSchema);

/// <summary>Input context passed to the Lane D orchestrator.</summary>
public sealed record LaneDContext(
    Guid JobId,
    Guid DocumentId,
    Guid TenantId,
    Guid UserId,
    string StorageUri,
    string ContentType,
    string? Title,
    string? TherapeuticArea,
    string? Audience,
    string? PromptOverride);

/// <summary>Output returned by the Lane D orchestrator.</summary>
public sealed record LaneDResult(
    string FinalMarkdown,
    string? ModelInfoJson,
    IReadOnlyList<LaneDStageArtifact> Stages);

/// <summary>Per-stage artifact emitted during Lane D orchestration.</summary>
public sealed record LaneDStageArtifact(
    string StageId,
    string StageName,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string? ArtifactJson);

/// <summary>Runtime contract used by agent stages — switches between Local (direct LLM) and Foundry.</summary>
public interface ILaneDAgentRuntime
{
    Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken);
}

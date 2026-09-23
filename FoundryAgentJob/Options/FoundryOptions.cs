namespace FoundryAgentJob.Options;

public class FoundryOptions
{
    public const string SectionName = "Foundry";

    public string ProjectEndpoint { get; set; } = string.Empty;

    public string AgentName { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public int JobIntervalSeconds { get; set; } = 30;
}

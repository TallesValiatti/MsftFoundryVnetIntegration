using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using FoundryAgentJob.Options;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using Quartz;

namespace FoundryAgentJob.Jobs;

[DisallowConcurrentExecution]
public class AgentJob(
    AIProjectClient projectClient,
    IOptions<FoundryOptions> options,
    ILogger<AgentJob> logger) : IJob
{
    private readonly FoundryOptions _options = options.Value;

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Calling Foundry agent '{AgentName}' at {ProjectEndpoint}",
            _options.AgentName,
            _options.ProjectEndpoint);

        try
        {
            AgentReference agentReference = new(name: _options.AgentName, version: null);

            ProjectResponsesClient responsesClient = projectClient.ProjectOpenAIClient
                .GetProjectResponsesClientForAgent(agentReference);

            ResponseResult response = await responsesClient.CreateResponseAsync(
                userInputText: _options.Prompt,
                previousResponseId: null,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Foundry agent response: {AgentResponse}",
                response.GetOutputText());
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error calling Foundry agent '{AgentName}': {ErrorMessage}",
                _options.AgentName,
                ex.Message);
        }
    }
}

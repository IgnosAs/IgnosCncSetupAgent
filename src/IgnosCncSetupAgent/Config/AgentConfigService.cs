using Ignos.Api.Client;

namespace IgnosCncSetupAgent.Config;

public class AgentConfigService : IAgentConfigService
{
    private readonly ICncFileTransferClient _cncFileTransferClient;

    public AgentConfigService(ICncFileTransferClient cncFileTransferClient)
    {
        _cncFileTransferClient = cncFileTransferClient;
    }

    public Task<AgentConfigDto> GetQueueConfig(string? agentId, CancellationToken cancellationToken)
    {
        return _cncFileTransferClient.GetCncAgentConfigAsync(agentId, cancellationToken);
    }
}

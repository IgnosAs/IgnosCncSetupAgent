using Ignos.Api.Client;
using Microsoft.Extensions.Options;

namespace IgnosCncSetupAgent.Config;

public class AgentConfigService(ICncSetupAgentClient cncSetupAgentClient,
    IOptions<FileTransferWorkerOptions> options) : IAgentConfigService
{
    public Task<AgentConfigDto> GetQueueConfig(CancellationToken cancellationToken)
    {
        return cncSetupAgentClient.GetCncAgentConfigAsync(
            options.Value.AgentId, options.Value.AgentVersion, cancellationToken);
    }
}

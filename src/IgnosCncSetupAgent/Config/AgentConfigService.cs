using Ignos.Api.Client;
using Microsoft.Extensions.Options;

namespace IgnosCncSetupAgent.Config;

public class AgentConfigService : IAgentConfigService
{
    private readonly ICncFileTransferClient _cncFileTransferClient;
    private readonly FileTransferWorkerOptions _options;

    public AgentConfigService(ICncFileTransferClient cncFileTransferClient,
        IOptions<FileTransferWorkerOptions> options)
    {
        _cncFileTransferClient = cncFileTransferClient;
        _options = options.Value;
    }

    public Task<AgentConfigDto> GetQueueConfig(CancellationToken cancellationToken)
    {
        return _cncFileTransferClient.GetCncAgentConfigAsync(
            _options.AgentId, _options.AgentVersion, cancellationToken);
    }
}

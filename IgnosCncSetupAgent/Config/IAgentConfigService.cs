﻿using Ignos.Api.Client;

namespace IgnosCncSetupAgent.Config;

public interface IAgentConfigService
{
    Task<AgentConfigDto> GetQueueConfig(string? agentId, CancellationToken cancellationToken);
}
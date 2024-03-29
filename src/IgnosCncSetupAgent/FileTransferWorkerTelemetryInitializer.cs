﻿using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Options;

namespace IgnosCncSetupAgent;

public class FileTransferWorkerTelemetryInitializer(IOptions<FileTransferWorkerOptions> options) : ITelemetryInitializer
{
    private readonly string _agentVersion = options.Value.AgentVersion;

    public void Initialize(ITelemetry telemetry)
    {
        telemetry.Context.GlobalProperties["AgentVersion"] = _agentVersion;
    }
}

using System.Reflection;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace IgnosCncSetupAgent;

public class FileTransferWorkerTelemetryInitializer : ITelemetryInitializer
{
    private static string buildNumber;

    static FileTransferWorkerTelemetryInitializer()
    {
        var assembly = Assembly.GetExecutingAssembly();
        buildNumber =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "local";
    }

    public void Initialize(ITelemetry telemetry)
    {
        telemetry.Context.GlobalProperties["AgentBuildNumber"] = buildNumber;
    }
}

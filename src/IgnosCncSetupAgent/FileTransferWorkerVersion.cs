using System.Reflection;

namespace IgnosCncSetupAgent;

public class FileTransferWorkerVersion
{
    public static string AgentVersion { get; private set; }

    static FileTransferWorkerVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        AgentVersion =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "local";
    }
}

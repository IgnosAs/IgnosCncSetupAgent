using Azure.Messaging.ServiceBus;

namespace IgnosCncSetupAgent;

public class FileTransferWorkerOptions
{
    public const string FileTransferWorker = "FileTransferWorker";

    public string? AgentId { get; set; }

    public ServiceBusTransportType ServiceBusTransportType { get; set; } 
        = ServiceBusTransportType.AmqpTcp;

    public int MaxConcurrentListeners { get; set; } = 1;

    public string AgentVersion => FileTransferWorkerVersion.AgentVersion;
}

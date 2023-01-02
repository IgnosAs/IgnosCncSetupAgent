using Azure.Messaging.ServiceBus;
using Ignos.Api.Client;
using Microsoft.Extensions.Options;

namespace IgnosCncSetupAgent.Messaging;

public class ServiceBusListenerFactory : IServiceBusListenerFactory
{
    private readonly FileTransferWorkerOptions _options;

    public ServiceBusListenerFactory(IOptions<FileTransferWorkerOptions> options)
    {
        _options = options.Value;
    }

    public ServiceBusListener CreateServiceBusListener(
        AgentConfigDto agentConfig,
        Func<ProcessMessageEventArgs, Task> messageHandler,
        Func<ProcessErrorEventArgs, Task> errorHandler) =>
        new ServiceBusListener(agentConfig, messageHandler, errorHandler,
            _options.ServiceBusTransportType, _options.MaxConcurrentListeners);
}

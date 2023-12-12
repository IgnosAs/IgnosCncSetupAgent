using Azure.Messaging.ServiceBus;
using Ignos.Api.Client;
using Microsoft.Extensions.Options;

namespace IgnosCncSetupAgent.Messaging;

public class ServiceBusListenerFactory(IOptions<FileTransferWorkerOptions> options) : IServiceBusListenerFactory
{
    public ServiceBusListener CreateServiceBusListener(
        AgentConfigDto agentConfig,
        Func<ProcessMessageEventArgs, Task> messageHandler,
        Func<ProcessErrorEventArgs, Task> errorHandler) =>
        new ServiceBusListener(agentConfig, messageHandler, errorHandler,
            options.Value.ServiceBusTransportType, options.Value.MaxConcurrentListeners);
}

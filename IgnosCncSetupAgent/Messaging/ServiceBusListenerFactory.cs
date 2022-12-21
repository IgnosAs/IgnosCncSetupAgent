using Azure.Messaging.ServiceBus;
using Ignos.Api.Client;

namespace IgnosCncSetupAgent.Messaging;

public class ServiceBusListenerFactory : IServiceBusListenerFactory
{
    public ServiceBusListener CreateServiceBusListener(
        AgentConfigDto agentConfig,
        Func<ProcessMessageEventArgs, Task> messageHandler,
        Func<ProcessErrorEventArgs, Task> errorHandler,
        ServiceBusTransportType serviceBusTransportType,
        int maxConcurrentListeners) =>
        new ServiceBusListener(agentConfig, messageHandler, errorHandler,
            serviceBusTransportType, maxConcurrentListeners);
}

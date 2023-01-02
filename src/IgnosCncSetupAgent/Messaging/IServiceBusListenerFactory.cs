using Azure.Messaging.ServiceBus;
using Ignos.Api.Client;

namespace IgnosCncSetupAgent.Messaging;

public interface IServiceBusListenerFactory
{
    ServiceBusListener CreateServiceBusListener(
        AgentConfigDto agentConfig,
        Func<ProcessMessageEventArgs, Task> messageHandler,
        Func<ProcessErrorEventArgs, Task> errorHandler);
}
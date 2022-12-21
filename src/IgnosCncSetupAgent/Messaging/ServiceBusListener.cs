using Azure;
using Azure.Messaging.ServiceBus;
using Ignos.Api.Client;

namespace IgnosCncSetupAgent.Messaging;

public sealed class ServiceBusListener : IAsyncDisposable
{
    private readonly AzureSasCredential _credential;
    private readonly ServiceBusClient _client;
    private readonly ServiceBusProcessor _serviceBusProcessor;

    public ServiceBusListener(AgentConfigDto agentConfig,
        Func<ProcessMessageEventArgs, Task> messageHandler, Func<ProcessErrorEventArgs, Task> errorHandler,
        ServiceBusTransportType serviceBusTransportType, int maxConcurrentListeners)
    {
        _credential = new AzureSasCredential(agentConfig.SharedAccessSignature);
        _client = new ServiceBusClient(agentConfig.ServiceBusNamespace, _credential, new ServiceBusClientOptions { TransportType = serviceBusTransportType });
        _serviceBusProcessor = _client.CreateProcessor(
            agentConfig.QueueName,
            new ServiceBusProcessorOptions
            {
                Identifier = $"ignos-agent-{agentConfig.QueueName}",
                MaxConcurrentCalls = maxConcurrentListeners
            });

        _serviceBusProcessor.ProcessMessageAsync += messageHandler;
        _serviceBusProcessor.ProcessErrorAsync += errorHandler;
    }

    public void Reconfigure(AgentConfigDto agentConfig)
    {
        _credential.Update(agentConfig.SharedAccessSignature);
    }

    public Task StartProcessing(CancellationToken cancellationToken) => _serviceBusProcessor.StartProcessingAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _serviceBusProcessor.DisposeAsync();
        await _client.DisposeAsync();
    }
}

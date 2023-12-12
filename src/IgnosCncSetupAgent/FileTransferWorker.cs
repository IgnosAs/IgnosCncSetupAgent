using IgnosCncSetupAgent.Config;
using IgnosCncSetupAgent.FileTransfer;
using IgnosCncSetupAgent.Messaging;
using Microsoft.Extensions.Options;

namespace IgnosCncSetupAgent;

public class FileTransferWorker(IAgentConfigService agentConfigService,
    IServiceBusListenerFactory serviceBusListenerFactory,
    IFileTransferHandlers fileTransferHandlers,
    ILogger<FileTransferWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var config = await agentConfigService.GetQueueConfig(stoppingToken);

            await using var serviceBusListener = serviceBusListenerFactory.CreateServiceBusListener(
                config,
                fileTransferHandlers.MessageHandler,
                fileTransferHandlers.ErrorHandler);

            await serviceBusListener.StartProcessing(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("FileTransferWorker running at: {time}", DateTimeOffset.Now);

                var delayUntilConfigurationRefresh = config.ConfigurationRefreshTime - DateTimeOffset.Now;

                if (delayUntilConfigurationRefresh > TimeSpan.FromSeconds(5))
                    await Task.Delay(delayUntilConfigurationRefresh, stoppingToken);

                logger.LogInformation("FileTransferWorker refreshing configuration at: {time}", DateTimeOffset.Now);

                config = await agentConfigService.GetQueueConfig(stoppingToken);

                serviceBusListener.Reconfigure(config);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Exception in {nameof(FileTransferWorker)}");
            throw;
        }
    }
}

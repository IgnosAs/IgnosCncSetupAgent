using IgnosCncSetupAgent.Config;
using IgnosCncSetupAgent.FileTransfer;
using IgnosCncSetupAgent.Messaging;
using Microsoft.ApplicationInsights;

namespace IgnosCncSetupAgent;

public class FileTransferWorker(IAgentConfigService agentConfigService,
    IServiceBusListenerFactory serviceBusListenerFactory,
    IFileTransferHandlers fileTransferHandlers,
    TelemetryClient telemetryClient,
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
        catch (OperationCanceledException)
        {
            // When the stopping token is canceled, for example, a call made from services.msc,
            // we shouldn't exit with a non-zero exit code. In other words, this is expected...
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Exception in {nameof(FileTransferWorker)}");

            //https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service
            // Terminates this process and returns an exit code to the operating system.
            // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
            // performs one of two scenarios:
            // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
            // 2. When set to "StopHost": will cleanly stop the host, and log errors.
            //
            // In order for the Windows Service Management system to leverage configured
            // recovery options, we need to terminate the process with a non-zero exit code.

            Environment.Exit(1);
        }
        finally
        {
            //https://learn.microsoft.com/en-us/azure/azure-monitor/app/api-custom-events-metrics#flushing-data
            await telemetryClient.FlushAsync(CancellationToken.None);
        }
    }
}

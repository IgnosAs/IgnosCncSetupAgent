using IgnosCncSetupAgent.Config;
using IgnosCncSetupAgent.FileTransfer;
using IgnosCncSetupAgent.Messaging;
using Microsoft.Extensions.Options;

namespace IgnosCncSetupAgent;

public class FileTransferWorker : BackgroundService
{
    private readonly IAgentConfigService _agentConfigService;
    private readonly IServiceBusListenerFactory _serviceBusListenerFactory;
    private readonly ILogger<FileTransferWorker> _logger;
    private readonly IFileTransferHandlers _fileTransferHandlers;
    private readonly FileTransferWorkerOptions _options;

    public FileTransferWorker(IAgentConfigService agentConfigService,
        IServiceBusListenerFactory serviceBusListenerFactory,
        IFileTransferHandlers fileTransferHandlers, IOptions<FileTransferWorkerOptions> options,
        ILogger<FileTransferWorker> logger)
    {
        _agentConfigService = agentConfigService;
        _serviceBusListenerFactory = serviceBusListenerFactory;
        _logger = logger;
        _fileTransferHandlers = fileTransferHandlers;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var config = await _agentConfigService.GetQueueConfig(stoppingToken);

            await using var serviceBusListener = _serviceBusListenerFactory.CreateServiceBusListener(
                config,
                _fileTransferHandlers.MessageHandler,
                _fileTransferHandlers.ErrorHandler);

            await serviceBusListener.StartProcessing(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("FileTransferWorker running at: {time}", DateTimeOffset.Now);

                var delayUntilConfigurationRefresh = config.ConfigurationRefreshTime - DateTimeOffset.Now;

                if (delayUntilConfigurationRefresh > TimeSpan.FromSeconds(5))
                    await Task.Delay(delayUntilConfigurationRefresh, stoppingToken);

                _logger.LogInformation("FileTransferWorker refreshing configuration at: {time}", DateTimeOffset.Now);

                config = await _agentConfigService.GetQueueConfig(stoppingToken);

                serviceBusListener.Reconfigure(config);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception in {nameof(FileTransferWorker)}");
            throw;
        }
    }
}

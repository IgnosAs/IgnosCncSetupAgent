using Ignos.Api.Client;
using IgnosCncSetupAgent;
using IgnosCncSetupAgent.Config;
using IgnosCncSetupAgent.FileTransfer;
using IgnosCncSetupAgent.Messaging;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => options.ServiceName = "Ignos CNC Setup Agent Service")
    .ConfigureServices((hostContext, services) =>
    {
        services.Configure<FileTransferWorkerOptions>(
            hostContext.Configuration.GetSection(FileTransferWorkerOptions.FileTransferWorker));

        services.AddTransient<IAgentConfigService, AgentConfigService>();
        services.AddTransient<IServiceBusListenerFactory, ServiceBusListenerFactory>();
        services.AddTransient<IFileTransferHandlers, FileTransferHandlers>();

        services.AddHostedService<FileTransferWorker>();

        services.AddApplicationInsightsTelemetryWorkerService();

        services.AddIgnosHttpClient(hostContext.Configuration)
            .AddAzureAuthenticationHandler()
            .AddDefaultPolicyHandler();

        services.AddIgnosApiClient<ICncFileTransferClient, CncFileTransferClient>();
    })
    .Build();

host.Run();

using System.Runtime.InteropServices;
using Ignos.Api.Client;
using IgnosCncSetupAgent;
using IgnosCncSetupAgent.Config;
using IgnosCncSetupAgent.FileTransfer;
using IgnosCncSetupAgent.Messaging;
using Microsoft.ApplicationInsights.Extensibility;

// sc.exe create "Ignos Keep Agent Service" start=auto binpath="path.to.agent.exe" obj="NT AUTHORITY\LocalService"
// (if desirable, and local computer has access to shares, run as obj="NT AUTHORITY\NetworkService")
// Give access to folder containing service files to account running the service

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => options.ServiceName = "Ignos Keep Agent Service")
    .ConfigureServices((hostContext, services) =>
    {
        services.Configure<FileTransferWorkerOptions>(
            hostContext.Configuration.GetSection(FileTransferWorkerOptions.FileTransferWorker));

        services.AddTransient<IAgentConfigService, AgentConfigService>();
        services.AddTransient<IServiceBusListenerFactory, ServiceBusListenerFactory>();
        services.AddTransient<IFileTransferHandlers, FileTransferHandlers>();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddTransient<IMachineShareAuthenticator, AuthenticateMachineShareWithNetworkConnection>();
        }
        else
        {
            services.AddTransient<IMachineShareAuthenticator, AuthenticateMachineShareNoAuthentication>();
        }

        services.AddHostedService<FileTransferWorker>();

        services.AddSingleton<ITelemetryInitializer, FileTransferWorkerTelemetryInitializer>();
        services.AddApplicationInsightsTelemetryWorkerService();

        services.AddIgnosHttpClient(hostContext.Configuration)
            .AddAzureAuthenticationHandler()
            .AddDefaultPolicyHandler();

        services.AddIgnosApiClient<ICncSetupAgentClient, CncSetupAgentClient>();
    })
    .Build();

await host.RunAsync();

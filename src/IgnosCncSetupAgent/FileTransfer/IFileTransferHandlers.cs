using Azure.Messaging.ServiceBus;

namespace IgnosCncSetupAgent.FileTransfer;

public interface IFileTransferHandlers
{
    Task ErrorHandler(ProcessErrorEventArgs args);
    Task MessageHandler(ProcessMessageEventArgs args);
}
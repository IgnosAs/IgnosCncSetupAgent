using Azure.Messaging.ServiceBus;

namespace IgnosCncSetupAgent.FileTransfer;

public class FileTransferHandlers : IFileTransferHandlers
{
    public Task MessageHandler(ProcessMessageEventArgs args)
    {
        string body = args.Message.Body.ToString();
        Console.WriteLine(body);

        return Task.CompletedTask;
    }

    public Task ErrorHandler(ProcessErrorEventArgs args)
    {
        // the error source tells me at what point in the processing an error occurred
        Console.WriteLine(args.ErrorSource);
        // the fully qualified namespace is available
        Console.WriteLine(args.FullyQualifiedNamespace);
        // as well as the entity path
        Console.WriteLine(args.EntityPath);
        Console.WriteLine(args.Exception.ToString());
        return Task.CompletedTask;
    }
}

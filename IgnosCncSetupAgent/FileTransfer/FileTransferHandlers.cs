using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Ignos.Common.Domain.CncSetup;

namespace IgnosCncSetupAgent.FileTransfer;

public class FileTransferHandlers : IFileTransferHandlers
{
    private readonly ILogger<FileTransferHandlers> _logger;
    
    public FileTransferHandlers(ILogger<FileTransferHandlers> logger)
    {
        _logger = logger;
    }

    public async Task MessageHandler(ProcessMessageEventArgs args)
    {
        var cncMachineOperationTransfer = GetCncMachineOperationTransfer(args.Message);

        // Temp
        var localPath = args.Message.Body.ToString();

        _logger.LogDebug("Received message {Message} for local path {LocalPath}",
            cncMachineOperationTransfer, localPath);

        await HandleTransferAndReport(cncMachineOperationTransfer, localPath, args.CancellationToken);
    }

    private CncMachineOperationTransfer GetCncMachineOperationTransfer(ServiceBusReceivedMessage message)
    {
        return new CncMachineOperationTransfer
        {
            Direction = FileTransferDirection.FromCloud,
            Files = new List<string>
            {
                "https://ignossttest.blob.core.windows.net/ingusprod-measurementschemas/0ae6e15b-0cea-47f1-be6a-62be58e4295c/6/markeddrawing.pdf?sv=2021-10-04&se=2022-12-22T00%3A49%3A02Z&sr=b&sp=r&sig=Y2Llj8tOqEKelvtvVj29PwMZR2Pp8F3T1DMWNEkEv2Y%3D",
                "https://ignossttest.blob.core.windows.net/ingusprod-measurementschemas/2bd8ff04-1ca3-42a6-bb90-9d4bbc1d8cba/36/drawing.pdf?sv=2021-10-04&se=2022-12-22T00%3A49%3A02Z&sr=b&sp=r&sig=jJp8COaltu%2BNMAaKfS3KC%2FFqyEUbEpL7WZuaKvJVqEA%3D"
            }
        };
    }

    public Task ErrorHandler(ProcessErrorEventArgs args)
    {
        // the error source tells me at what point in the processing an error occurred
        _logger.LogError(args.Exception, "Message handling error {ErrorSource} {FullyQualifiedNamespace} {EntityPath}",
            args.ErrorSource, args.FullyQualifiedNamespace, args.EntityPath);

        return Task.CompletedTask;
    }

    private async Task HandleTransferAndReport(CncMachineOperationTransfer cncMachineOperationTransfer, string localPath, CancellationToken cancellationToken)
    {
        try
        {
            // Report in progress to API

            // Decide if we can handle or not
            switch (cncMachineOperationTransfer.Direction)
            {
                case FileTransferDirection.FromCloud:
                    await HandleTransferFromCloud(cncMachineOperationTransfer, localPath, cancellationToken);
                    break;
                case FileTransferDirection.ToCloud:
                   await HandleTransferToCloud(cncMachineOperationTransfer, localPath, cancellationToken);
                    break;
                default:
                    _logger.LogError("Received unknown direction {Direction}", cncMachineOperationTransfer.Direction);
                    return;
            }

            // Report completed to API
        }
        catch (Exception)
        {
            // Report failure to API.
            
            // Throw to retry the message? 
            throw;
        }
    }

    public async Task HandleTransferFromCloud(CncMachineOperationTransfer cncMachineOperationTransfer, string localPath, CancellationToken cancellationToken)
    {
        await DeleteAllFilesAndFolders(localPath, cancellationToken);

        await DownloadAllFiles(cncMachineOperationTransfer.Files, localPath, cancellationToken);
    }

    private async Task HandleTransferToCloud(CncMachineOperationTransfer cncMachineOperationTransfer, string localPath, CancellationToken cancellationToken)
    {
        // Get file upload URIs.

        var uploadUris = await GetUploadUris(Directory.GetFiles(localPath), cancellationToken);

        // Upload files
        await Task.WhenAll(uploadUris.Select(uri => UploadFile(uri, localPath, cancellationToken)));
    }

    private Task<List<string>> GetUploadUris(string[] files, CancellationToken cancellationToken)
    {
        return Task.FromResult(new List<string> { "" });
    }

    private async Task DeleteAllFilesAndFolders(string path, CancellationToken cancellationToken)
    {
        var deleteTasks = new[]
        {
            Task.Run(() => Parallel.ForEach(
                Directory.GetFiles(path),
                new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = 5 },
                File.Delete), cancellationToken),

            Task.Run(() => Parallel.ForEach(
                Directory.GetDirectories(path),
                new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = 5 },
                directory => Directory.Delete(directory, true)), cancellationToken)
        };

        await Task.WhenAll(deleteTasks);
    }

    private async Task DownloadAllFiles(List<string> fileUris, string localPath, CancellationToken cancellationToken)
    {
        await Task.WhenAll(fileUris.Select(fileUrl => DownloadFile(fileUrl, localPath, cancellationToken)));
    }

    private async Task DownloadFile(string fileUri, string localPath, CancellationToken cancellationToken)
    {
        var blobClient = new BlobClient(new Uri(fileUri));

        using var localFile = File.Create(Path.Combine(localPath, Path.GetFileName(blobClient.Name)));

        await blobClient.DownloadToAsync(localFile, cancellationToken);
    }

    private async Task UploadFile(string fileUri, string localPath, CancellationToken cancellationToken)
    {
        var blobClient = new BlobClient(new Uri(fileUri));

        using var localFile = File.OpenRead(Path.Combine(localPath, Path.GetFileName(blobClient.Name)));

        await blobClient.UploadAsync(localFile, cancellationToken);
    }
}

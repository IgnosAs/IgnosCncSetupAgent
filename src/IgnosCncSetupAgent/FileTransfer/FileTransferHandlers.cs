using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Ignos.Common.Domain.CncSetup;
using Ignos.Common.Domain.CncSetup.Messages;

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
        var cncTransferMessage = await GetCncTransferMessage(args);

        if (cncTransferMessage == null)
            return;

        _logger.LogDebug("Received message {TransferId} with direction {Direction} for local path {LocalPath}",
            cncTransferMessage.TransferId, cncTransferMessage.Direction.ToString(), cncTransferMessage.MachinePath);

        if (!CncTransferMessageIsValid(cncTransferMessage))
            return;

        await HandleTransferAndReport(cncTransferMessage, args.CancellationToken);
    }

    private bool CncTransferMessageIsValid(CncTransferMessage cncTransferMessage)
    {
        if (string.IsNullOrEmpty(cncTransferMessage.MachinePath))
        {
            _logger.LogError("CncTransferMessage with transfer id {TransferId} has empty machine path",
                cncTransferMessage.TransferId);
            return false;
        }

        if (cncTransferMessage.Direction == FileTransferDirection.FromCloud && 
            cncTransferMessage.FilesToDownload.Count == 0)
        {
            _logger.LogError("CncTransferMessage with transfer id {TransferId} has no files to download from cloud",
                cncTransferMessage.TransferId);
            return false;
        }

        return true;
    }

    private async Task<CncTransferMessage?> GetCncTransferMessage(ProcessMessageEventArgs args)
    {
        CncTransferMessage? result = null;
        try
        {
            result = JsonSerializer.Deserialize<CncTransferMessage>(args.Message.Body);
        }
        catch (Exception ex)
        {
            // No point in retrying this message. 
            _logger.LogError(ex, "Failed to deserialize message");
            await args.DeadLetterMessageAsync(args.Message, "Failed to deserialize", ex.Message, args.CancellationToken);
        }

        return result;
    }

    public Task ErrorHandler(ProcessErrorEventArgs args)
    {
        // the error source tells me at what point in the processing an error occurred
        _logger.LogError(args.Exception, "Message handling error {ErrorSource} {FullyQualifiedNamespace} {EntityPath}",
            args.ErrorSource, args.FullyQualifiedNamespace, args.EntityPath);

        return Task.CompletedTask;
    }

    private async Task HandleTransferAndReport(CncTransferMessage cncTransferMessage, CancellationToken cancellationToken)
    {
        try
        {
            // Report in progress to API

            // Decide if we can handle or not
            switch (cncTransferMessage.Direction)
            {
                case FileTransferDirection.FromCloud:
                    await HandleTransferFromCloud(cncTransferMessage, cancellationToken);
                    break;
                case FileTransferDirection.ToCloud:
                   await HandleTransferToCloud(cncTransferMessage, cancellationToken);
                    break;
                default:
                    _logger.LogError("Received unknown direction {Direction}", cncTransferMessage.Direction);
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

    public async Task HandleTransferFromCloud(CncTransferMessage cncTransferMessage, CancellationToken cancellationToken)
    {
        await DeleteAllFilesAndFolders(cncTransferMessage.MachinePath, cancellationToken);

        await DownloadAllFiles(cncTransferMessage, cancellationToken);
    }

    private async Task HandleTransferToCloud(CncTransferMessage cncTransferMessage, CancellationToken cancellationToken)
    {
        // Get file upload URIs.
        var fileUploadUris = new string[0];

        // Upload files
        await Task.WhenAll(fileUploadUris.Select(
            uri => UploadFile(uri, cncTransferMessage.MachinePath, cancellationToken)));
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

    private async Task DownloadAllFiles(CncTransferMessage cncTransferMessage, CancellationToken cancellationToken)
    {
        await Task.WhenAll(cncTransferMessage.FilesToDownload.Select(
            fileToDownload => DownloadFile(fileToDownload, cncTransferMessage.MachinePath, cancellationToken)));
    }

    private async Task DownloadFile(FileToDownload fileToDownload, string machinePath, CancellationToken cancellationToken)
    {
        var blobClient = new BlobClient(new Uri(fileToDownload.Url));

        using var localFile = File.Create(Path.Combine(machinePath, fileToDownload.Name));

        await blobClient.DownloadToAsync(localFile, cancellationToken);
    }

    private async Task UploadFile(string fileUri, string localPath, CancellationToken cancellationToken)
    {
        var blobClient = new BlobClient(new Uri(fileUri));

        using var localFile = File.OpenRead(Path.Combine(localPath, Path.GetFileName(blobClient.Name)));

        await blobClient.UploadAsync(localFile, cancellationToken);
    }
}

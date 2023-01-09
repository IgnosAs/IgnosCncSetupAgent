using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Ignos.Api.Client;
using Ignos.Common.Domain.CncSetup.Messages;
using FileTransferDirection = Ignos.Common.Domain.CncSetup.FileTransferDirection;

namespace IgnosCncSetupAgent.FileTransfer;

public class FileTransferHandlers : IFileTransferHandlers
{
    private readonly ICncSetupClient _cncSetupClient;
    private readonly ICncFileTransferClient _cncFileTransferClient;
    private readonly IMachineShareAuthenticator _machineShareAuthenticator;
    private readonly ILogger<FileTransferHandlers> _logger;

    public FileTransferHandlers(
        ICncSetupClient cncSetupClient,
        ICncFileTransferClient cncFileTransferClient,
        IMachineShareAuthenticator machineShareAuthenticator,
        ILogger<FileTransferHandlers> logger
        )
    {
        _cncSetupClient = cncSetupClient;
        _cncFileTransferClient = cncFileTransferClient;
        _machineShareAuthenticator = machineShareAuthenticator;
        _logger = logger;
    }

    public Task ErrorHandler(ProcessErrorEventArgs args)
    {
        // the error source tells me at what point in the processing an error occurred
        _logger.LogError(args.Exception, "Message handling error {ErrorSource} {FullyQualifiedNamespace} {EntityPath}",
            args.ErrorSource, args.FullyQualifiedNamespace, args.EntityPath);

        return Task.CompletedTask;
    }

    public async Task MessageHandler(ProcessMessageEventArgs args)
    {
        var cncTransferMessage = await GetCncTransferMessage(args);

        if (cncTransferMessage == null)
            return;

        if (!CncTransferMessageIsValid(cncTransferMessage))
        {
            await ReportCncTransferStatus(cncTransferMessage, FileTransferStatus.Failed, args.CancellationToken);
            await args.DeadLetterMessageAsync(args.Message, "Invalid", "Invalid message", args.CancellationToken);
            return;
        }

        _logger.LogDebug("Received message {TransferId} with direction {Direction} for local path {LocalPath}",
            cncTransferMessage.TransferId, cncTransferMessage.Direction.ToString(), cncTransferMessage.GetLocalPath());

        await HandleTransferAndReport(cncTransferMessage, args.CancellationToken);
    }

    private bool CncTransferMessageIsValid(CncTransferMessage cncTransferMessage)
    {
        if (string.IsNullOrEmpty(cncTransferMessage.MachineShare))
        {
            _logger.LogError("CncTransferMessage with transfer id {TransferId} has empty machine share",
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

        if (cncTransferMessage.Direction != FileTransferDirection.ToCloud &&
            cncTransferMessage.Direction != FileTransferDirection.FromCloud)
        {
            _logger.LogError("Received unknown direction {Direction}", cncTransferMessage.Direction);
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

    private Task ReportCncTransferStatus(CncTransferMessage cncTransferMessage, FileTransferStatus fileTransferStatus,
        CancellationToken cancellationToken, List<string>? filesTransferred = null)
    {
        return _cncFileTransferClient.SetTransferStatusAsync(
            cncTransferMessage.TransferId,
            new SetTransferStatusRequest
            {
                Status = fileTransferStatus,
                Files = filesTransferred ?? cncTransferMessage.FilesToDownload.Select(f => f.Name).ToList(),
            },
            cancellationToken);
    }

    private async Task HandleTransferAndReport(CncTransferMessage cncTransferMessage, CancellationToken cancellationToken)
    {
        try
        {
            await _machineShareAuthenticator.AuthenticateIfRequiredAndRun(
                cncTransferMessage,
                () => HandleTransfer(cncTransferMessage, cancellationToken));
        }
        catch (Exception)
        {
            // Report failure to API.
            await ReportCncTransferStatus(cncTransferMessage, FileTransferStatus.Failed, cancellationToken);

            // Throw to retry the message? 
            throw;
        }
    }

    private async Task HandleTransfer(CncTransferMessage cncTransferMessage, CancellationToken cancellationToken)
    {
        // Decide if we can handle or not
        switch (cncTransferMessage.Direction)
        {
            case FileTransferDirection.FromCloud:
                await HandleTransferFromCloud(cncTransferMessage, cancellationToken);
                break;
            case FileTransferDirection.ToCloud:
                await HandleTransferToCloud(cncTransferMessage, cancellationToken);
                break;
        }
    }

    private async Task HandleTransferFromCloud(CncTransferMessage cncTransferMessage, CancellationToken cancellationToken)
    {
        if (cncTransferMessage.DeleteLocalFiles)
        {
            await DeleteAllFiles(cncTransferMessage.GetLocalPath(), cancellationToken);
        }

        await DownloadAllFiles(cncTransferMessage, cancellationToken);

        // Report completed to API
        await ReportCncTransferStatus(cncTransferMessage, FileTransferStatus.Success, cancellationToken);
    }

    private async Task HandleTransferToCloud(CncTransferMessage cncTransferMessage, CancellationToken cancellationToken)
    {
        string localPath = cncTransferMessage.GetLocalPath();

        // Path.GetFileName returns null only if we pass in null. So cast to string is OK
        var localFiles = Directory.GetFiles(localPath)
            .Select(Path.GetFileName)
            .Cast<string>()
            .ToList();

        // Get file upload URIs.
        var fileUploads = await _cncSetupClient.CreateUploadProgramsInfoAsync(
            cncTransferMessage.CncMachineOperationId,
            new UploadFileRequest { Filenames = localFiles });

        // Upload files
        await Task.WhenAll(fileUploads.Select(
            upload => UploadFile(upload, localPath, cancellationToken)));

        if (cncTransferMessage.DeleteLocalFiles)
        {
            // Delete files
            await DeleteAllFiles(localPath, cancellationToken);
        }

        // Report completed to API
        await ReportCncTransferStatus(cncTransferMessage, FileTransferStatus.Success, cancellationToken, localFiles);
    }

    private async Task DeleteAllFiles(string path, CancellationToken cancellationToken)
    {
        await Task.Run(() => Parallel.ForEach(
                Directory.GetFiles(path),
                new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = 5 },
                File.Delete), cancellationToken);
    }

    private async Task DownloadAllFiles(CncTransferMessage cncTransferMessage, CancellationToken cancellationToken)
    {
        await Task.WhenAll(cncTransferMessage.FilesToDownload.Select(
            fileToDownload => DownloadFile(fileToDownload, cncTransferMessage.GetLocalPath(), cancellationToken)));
    }

    private async Task DownloadFile(FileToDownload fileToDownload, string localPath, CancellationToken cancellationToken)
    {
        var blobClient = new BlobClient(new Uri(fileToDownload.Url));

        using var localFile = File.Create(Path.Join(localPath, fileToDownload.Name));

        await blobClient.DownloadToAsync(localFile, cancellationToken);
    }

    private async Task UploadFile(UploadFileDto upload, string localPath, CancellationToken cancellationToken)
    {
        var blobClient = new BlobClient(new Uri(upload.Url));

        using var localFile = File.OpenRead(Path.Join(localPath, upload.Filename));

        await blobClient.UploadAsync(localFile, true, cancellationToken);
    }
}


using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Ignos.Api.Client;
using Ignos.Common.Domain.CncSetup.Messages;
using FileTransferDirection = Ignos.Common.Domain.CncSetup.FileTransferDirection;

namespace IgnosCncSetupAgent.FileTransfer;

public class FileTransferHandlers(
    ICncSetupAgentClient cncSetupAgentClient,
    IMachineShareAuthenticator machineShareAuthenticator,
    ILogger<FileTransferHandlers> logger
        ) : IFileTransferHandlers
{

    public Task ErrorHandler(ProcessErrorEventArgs args)
    {
        // the error source tells me at what point in the processing an error occurred
        logger.LogError(args.Exception, "Message handling error {ErrorSource} {FullyQualifiedNamespace} {EntityPath}",
            args.ErrorSource, args.FullyQualifiedNamespace, args.EntityPath);

        return Task.CompletedTask;
    }

    public async Task MessageHandler(ProcessMessageEventArgs args)
    {
        var cncTransferMessage = JsonSerializer.Deserialize<CncTransferMessage>(args.Message.Body);

        if (cncTransferMessage == null)
            return;

        if (!CncTransferMessageIsValid(cncTransferMessage))
        {
            await ReportCncTransferStatus(cncTransferMessage, FileTransferStatus.Failed, args.CancellationToken, "Invalid transfer message");
            return;
        }

        logger.LogDebug("Received message {TransferId} with direction {Direction} for local path {LocalPath}",
            cncTransferMessage.TransferId, cncTransferMessage.Direction.ToString(), cncTransferMessage.GetLocalPath());

        await HandleTransferAndReport(cncTransferMessage, args.CancellationToken);
    }

    private bool CncTransferMessageIsValid(CncTransferMessage cncTransferMessage)
    {
        if (string.IsNullOrEmpty(cncTransferMessage.MachineShare))
        {
            logger.LogError("CncTransferMessage with transfer id {TransferId} has empty machine share",
                cncTransferMessage.TransferId);
            return false;
        }

        if (cncTransferMessage.Direction == FileTransferDirection.FromCloud &&
            cncTransferMessage.FilesToDownload.Count == 0)
        {
            logger.LogError("CncTransferMessage with transfer id {TransferId} has no files to download from cloud",
                cncTransferMessage.TransferId);
            return false;
        }

        if (cncTransferMessage.Direction != FileTransferDirection.ToCloud &&
            cncTransferMessage.Direction != FileTransferDirection.FromCloud)
        {
            logger.LogError("CncTransferMessage with transfer id {TransferId} has unknown direction {Direction}", 
                cncTransferMessage.TransferId, cncTransferMessage.Direction);
            return false;
        }

        return true;
    }

    private Task ReportCncTransferStatus(CncTransferMessage cncTransferMessage, FileTransferStatus fileTransferStatus,
        CancellationToken cancellationToken, string? statusMessage = null, List<string>? filesTransferred = null)
    {
        return cncSetupAgentClient.SetTransferStatusAsync(
            cncTransferMessage.TransferId,
            new SetTransferStatusRequest
            {
                Status = fileTransferStatus,
                Files = filesTransferred ?? cncTransferMessage.FilesToDownload.Select(f => f.Name).ToList(),
                StatusMessage = statusMessage?[..Math.Min(statusMessage.Length, 500)]
            },
            cancellationToken);
    }

    private async Task HandleTransferAndReport(CncTransferMessage cncTransferMessage, CancellationToken cancellationToken)
    {
        try
        {
            await machineShareAuthenticator.AuthenticateIfRequiredAndRun(
                cncTransferMessage,
                () => HandleTransfer(cncTransferMessage, cancellationToken));
        }
        catch (Exception ex)
        {
            // Report failure to API.
            await ReportCncTransferStatus(cncTransferMessage, FileTransferStatus.Failed, cancellationToken, ex.Message);
            logger.LogError(ex, "Failure during processing for file transfer {TransferId}", cncTransferMessage.TransferId);
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
        var fileUploads = await cncSetupAgentClient.CreateUploadProgramsInfoAsync(
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
        await ReportCncTransferStatus(cncTransferMessage, FileTransferStatus.Success, cancellationToken, filesTransferred: localFiles);
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


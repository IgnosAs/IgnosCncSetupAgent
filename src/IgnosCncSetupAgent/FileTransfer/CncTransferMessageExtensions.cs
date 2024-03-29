﻿using Ignos.Common.Domain.CncSetup.Messages;

namespace IgnosCncSetupAgent.FileTransfer;

public static class CncTransferMessageExtensions
{
    public static bool ShouldAuthenticate(this CncTransferMessage cncTransferMessage) =>
        !string.IsNullOrEmpty(cncTransferMessage.Username) && !string.IsNullOrEmpty(cncTransferMessage.Password);

    public static string GetLocalPath(this CncTransferMessage cncTransferMessage) => 
        Path.Join(cncTransferMessage.MachineShare, cncTransferMessage.MachinePath);
}

using Ignos.Common.Domain.CncSetup.Messages;

namespace IgnosCncSetupAgent.FileTransfer;
public interface IMachineShareAuthenticator
{
    Task AuthenticateIfRequiredAndRun(CncTransferMessage cncTransferMessage, Func<Task> job);
}
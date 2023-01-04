using Ignos.Common.Domain.CncSetup.Messages;

namespace IgnosCncSetupAgent.FileTransfer;
public interface IMachinePathAuthenticator
{
    Task AuthenticateIfRequiredAndRun(CncTransferMessage cncTransferMessage, Func<Task> job);
}
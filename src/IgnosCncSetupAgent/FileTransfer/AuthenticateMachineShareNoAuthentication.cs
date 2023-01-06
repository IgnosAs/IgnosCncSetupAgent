using Ignos.Common.Domain.CncSetup.Messages;

namespace IgnosCncSetupAgent.FileTransfer;

public class AuthenticateMachineShareNoAuthentication : IMachineShareAuthenticator
{
    public Task AuthenticateIfRequiredAndRun(CncTransferMessage cncTransferMessage, Func<Task> job)
    {
        return job();
    }
}

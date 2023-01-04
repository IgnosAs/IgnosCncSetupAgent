using Ignos.Common.Domain.CncSetup.Messages;

namespace IgnosCncSetupAgent.FileTransfer;

public class AuthenticateMachinePathNoAuthentication : IMachinePathAuthenticator
{
    public Task AuthenticateIfRequiredAndRun(CncTransferMessage cncTransferMessage, Func<Task> job)
    {
        return job();
    }
}

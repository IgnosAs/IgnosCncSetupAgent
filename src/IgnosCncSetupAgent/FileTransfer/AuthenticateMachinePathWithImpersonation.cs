using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using Ignos.Common.Domain.CncSetup.Messages;
using Microsoft.Win32.SafeHandles;

namespace IgnosCncSetupAgent.FileTransfer;

[SupportedOSPlatform("windows")]
public class AuthenticateMachinePathWithImpersonation : IMachinePathAuthenticator
{
    private readonly ILogger<AuthenticateMachinePathWithImpersonation> _logger;

    public AuthenticateMachinePathWithImpersonation(ILogger<AuthenticateMachinePathWithImpersonation> logger)
    {
        _logger = logger;
    }
    public async Task AuthenticateIfRequiredAndRun(CncTransferMessage cncTransferMessage, Func<Task> job)
    {
        if (cncTransferMessage.ShouldAuthenticate())
        {
            await WindowsIdentity.RunImpersonated(GetTokenForTransfer(cncTransferMessage), job);
        }
        else
        {
            await job();
        }
    }

    private SafeAccessTokenHandle GetTokenForTransfer(CncTransferMessage cncTransferMessage)
    {
        Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        const int LOGON32_PROVIDER_DEFAULT = 0;
        //This parameter causes LogonUser to create a primary token.   
        const int LOGON32_LOGON_INTERACTIVE = 2;

        SafeAccessTokenHandle safeAccessTokenHandle;

        bool returnValue = LogonUser(cncTransferMessage.Username, cncTransferMessage.Domain,
            cncTransferMessage.Password, LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT,
            out safeAccessTokenHandle);

        if (false == returnValue)
        {
            int ret = Marshal.GetLastWin32Error();
            _logger.LogError("LogonUser failed with error code : {errorCode}", ret);
            safeAccessTokenHandle = SafeAccessTokenHandle.InvalidHandle;
        }

        return safeAccessTokenHandle;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword,
    int dwLogonType, int dwLogonProvider, out SafeAccessTokenHandle phToken);


}

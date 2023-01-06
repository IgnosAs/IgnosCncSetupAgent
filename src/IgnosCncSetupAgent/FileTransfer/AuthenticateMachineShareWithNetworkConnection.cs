using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Ignos.Common.Domain.CncSetup.Messages;

namespace IgnosCncSetupAgent.FileTransfer;

[SupportedOSPlatform("windows")]
public class AuthenticateMachineShareWithNetworkConnection : IMachineShareAuthenticator
{
    public async Task AuthenticateIfRequiredAndRun(CncTransferMessage cncTransferMessage, Func<Task> job)
    {
        if (cncTransferMessage.ShouldAuthenticate())
        {
            var credential = new NetworkCredential(
                cncTransferMessage.Username, cncTransferMessage.Password, cncTransferMessage.Domain);
            using var connection = new NetworkConnection(cncTransferMessage.MachineShare, credential);
            await job();
        }
        else
        {
            await job();
        }
    }

    private class NetworkConnection : IDisposable
    {
        string _networkName;

        public NetworkConnection(string networkName,
            NetworkCredential credentials)
        {
            _networkName = networkName;

            var netResource = new NetResource()
            {
                ResourceType = ResourceType.Disk,
                RemoteName = networkName
            };

            var userName = string.IsNullOrEmpty(credentials.Domain)
                ? credentials.UserName
                : string.Format(@"{0}\{1}", credentials.Domain, credentials.UserName);

            var result = WNetAddConnection2(
                netResource,
                credentials.Password,
                userName,
                (int)Flags.CONNECT_TEMPORARY);

            if (result != 0)
            {
                throw new Win32Exception(result);
            }
        }

        ~NetworkConnection()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            WNetCancelConnection2(_networkName, 0, true);
        }

        [DllImport("mpr.dll")]
        private static extern int WNetAddConnection2(NetResource netResource,
            string password, string username, int flags);

        [DllImport("mpr.dll")]
        private static extern int WNetCancelConnection2(string name, int flags,
            bool force);
    }

    [StructLayout(LayoutKind.Sequential)]
    private class NetResource
    {
        public ResourceScope Scope;
        public ResourceType ResourceType;
        public ResourceDisplaytype DisplayType;
        public int Usage;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? LocalName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string RemoteName = string.Empty;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Comment;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Provider;
    }

    private enum ResourceScope : int
    {
        Connected = 1,
        GlobalNetwork,
        Remembered,
        Recent,
        Context
    };

    private enum ResourceType : int
    {
        Any = 0,
        Disk = 1,
        Print = 2,
        Reserved = 8,
    }

    private enum ResourceDisplaytype : int
    {
        Generic = 0x0,
        Domain = 0x01,
        Server = 0x02,
        Share = 0x03,
        File = 0x04,
        Group = 0x05,
        Network = 0x06,
        Root = 0x07,
        Shareadmin = 0x08,
        Directory = 0x09,
        Tree = 0x0a,
        Ndscontainer = 0x0b
    }

    private enum Flags : int
    {
        CONNECT_UPDATE_PROFILE = 0x00000001,
        CONNECT_UPDATE_RECENT = 0x00000002,
        CONNECT_TEMPORARY = 0x00000004,
        CONNECT_INTERACTIVE = 0x00000008,
        CONNECT_PROMPT = 0x00000010,
        CONNECT_REDIRECT = 0x00000080,
        CONNECT_CURRENT_MEDIA = 0x00000200,
        CONNECT_COMMANDLINE = 0x00000800,
        CONNECT_CMD_SAVECRED = 0x00001000,
        CONNECT_CRED_RESET = 0x00002000
    }
}

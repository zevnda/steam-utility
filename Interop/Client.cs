using System;
using System.Globalization;

namespace API
{
    public class Client : IDisposable
    {
        public Wrappers.SteamClient018 SteamClient;
        public Wrappers.SteamUtils005 SteamUtils;
        public Wrappers.SteamApps008 SteamApps008;
        public Wrappers.SteamApps001 SteamApps001;

        private bool m_disposed;
        private int m_pipeHandle;
        private int m_userHandle;

        public void Initialize(long applicationId)
        {
            // Verify Steam installation path exists
            string installPath = Steam.GetInstallPath();
            if (string.IsNullOrEmpty(installPath))
            {
                throw new ClientInitializeException(
                    ClientInitializeFailure.InstallPathNotFound,
                    "Unable to locate Steam installation path"
                );
            }

            // Set Steam app ID environment variable if provided
            if (applicationId != 0)
            {
                Environment.SetEnvironmentVariable(
                    "SteamAppId",
                    applicationId.ToString(CultureInfo.InvariantCulture)
                );
            }

            // Load Steam client library
            if (!Steam.Load())
            {
                throw new ClientInitializeException(
                    ClientInitializeFailure.LibraryLoadFailed,
                    "Failed to load Steam client library"
                );
            }

            // Create Steam client interface
            SteamClient = Steam.CreateInterface<Wrappers.SteamClient018>("SteamClient018");
            if (SteamClient == null)
            {
                throw new ClientInitializeException(
                    ClientInitializeFailure.ClientCreationFailed,
                    "Failed to create ISteamClient018 interface"
                );
            }

            // Create communication pipe
            m_pipeHandle = SteamClient.CreateSteamPipe();
            if (m_pipeHandle == 0)
            {
                throw new ClientInitializeException(
                    ClientInitializeFailure.PipeCreationFailed,
                    "Failed to create Steam pipe"
                );
            }

            // Connect to global user
            m_userHandle = SteamClient.ConnectToGlobalUser(m_pipeHandle);
            if (m_userHandle == 0)
            {
                throw new ClientInitializeException(
                    ClientInitializeFailure.UserConnectionFailed,
                    "Failed to connect to global Steam user"
                );
            }

            // Get utility interface and verify app ID
            SteamUtils = SteamClient.GetSteamUtils004(m_pipeHandle);
            if (applicationId > 0 && SteamUtils.GetAppId() != (uint)applicationId)
            {
                throw new ClientInitializeException(
                    ClientInitializeFailure.ApplicationIdMismatch,
                    "Application ID mismatch detected"
                );
            }

            // Initialize remaining interfaces
            SteamApps008 = SteamClient.GetSteamApps008(m_userHandle, m_pipeHandle);
            SteamApps001 = SteamClient.GetSteamApps001(m_userHandle, m_pipeHandle);
        }

        ~Client()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            // Release Steam resources
            if (SteamClient != null && m_pipeHandle > 0)
            {
                if (m_userHandle > 0)
                {
                    SteamClient.ReleaseUser(m_pipeHandle, m_userHandle);
                    m_userHandle = 0;
                }

                SteamClient.ReleaseSteamPipe(m_pipeHandle);
                m_pipeHandle = 0;
            }

            m_disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

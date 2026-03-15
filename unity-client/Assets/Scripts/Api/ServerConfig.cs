namespace HijackPoker.Api
{
    /// <summary>
    /// Central server URL config. Uses localhost in the Editor and
    /// the dev machine's LAN IP on device builds so the phone can
    /// reach the Docker backend over Wi-Fi.
    /// </summary>
    public static class ServerConfig
    {
        // Change this to your Mac's LAN IP (System Settings → Wi-Fi → Details → IP Address)
        private const string LanHost = "10.10.0.32";

#if UNITY_EDITOR
        private const string Host = "localhost";
#else
        private const string Host = LanHost;
#endif

        public const string HttpBaseUrl = "http://" + Host + ":3030";
        public const string WsBaseUrl   = "ws://"   + Host + ":3032";
    }
}

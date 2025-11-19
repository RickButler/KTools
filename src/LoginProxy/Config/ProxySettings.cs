// Config/ProxySettings.cs
using System.Collections.Generic;

namespace LoginProxy.Config
{
    public sealed class ProxySettings
    {
        public string ListenHost { get; set; } = "127.0.0.1";
        public int ListenPort { get; set; } = 5998;

        public string LoginServerHost { get; set; } = "127.0.0.1";
        public int LoginServerPort { get; set; } = 5998;

        public string DesKey { get; set; } = "6919379AC61BBE27";
        public string DesIV  { get; set; } = "0000000000000000";

        public int LoginPayloadOffset { get; set; } = 0;

        public Dictionary<string, AccountRewrite> AccountRewrites { get; set; } = new();
    }
}
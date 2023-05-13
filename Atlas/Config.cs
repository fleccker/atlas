using System.ComponentModel;

namespace Atlas.EntryPoint
{
    public class Config
    {
        [Description("Whether or not to allow loading incompatible Atlas versions.")]
        public bool AllowIncompatible { get; set; }

        [Description("Whether or not to display debug messages.")]
        public bool AllowDebugLogs { get; set; }
    }
}
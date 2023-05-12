using System.ComponentModel;

namespace Atlas.Loader
{
    public class Config
    {
        [Description("Whether or not to allow loading incompatible Atlas versions.")]
        public bool AllowIncompatible { get; set; }
    }
}
using System;
using System.IO;

using PluginAPI.Core.Attributes;

using Atlas.Loader.Interface;

namespace Atlas.Loader
{
    public class EntryPoint
    {
        public static Interface.Atlas Atlas { get; private set; }

        public static Version[] SupportedVersions { get; } = new Version[]
        {
            new Version(1, 0, 0, 0)
        };

        public event Action<Interface.Atlas> OnAtlasLoaded;
        public event Action<Interface.Atlas> OnAtlasUnloaded;
        public event Action<Interface.Atlas> OnAtlasReloaded;

        public event Action<Interface.Atlas, AtlasResult, Exception> OnError;

        [PluginConfig]
        public Config Config;

        [PluginEntryPoint(
            "Atlas.Loader",
            "1.0.0",
            "A plugin used to load the Atlas plugin framework.",
            "fleccker")]
        public void Load()
        {
            if (Atlas != null)
            {
                ThrowAtlasError(AtlasResult.FailedAlreadyLoaded);
                return;
            }

            Atlas = new Interface.Atlas(Config);

            var result = Atlas.TryLoad(out var exception);

            if (result != AtlasResult.Success)
            {
                ThrowAtlasError(result, exception);
                return;
            }
        }

        [PluginUnload]
        public void Unload()
        {
            var result = Atlas.TryUnload(out var exception);

            if (result != AtlasResult.Success)
            {
                ThrowAtlasError(result, exception);
                return;
            }
        }

        [PluginReload]
        public void Reload()
        {
            var result = Atlas.TryReload(out var exception);

            if (result != AtlasResult.Success)
            {
                ThrowAtlasError(result, exception);
                return;
            }
        }

        private void ThrowAtlasError(AtlasResult atlasLoadResult, Exception exception = null)
        {
            if (exception is null)
                exception = GetException(atlasLoadResult);

            OnError?.Invoke(Atlas, atlasLoadResult, exception);

            throw exception;
        }

        private Exception GetException(AtlasResult atlasResult)
        {
            switch (atlasResult)
            {
                case AtlasResult.FailedAlreadyLoaded:
                    return new InvalidOperationException("An instance of Atlas is already active.");

                case AtlasResult.Success:
                    return null;

                case AtlasResult.FailedToLocateFile:
                    return new FileNotFoundException($"Failed to find the main Atlas assembly! ({Atlas.GetPath(AtlasPath.MainAssembly)})");

                case AtlasResult.FailedToLocateDirectory:
                    return new DirectoryNotFoundException($"Failed to find the parent Atlas folder! ({Atlas.GetPath(AtlasPath.AtlasFolder)})");

                case AtlasResult.FailedToLoadAssembly:
                    return new BadImageFormatException($"Failed to load the main Atlas assembly!");

                case AtlasResult.FailedToLocateEntryType:
                    return new TypeAccessException($"Failed to find the entry point type! ({Interface.Atlas.EntryPointType})");

                case AtlasResult.FailedToInvokeEntryMethod:
                    return new MethodAccessException($"Failed to invoke the entry point method! ({Interface.Atlas.EntryPointType}::{Interface.Atlas.EntryPointMethod})");

                case AtlasResult.FailedToLocateEntryMethod:
                    return new MissingMethodException($"Failed to locate the entry point method! ({Interface.Atlas.EntryPointType}::{Interface.Atlas.EntryPointMethod})");

                case AtlasResult.FailedVersionMismatch:
                    return new InvalidDataException($"Version mismatch! Expected any of {string.Join(", ", SupportedVersions)}; got {Atlas.Version}");

                default:
                    return null;
            }
        }
    }
}
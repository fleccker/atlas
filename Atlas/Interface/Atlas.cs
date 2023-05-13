using PluginAPI.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using UnityEngine;

namespace Atlas.EntryPoint.Interface
{
    public class Atlas
    {
        private MethodInfo _unload;
        private MethodInfo _load;
        private MethodInfo _reload;

        private object _loaderHandle;
        private Type _loaderType;

        private Assembly _loaderAssembly;

        public Config Config { get; }
        public IReadOnlyDictionary<AtlasPath, string> Paths { get; }

        public const string EntryPointType = "Atlas.Loader.PluginLoader";
        public const string EntryPointMethod = "Load";

        public Version Version { get; private set; }

        public Atlas(Config config)
        {
            Config = config;

            var paths = new Dictionary<AtlasPath, string>();

            paths[AtlasPath.CommonAppDataFolder] = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            paths[AtlasPath.LocalAppDataFolder] = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            paths[AtlasPath.AppDataFolder] = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            paths[AtlasPath.UnityDataFolder] = Application.dataPath;
            paths[AtlasPath.UnityPersistentDataFolder] = Application.persistentDataPath;

            paths[AtlasPath.ServerFolder] = Directory.GetParent(Application.dataPath).FullName;
            paths[AtlasPath.AtlasFolder] = $"{paths[AtlasPath.ServerFolder]}/atlas";
            paths[AtlasPath.MainFolder] = $"{paths[AtlasPath.AtlasFolder]}/{ServerStatic.ServerPort}";
            paths[AtlasPath.PluginFolder] = $"{paths[AtlasPath.MainFolder]}/plugins";
            paths[AtlasPath.ConfigFolder] = $"{paths[AtlasPath.MainFolder]}/configs";
            paths[AtlasPath.PluginConfigFolder] = $"{paths[AtlasPath.ConfigFolder]}/plugins";
            paths[AtlasPath.DependencyFolder] = $"{paths[AtlasPath.MainFolder]}/dependencies";

            paths[AtlasPath.MainAssembly] = $"{paths[AtlasPath.MainFolder]}/main.dll";
            paths[AtlasPath.PluginAssembly] = $"{PluginAPI.Helpers.Paths.Plugins}/{Assembly.GetExecutingAssembly().GetName().Name}.dll";

            Paths = paths;
        }

        public void ReloadDirectories()
        {
            Log.Debug($"Reloading directories ..", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");

            foreach (var path in Paths)
            {
                if (path.Key is AtlasPath.MainAssembly || path.Key is AtlasPath.PluginAssembly)
                    continue;

                if (!Directory.Exists(path.Value))
                {
                    Directory.CreateDirectory(path.Value);
                    Log.Debug($"Created directory: ({path.Key}) {path.Value}", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");
                }
            }
        }

        public string GetPath(AtlasPath path)
        {
            return TryGetPath(path, out var pathInfo) ? pathInfo : throw new KeyNotFoundException($"Path \"{path}\" was not present.");
        }

        public bool TryGetPath(AtlasPath path, out string result)
        {
            return Paths.TryGetValue(path, out result);
        }

        public AtlasResult TryLoad(out Exception exception)
        {
            exception = null;

            if (_load is null)
            {
                Log.Debug($"Load method is null - perhaps it's the first time?", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");

                ReloadDirectories();

                if (!TryGetPath(AtlasPath.MainAssembly, out var mainAssemblyPath)
                    || !File.Exists(mainAssemblyPath))
                {
                    Log.Debug($"Failed to locate the main assembly at {mainAssemblyPath ?? "missing path"}", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");
                    return AtlasResult.FailedToLocateFile;
                }

                try
                {
                    _loaderAssembly = Assembly.Load(File.ReadAllBytes(mainAssemblyPath));
                }
                catch (Exception ex)
                {
                    exception = ex;
                    Log.Debug($"Failed to load the main assembly: {ex.Message}", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");
                }

                if (_loaderAssembly is null)
                {
                    return AtlasResult.FailedToLoadAssembly;
                }

                _loaderType = _loaderAssembly
                    .GetTypes()
                    .FirstOrDefault(x => x.FullName == EntryPointType);

                if (_loaderType is null)
                {
                    Log.Debug($"Failed to locate the loader class: {EntryPointType}", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");
                    return AtlasResult.FailedToLocateEntryType;
                }

                var versionField = _loaderType.GetProperty("Version");

                if (versionField is null)
                {
                    Log.Debug($"Failed to locate the Version field.", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");
                    return AtlasResult.FailedToLocateEntryType;
                }

                try
                {
                    _loaderHandle = Activator.CreateInstance(_loaderType);
                }
                catch (Exception ex)
                {
                    exception = ex;
                    Log.Debug($"Failed to create an instance of the loader class: {ex.Message}", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");
                }

                if (_loaderHandle is null)
                {
                    return AtlasResult.FailedToInstantiateEntryType;
                }

                Version = (Version)versionField.GetValue(_loaderHandle);

                if (!EntryPoint.SupportedVersions.Any(x => x >= Version)
                    && !EntryPoint.Instance.Config.AllowIncompatible)
                {
                    Log.Debug($"Version {Version} is unsupported by this release.", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");
                    return AtlasResult.FailedVersionMismatch;
                }

                try
                {
                    _load = _loaderType.GetMethod(EntryPointMethod);
                    _unload = _loaderType.GetMethod("Unload");
                    _reload = _loaderType.GetMethod("Reload");
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                if (_load is null
                    || _unload is null
                    || _reload is null)
                {
                    Log.Debug($"Failed to locate one of the load methods (LOAD: {_load is null} / UNLOAD: {_unload is null} / RELOAD: {_reload} is null)", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");
                    return AtlasResult.FailedToLocateEntryMethod;
                }
            }

            try
            {
                Log.Debug($"Invoking the Load method.", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");
                _load.Invoke(_loaderHandle, null);
            }
            catch (Exception ex)
            {
                exception = ex;
                Log.Debug($"Failed to invoke the Load method: {ex.Message}", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");
                return AtlasResult.FailedToInvokeEntryMethod;
            }

            Log.Debug($"Loading succesfull.", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");
            return AtlasResult.Success;
        }

        public AtlasResult TryUnload(out Exception exception) 
        { 
            exception = null;
            
            try
            {
                Log.Debug($"Invoking the Unload method.", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");
                _unload?.Invoke(_loaderHandle, null);
                Log.Debug($"Invoked the Unload method.", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");
            }
            catch (Exception ex)
            {
                exception = ex;
                return AtlasResult.FailedToInvokeEntryMethod;
            }
            
            return AtlasResult.Success; 
        }

        public AtlasResult TryReload(out Exception exception)
        {
            exception = null;

            try
            {
                Log.Debug($"Invoking the Reload method.", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");
                _reload?.Invoke(_loaderHandle, null);
                Log.Debug($"Invoked the Reload method.", EntryPoint.Instance.Config.AllowDebugLogs, "Atlas Interface");
            }
            catch (Exception ex)
            {
                exception = ex;
                return AtlasResult.FailedToInvokeEntryMethod;
            }

            return AtlasResult.Success;
        }
    }
}
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using UnityEngine;

namespace Atlas.Loader.Interface
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

        public const string EntryPointType = "Atlas.EntryPoint.Loader";
        public const string EntryPointMethod = "Load";

        public Version Version { get; }

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
        }

        public void ReloadDirectories()
        {
            foreach (var path in Paths)
            {
                if (!Directory.Exists(path.Value))
                {
                    Directory.CreateDirectory(path.Value);
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
                ReloadDirectories();

                if (!TryGetPath(AtlasPath.MainAssembly, out var mainAssemblyPath)
                    || !File.Exists(mainAssemblyPath))
                {
                    return AtlasResult.FailedToLocateFile;
                }

                try
                {
                    _loaderAssembly = Assembly.Load(File.ReadAllBytes(mainAssemblyPath));
                }
                catch (Exception ex)
                {
                    exception = ex;
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
                    return AtlasResult.FailedToLocateEntryType;
                }

                try
                {
                    _loaderHandle = Activator.CreateInstance(_loaderType);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                if (_loaderHandle is null)
                {
                    return AtlasResult.FailedToInstantiateEntryType;
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
                    return AtlasResult.FailedToLocateEntryMethod;
                }
            }

            try
            {
                _load?.Invoke(_loaderHandle, new object[]
                {

                });
            }
            catch (Exception ex)
            {
                exception = ex;
                return AtlasResult.FailedToInvokeEntryMethod;
            }

            return AtlasResult.Success;
        }

        public AtlasResult TryUnload(out Exception exception) 
        { 
            exception = null; 
            
            return AtlasResult.Success; 
        }

        public AtlasResult TryReload(out Exception exception)
        {
            exception = null;

            return AtlasResult.Success;
        }
    }
}
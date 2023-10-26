using System.Collections.Concurrent;
using PlayFab.Internal;
using PlayFab.Json;

namespace PlayFab
{
    public class PluginManager
    {
        private readonly ConcurrentDictionary<Tuple<PluginContract, string>, IPlayFabPlugin> _plugins = new();

        /// <summary>
        /// The singleton instance of plugin manager.
        /// </summary>
        private static readonly PluginManager Instance = new();

        private PluginManager()
        {
        }

        /// <summary>
        /// Gets a plugin.
        /// If a plugin with specified contract and optional instance name does not exist, it will create a new one.
        /// </summary>
        /// <param name="contract">The plugin contract.</param>
        /// <param name="instanceName">The optional plugin instance name. Instance names allow to have mulptiple plugins with the same contract.</param>
        /// <returns>The plugin instance.</returns>
        public static T GetPlugin<T>(PluginContract contract, string instanceName = "") where T : IPlayFabPlugin
        {
            return (T)Instance.GetPluginInternal(contract, instanceName);
        }

        /// <summary>
        /// Sets a custom plugin.
        /// If a plugin with specified contract and optional instance name already exists, it will be replaced with specified instance.
        /// </summary>
        /// <param name="plugin">The plugin instance.</param>
        /// <param name="contract">The app contract of plugin.</param>
        /// <param name="instanceName">The optional plugin instance name. Instance names allow to have mulptiple plugins with the same contract.</param>
        public static void SetPlugin(IPlayFabPlugin plugin, PluginContract contract, string instanceName = "")
        {
            Instance.SetPluginInternal(plugin, contract, instanceName);
        }

        private IPlayFabPlugin GetPluginInternal(PluginContract contract, string instanceName)
        {
            var key = new Tuple<PluginContract, string>(contract, instanceName);
            if (_plugins.TryGetValue(key, out var plugin))
            {
	            return plugin;
            }
            
            // Requested plugin is not in the cache, create the default one
            plugin = contract switch
            {
	            PluginContract.PlayFab_Serializer => CreatePlugin<TextJsonSerializerPlugin>(),
	            PluginContract.PlayFab_Transport => CreatePlugin<PlayFabSysHttp>(),
	            _ => throw new ArgumentException("This contract is not supported", nameof(contract))
            };

            _plugins[key] = plugin ?? throw new ArgumentException("This contract wasn't created", nameof(contract));

            return plugin;
        }

        private void SetPluginInternal(IPlayFabPlugin plugin, PluginContract contract, string instanceName)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null");
            }

            var key = new Tuple<PluginContract, string>(contract, instanceName);
            _plugins[key] = plugin;
        }

        private IPlayFabPlugin? CreatePlugin<T>() where T : IPlayFabPlugin, new()
        {
            return (IPlayFabPlugin?)Activator.CreateInstance(typeof(T).AsType());
        }
    }
}
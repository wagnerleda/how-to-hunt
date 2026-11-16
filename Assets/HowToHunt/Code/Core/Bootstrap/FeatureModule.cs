using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// A feature's single entry point. A module registers its cvars, commands, content types, network messages,
    /// UI panels and save slices, then initializes. Core never references features: modules are discovered
    /// through <see cref="FeatureModuleAttribute"/> on their assembly.
    /// </summary>
    public interface IFeatureModule
    {
        /// <summary>Stable id, e.g. "player".</summary>
        string Id { get; }

        /// <summary>Lower runs first. Platform -1000, Net -900, features 0+, UI/dev tools 1000.</summary>
        int Order { get; }

        /// <summary>Register cvars, commands and content. Do not touch other modules here.</summary>
        void Register(ModuleRegistrar registrar);

        /// <summary>All modules are registered; services are available.</summary>
        void Initialize();

        void Shutdown();
    }

    /// <summary>Declares a feature module: <c>[assembly: FeatureModule(typeof(MyModule))]</c>.</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class FeatureModuleAttribute : Attribute
    {
        public FeatureModuleAttribute(Type moduleType) => ModuleType = moduleType;
        public Type ModuleType { get; }
    }

    /// <summary>What a module may register during <see cref="IFeatureModule.Register"/>.</summary>
    public sealed class ModuleRegistrar
    {
        internal ModuleRegistrar(IFeatureModule module) => Module = module;

        public IFeatureModule Module { get; }
        public Foundation.ConsoleSystem Console => Foundation.ConsoleSystem.Global;

        public T Cvar<T>(T cvar) where T : Foundation.Cvar => Console.Register(cvar);

        /// <summary>Adds a slice to the player profile (versioned, migrates itself).</summary>
        public void SaveSlice(ISaveSlice slice) => ProfileStore.Register(slice);

        public void Command(string name, string description, Foundation.CommandHandler handler,
            Foundation.CommandFlags flags = Foundation.CommandFlags.None)
            => Console.Register(name, description, handler, flags);
    }

    /// <summary>Discovers, orders and drives all feature modules.</summary>
    public sealed class ModuleRegistry
    {
        private readonly List<IFeatureModule> _modules = new List<IFeatureModule>();

        public IReadOnlyList<IFeatureModule> Modules => _modules;

        public void DiscoverAndRegister()
        {
            foreach (Assembly assembly in UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies())
            {
                if (!assembly.GetName().Name.StartsWith("HowToHunt", StringComparison.Ordinal)) continue;
                foreach (FeatureModuleAttribute attr in assembly.GetCustomAttributes<FeatureModuleAttribute>())
                {
                    if (!typeof(IFeatureModule).IsAssignableFrom(attr.ModuleType))
                    {
                        Debug.LogError($"[Modules] {attr.ModuleType} does not implement IFeatureModule.");
                        continue;
                    }
                    _modules.Add((IFeatureModule)Activator.CreateInstance(attr.ModuleType));
                }
            }
            _modules.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));

            foreach (IFeatureModule module in _modules)
            {
                try { module.Register(new ModuleRegistrar(module)); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public void InitializeAll()
        {
            foreach (IFeatureModule module in _modules)
            {
                try { module.Initialize(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public void ShutdownAll()
        {
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                try { _modules[i].Shutdown(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            _modules.Clear();
        }
    }
}

using Microsoft.JavaScript.NodeApi.Runtime;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace JavascriptForGrasshopper
{
    internal static class Node
    {
        /// <summary>
        /// When <see cref="DebuggerEnabled"/> is true, the debugger will listen on this port for incoming connections.
        /// </summary>
        public const int DEBUGGER_PORT = 9229;

        public static string EnvironmentRoot => PluginInstalledFolder;

        /// <summary>
        /// The plugin folder on the users machine.
        /// </summary>
        public static string PluginInstalledFolder => Path.GetDirectoryName(Assembly.GetAssembly(typeof(Node)).Location);

        private static NodejsPlatform m_platform;

        /// <summary>
        /// The single instance of the NodeJS platform.
        /// </summary>
        private static NodejsPlatform Platform
        {
            get
            {
                if (m_platform == null)
                {
                    string path = Rhino.Runtime.HostUtils.RunningOnWindows ?
                        Path.Combine(PluginInstalledFolder, "native", "win-x64", "libnode.dll") :
                        Path.Combine(PluginInstalledFolder, "native", "osx-universal", "libnode.dylib");

                    m_platform = new NodejsPlatform(path);

                    // Create a package.json that specifies that node environments should use module mode
                    string package = Path.Combine(EnvironmentRoot, "package.json");
                    if (!File.Exists(package))
                    {
                        File.WriteAllText(package, $"{{\"type\":\"module\"}}");

                    }
                }
                return m_platform;
            }
        }

        private static NodejsEnvironment m_environment;

        /// <summary>
        /// The current nodeJS environment. Shared between all JS/TS components. 
        /// This environment may change frequently as scripts get recompiled.  
        /// It will be instantiated on demand.
        /// </summary>
        internal static NodejsEnvironment Environment
        {
            get
            {
                if (m_environment == null)
                {
                    // Use the package.json in the plugin root folder.
                    string dir = Path.GetDirectoryName(Assembly.GetAssembly(typeof(Node)).Location);

                    m_environment = Platform.CreateEnvironment(dir);

                    NodeConsole.SetupConsole(m_environment);

                    if (m_debuggerEnabled)
                    {
                        m_environment.StartInspector(DEBUGGER_PORT);
                    }
                }
                return m_environment;
            }
        }

        /// <summary>
        /// Called when imports need to be re-resolved (i.e. a script we depend on has changed).
        /// </summary>
        public static void Reset()
        {
            m_environment?.Dispose();
            m_environment = null;
        }

        private static bool m_debuggerEnabled = true;

        /// <summary>
        /// Enables or disables debugging for all JS components.
        /// </summary>
        public static bool DebuggerEnabled
        {
            get => m_debuggerEnabled;
            set
            {
                if (value != m_debuggerEnabled)
                {
                    m_debuggerEnabled = value;
                    if (m_debuggerEnabled)
                    {
                        if (m_environment != null)
                        {
                            Uri uri = m_environment.StartInspector(DEBUGGER_PORT);
                            Debug.WriteLine($"Debugger enabled: {uri}");
                        }
                    }
                    else
                    {
                        if (m_environment != null)
                        {
                            m_environment.StopInspector();
                        }
                    }
                }
            }
        }
    }
}

using Microsoft.JavaScript.NodeApi.Runtime;
using System;
using System.IO;
using System.Reflection;
using static Plugin.NodeConsole;

namespace Plugin
{
    internal static class Node
    {
        public const int DEBUGGER_PORT = 9229;

        public static event EventHandler<ConsoleEventArgs> OnMessage;

        internal static void InvokeOnMessage(object sender, ConsoleEventArgs e)
        {
            OnMessage?.Invoke(sender, e);
        }

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
                    // TODO: Support macOS binary
                    if (!Rhino.Runtime.HostUtils.RunningOnWindows)
                    {
                        throw new NotSupportedException("This platform is not supported.");
                    }

                    string path = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(JSComponent)).Location), "native", "win64", "libnode.dll");
                    m_platform = new NodejsPlatform(path);
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

                    SetupConsole(m_environment);

                    if (debuggerEnabled)
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

        /// <summary>
        /// True if the debugger is enabled.
        /// </summary>
        private static bool debuggerEnabled = true;

        /// <summary>
        /// Enables or disables debugging for all JS components.
        /// </summary>
        public static bool DebuggerEnabled
        {
            get => debuggerEnabled;
            set
            {
                if (value != debuggerEnabled)
                {
                    debuggerEnabled = value;
                    if (debuggerEnabled)
                    {
                        if (m_environment != null)
                        {
                            m_environment.StartInspector(DEBUGGER_PORT);
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

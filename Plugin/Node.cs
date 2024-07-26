using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.Runtime;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace JavascriptForGrasshopper
{
    internal static class Node
    {
        /// <summary>
        /// When <see cref="DebuggerEnabled"/> is true, the debugger will listen on this port for incoming connections.
        /// </summary>
        public const int DEBUGGER_PORT = 9229;

        public static string ModuleRootFolder => Path.Combine(JSComponent.TemplatesFolder, "node");

        /// <summary>
        /// The plugin folder on the users machine.
        /// </summary>
        public static string PluginFolder => Path.GetDirectoryName(Assembly.GetAssembly(typeof(Node)).Location);

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
                    string path =
#if RHINO_WIN
                        Path.Combine(PluginFolder, "native", "win-x64", "libnode.dll");
#else
                        Path.Combine(PluginFolder, "native", "osx-universal", "libnode.dylib");
#endif

                    m_platform = new NodejsPlatform(path);

                    string esbuildBinaryPath =
#if RHINO_MAC && RHINO_ARM64
                        Path.Combine(ModuleRootFolder, "node_modules", "@esbuild", "darwin-arm64", "bin", "esbuild");
#elif RHINO_MAC && RHINO_X64
                        Path.Combine(ModuleRootFolder, "node_modules", "@esbuild", "darwin-x64", "bin", "esbuild");
#elif RHINO_WIN && RHINO_X64
                        Path.Combine(ModuleRootFolder, "node_modules", "@esbuild", "win32-x64", "bin", "esbuild.exe");
#else
#error Unsupported ESBuild platform
#endif
                    System.Environment.SetEnvironmentVariable("ESBUILD_BINARY_PATH", esbuildBinaryPath);
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
                    m_environment = Platform.CreateEnvironment(ModuleRootFolder);

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
        /// Creates a JS bundle from a source folder
        /// </summary>
        /// <param name="entryPoint">The entry point, typically index.js in the source folder.</param>
        /// <param name="outFile">The output js file.</param>
        internal static async Task<bool> Bundle(string entryPoint, string outFile, Action<NodeConsole.MessageLevel, string[]> errorCallback = null, bool minify = true)
        {
            bool success = false;
            await Environment.RunAsync(async () =>
            {
                using (new NodeConsole.ConsoleToRuntimeMessage((level, msgs) =>
                {
                    errorCallback?.Invoke(level, msgs);
                }))
                {
                    try
                    {
                        string bundleScript = Path.Combine(ModuleRootFolder, "index.js");
                        JSValue bundleFunction = await Environment.ImportAsync(bundleScript, "bundle", true);
                        bool isFunc = bundleFunction.IsFunction();
                        Debug.Assert(bundleFunction.IsFunction(), "Bundle was not a function!");
                        JSValue buildResult = bundleFunction.Call(thisArg: default, entryPoint, outFile, minify);

                        if (buildResult.IsPromise())
                        {
                            buildResult = await ((JSPromise)buildResult).AsTask();
                        }
                        if (buildResult.IsBoolean())
                        {
                            success = buildResult.GetValueBool();
                        }
                        else
                        {
                            throw new InvalidDataException("Expected a boolean result from the JS bundle function.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Something went wrong during bundling...");
                        Debug.WriteLine(ex.ToString());
                        success = false;
                    }
                }
            });
            return success;

        }

        /// <summary>
        /// Called when imports need to be re-resolved (i.e. a script we depend on has changed).
        /// </summary>
        public static void ClearEnvironment()
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

﻿using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.DotNetHost;
using Microsoft.JavaScript.NodeApi.Runtime;
using Rhino;
using System;
using System.Collections.Generic;
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
                    System.Environment.SetEnvironmentVariable("ESBUILD_BINARY_PATH", ESBinaryPath);
                    m_platform = new NodejsPlatform(LibNodePath);
                }
                return m_platform;
            }
        }

        /// <summary>
        /// The path to the libnode binary
        /// </summary>
        private static string LibNodePath
        {
            get
            {
                if (Rhino.Runtime.HostUtils.RunningOnWindows)
                {
                    return Path.Combine(PluginFolder, "native", "win-x64", "libnode.dll"); ;
                }
                else if (Rhino.Runtime.HostUtils.RunningOnOSX)
                {
                    return Path.Combine(PluginFolder, "native", "osx-universal", "libnode.dylib"); ;
                }
                else
                {
                    throw new NotSupportedException("Only Rhino on Windows and OSX are supported.");

                }
            }
        }

        /// <summary>
        /// The path to the esbuild binary based on OS and architecture
        /// </summary>
        private static string ESBinaryPath
        {
            get
            {
                if (Rhino.Runtime.HostUtils.RunningOnWindows)
                {
                    return Path.Combine(ModuleRootFolder, "node_modules", "@esbuild", "win32-x64", "esbuild.exe"); ;
                }
                else if (Rhino.Runtime.HostUtils.RunningOnOSX)
                {
                    switch (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture)
                    {
                        case System.Runtime.InteropServices.Architecture.Arm64:
                            return Path.Combine(ModuleRootFolder, "node_modules", "@esbuild", "darwin-arm64", "bin", "esbuild");
                        case System.Runtime.InteropServices.Architecture.X64:
                            return Path.Combine(ModuleRootFolder, "node_modules", "@esbuild", "darwin-x64", "bin", "esbuild");
                        default:
                            throw new NotSupportedException($"Unsupported processor architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
                    }
                }
                else
                {
                    throw new NotSupportedException("Only Rhino on Windows and OSX are supported.");
                }
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
                    LoadDefaultDotNetTypes(m_environment);

                    if (m_debuggerEnabled)
                    {
                        m_environment.StartInspector(DEBUGGER_PORT);
                    }
                }
                return m_environment;
            }
        }

        private static HashSet<Type> m_exportedTypes;
        private static TypeExporter m_typeExporter;

        /// <summary>
        /// Loads dotnet types into the "dotnet" global JS object.
        /// </summary>
        /// <param name="env"></param>
        private static void LoadDefaultDotNetTypes(NodejsEnvironment env)
        {
            env.Run(() =>
            {
                JSObject managedTypes = (JSObject)JSValue.CreateObject();
                JSValue.Global.SetProperty("dotnet", managedTypes);
                typeof(JSMarshaller)
                .GetField("s_current", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, new JSMarshaller()
                {
                    AutoCamelCase = false
                });

                m_exportedTypes = new HashSet<Type>();
                m_typeExporter = new TypeExporter(JSMarshaller.Current, managedTypes);
                m_typeExporter.ExportAssemblyTypes(typeof(RhinoDoc).Assembly);
                m_typeExporter.ExportAssemblyTypes(typeof(Grasshopper.Kernel.GH_Document).Assembly);
            });
        }

        internal static void EnsureType(Type type)
        {
            if (m_exportedTypes.Add(type))
            {
                try
                {
                    m_typeExporter.ExportType(type);
                    Debug.WriteLine("Exported type: " + type.FullName);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"{type.FullName} is not supported: " + e.Message);
                }
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
                        Debug.Assert(bundleFunction.IsFunction(), "bundle() was not a function!");
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
            m_exportedTypes = null;
            m_typeExporter = null;
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

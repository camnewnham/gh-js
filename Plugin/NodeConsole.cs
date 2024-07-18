using Grasshopper.Kernel;
using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.Runtime;
using System;

namespace Plugin
{
    internal static class NodeConsole
    {
        /// <summary>
        /// Configures console.log functions in JS to redirect to component message bubbles.
        /// </summary>
        internal static void SetupConsole(NodejsEnvironment env)
        {
            env.Run(() =>
            {
                JSValue console = JSValue.CreateObject();

                console.SetProperty("log", CreateLogFunction(GH_RuntimeMessageLevel.Remark));
                console.SetProperty("info", CreateLogFunction(GH_RuntimeMessageLevel.Remark));
                console.SetProperty("warn", CreateLogFunction(GH_RuntimeMessageLevel.Warning));
                console.SetProperty("error", CreateLogFunction(GH_RuntimeMessageLevel.Error));

                JSValue.Global.SetProperty("console", console);
            });
        }

        /// <summary>
        /// Creates a function that raises <see cref="OnMessage"/> with it's arguments
        /// </summary>
        /// <param name="messageLevel">The level to provide for messages</param>
        /// <returns></returns>
        private static JSValue CreateLogFunction(GH_RuntimeMessageLevel messageLevel)
        {
            return JSValue.CreateFunction(null, (args) =>
            {
                Node.InvokeOnMessage(null, new ConsoleEventArgs(messageLevel, args));
                return JSValue.Undefined;
            });
        }

        internal class ConsoleEventArgs : EventArgs
        {
            public readonly string[] Args;
            public readonly GH_RuntimeMessageLevel Level;
            public ConsoleEventArgs(GH_RuntimeMessageLevel level, JSCallbackArgs args)
            {
                Level = level;
                Args = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    Args[i] = (string)args[i].CoerceToString();
                }
            }
        }

        /// <summary>
        /// Pipes messages from js "console.log" into GH runtime messages. 
        /// Ensure it is only used in a "using" context.
        /// </summary>
        public class ConsoleToRuntimeMessage : IDisposable
        {
            private readonly IGH_Component Component;
            public ConsoleToRuntimeMessage(IGH_Component component)
            {
                Component = component;
                Node.OnMessage += OnMessage;
            }

            private void OnMessage(object sender, ConsoleEventArgs e)
            {
                Component.AddRuntimeMessage(e.Level, string.Join(Environment.NewLine, e.Args));
            }

            void IDisposable.Dispose()
            {
                Node.OnMessage -= OnMessage;
            }
        }
    }
}

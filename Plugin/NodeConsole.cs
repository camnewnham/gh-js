using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.Runtime;
using System;

namespace JavascriptForGrasshopper
{
    /// <summary>
    /// Utilities for intercepting console.log and related functions to assist with diagnostics in Rhino and Grasshopper.
    /// </summary>
    internal static class NodeConsole
    {
        /// <summary>
        /// Raised when a message is written to the console via the javascript console.log functions.
        /// </summary>
        public static event EventHandler<ConsoleEventArgs> OnMessage;

        internal static void InvokeOnMessage(object sender, ConsoleEventArgs e)
        {
            OnMessage?.Invoke(sender, e);
        }

        /// <summary>
        /// Configures console.log functions in JS to redirect to component message bubbles.
        /// </summary>
        internal static void SetupConsole(NodejsEnvironment env)
        {
            env.Run(() =>
            {
                JSValue console = JSValue.CreateObject();

                console.SetProperty("log", CreateLogFunction(MessageLevel.Debug));
                console.SetProperty("info", CreateLogFunction(MessageLevel.Information));
                console.SetProperty("warn", CreateLogFunction(MessageLevel.Warning));
                console.SetProperty("error", CreateLogFunction(MessageLevel.Error));

                JSValue.Global.SetProperty("console", console);
            });
        }

        /// <summary>
        /// Creates a function that raises <see cref="OnMessage"/> with it's arguments
        /// </summary>
        /// <param name="messageLevel">The level to provide for messages</param>
        /// <returns></returns>
        private static JSValue CreateLogFunction(MessageLevel messageLevel)
        {
            return JSValue.CreateFunction(null, (args) =>
            {
                OnMessage?.Invoke(null, new ConsoleEventArgs(messageLevel, args));
                return JSValue.Undefined;
            });
        }

        public enum MessageLevel
        {
            Debug,
            Information,
            Warning,
            Error
        }

        /// <summary>
        /// Wrapper for console.log events to be raised as GH runtime messages.
        /// </summary>
        internal class ConsoleEventArgs : EventArgs
        {

            public readonly string[] Args;
            public readonly MessageLevel Level;
            public ConsoleEventArgs(MessageLevel level, JSCallbackArgs args)
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
            private Action<MessageLevel, string[]> Action;
            public ConsoleToRuntimeMessage(Action<MessageLevel, string[]> handler)
            {
                Action = handler;
                NodeConsole.OnMessage += OnMessage;
            }

            private void OnMessage(object sender, ConsoleEventArgs e)
            {
                Action?.Invoke(e.Level, e.Args);
            }

            void IDisposable.Dispose()
            {
                NodeConsole.OnMessage -= OnMessage;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace JavascriptForGrasshopper
{
    internal class BundleFailedEventArgs : EventArgs
    {
        public List<KeyValuePair<NodeConsole.MessageLevel, string[]>> Errors;

        public BundleFailedEventArgs(List<KeyValuePair<NodeConsole.MessageLevel, string[]>> errors)
        {
            Errors = errors;
        }
    }

    /// <summary>
    /// Watches for source changes and produces bundles.
    /// </summary>
    internal class Bundler : IDisposable
    {
        public event Action<string> BundleChanged;
        public event Action<string> SourceChanged;
        public event Action<BundleFailedEventArgs> BundleFailed;

        /// <summary>
        /// Watcher for the source code
        /// </summary>
        private FileSystemWatcher m_sourceWatcher;

        /// <summary>
        /// The entry point to the bundle code, i.e. index.js
        /// </summary>
        public readonly string EntryPoint;
        /// <summary>
        /// The output path for bundled source code.
        /// </summary>
        public readonly string BundlePath;

        /// <summary>
        /// Extensions or file names that are monitored for changes.
        /// </summary>
        public static readonly string[] MonitoredSuffixes = new string[]
        {
            ".js", ".ts", "package.json"
        };

        /// <summary>
        /// Creates a new bundler
        /// </summary>
        /// <param name="entryPoint">The entry point of the source code, typically index.js</param>
        /// <param name="bundlePath">The path to the bundle</param>
        public Bundler(string entryPoint, string bundlePath)
        {
            EntryPoint = entryPoint;
            BundlePath = bundlePath;

            m_sourceWatcher = new FileSystemWatcher(Path.GetDirectoryName(EntryPoint))
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
            m_sourceWatcher.Changed += OnSourceChanged;
            m_sourceWatcher.Created += OnSourceChanged;
        }

        /// <summary>
        /// Immediately requests a bundling operation.
        /// </summary>
        public void Bundle()
        {
            OnSourceChanged(null, null);
        }

        private void OnBundleChanged(object sender, FileSystemEventArgs e)
        {
            Rhino.RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                BundleChanged?.Invoke(BundlePath);
            }));
        }

        /// <summary>
        /// Checks if a file has a name or extension that we care about
        /// </summary>
        /// <param name="path">The file path</param>
        /// <returns>True if it was a monitored file</returns>
        private bool IsMonitored(string path)
        {
            if (path.Contains("node_modules"))
            {
                return false;
            }

            foreach (string suffix in MonitoredSuffixes)
            {
                if (path.EndsWith(suffix))
                {
                    return true;
                }
            }
            return false;
        }

        private async void OnSourceChanged(object sender, FileSystemEventArgs e)
        {
            if (!IsMonitored(e.FullPath))
            {
                return;
            }

            if (e.FullPath == BundlePath)
            {
                Rhino.RhinoApp.InvokeOnUiThread((Action)(() =>
                {
                    BundleChanged?.Invoke(BundlePath);
                }));
                return;
            }

            if (e.FullPath.EndsWith("ts"))
            {
                Rhino.RhinoApp.InvokeOnUiThread((Action)(() =>
                {
                    SourceChanged?.Invoke(Path.GetDirectoryName(EntryPoint));
                }));
            }

            List<KeyValuePair<NodeConsole.MessageLevel, string[]>> errors = new List<KeyValuePair<NodeConsole.MessageLevel, string[]>>();
            void ErrorCallback(NodeConsole.MessageLevel level, string[] msgs)
            {
                errors.Add(new KeyValuePair<NodeConsole.MessageLevel, string[]>(level, msgs));
            }
            if (!await Node.Bundle(EntryPoint, BundlePath, ErrorCallback, true))
            {
                Rhino.RhinoApp.InvokeOnUiThread((Action)(() =>
                {
                    BundleFailed?.Invoke(new BundleFailedEventArgs(errors));
                }));
            }

        }

        public void Dispose()
        {
            m_sourceWatcher?.Dispose();
            m_sourceWatcher = null;
        }
    }
}

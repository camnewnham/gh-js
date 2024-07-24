using GH_IO.Serialization;
using Grasshopper.Kernel;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace JavascriptForGrasshopper
{
    public partial class JSComponent
    {
        /// <summary>
        /// Where source code and bundle files should be stored for execution.
        /// </summary>
        private static readonly string WorkingDir = Path.Combine(Path.GetTempPath(), "GrasshopperJavascript");

        /// <summary>
        /// The folder where the template project is located.
        /// </summary>
        private static readonly string TemplateFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(JSComponent)).Location), "Template");

        /// <summary>
        /// If true, <see cref="Read(GH_IReader)"/> has been called and this component exists.  
        /// Used to flag whether this is a "new" component in <see cref="AddedToDocument(GH_Document)"/>
        /// </summary>
        private bool m_hasDeserialized = false;
        private FileSystemWatcher m_fileSystemWatcher;

        /// <summary>
        /// Folders to be ignored when extracting the template.
        /// </summary>
        private static string[] m_ignoreFolders = new string[]
        {
            "bin", "obj", "node_modules"
        };

        /// <summary>
        /// If the bundle has been written to file, it should be here. 
        /// This is the file that gets executed.
        /// </summary>
        public string JSBundlePath
        {
            get => m_jsBundlePath;
            private set
            {
                if (value != m_jsBundlePath)
                {
                    m_jsBundlePath = value;
                    OnBundlePathChanged();
                }
            }
        }

        /// <summary>
        /// If the source code has been extracted, it should be here.
        /// If the file is not found, which will be the case when this component is transferred to another PC, this should be nullified.
        /// </summary>
        public string JSSourcePath
        {
            get => m_sourcePath;
            set => m_sourcePath = value;
        }

        /// <summary>
        /// The javascript bundle code. Can be extracted to form <see cref="JSBundlePath"/>
        /// </summary>
        public string JSBundleCode
        {
            get => m_jsBundleCode;
            private set => m_jsBundleCode = value;
        }

        /// <summary>
        /// A zip file of the source code of the component the last time it was compiled.
        /// Excludes node modules.
        /// </summary>
        public byte[] JSSourceZipContents
        {
            get => m_sourceCodeZipContents;
            private set => m_sourceCodeZipContents = value;
        }

        /// <summary>
        /// The relative path to the source code from the <see cref="WorkingDir"/>
        /// </summary>
        private string m_sourcePath;
        /// <summary>
        /// The relative path to the js bundle from the <see cref="WorkingDir"/>
        /// </summary>
        private string m_jsBundlePath;
        /// <summary>
        /// The contents of dist/index.js
        /// </summary>
        private string m_jsBundleCode;
        /// <summary>
        /// The source code for this component
        /// </summary>
        private byte[] m_sourceCodeZipContents;

        private void OnBundlePathChanged()
        {
            StopWatchFile();
            if (OnPingDocument() != null)
            {
                StartWatchFile();
            }
        }

        /// <summary>
        /// If true, the source code and/or bundle has been modified since the last time since <see cref="Write(GH_IWriter)"/> was called.
        /// On the next write, the source and bundle code should both be re-loaded from file.
        /// </summary>
        private bool m_isModifiedSinceLastWrite = false;

        public override bool Write(GH_IWriter writer)
        {
            if (m_isModifiedSinceLastWrite)
            {
                // Create zip file from the source folder
                if (JSSourcePath != null && File.Exists(Path.Combine(JSSourcePath, "dist", "index.js")))
                {
                    // Create a temp folder to copy the source to before creating a zip.
                    // This is so we can exclude node_modules etc. from the zip
                    string tmpDir = Path.Combine(WorkingDir, "Temp");
                    Directory.CreateDirectory(tmpDir);
                    CopyDirectoryRecursive(JSSourcePath, tmpDir, folder => !m_ignoreFolders.Contains(Path.GetFileName(folder)));
                    string zipFile = Path.Combine(WorkingDir, "tmp_source.zip");
                    ZipFile.CreateFromDirectory(tmpDir, zipFile);

                    using (FileStream fs = File.OpenRead(zipFile))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            fs.CopyTo(ms);
                            JSSourceZipContents = ms.ToArray();
                        }
                    };
                    File.Delete(zipFile);
                    Directory.Delete(tmpDir, true);
                }

                // Cache the bundled code for execution
                if (JSBundlePath != null && File.Exists(JSBundlePath))
                {
                    JSBundleCode = File.ReadAllText(JSBundlePath);
                }
            }
            m_isModifiedSinceLastWrite = false;

            Debug.Assert(JSBundleCode != null, "No bundle code to store!");
            writer.SetString("js_bundle_code", JSBundleCode);

            Debug.Assert(JSBundlePath != null, "No extracted bundle path to store!");
            writer.SetString("js_bundle_path_relative", GetRelativePath(JSBundlePath));

            if (JSSourceZipContents != null)
            {
                writer.SetByteArray("js_source_zip", JSSourceZipContents);
            }

            if (JSSourcePath != null)
            {
                writer.SetString("js_source_path_relative", GetRelativePath(JSSourcePath));
            }

            writer.SetBoolean("js_is_typescript", IsTypescript);

            writer.SetBoolean("js_use_output_param", UseOutputParam);

            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            m_hasDeserialized = true;

            JSBundleCode = reader.GetString("js_bundle_code");
            JSBundlePath = Path.Combine(WorkingDir, reader.GetString("js_bundle_path_relative"));
            if (reader.ItemExists("js_source_zip"))
            {
                JSSourceZipContents = reader.GetByteArray("js_source_zip");
            }
            if (reader.ItemExists("js_source_path_relative"))
            {
                JSSourcePath = Path.Combine(WorkingDir, reader.GetString("js_source_path_relative"));
            }

            IsTypescript = reader.GetBoolean("js_is_typescript");
            UseOutputParam = reader.GetBoolean("js_use_output_param");
            return base.Read(reader);
        }

        /// <summary>
        /// Gets a relative path to the working directory.
        /// </summary>
        /// <param name="absolutePath">The absolute path</param>
        /// <returns>The relative path</returns>
        private static string GetRelativePath(string absolutePath)
        {
#if NET5_0_OR_GREATER
            return Path.GetRelativePath(WorkingDir, absolutePath);
#else
            string path = absolutePath.Replace(WorkingDir, "");
            // Remove leading slashes
            return new Regex("^[\\\\\\/]+").Replace(path, "");
#endif
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            EnsureBundle();
            StartWatchFile();
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            StopWatchFile();
        }

        /// <summary>
        /// Starts watching the execution bundle for changes.
        /// </summary>
        private void StartWatchFile()
        {
            StopWatchFile();
            if (File.Exists(JSBundlePath))
            {
                m_fileSystemWatcher = new FileSystemWatcher(Path.GetDirectoryName(JSBundlePath), Path.GetFileName(JSBundlePath))
                {
                    IncludeSubdirectories = false
                };
                m_fileSystemWatcher.Changed += OnFileChanged;
                m_fileSystemWatcher.EnableRaisingEvents = true;
            }
        }

        /// <summary>
        /// Raised when the execution bundle is updated
        /// </summary>
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            Rhino.RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                OnPingDocument().ScheduleSolution(5, (doc) =>
                {
                    ExpireSolution(false);
                    Node.Reset();
                });
            }));
        }

        /// <summary>
        /// Dispose of the file system watcher, if it exists.
        /// </summary>
        private void StopWatchFile()
        {
            m_fileSystemWatcher?.Dispose();
            m_fileSystemWatcher = null;
        }

        /// <summary>
        /// Ensures the js bundle exists
        /// </summary>
        private void EnsureBundle()
        {
            if (JSBundlePath != null && File.Exists(JSBundlePath))
            {
                // OK
            }
            else if (JSBundleCode != null)
            {
                // Load from cached code
                JSBundlePath ??= Path.Combine(WorkingDir, "Bundles", "JSComponent-" + Guid.NewGuid().ToString() + ".js");
                Directory.CreateDirectory(Path.GetDirectoryName(JSBundlePath));
                File.WriteAllText(JSBundlePath, JSBundleCode);
            }
            else if (!m_hasDeserialized)
            {
                // Load from template
                JSBundleCode = File.ReadAllText(Path.Combine(TemplateFolder, "dist", "index.js"));
                JSBundlePath = Path.Combine(WorkingDir, "Bundles", "JSComponent-" + Guid.NewGuid().ToString() + ".js");
                Directory.CreateDirectory(Path.GetDirectoryName(JSBundlePath));
                File.WriteAllText(JSBundlePath, JSBundleCode);
            }

            Debug.Assert(File.Exists(JSBundlePath), "Bundle file does not exist!");
        }

        /// <summary>
        /// If this is a new component, instantiate the template and update the index path.
        /// If we have a cached source code location, ensure our paths are correct.
        /// If we don't have a source code location, but this is not a new component, load the serialized source code.
        /// </summary>
        /// <returns>The path to the folder containing the source code.</returns>
        private string GetOrCreateSourceCode()
        {
            void SetBundleToSourceDirectory()
            {
                JSBundlePath = Path.Combine(JSSourcePath, "dist", "index.js");
                Debug.Assert(File.Exists(JSBundlePath), $"index.js not found at {JSBundlePath}!");
                JSBundleCode = File.ReadAllText(JSBundlePath);
            }

            if (JSSourcePath != null && File.Exists(Path.Combine(JSSourcePath, "dist", "index.js")))
            {
                // We already have the source directory and it exists. Prioritize using the dist in the source directory.
                SetBundleToSourceDirectory();
                UpdateTypeDefinitions();
                // It may have changed while we were not monitoring the component, so flag it for re-serialization.
                m_isModifiedSinceLastWrite = true;
            }
            else if (JSSourceZipContents != null)
            {
                // Set up source code from cache
                string tmpZipFile = Path.Combine(WorkingDir, "tmp_source_restore.zip");
                Directory.CreateDirectory(Path.GetDirectoryName(tmpZipFile));
                File.WriteAllBytes(tmpZipFile, JSSourceZipContents);
                JSSourcePath = Path.Combine(WorkingDir, "Source", $"JSComponent-{Guid.NewGuid()}");
                Directory.CreateDirectory(JSSourcePath);
                ZipFile.ExtractToDirectory(tmpZipFile, JSSourcePath);
                File.Delete(tmpZipFile);
                SetBundleToSourceDirectory();
                UpdateTypeDefinitions();
            }
            else
            {
                // Set up source code from template
                string templateFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(JSComponent)).Location), "Template");
                JSSourcePath = Path.Combine(WorkingDir, "Source", "JSComponent-" + Guid.NewGuid().ToString());
                CopyDirectoryRecursive(templateFolder, JSSourcePath, folder => !m_ignoreFolders.Contains(Path.GetFileName(folder)));
                SetBundleToSourceDirectory();
                ConfigureTemplate(JSSourcePath, IsTypescript);
                UpdateTypeDefinitions();
                m_isModifiedSinceLastWrite = true;
            }
            return JSSourcePath;
        }

        /// <summary>
        /// Configures an instance of the template to either be javascript or typescript
        /// </summary>
        /// <param name="sourcePath">The directory of the source code</param>
        /// <param name="isTypescript">True to configure for typescript, false to configure for javascript.</param>
        private static void ConfigureTemplate(string sourcePath, bool isTypescript)
        {
            // Switch between typescript and javascript based on how the component was created
            string[] delete_extensions = isTypescript ? new string[] { ".js" } : new string[] { ".ts", ".d.ts" };
            DirectoryInfo di = new DirectoryInfo(sourcePath);
            foreach (FileInfo file in di.GetFiles().Where(fi => delete_extensions.Contains(fi.Extension)))
            {
                file.Delete();
            }
            if (!isTypescript)
            {
                Directory.Delete(Path.Combine(sourcePath, "types"), true);
                File.Delete(Path.Combine(sourcePath, "tsconfig.json"));
            }
        }

        /// <summary>
        /// Attempts to launch a code editor.  
        /// If it fails, just open the folder.
        /// </summary>
        public void LaunchCodeEditor()
        {
            string sourceFolder = GetOrCreateSourceCode();

            string sourcePath = Path.Combine(sourceFolder, "index.js");
            if (!File.Exists(sourcePath))
            {
                sourcePath = Path.Combine(sourceFolder, "index.ts");
            }
            Debug.Assert(File.Exists(sourcePath), "No index file in source folder!");

            bool launchedEditor = false;
            try
            {
                // Attempt to launch visual studio code
#if DEBUG
                string prefix = "";
#else
                string prefix = "--reuse-window ";
#endif
                Process launchCodeProcess = Process.Start(new ProcessStartInfo()
                {
                    FileName = "code",
                    Arguments = $"{prefix}\"{sourceFolder}\" \"{sourcePath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                launchCodeProcess.WaitForExit();
                launchedEditor = launchCodeProcess.ExitCode == 0;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to launch code editor due to exception: " + e.Message);
            }

            if (!launchedEditor)
            {
                // If code fails to open, just browse to the folder.
                Process.Start(Rhino.Runtime.HostUtils.RunningOnWindows ? "explorer" : "open", $"\"{sourceFolder}\"");
            }
        }

        /// <summary>
        /// Copies a directory recursively with a filter to exclude certain folders
        /// </summary>
        /// <param name="sourceDir">The source to copy</param>
        /// <param name="destinationDir">The destination</param>
        /// <param name="folderNameFilter">A function which returns true if a folder should be included. If null, all folders are included</param>
        private static void CopyDirectoryRecursive(string sourceDir, string destinationDir, Func<string, bool> folderNameFilter = null)
        {
            if (folderNameFilter != null && !folderNameFilter(sourceDir))
            {
                return;
            }

            DirectoryInfo dir = new DirectoryInfo(sourceDir);

            DirectoryInfo[] dirs = dir.GetDirectories();

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectoryRecursive(subDir.FullName, newDestinationDir, folderNameFilter);
            }
        }

        /// <summary>
        /// Flags the type definitions for regeneration.
        /// </summary>
        public void ExpireTypeDefinitions()
        {
            UpdateTypeDefinitions();
        }

        /// <summary>
        /// Updates the typescript definitions.
        /// </summary>
        private void UpdateTypeDefinitions()
        {
            if (!IsTypescript)
            {
                return;
            }

            if (JSSourcePath == null)
            {
                return;
            }

            string typesFile = Path.Combine(JSSourcePath, "types", "component.d.ts");

            if (!Directory.Exists(JSSourcePath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(typesFile));

            string generated = new CodeGenerator.ComponentTypeGenerator(
                Params.Input.Where(x => x is JSVariableParam).Cast<JSVariableParam>().Select(x => x.GetTypeDefinition()).ToArray(),
                Params.Output.Where(x => x is JSVariableParam).Cast<JSVariableParam>().Select(x => x.GetTypeDefinition()).ToArray()
                ).TransformText();

            File.WriteAllText(typesFile, generated);

            m_isModifiedSinceLastWrite = true;
        }

    }
}

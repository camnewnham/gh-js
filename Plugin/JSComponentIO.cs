using GH_IO.Serialization;
using Grasshopper.Kernel;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace JavascriptForGrasshopper
{
    public partial class JSComponent
    {
        /// <summary>
        /// Where source code and bundle files should be stored for execution.
        /// </summary>
        private static string WorkingDir => Path.Combine(Path.GetTempPath(), "GrasshopperJavascript");

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
        /// The path to the javascript bundle ready for execution in <see cref="SolveInstance(IGH_DataAccess)"/>.
        /// </summary>
        public string JSBundlePath => m_jsBundlePath;

        /// <summary>
        /// If the source code has been extracted, it should be here.
        /// If the file is not found, which will be the case when this component is transferred to another PC, this should be nullified.
        /// </summary>
        private string m_sourcePath;

        /// <summary>
        /// If the bundle has been written to file, it should be here. 
        /// This is the file that gets executed.
        /// </summary>
        private string m_jsBundlePath;

        /// <summary>
        /// The javascript bundle code. Can be extracted to form <see cref="m_jsBundlePath"/>
        /// </summary>
        private string m_jsBundleCode;

        /// <summary>
        /// A zip file of the source code of the component the last time it was compiled.
        /// Excludes node modules.
        /// </summary>
        private byte[] m_sourceCodeZip;

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
                if (m_sourcePath != null && File.Exists(GetBundlePathFromSourceFolder(m_sourcePath)))
                {
                    // Create a temp folder to copy the source to before creating a zip.
                    // This is so we can exclude node_modules etc. from the zip
                    string tmpDir = Path.Combine(WorkingDir, "Temp");
                    Directory.CreateDirectory(tmpDir);
                    CopyDirectoryRecursive(m_sourcePath, tmpDir, folder => !m_ignoreFolders.Contains(Path.GetFileName(folder)));
                    string zipFile = Path.Combine(WorkingDir, "tmp_source.zip");
                    ZipFile.CreateFromDirectory(tmpDir, zipFile);

                    using (FileStream fs = File.OpenRead(zipFile))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            fs.CopyTo(ms);
                            m_sourceCodeZip = ms.ToArray();
                        }
                    };
                    File.Delete(zipFile);
                    Directory.Delete(tmpDir, true);
                }

                // Cache the bundled code for execution
                if (m_jsBundlePath != null && File.Exists(m_jsBundlePath))
                {
                    m_jsBundleCode = File.ReadAllText(m_jsBundlePath);
                }
            }
            m_isModifiedSinceLastWrite = false;

            Debug.Assert(m_jsBundleCode != null, "No bundle code to store!");
            writer.SetString("js_bundle_code", m_jsBundleCode);

            Debug.Assert(m_jsBundlePath != null, "No extracted bundle path to store!");
            writer.SetString("js_bundle_path", m_jsBundlePath);

            Debug.Assert(m_sourceCodeZip != null, "No source code zip found to store!");
            writer.SetByteArray("js_source_zip", m_sourceCodeZip);

            if (m_sourcePath != null)
            {
                writer.SetString("js_source_path", m_sourcePath);
            }

            writer.SetBoolean("js_is_typescript", m_isTypeScript);

            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            m_hasDeserialized = true;

            m_jsBundleCode = reader.GetString("js_bundle_code");
            m_jsBundlePath = reader.GetString("js_bundle_path");
            m_sourceCodeZip = reader.GetByteArray("js_source_zip");
            m_isTypeScript = reader.GetBoolean("js_is_typescript");
            reader.TryGetString("js_source_path", ref m_sourcePath);
            return base.Read(reader);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (!m_hasDeserialized)
            {
                LaunchCodeEditor();
            }
            else
            {
                // Ensure we have a bundle file to execute
                if (m_sourcePath != null && File.Exists(GetBundlePathFromSourceFolder(m_sourcePath)))
                {
                    // Use the existing bundle path in the source code
                    m_jsBundlePath = GetBundlePathFromSourceFolder(m_sourcePath);
                }
                else
                {
                    Debug.Assert(m_jsBundleCode != null, "Cannot extract saved bundle code. Not found!");
                    // Create a temporary bundle file to use for runtime execution
                    m_sourcePath = null;
                    m_jsBundlePath = Path.Combine(WorkingDir, "Cache", Guid.NewGuid().ToString(), "index.js");
                    Directory.CreateDirectory(Path.GetDirectoryName(m_jsBundlePath));
                    File.WriteAllText(m_jsBundlePath, m_jsBundleCode);
                }
            }

            Debug.Assert(m_jsBundlePath != null, "Bundle path did not exist after component was added to document!");
            Debug.Assert(m_jsBundleCode != null, "Bundle code was not stored after component was added to document!");

            StartWatchFile();
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            StopWatchFile();
        }

        public override void MovedBetweenDocuments(GH_Document oldDocument, GH_Document newDocument)
        {
            base.MovedBetweenDocuments(oldDocument, newDocument);
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
        /// If this is a new component, instantiate the template and update the index path.
        /// If we have a cached source code location, ensure our paths are correct.
        /// If we don't have a source code location, but this is not a new component, load the serialized source code.
        /// </summary>
        /// <returns>The path to the folder containing the source code.</returns>
        private string GetOrCreateSourceCode()
        {
            void SetBundleToSourceDirectory()
            {
                m_jsBundlePath = GetBundlePathFromSourceFolder(m_sourcePath);
                Debug.Assert(File.Exists(m_jsBundlePath), $"index.js not found at {m_jsBundlePath}!");
                m_jsBundleCode = File.ReadAllText(m_jsBundlePath);
            }

            if (m_sourcePath != null && File.Exists(Path.Combine(m_sourcePath, "dist", "index.js")))
            {
                // We already have the source directory and it exists. Prioritize using the dist in the source directory.
                SetBundleToSourceDirectory();
                UpdateTypeDefinitions();
                // It may have changed while we were not monitoring the component, so flag it for re-serialization.
                m_isModifiedSinceLastWrite = true;
                return m_sourcePath;
            }
            else if (m_sourceCodeZip != null)
            {
                // Set up source code from cache
                string tmpZipFile = Path.Combine(WorkingDir, "tmp_source_restore.zip");
                Directory.CreateDirectory(Path.GetDirectoryName(tmpZipFile));
                File.WriteAllBytes(tmpZipFile, m_sourceCodeZip);
                m_sourcePath = Path.Combine(WorkingDir, "Source", $"JSComponent-{Guid.NewGuid()}");
                Directory.CreateDirectory(m_sourcePath);
                ZipFile.ExtractToDirectory(tmpZipFile, m_sourcePath);
                File.Delete(tmpZipFile);
                SetBundleToSourceDirectory();
                UpdateTypeDefinitions();
                return m_sourcePath;
            }
            else if (!m_hasDeserialized)
            {
                // Set up source code from template
                string templateFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(JSComponent)).Location), "Template");
                m_sourcePath = Path.Combine(WorkingDir, "Source", "JSComponent-" + Guid.NewGuid().ToString());
                CopyDirectoryRecursive(templateFolder, m_sourcePath, folder => !m_ignoreFolders.Contains(Path.GetFileName(folder)));
                SetBundleToSourceDirectory();
                ConfigureTemplate(m_sourcePath, m_isTypeScript);
                UpdateTypeDefinitions();
                m_isModifiedSinceLastWrite = true;
                return m_sourcePath;
            }
            else
            {
                throw new InvalidOperationException("Unable to create soure code. Conditions are not satisfied.");
            }
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
        /// Gets the path to the javascript bundle in a given source folder. 
        /// Does not check whether the file exists.
        /// </summary>inde
        /// <param name="sourcePath"></param>
        /// <returns>The bundle path.</returns>
        private static string GetBundlePathFromSourceFolder(string sourcePath)
        {
            return Path.Combine(sourcePath, "dist", "index.js");
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

            // Attempt to launch visual studio code
            Process launchCodeProcess = Process.Start(new ProcessStartInfo()
            {
                CreateNoWindow = true,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "code",
                Arguments = $"-r \"{sourceFolder}\" \"{sourcePath}\""
            });

            launchCodeProcess.WaitForExit();
            if (launchCodeProcess.ExitCode != 0)
            {
                // If code fails to open, just browse to the folder.
                Process.Start(new ProcessStartInfo()
                {
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = Rhino.Runtime.HostUtils.RunningOnWindows ? "explorer" : "open",
                    Arguments = $"\"{sourceFolder}\"",
                });
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

        private void UpdateTypeDefinitions()
        {
            if (!m_isTypeScript)
            {
                return;
            }

            string typesFile = Path.Combine(m_sourcePath, "types", "component.d.ts");

            if (!Directory.Exists(m_sourcePath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(typesFile));

            string generated = new Templating.ComponentTypeGenerator(
                Params.Input.Select(x => ParamToTypeDefinition(x)).ToArray(),
                Params.Output.Select(x => ParamToTypeDefinition(x)).ToArray()
                ).TransformText();

            File.WriteAllText(typesFile, generated);

            m_isModifiedSinceLastWrite = true;
        }

        private static Templating.TypeDefinition ParamToTypeDefinition(IGH_Param param)
        {
            return new Templating.TypeDefinition()
            {
                VariableName = ToCamelCase(param.NickName),
                Name = param.NickName,
                Description = param.Description,
                Type = GetJSParamType(param)
            };
        }

        private static string ToCamelCase(string s)
        {
            return string.Join("", s.Split(null).Select((word, index) =>
            {
                if (index == 0)
                {
                    return char.ToLower(word[0]) + word.Substring(1);
                }
                else
                {
                    return char.ToUpper(word[0]) + word.Substring(1);
                }
            }));
        }

        private static string GetJSParamType(IGH_Param param)
        {
            // TODO
            return "string";
        }
    }
}

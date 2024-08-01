using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace JavascriptForGrasshopper
{
    public partial class JSComponent
    {
        /// <summary>
        /// A safe (but temporary) place to store runtime data.
        /// </summary>
        public static string WorkingDir = Path.Combine(Path.GetTempPath(), "GrasshopperJavascript");

        /// <summary>
        /// The folder where the template project is located.
        /// </summary>
        internal static readonly string TemplatesFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(JSComponent)).Location), "Templates");

        /// <summary>
        /// Path to shared project, including type definitions.
        /// </summary>
        public static string TypesPath => Path.Combine(WorkingDir, "types");

        /// <summary>
        /// Watcher for file changes.
        /// </summary>
        private Bundler m_bundler;

        /// <summary>
        /// Folders to be ignored when extracting the template.
        /// </summary>
        private static string[] m_ignoreFolders = new string[]
        {
            "bin", "obj", "node_modules", "dist"
        };

        /// <summary>
        /// If the bundle has been written to file, it should be here. 
        /// This is the file that gets executed.
        /// </summary>
        public string JSBundlePath => Path.Combine(JSSourcePath, "dist", "index.js");

        /// <summary>
        /// If the source code has been extracted, it should be here.
        /// If the file is not found, which will be the case when this component is transferred to another PC, this should be nullified.
        /// </summary>
        public string JSSourcePath => Path.Combine(WorkingDir, "source", m_bundleId.ToString());

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
        /// The contents of dist/index.js
        /// </summary>
        private string m_jsBundleCode;
        /// <summary>
        /// The source code for this component
        /// </summary>
        private byte[] m_sourceCodeZipContents;

        /// <summary>
        /// If true, the source code and/or bundle has been modified since the last time since <see cref="Write(GH_IWriter)"/> was called.
        /// On the next write, the source and bundle code should both be re-loaded from file.
        /// </summary>
        private bool m_isModifiedSinceLastWrite = false;

        /// <summary>
        /// The ID of the bundle. Used to find the source code or bundled code.
        /// </summary>
        private Guid m_bundleId;

        /// <summary>
        /// If true, the bundle was not created due to errors in the source code.
        /// </summary>
        private bool m_hasCompileErrors = false;

        /// <summary>
        /// If true, the component has variable names which break compilation.
        /// </summary>
        private bool m_hasInvalidParams = false;

        /// <summary>
        /// Whether the source code has been extracted.
        /// </summary>
        private bool m_hasExtractedSource = false;

        /// <summary>
        /// If we have source code extracted, monitor for bundle changes
        /// </summary>
        private void WatchForFileChanges()
        {
            if (m_bundler != null)
            {
                return;
            }

            if (Directory.Exists(JSSourcePath))
            {
                m_bundler = new Bundler(Path.Combine(JSSourcePath, IsTypescript ? "index.ts" : "index.js"), JSBundlePath);
                m_bundler.SourceChanged += (arg) =>
                {
                    m_isModifiedSinceLastWrite = true;
                };
                m_bundler.BundleChanged += (arg) =>
                {
                    m_hasCompileErrors = false;
                    m_isModifiedSinceLastWrite = true;
                    Node.ClearEnvironment();
                    if (!Locked)
                    {
                        OnPingDocument()?.ScheduleSolution(5, (doc) =>
                        {
                            ExpireSolution(false);
                        });
                    }
                };
                m_bundler.BundleFailed += (args) =>
                {
                    m_hasCompileErrors = true;
                    ClearRuntimeMessages();
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to bundle source code.");
                    foreach (KeyValuePair<NodeConsole.MessageLevel, string[]> err in args.Errors)
                    {
                        AddRuntimeMessage(err.Key, err.Value);
                    }
                    Instances.RedrawCanvas();
                };
            }
        }

        /// <summary>
        /// Stops monitoring for changes to the source code or output bundle
        /// </summary>
        private void StopWatchingForFileChanges()
        {
            m_bundler?.Dispose();
            m_bundler = null;
        }

        public override bool Write(GH_IWriter writer)
        {
            if (m_isModifiedSinceLastWrite)
            {
                // Create zip file from the source folder
                if (Directory.Exists(JSSourcePath))
                {
                    JSSourceZipContents = Utils.ZipFolder(JSSourcePath, m_ignoreFolders);
                }

                // Cache the bundled code for execution
                if (File.Exists(JSBundlePath))
                {
                    JSBundleCode = File.ReadAllText(JSBundlePath);
                }
            }
            m_isModifiedSinceLastWrite = false;

            Debug.Assert(JSBundleCode != null, "No bundle code to store!");
            writer.SetString("js_bundle_code", JSBundleCode);

            if (JSSourceZipContents != null)
            {
                writer.SetByteArray("js_source_zip", JSSourceZipContents);
            }

            writer.SetBoolean("js_is_typescript", IsTypescript);

            writer.SetBoolean("js_use_output_param", UseOutputParam);

            writer.SetBoolean("js_has_compile_errors", m_hasCompileErrors);

            writer.SetString("plugin_version", typeof(JSComponent).Assembly.GetName().Version.ToString());

            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            JSBundleCode = reader.GetString("js_bundle_code");
            if (reader.ItemExists("js_source_zip"))
            {
                JSSourceZipContents = reader.GetByteArray("js_source_zip");
            }

            IsTypescript = reader.GetBoolean("js_is_typescript");
            UseOutputParam = reader.GetBoolean("js_use_output_param");
            m_hasCompileErrors = reader.GetBoolean("js_has_compile_errors");
            return base.Read(reader);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            InitializeBundle();
            WatchForFileChanges();

            if (ValidateParams(out string errReason))
            {
                m_hasInvalidParams = true;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, errReason);
            }

            if (m_hasCompileErrors)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to bundle source code. Please check the source code for syntax errors.");
            }
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            StopWatchingForFileChanges();
        }

        /// <summary>
        /// Generates a new ID for the bundle.  
        /// Loads the cached bundle to a file, or loads the template bundle if it does not exist.
        /// </summary>
        private void InitializeBundle()
        {
            m_bundleId = Guid.NewGuid();

            if (JSBundleCode != null) // Restore from component
            {
                Directory.CreateDirectory(Path.GetDirectoryName(JSBundlePath));
                File.WriteAllText(JSBundlePath, JSBundleCode);
                Debug.WriteLine($"Wrote JS cached bundle to {JSBundlePath}");
            }
            else // New component
            {
                string templateBundle = Path.Combine(TemplatesFolder, IsTypescript ? "ts" : "js", "dist", "index.js");
                Directory.CreateDirectory(Path.GetDirectoryName(JSBundlePath));
                File.Copy(templateBundle, JSBundlePath);
                m_isModifiedSinceLastWrite = true;
                Debug.WriteLine($"Wrote JS template bundle to {JSBundlePath}");
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
            if (m_hasExtractedSource && Directory.Exists(JSSourcePath)) // Use existing source
            {
                // Source directory exists.
            }
            else if (JSSourceZipContents != null) // Use zip source
            {
                Directory.CreateDirectory(JSSourcePath);
                Utils.UnzipFolder(JSSourceZipContents, JSSourcePath);
                UpdateTypeDefinitions();
                m_hasExtractedSource = true;
                Debug.WriteLine($"Extracted zip source to {JSSourcePath}");
                Utils.RunNpmInstall(JSSourcePath);
            }
            else // Use template source
            {
                Directory.CreateDirectory(JSSourcePath);
                Utils.CopyDirectoryRecursive(Path.Combine(TemplatesFolder, IsTypescript ? "ts" : "js"), JSSourcePath);
                UpdateTypeDefinitions();
                m_hasExtractedSource = true;
                Debug.WriteLine($"Extracted template to {JSSourcePath}");
            }
            WatchForFileChanges();
            return JSSourcePath;
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

            if (!Directory.Exists(JSSourcePath))
            {
                return;
            }

            EnsureTypesDirectory();

            string typesFile = Path.Combine(JSSourcePath, "types", "component.d.ts");

            Directory.CreateDirectory(Path.GetDirectoryName(typesFile));

            string generated = new CodeGenerator.ComponentTypeGenerator(
                Params.Input.Where(x => x is JSVariableParam).Cast<JSVariableParam>().Select(x => x.GetTypeDefinition()).ToArray(),
                Params.Output.Where(x => x is JSVariableParam).Cast<JSVariableParam>().Select(x => x.GetTypeDefinition()).ToArray()
                ).TransformText();

            File.WriteAllText(typesFile, generated);

            m_isModifiedSinceLastWrite = true;
        }

        private static void EnsureTypesDirectory()
        {
            if (Directory.Exists(TypesPath)) return;

            Directory.CreateDirectory(TypesPath);
            Utils.CopyDirectoryRecursive(Path.Combine(TemplatesFolder, "types"), TypesPath);
        }
    }
}

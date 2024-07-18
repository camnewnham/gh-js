using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Microsoft.JavaScript.NodeApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Plugin
{
    public class JSComponent : GH_Component, IGH_VariableParameterComponent
    {
        public override GH_Exposure Exposure => GH_Exposure.primary;
        public override Guid ComponentGuid => new Guid("440e1113-51b0-46a9-be9b-a7d025e6e312");
        public JSComponent() : base("JavaScript", "JS", "Write and execute JavaScript.", "Maths", "Script") { }

        public readonly bool IsTypescript = false;
        public JSComponent(bool typescript) : this()
        {
            NickName = "TS";
            IsTypescript = true;
        }

        private static string[] m_keywords = new string[]
        {
            "javascript", "js", "typescript", "ts", "nodejs", "node", "script", "execute", "run", "code", "vs", "vsc"
        };

        public override IEnumerable<string> Keywords => m_keywords;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Result", "R", "Whatever was returned from the script", GH_ParamAccess.item);
        }

        public static readonly string IndexPath = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(JSComponent)).Location), "..", "..", "..", "Template", ".dist", "index.js");

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
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

        private FileSystemWatcher m_fileSystemWatcher;

        private void StartWatchFile()
        {
            StopWatchFile();
            if (File.Exists(IndexPath))
            {
                m_fileSystemWatcher = new FileSystemWatcher(Path.GetDirectoryName(IndexPath), Path.GetFileName(IndexPath))
                {
                    IncludeSubdirectories = false
                };
                m_fileSystemWatcher.Changed += OnFileChanged;
                m_fileSystemWatcher.EnableRaisingEvents = true;
            }
        }

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

        private void StopWatchFile()
        {
            m_fileSystemWatcher?.Dispose();
            m_fileSystemWatcher = null;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!File.Exists(IndexPath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "File not found. Has the module been built yet?");
                return;
            }

            string code = File.ReadAllText(IndexPath);

            ManualResetEventSlim mre = new ManualResetEventSlim(false);

            Node.Environment.RunAsync(async () =>
            {
                using (new NodeConsole.ConsoleToRuntimeMessage(this))
                {
                    try
                    {
                        JSValue runScript = await Node.Environment.ImportAsync(IndexPath, "runScript", true);

                        JSValue inputs = JSValue.CreateObject();
                        inputs.SetProperty("test", 4);

                        JSValue result = runScript.Call(thisArg: default, inputs);

                        if (result.IsPromise())
                        {
                            result = await ((JSPromise)result).AsTask();
                        }

                        if (!result.IsObject() && Params.Output.Count > 0)
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid return type. Expected an object with names matching component outputs.");
                            return;
                        }

                        DA.SetData(0, result["square"].GetValueExternalOrPrimitive());
                    }
                    catch (JSException jsex)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A javaScript exception occurred: " + jsex.Message);
                    }
                    finally
                    {
                        mre.Set();
                    }
                }
            });

            mre.Wait();
        }
        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Menu_AppendItem(menu, "Enable Debugger", (ev, arg) =>
            {
                Node.DebuggerEnabled = !Node.DebuggerEnabled;
            }, true, Node.DebuggerEnabled);
        }

        bool IGH_VariableParameterComponent.CanInsertParameter(GH_ParameterSide side, int index)
        {
            return true;
        }

        bool IGH_VariableParameterComponent.CanRemoveParameter(GH_ParameterSide side, int index)
        {
            return true;
        }

        IGH_Param IGH_VariableParameterComponent.CreateParameter(GH_ParameterSide side, int index)
        {
            return new Param_GenericObject();
        }

        bool IGH_VariableParameterComponent.DestroyParameter(GH_ParameterSide side, int index)
        {
            return true;
        }

        void IGH_VariableParameterComponent.VariableParameterMaintenance()
        {

        }
    }
}
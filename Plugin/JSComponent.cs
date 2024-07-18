using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.Runtime;
using System;
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
        public JSComponent()
          : base("Javascript Code", "Javascript",
            "Write and/or execute javascript.",
            "Maths", "Script")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Result", "R", "Whatever was returned from the script", GH_ParamAccess.item);
        }

        public static readonly string IndexPath = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(JSComponent)).Location), "..", "..", "..", "Template", ".dist", "index.js");

        private bool m_debugNext = false;

        private static NodejsPlatform m_Node;
        private static NodejsPlatform Node
        {
            get
            {
                if (m_Node == null)
                {
                    // TODO: Support macOS binary
                    if (!Rhino.Runtime.HostUtils.RunningOnWindows)
                    {
                        throw new NotSupportedException("This platform is not supported.");
                    }

                    string path = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(JSComponent)).Location), "native", "win64", "libnode.dll");
                    m_Node = new NodejsPlatform(path);
                }
                return m_Node;
            }
        }

        private NodejsEnvironment m_nodeEnvironment;

        private NodejsEnvironment NodeEnvironment
        {
            get
            {
                if (m_nodeEnvironment == null)
                {
                    m_nodeEnvironment = Node.CreateEnvironment(Path.GetDirectoryName(Path.Combine(Path.GetDirectoryName(IndexPath), "..")));
                    m_nodeEnvironment.Run(() =>
                    {
                        SetupConsole();
                    });
                }
                return m_nodeEnvironment;
            }
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            ClearEnvironment();
            StartWatchFile();
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            ClearEnvironment();
            StopWatchFile();
        }

        public override void MovedBetweenDocuments(GH_Document oldDocument, GH_Document newDocument)
        {
            base.MovedBetweenDocuments(oldDocument, newDocument);
            ClearEnvironment();
            StopWatchFile();
        }

        public void ClearEnvironment()
        {
            m_nodeEnvironment?.Dispose();
            m_nodeEnvironment = null;
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
                    ClearEnvironment();
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

            if (m_debugNext)
            {
                NodeEnvironment.StartInspector(9229, null, true);
            }

            ManualResetEventSlim mre = new ManualResetEventSlim(false);

            NodeEnvironment.RunAsync(async () =>
            {
                try
                {
                    JSValue runScript = await NodeEnvironment.ImportAsync(IndexPath, "runScript", true);

                    JSValue inputs = JSValue.CreateObject();
                    inputs.SetProperty("test", 4);

                    JSValue result = runScript.Call(thisArg: default(JSValue), inputs);

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
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A javascript exception occurred: " + jsex.Message);
                }
                finally
                {
                    mre.Set();
                }
            });

            mre.Wait();

            if (m_debugNext)
            {
                m_debugNext = false;
                NodeEnvironment.StopInspector();
            }
        }

        /// <summary>
        /// Configures console.log functions in JS to redirect to component message bubbles.
        /// </summary>
        private void SetupConsole()
        {
            JSValue console = JSValue.CreateObject();

            console.SetProperty("log", CreateLogFunction(GH_RuntimeMessageLevel.Remark));
            console.SetProperty("info", CreateLogFunction(GH_RuntimeMessageLevel.Remark));
            console.SetProperty("warn", CreateLogFunction(GH_RuntimeMessageLevel.Warning));
            console.SetProperty("error", CreateLogFunction(GH_RuntimeMessageLevel.Error));

            JSValue.Global.SetProperty("console", console);
        }

        private JSValue CreateLogFunction(GH_RuntimeMessageLevel errorLevel)
        {
            return JSValue.CreateFunction(null, (args) =>
            {
                for (int i = 0; i < args.Length; i++)
                {
                    AddRuntimeMessage(errorLevel, (string)args[i].CoerceToString());
                }
                return JSValue.Undefined;
            });
        }

        protected override void AfterSolveInstance()
        {
            m_debugNext = false;
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Menu_AppendItem(menu, "Debug", (ev, arg) =>
            {
                if (Rhino.UI.Dialogs.ShowMessage("Please start a debugger on port 9229. If no debugger is available, this component will hang until one is available. Are you sure you want to debug?", "Debug Component", Rhino.UI.ShowMessageButton.YesNo, Rhino.UI.ShowMessageIcon.Warning) == Rhino.UI.ShowMessageResult.Yes)
                {
                    m_debugNext = true;
                    ExpireSolution(true);
                }
            });
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
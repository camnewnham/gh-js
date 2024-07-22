using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Microsoft.JavaScript.NodeApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace JavascriptForGrasshopper
{
    public partial class JSComponent : GH_Component, IGH_VariableParameterComponent
    {
        public override GH_Exposure Exposure => GH_Exposure.primary;
        public override Guid ComponentGuid => new Guid("440e1113-51b0-46a9-be9b-a7d025e6e312");
        public JSComponent() : base("JavaScript", "JS", "Write and execute JavaScript.", "Maths", "Script") { }

        private bool m_isTypeScript = false;
        public JSComponent(bool typescript) : this()
        {
            NickName = "TS";
            m_isTypeScript = true;
        }

        private static string[] m_keywords = new string[]
        {
            "javascript", "js", "typescript", "ts", "nodejs", "node", "script", "execute", "run", "code", "vs", "vsc"
        };

        public override IEnumerable<string> Keywords => m_keywords;

        public override void CreateAttributes()
        {
            Attributes = new JSComponentAttributes(this);
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Result", "R", "Whatever was returned from the script", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!File.Exists(JSBundlePath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "File not found. Has the module been built yet?");
                return;
            }

            ManualResetEventSlim mre = new ManualResetEventSlim(false);

            Node.Environment.RunAsync(async () =>
            {
                using (new NodeConsole.ConsoleToRuntimeMessage(this))
                {
                    try
                    {
                        JSValue runScript = await Node.Environment.ImportAsync(JSBundlePath, "runScript", true);

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
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A JavaScript exception occurred: " + jsex.Message);
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
            UpdateTypeDefinitions();
        }
    }
}
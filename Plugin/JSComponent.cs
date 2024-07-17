using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
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
        private bool m_debugNext = false;


        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(JSComponent)).Location), "..", "..", "..", "Template", "dist", "index.js");

            if (!File.Exists(path))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "File not found. Has the module been built yet?");
                return;
            }

            string code = File.ReadAllText(path);

            V8ScriptEngine engine;
            if (m_debugNext) // Note: This will hang until a debugger is attached. Use with care.
            {
                engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableTaskPromiseConversion | V8ScriptEngineFlags.EnableDebugging | V8ScriptEngineFlags.EnableRemoteDebugging | V8ScriptEngineFlags.AwaitDebuggerAndPauseOnStart, 9229);
            }
            else
            {
                engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableTaskPromiseConversion);
            }
            engine.DocumentSettings.AccessFlags |= DocumentAccessFlags.EnableFileLoading;

            engine.AddHostObject("console", Microsoft.ClearScript.HostItemFlags.GlobalMembers, new JavascriptConsoleOutput(this));

            PropertyBag inputs = new PropertyBag()
            {
                { "test", 4 }
            };

            PropertyBag outputs = new PropertyBag();

            engine.AddHostObject("inputs", inputs);
            engine.AddHostObject("outputs", outputs);

            try
            {

                object result = engine.Evaluate(new DocumentInfo(new Uri(path))
                {
                    Category = ModuleCategory.Standard,
                }, code);

                if (result is Task task)
                {
                    task.Wait();
                }
                DA.SetData(0, outputs["myOutput"]);

            }
            catch (ScriptEngineException codeEx)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, codeEx.ErrorDetails);
            }
            finally
            {
                engine.Dispose();
                m_debugNext = false;
            }
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
                if (Rhino.UI.Dialogs.ShowMessage("Debugging components requires that a debugger is running and waiting on port 9229. If no debugger is available, this component will hang until one is available. Are you sure you want to continue debugging?", "Debug Component", Rhino.UI.ShowMessageButton.YesNo, Rhino.UI.ShowMessageIcon.Warning) == Rhino.UI.ShowMessageResult.Yes)
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
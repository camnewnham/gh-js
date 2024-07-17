using Grasshopper.Kernel;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using System;
using System.IO;

namespace Plugin
{
    public class JSComponent : GH_Component
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
            pManager.AddTextParameter("File Path", "P", "The file path of the code.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Debug", "D", "Enable debugging.", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Result", "R", "Whatever was returned from the script", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string path = null;
            if (!DA.GetData(0, ref path))
            {
                return;
            }

            if (!File.Exists(path))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "File not found.");
                return;
            }

            bool debug = false;
            DA.GetData(1, ref debug);


            string code = File.ReadAllText(path);

            V8ScriptEngine engine;
            if (debug)
            {
                // Note: This will hang until a debugger is attached. Use with care.
                engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDebugging | V8ScriptEngineFlags.EnableRemoteDebugging | V8ScriptEngineFlags.AwaitDebuggerAndPauseOnStart, 9229);
            }
            else
            {
                engine = new V8ScriptEngine();
            }

            engine.DocumentSettings.AddSystemDocument("main", new StringDocument(new DocumentInfo(new Uri(path)), code));

            try
            {
                object result = engine.EvaluateDocument("main");
                DA.SetData("Result", result);
            }
            catch (ScriptEngineException codeEx)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "An error occurred while executing the javascript bundle. \n" + codeEx.ErrorDetails);
            }
        }
    }
}
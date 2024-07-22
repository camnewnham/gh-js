using Grasshopper.Kernel;
using Microsoft.JavaScript.NodeApi;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace JavascriptForGrasshopper
{
    public partial class JSComponent : GH_Component, IGH_VariableParameterComponent
    {
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        public override Guid ComponentGuid => new Guid("440e1113-51b0-46a9-be9b-a7d025e6e312");
        protected override Bitmap Icon => IsTypescript ? Resources.logo_typescript : Resources.logo_javascript;
        public JSComponent() : base("JavaScript", "JS", "Write and execute JavaScript with NodeJS.", "Maths", "Script") { }

        public bool IsTypescript { get; private set; } = false;

        public JSComponent(bool typescript) : this()
        {
            NickName = "TS";
            Name = "TypeScript";
            Description = Description.Replace("JavaScript", "TypeScript");
            IsTypescript = true;
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
            pManager.AddParameter(((IGH_VariableParameterComponent)this).CreateParameter(GH_ParameterSide.Input, Params.Input.Count));
            pManager.AddParameter(((IGH_VariableParameterComponent)this).CreateParameter(GH_ParameterSide.Input, Params.Input.Count));

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(((IGH_VariableParameterComponent)this).CreateParameter(GH_ParameterSide.Output, Params.Output.Count));
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

                        JSValue inputs = GetInputParameters(DA);
                        JSValue result = runScript.Call(thisArg: default, inputs);

                        if (!runScript.IsFunction())
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unable to find runScript method.");
                            return;
                        };

                        if (result.IsPromise())
                        {
                            result = await ((JSPromise)result).AsTask();
                        }

                        ProcessOutputParameters(DA, result);
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

        /// <summary>
        /// Parses input params and marhsalls to JS
        /// </summary>
        /// <param name="DA">The data access</param>
        /// <returns>The input parameters object</returns>
        private JSValue GetInputParameters(IGH_DataAccess DA)
        {
            JSValue inputs = JSValue.CreateObject();

            for (int i = 0; i < Params.Input.Count; i++)
            {
                JSVariableParam param = Params.Input[i] as JSVariableParam;
                if (param.TryAccessData(DA, i, out JSValue value))
                {
                    inputs.SetProperty(param.VariableName, value);
                }
            }
            return inputs;
        }

        /// <summary>
        /// Parses the output data and marshalls to the DataAccess
        /// </summary>
        /// <param name="DA">The data access</param>
        /// <param name="outputs">The output data</param>
        private void ProcessOutputParameters(IGH_DataAccess DA, JSValue outputs)
        {
            if (!outputs.IsObject() && Params.Output.Count > 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid return type. Expected an object with names matching component outputs.");
                return;
            }

            for (int p = 0; p < Params.Output.Count; p++)
            {
                JSVariableParam param = Params.Output[p] as JSVariableParam;

                JSValue obj = outputs[param.VariableName];

                if (obj.IsUndefined())
                {
                    continue;
                }
                else if (obj.IsNull())
                {
                    DA.SetData(p, null);
                }
                else if (obj.IsArray())
                {
                    List<object> result = new List<object>();
                    for (int i = 0; i < obj.GetArrayLength(); i++)
                    {
                        result.Add(obj[i].GetValueExternalOrPrimitive());
                    }
                    DA.SetDataList(p, result);
                }
                else
                {
                    object val = obj.GetValueExternalOrPrimitive();
                    DA.SetData(p, val);
                }
            }
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
            return JSVariableParam.Create(this, side, index);
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
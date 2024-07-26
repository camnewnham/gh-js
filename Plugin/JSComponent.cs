using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using JavascriptForGrasshopper.CodeGenerator;
using Microsoft.JavaScript.NodeApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace JavascriptForGrasshopper
{
    public partial class JSComponent : GH_Component, IGH_VariableParameterComponent
    {
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        public override Guid ComponentGuid => new Guid("440e1113-51b0-46a9-be9b-a7d025e6e312");

        protected override Bitmap Icon => IsTypescript ? Resources.Icon_TS : Resources.Icon_JS;
        public bool IsTypescript { get; private set; } = false;
        public bool UseOutputParam { get; private set; } = true;
        public JSComponent() : base("JavaScript", "JS", "Write and execute JavaScript with NodeJS.", "Maths", "Script") { }
        public JSComponent(bool typescript) : this()
        {
            if (typescript)
            {
                NickName = "TS";
                Name = "TypeScript";
                Description = Description.Replace("JavaScript", "TypeScript");
                IsTypescript = true;
            }
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
            if (UseOutputParam)
            {
                AddOutParam();
            }
            pManager.AddParameter(((IGH_VariableParameterComponent)this).CreateParameter(GH_ParameterSide.Output, Params.Output.Count));
        }

        /// <summary>
        /// Adds the "out" parameter for console.log messages
        /// </summary>
        private void AddOutParam()
        {
            Debug.Assert(UseOutputParam == true, $"Can not add output parameter when {nameof(UseOutputParam)} is false.");
            Params.RegisterOutputParam(new Param_String()
            {
                Name = "Out",
                NickName = "out",
                Description = "Output from console.log() and related commands. \"info\", \"warn\" and \"error\" are also output through the message balloon.",
                Access = GH_ParamAccess.list,
                MutableNickName = false,
            }, 0);
            Params.OnParametersChanged();
        }

        /// <summary>
        /// Removes the "out" parameter for console.log messages
        /// </summary>
        private void RemoveOutParam()
        {
            Debug.Assert(UseOutputParam == false, $"Can not remove output parameter when {nameof(UseOutputParam)} is false.");
            Params.UnregisterOutputParameter(Params.Output[0]);
            Params.OnParametersChanged();
        }

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();
            if (!ValidateParams(out string reason))
            {
                throw new InvalidDataException(reason);
            }
        }

        /// <summary>
        /// Validates parameter inputs and outputs for syntax and uniqueness.
        /// </summary>
        /// <returns>True if the parameters are all valid.</returns>
        public bool ValidateParams(out string reason)
        {
            HashSet<string> inputVariables = new HashSet<string>();
            HashSet<string> outputVariables = new HashSet<string>();
            foreach (JSVariableParam param in Params.Input.Concat(Params.Output.Where(p => p is JSVariableParam)))
            {
                if (!TypescriptSupport.IsValidVariableName(param.VariableName))
                {
                    reason = $"\"{param.VariableName}\" is not a valid variable name";
                    return false;
                }
                if ((param.Kind == GH_ParamKind.input && !inputVariables.Add(param.VariableName)) ||
                    (param.Kind == GH_ParamKind.output && !outputVariables.Add(param.VariableName)))
                {
                    reason = $"Variable \"{param.VariableName}\" is already in use.";
                    return false;
                }
            }
            reason = null;
            return true;
        }

        /// <summary>
        /// Utility for adding runtime messages from nodejs
        /// </summary>
        /// <param name="level">The console message level</param>
        /// <param name="msgs">A list of messages if console.log(a,b,c,d) was used.</param>
        internal void AddRuntimeMessage(NodeConsole.MessageLevel level, string[] msgs)
        {
            GH_RuntimeMessageLevel msgLevel = level switch
            {
                NodeConsole.MessageLevel.Warning => GH_RuntimeMessageLevel.Warning,
                NodeConsole.MessageLevel.Error => GH_RuntimeMessageLevel.Error,
                _ => GH_RuntimeMessageLevel.Remark,
            };
            foreach (string str in msgs)
            {
                AddRuntimeMessage(msgLevel, str);
            }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Debug.Assert(JSBundlePath != null, "Bundle path was not set");
            Debug.Assert(File.Exists(JSBundlePath), "Bundle file not found.");

            ManualResetEventSlim mre = new ManualResetEventSlim(false);

            Node.Environment.RunAsync(async () =>
            {
                List<string> consoleOutput = new List<string>();
                using (new NodeConsole.ConsoleToRuntimeMessage((level, msgs) =>
                {
                    consoleOutput.AddRange(msgs);
                    if (level > NodeConsole.MessageLevel.Debug)
                    {
                        AddRuntimeMessage(level, msgs);
                    }
                }))
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

                        if (UseOutputParam && consoleOutput.Count > 0)
                        {
                            DA.SetDataList(0, consoleOutput);
                        }
                    }
                    catch (JSException jsex)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, jsex.Message ?? "A javascript error occurred.");
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

            for (int p = UseOutputParam ? 1 : 0; p < Params.Output.Count; p++)
            {
                JSVariableParam param = Params.Output[p] as JSVariableParam;

                JSValue obj = outputs[param.VariableName];

                if (obj.IsUndefined())
                {
                    DA.SetDataList(p, new List<object>());
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

            Menu_AppendItem(menu, "Launch Code Editor", (obj, arg) =>
            {
                LaunchCodeEditor();
            });

            Menu_AppendItem(menu, "Standard Output/Error Parameter (\"out\")", (obj, arg) =>
            {
                UseOutputParam = !UseOutputParam;
                if (!UseOutputParam)
                {
                    RemoveOutParam();
                }
                else
                {
                    AddOutParam();
                }
                Attributes.ExpireLayout();
                Instances.RedrawCanvas();

            }, true, UseOutputParam);


            Menu_AppendItem(menu, $"Enable Debugger (port {Node.DEBUGGER_PORT})", (ev, arg) =>
            {
                Node.DebuggerEnabled = !Node.DebuggerEnabled;
            }, true, Node.DebuggerEnabled);

        }

        public void LaunchCodeEditor()
        {
            string srcFolder = GetOrCreateSourceCode();
            string entryPoint = Path.Combine(srcFolder, IsTypescript ? "index.ts" : "index.js");
            Utils.LaunchCodeEditor(entryPoint);
        }

        bool IGH_VariableParameterComponent.CanInsertParameter(GH_ParameterSide side, int index)
        {
            // "out" param always comes first.
            if (side == GH_ParameterSide.Output && index == 0 && UseOutputParam)
            {
                return false;
            }

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
            if (side == GH_ParameterSide.Output && index == 0 && UseOutputParam)
            {
                UseOutputParam = false;
            }
            return true;
        }

        void IGH_VariableParameterComponent.VariableParameterMaintenance()
        {
            UpdateTypeDefinitions();
        }
    }
}
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Microsoft.JavaScript.NodeApi;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace JavascriptForGrasshopper
{
    public class JSVariableParam : Param_GenericObject
    {
        public string JavascriptType => Access == GH_ParamAccess.item ? "any" : "any[]";
        public string VariableName => NickName;
        public string ToolTip => Description;
        public string PrettyName => Name ?? NickName;

        public JSComponent Owner => (Attributes.Parent as JSComponentAttributes).Owner as JSComponent;

        public JSVariableParam(string name, string nickname, string description) : base()
        {
            Name = name;
            NickName = nickname;
            Description = description;
        }

        public static JSVariableParam Create(JSComponent component, GH_ParameterSide side, int index)
        {
            return new JSVariableParam(
                side == GH_ParameterSide.Input ? "Input" : "Output",
                GH_ComponentParamServer.InventUniqueNickname(side == GH_ParameterSide.Input ? "abcdefghijklmn" : "xyzuvwst", side == GH_ParameterSide.Input ? component.Params.Input : component.Params.Output),
                side == GH_ParameterSide.Input ? "Input Parameter" : "Output Parameter"
                )
            {
                Optional = true
            };
        }

        public Templating.TypeDefinition GetTypeDefinition()
        {
            return new Templating.TypeDefinition()
            {
                VariableName = NickName,
                Name = PrettyName,
                Description = ToolTip,
                Type = JavascriptType,
                Optional = true
            };
        }

        public bool TryAccessData(IGH_DataAccess DA, int paramIndex, out JSValue value)
        {
            if (Access == GH_ParamAccess.item)
            {
                IGH_Goo obj = null;

                if (!DA.GetData(paramIndex, ref obj))
                {
                    value = default;
                    return false;
                }

                object scriptVar = obj.ScriptVariable();
                value = Converter.JSValueFromObject(scriptVar);
            }
            else if (Access == GH_ParamAccess.list)
            {
                value = JSValue.CreateArray();
                List<IGH_Goo> goos = new List<IGH_Goo>();
                if (!DA.GetDataList(paramIndex, goos))
                {
                    return false;
                }
                foreach (IGH_Goo itm in goos)
                {
                    value.Items.Add(Converter.JSValueFromObject(itm.ScriptVariable()));
                }
            }
            else
            {
                // TODO: Should we support tree access via nested arrays?
                throw new InvalidOperationException("Unable to process tree structure for script input.");
            }

            return true;
        }

        public override bool AppendMenuItems(ToolStripDropDown menu)
        {
            base.AppendMenuItems(menu);

            Menu_AppendSeparator(menu);

            if (Kind == GH_ParamKind.input)
            {
                Menu_AppendItem(menu, "Item Access", (obj, arg) =>
                {
                    Access = GH_ParamAccess.item;
                    Owner?.ExpireTypeDefinitions();
                }, Access != GH_ParamAccess.item, Access == GH_ParamAccess.item);

                Menu_AppendItem(menu, "List Access", (obj, arg) =>
                {
                    Access = GH_ParamAccess.list;
                    Owner?.ExpireTypeDefinitions();
                }, Access != GH_ParamAccess.list, Access == GH_ParamAccess.list);
            }
            return true;
        }
    }
}
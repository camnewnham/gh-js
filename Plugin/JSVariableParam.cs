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
        public string VariableName => NickName;
        public string ToolTip => Description;
        public string PrettyName => Name ?? NickName;

        public override GH_Exposure Exposure => GH_Exposure.hidden;

        public override Guid ComponentGuid => new Guid("{ED4AA333-216C-4AE5-BCAC-43CD4FC152B7}");

        public JSComponent Owner => (Attributes.Parent as JSComponentAttributes).Owner as JSComponent;

        public JSVariableParam() : base() { }

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
            string tsType = TypeHint.ToString().ToLower() + (Access == GH_ParamAccess.item ? "" : "[]");
            return new Templating.TypeDefinition()
            {
                VariableName = NickName,
                Name = PrettyName,
                Description = ToolTip,
                Type = tsType,
                Optional = Optional
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


            if (Kind == GH_ParamKind.input)
            {
                Menu_AppendSeparator(menu);

                Menu_AppendItem(menu, "Item Access", (obj, arg) =>
                {
                    if (Access == GH_ParamAccess.item)
                    {
                        return;
                    }

                    Access = GH_ParamAccess.item;
                    Owner?.ExpireTypeDefinitions();
                    Owner?.ExpireSolution(true);
                }, true, Access == GH_ParamAccess.item);

                Menu_AppendItem(menu, "List Access", (obj, arg) =>
                {
                    if (Access == GH_ParamAccess.list)
                    {
                        return;
                    }

                    Access = GH_ParamAccess.list;
                    Owner?.ExpireTypeDefinitions();
                    Owner?.ExpireSolution(true);
                }, true, Access == GH_ParamAccess.list);
            }

            Menu_AppendSeparator(menu);

            Menu_AppendItem(menu, "Optional", (obj, arg) =>
            {
                Optional = !Optional;
                Owner?.ExpireTypeDefinitions();
                ExpireSolution(true);
            }, true, Optional);


            if (Owner?.IsTypescript ?? false)
            {
                ToolStripMenuItem typeHintMenu = Menu_AppendItem(menu, "Type Hint");

                foreach (JSTypeHint hint in Enum.GetValues(typeof(JSTypeHint)))
                {
                    ToolStripMenuItem item = new ToolStripMenuItem(hint.ToString().ToLower())
                    {
                        Tag = hint,
                        Checked = TypeHint == hint,
                    };
                    item.Click += (obj, arg) =>
                    {
                        if (TypeHint == hint)
                        {
                            return;
                        }

                        TypeHint = (JSTypeHint)(obj as ToolStripMenuItem).Tag;
                        Owner?.ExpireTypeDefinitions();
                    };
                    typeHintMenu.DropDownItems.Add(item);
                }
            }

            return true;
        }

        /// <summary>
        /// Provides a type hint for TypeScript
        /// </summary>
        public JSTypeHint TypeHint { get; private set; } = JSTypeHint.Any;

        public enum JSTypeHint
        {
            Any,
            Number,
            String,
            Boolean
        }
    }
}
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Microsoft.JavaScript.NodeApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace JavascriptForGrasshopper
{
    public class JSVariableParam : Param_GenericObject
    {
        public string VariableName
        {
            get => NickName;
            private set => NickName = value;
        }
        public string ToolTip
        {
            get => Description;
            set => Description = value;
        }

        public string PrettyName
        {
            get => Name;
            set => Name = value;
        }

        /// <summary>
        /// A name the user may have assigned that is different to the variable name.
        /// </summary>
        private string m_prettyName;

        public override string Name
        {
            get => string.IsNullOrEmpty(m_prettyName) ? VariableName : m_prettyName;
            set => m_prettyName = value;
        }

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
                null,
                GH_ComponentParamServer.InventUniqueNickname(side == GH_ParameterSide.Input ? "abcdefghijklmn" : "xyzuvwst", side == GH_ParameterSide.Input ? component.Params.Input : component.Params.Output), side == GH_ParameterSide.Input ? "Input script variable" : "Output script variable"
                )
            {
                Optional = true
            };
        }

        public CodeGenerator.TypeDefinition GetTypeDefinition()
        {
            string typeName = TypeHint;
            if (typeName.StartsWith("Rhino"))
            {
                typeName = @"InstanceType<typeof dotnet." + typeName + ">";
            }
            if (Access == GH_ParamAccess.list)
            {
                typeName += "[]";
            }
            else if (Access == GH_ParamAccess.tree)
            {
                typeName = @"InstanceType<typeof dotnet.Grasshopper.DataTree$1<" + typeName + ">>";
            }

            return new CodeGenerator.TypeDefinition()
            {
                VariableName = NickName,
                Name = PrettyName,
                Description = ToolTip?.Replace("*/", ""),
                Type = typeName,
                Optional = Optional
            };
        }

        public bool TryAccessData(IGH_DataAccess DA, int paramIndex, out JSValue value)
        {
            switch (Access)
            {
                case GH_ParamAccess.item:
                    IGH_Goo obj = null;

                    if (!DA.GetData(paramIndex, ref obj))
                    {
                        value = default;
                        return false;
                    }

                    value = Converter.ToJS(obj.ScriptVariable());
                    return true;
                case GH_ParamAccess.list:
                    value = JSValue.CreateArray();
                    List<IGH_Goo> goos = new List<IGH_Goo>();
                    if (!DA.GetDataList(paramIndex, goos))
                    {
                        return false;
                    }
                    foreach (IGH_Goo itm in goos)
                    {
                        value.Items.Add(Converter.ToJS(itm.ScriptVariable()));
                    }
                    return true;
                case GH_ParamAccess.tree:
                    if (!DA.GetDataTree(paramIndex, out GH_Structure<IGH_Goo> structure))
                    {
                        value = default;
                        return false;
                    }
                    var dt = new DataTree<object>();
                    foreach (var p in structure.Paths)
                    {
                        dt.EnsurePath(p);
                        dt.AddRange((structure.get_Branch(p) as IEnumerable<IGH_Goo>).Select(x => x.ScriptVariable()), p);
                    }
                    value = Converter.ToJS(dt);
                    return true;
                default:
                    throw new NotSupportedException("Unsupported param access type: " + Access);
            }
        }

        private static ToolStripTextBox Menu_AppendTextField(ToolStripDropDown menu, string text, Action<string> textChanged, bool closeParentOnEnter = true)
        {
            ToolStripTextBox textBox = new ToolStripTextBox()
            {
                Text = text,
                AutoSize = true
            };
            textBox.TextChanged += (obj, arg) => textChanged(textBox.Text);
            textBox.KeyDown += (obj, arg) =>
            {
                if (arg.KeyCode == Keys.Enter && closeParentOnEnter)
                {
                    menu.Close();
                }
            };
            menu.Items.Add(textBox);
            textBox.TextBox.MinimumSize = new System.Drawing.Size(200, 0);
            return textBox;
        }

        public override bool AppendMenuItems(ToolStripDropDown menu)
        {
            bool compilationDataChanged = false;
            bool typeMetadataChanged = false;

            // Note: Actions that close the menu (ie. clicking a toggle) should call this directly.
            // For text fields, this will be handled when the menu closes.
            void HandleChanges()
            {
                if (typeMetadataChanged)
                {
                    Owner?.OnTypeMetadataChanged(this);
                    typeMetadataChanged = false;
                }
                if (compilationDataChanged)
                {
                    Owner?.OnParameterDataChanged(this);
                    compilationDataChanged = false;
                }

            }

            menu.Closed += (obj, arg) =>
            {
                HandleChanges();
            };

            ToolStripTextBox variableNameBox = Menu_AppendTextField(menu, VariableName, text =>
            {
                compilationDataChanged = true;
                typeMetadataChanged = true;
                VariableName = text;
                Owner?.Attributes.ExpireLayout();
                Instances.RedrawCanvas();
            });

            variableNameBox.BorderStyle = BorderStyle.FixedSingle;

            ToolStripTextBox nameBox = Menu_AppendTextItem(Menu_AppendItem(menu, "Name (for humans, optional):").DropDown, PrettyName, null, (obj, arg) =>
            {
                PrettyName = obj.Text;
                typeMetadataChanged = true;
                Owner?.Attributes.ExpireLayout();
                Instances.RedrawCanvas();
            }, true, -1, true);

            ToolStripTextBox tipBox = Menu_AppendTextItem(Menu_AppendItem(menu, "Tooltip (optional):").DropDown, ToolTip, null, (obj, arg) =>
            {
                ToolTip = obj.Text;
                typeMetadataChanged = true;
            }, true, -1, true);
            tipBox.ToolTipText = "This is .Description property of input parameter";
            tipBox.TextBox.AcceptsReturn = true;
            tipBox.TextBox.Multiline = true;
            tipBox.AutoSize = false;
            tipBox.Height = 100;

            Menu_AppendSeparator(menu);

            if (Kind == GH_ParamKind.output)
            {
                Menu_AppendPreviewItem(menu);
            }
            Menu_AppendBakeItem(menu);
            Menu_AppendRuntimeMessages(menu);
            AppendAdditionalMenuItems(menu);


            if (Kind == GH_ParamKind.input || Owner.IsTypescript)
            {
                Menu_AppendSeparator(menu);

                foreach (var accessType in Enum.GetValues(typeof(GH_ParamAccess)).Cast<GH_ParamAccess>())
                {
                    string name = accessType.ToString();
                    name = Char.ToUpperInvariant(name[0]) + name.Substring(1) + " Access";
                    Menu_AppendItem(menu, name, (obj, arg) =>
                    {
                        if (Access != accessType)
                        {
                            Access = accessType;
                            typeMetadataChanged = true;
                            compilationDataChanged = true;
                            HandleChanges();
                        }
                    }, true, Access == accessType);
                }
            }

            Menu_AppendSeparator(menu);

            Menu_AppendItem(menu, "Optional", (obj, arg) =>
            {
                Optional = !Optional;
                typeMetadataChanged = true;
                HandleChanges();
            }, true, Optional);


            if (Owner?.IsTypescript ?? false)
            {
                Menu_AppendTypeHint(menu, (change) =>
                {
                    TypeHint = change;
                    typeMetadataChanged = true;
                    HandleChanges();
                });
            }

            GH_DocumentObject.Menu_AppendSeparator(menu);
            Menu_AppendObjectHelp(menu);

            return true;
        }

        private void Menu_AppendTypeHint(ToolStripDropDown menu, Action<string> onChange)
        {
            ToolStripMenuItem typeHintMenu = Menu_AppendItem(menu, "Type Hint");

            Menu_AppendItem(typeHintMenu.DropDown, "any", (obj, arg) => onChange("any"), true, TypeHint == "any");
            Menu_AppendItem(typeHintMenu.DropDown, "boolean", (obj, arg) => onChange("boolean"), true, TypeHint == "boolean");
            Menu_AppendItem(typeHintMenu.DropDown, "number", (obj, arg) => onChange("number"), true, TypeHint == "number");
            Menu_AppendItem(typeHintMenu.DropDown, "string", (obj, arg) => onChange("string"), true, TypeHint == "string");
            Menu_AppendItem(typeHintMenu.DropDown, "Date", (obj, arg) => onChange("Date"), true, TypeHint == "Date");


            void AppendSection(params Type[] types)
            {
                Menu_AppendSeparator(typeHintMenu.DropDown);

                foreach (Type type in types)
                {
                    Menu_AppendItem(typeHintMenu.DropDown, type.Name, (obj, arg) => onChange(type.FullName), true, TypeHint == type.FullName);
                }
            }

            AppendSection(typeof(Rhino.Geometry.Point3d), typeof(Rhino.Geometry.Vector3d), typeof(Rhino.Geometry.Plane), typeof(Rhino.Geometry.Interval));
            AppendSection(typeof(Rhino.Geometry.Box), typeof(Rhino.Geometry.Transform));
            AppendSection(typeof(Rhino.Geometry.Line), typeof(Rhino.Geometry.Circle), typeof(Rhino.Geometry.Arc), typeof(Rhino.Geometry.Curve), typeof(Rhino.Geometry.Polyline), typeof(Rhino.Geometry.Rectangle3d));
            AppendSection(typeof(Rhino.Geometry.Mesh), typeof(Rhino.Geometry.Surface), typeof(Rhino.Geometry.Extrusion),
#if NET7_0_OR_GREATER
                typeof(Rhino.Geometry.SubD), 
#endif
                typeof(Rhino.Geometry.Brep), typeof(Rhino.Geometry.PointCloud), typeof(Rhino.Geometry.GeometryBase));
        }


        protected override void Menu_AppendExtractParameter(ToolStripDropDown menu)
        {
            return;
        }

        /// <summary>
        /// Provides a type hint for TypeScript
        /// </summary>
        public string TypeHint { get; private set; } = "any";

        public override bool Write(GH_IWriter writer)
        {
            if (!string.IsNullOrEmpty(m_prettyName))
            {
                writer.SetString("pretty_name", m_prettyName);
            }
            writer.SetString("type_hint", TypeHint);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            reader.TryGetString("pretty_name", ref m_prettyName);
            TypeHint = reader.GetString("type_hint");
            return base.Read(reader);
        }
    }
}
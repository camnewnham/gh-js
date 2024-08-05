using GH_IO.Serialization;
using Grasshopper;
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
            string tsType = TypeHint.ToString().ToLower() + (Access == GH_ParamAccess.item ? "" : "[]");
            return new CodeGenerator.TypeDefinition()
            {
                VariableName = NickName,
                Name = PrettyName,
                Description = ToolTip?.Replace("*/", ""),
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

                value = Converter.ToJS(obj.ScriptVariable());
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
                    value.Items.Add(Converter.ToJS(itm.ScriptVariable()));
                }
            }
            else
            {
                // TODO: Should we support tree access via nested arrays?
                throw new InvalidOperationException("Unable to process tree structure for script input.");
            }

            return true;
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


            if (Kind == GH_ParamKind.input)
            {
                Menu_AppendSeparator(menu);

                Menu_AppendItem(menu, "Item Access", (obj, arg) =>
                {
                    if (Access == GH_ParamAccess.item)
                    {
                        Access = GH_ParamAccess.item;
                        typeMetadataChanged = true;
                        compilationDataChanged = true;
                        HandleChanges();
                    }
                }, true, Access == GH_ParamAccess.item);

                Menu_AppendItem(menu, "List Access", (obj, arg) =>
                {
                    if (Access != GH_ParamAccess.list)
                    {
                        Access = GH_ParamAccess.list;
                        typeMetadataChanged = true;
                        compilationDataChanged = true;
                        HandleChanges();
                    }
                }, true, Access == GH_ParamAccess.list);
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
                        typeMetadataChanged = true;
                        HandleChanges();
                    };
                    typeHintMenu.DropDownItems.Add(item);
                }
            }

            GH_DocumentObject.Menu_AppendSeparator(menu);
            Menu_AppendObjectHelp(menu);

            return true;
        }

        protected override void Menu_AppendExtractParameter(ToolStripDropDown menu)
        {
            return;
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

        public override bool Write(GH_IWriter writer)
        {
            if (!string.IsNullOrEmpty(m_prettyName))
            {
                writer.SetString("pretty_name", m_prettyName);
            }
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            reader.TryGetString("pretty_name", ref m_prettyName);
            return base.Read(reader);
        }
    }
}
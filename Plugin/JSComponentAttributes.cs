using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using System.Diagnostics;

namespace JavascriptForGrasshopper
{
    /// <summary>
    /// Simple attributes that enables double-clicking to edit the source code of <see cref="JSComponent"/>
    /// </summary>
    internal class JSComponentAttributes : GH_ComponentAttributes
    {
        public JSComponentAttributes(IGH_Component component) : base(component)
        {
            Debug.Assert(component is JSComponent, $"Cannot create {nameof(JSComponentAttributes)} for {component?.GetType().FullName})");
        }

        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (ContentBox.Contains(e.CanvasLocation))
            {
                JSComponent component = Owner as JSComponent;
                if (component != null)
                {
                    component.LaunchCodeEditor();
                    return GH_ObjectResponse.Handled;
                }
            }
            return base.RespondToMouseDoubleClick(sender, e);
        }
    }
}

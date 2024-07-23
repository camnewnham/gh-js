using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace JavascriptForGrasshopper
{
    /// <summary>
    /// Shim component to allow having two options in the menu for each scripting language.
    /// On creation, this component just replaces itself with the correct one.
    /// </summary>
    public class TSComponent : GH_Component
    {
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        public override Guid ComponentGuid => new Guid("A7887C6C-BC3D-45CD-90FA-609190F00DAA");

        protected override Bitmap Icon => Resources.logo_typescript;

        public TSComponent() : base("TypeScript", "TS", "Write and execute typescript with NodeJS.", "Maths", "Script") { }


        private static string[] m_keywords = new string[]
        {
            "typescript", "ts", "javascript", "js", "nodejs", "node", "script", "execute", "run", "code", "vs", "vsc"
        };

        public override IEnumerable<string> Keywords => m_keywords;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) { }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) { }

        protected override void SolveInstance(IGH_DataAccess DA) { }

        public override void AddedToDocument(GH_Document document)
        {
            Grasshopper.Kernel.Undo.GH_UndoServer ds = document.UndoServer;
            JSComponent jsComponent = new JSComponent(true);
            jsComponent.Attributes.Pivot = Attributes.Pivot;
            document.RemoveObject(this, false);

            document.AddObject(jsComponent, false);
            document.UndoServer.PushUndoRecord(document.UndoUtil.CreateAddObjectEvent($"Swap {jsComponent.Name}", jsComponent));

            // Repair the undo record
            Task.Run(() => {
                Rhino.RhinoApp.InvokeOnUiThread((Action) (() => {
                    if (document.UndoServer.FirstUndoName == $"Add {Name}")
                    {
                        document.UndoServer.RemoveRecord(document.UndoServer.UndoGuids[0]);
                    }
                }));
            });
        }
    }
}
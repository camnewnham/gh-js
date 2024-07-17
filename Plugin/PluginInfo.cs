using Grasshopper.Kernel;
using System;

namespace Plugin
{
    public class PluginInfo : GH_AssemblyInfo
    {
        public override string Name => "Javascript";
        //public override Bitmap Icon => null;
        public override string Description => "Write Javascript or Typescript components in Grasshopper";
        public override Guid Id => new Guid("7079138d-34f4-493c-bc8d-fb29f41dcb11");
        public override string AuthorName => "Cameron Newnham";
        public override string AuthorContact => "https://github.com/camnewnham";
    }
}
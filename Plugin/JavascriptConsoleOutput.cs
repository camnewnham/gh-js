using Grasshopper.Kernel;

namespace Plugin
{
    /// <summary>
    /// Utility for allowing console.info etc. from javascript
    /// </summary>
    public class JavascriptConsoleOutput
    {
        private IGH_Component m_component;

        public JavascriptConsoleOutput(IGH_Component component)
        {
            m_component = component;
        }

        public void info(params object[] args)
        {
            m_component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, string.Join("\r\n", args));
        }

        public void log(params object[] args)
        {
            m_component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, string.Join("\r\n", args));
        }

        public void warn(params object[] args)
        {
            m_component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, string.Join("\r\n", args));
        }

        public void error(params object[] args)
        {
            m_component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, string.Join("\r\n", args));
        }
    }
}

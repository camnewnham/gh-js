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

        public void info(string message)
        {
            m_component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, message);
            System.Diagnostics.Debug.WriteLine(message);
        }

        public void log(string message)
        {
            m_component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, message);
            System.Diagnostics.Debug.WriteLine(message);
        }

        public void warn(string message)
        {
            m_component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, message);
            System.Diagnostics.Debug.WriteLine(message);
        }

        public void error(string message)
        {
            m_component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
            System.Diagnostics.Debug.WriteLine(message);
        }
    }
}

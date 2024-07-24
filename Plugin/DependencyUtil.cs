using System;
using System.Diagnostics;

namespace JavascriptForGrasshopper
{
    /// <summary>
    /// Utilities for checking dependencies and prompting the user to install them.
    /// </summary>
    internal static class DependencyUtil
    {

        /// <summary>
        /// Once per session, check if node, npm and visual studio are installed
        /// Returns true if we have the dependencies or the check was skipped.
        /// </summary>
        internal static bool ValidateDependencyInstallation()
        {
            return ValidateNpmInstallation() && ValidateVSCodeInstallation();
        }

        /// <summary>
        /// Flags if we should skip checking for visual studio code, either because it has succeeded
        /// or because the user wishes to ignore it.
        /// </summary>
        private static bool skipNodeCheck = false;

        /// <summary>
        /// Validates that npm is installed
        /// </summary>
        /// <returns>True if it is installed or the user has opted to ignore the prompt.</returns>
        private static bool ValidateNpmInstallation()
        {
            if (skipNodeCheck || IsNpmInstalled())
            {
                return skipNodeCheck = true;
            }

            Rhino.UI.ShowMessageResult prompt = Rhino.UI.Dialogs.ShowMessage(
                    "Unable to find npm installation. Node and npm are required to create javascript bundles. Would you like to download this now?",
                    "Missing Dependency",
                    Rhino.UI.ShowMessageButton.YesNoCancel,
                    Rhino.UI.ShowMessageIcon.Warning);

            switch (prompt)
            {
                case Rhino.UI.ShowMessageResult.No:
                    return skipNodeCheck = true;
                case Rhino.UI.ShowMessageResult.Cancel:
                    return false;
                case Rhino.UI.ShowMessageResult.Yes:
                    Process.Start(new ProcessStartInfo()
                    {
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = "https://nodejs.org/en/download"
                    });
                    return false;
                default:
                    throw new Exception("Unexpected return from prompt: " + prompt);
            }
        }


        /// <summary>
        /// Flags if we should skip checking for npm, either because it has succeeded
        /// or because the user wishes to ignore it.
        /// </summary>
        private static bool skipVSCheck = false;

        /// <summary>
        /// Validates that visual studio code is installed
        /// </summary>
        /// <returns>True if it is installed or the user has opted to ignore the prompt.</returns>
        private static bool ValidateVSCodeInstallation()
        {
            if (skipVSCheck || IsVisualStudioCodeInstalled())
            {
                return skipVSCheck = true;
            }

            Rhino.UI.ShowMessageResult prompt = Rhino.UI.Dialogs.ShowMessage(
                    "Unable to find Visual Studio Code installation. You may need to add it to your PATH. Other script editors can also be used. Would you like to download Visual Studio Code now?",
                    "Unable to find editor",
                    Rhino.UI.ShowMessageButton.YesNoCancel,
                    Rhino.UI.ShowMessageIcon.Warning);

            switch (prompt)
            {
                case Rhino.UI.ShowMessageResult.No:
                    return skipVSCheck = true;
                case Rhino.UI.ShowMessageResult.Cancel:
                    return false;
                case Rhino.UI.ShowMessageResult.Yes:
                    Process.Start(new ProcessStartInfo()
                    {
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = "https://code.visualstudio.com/download"
                    });
                    return false;
                default:
                    throw new Exception("Unexpected return from prompt: " + prompt);
            }
        }


        /// <summary>
        /// Checks if npm is found on the users' PATH. 
        /// Note: System npm is used for bundling, but not execution.
        /// </summary>
        /// <returns>True if node is installed on the system.</returns>
        public static bool IsNpmInstalled()
        {
            try
            {
                Process proc = Process.Start(new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "npx",
                    Arguments = "--version"
                })!;
                proc.WaitForExit();
                return proc.ExitCode == 0;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Unable to start npx for version check: " + e.Message);
                return false;
            }
        }


        /// <summary>
        /// Checks if visual studio code ("code") is found on the users' PATH. 
        /// Note: Visual studio code is used for editing, but not execution.
        /// </summary>
        /// <returns>True if the code command executes.</returns>
        public static bool IsVisualStudioCodeInstalled()
        {
            try
            {
                Process proc = Process.Start(new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "code",
                    Arguments = "--version"
                })!;
                proc.WaitForExit();
                return proc.ExitCode == 0;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Unable to start code for version check: " + e.Message);
                return false;
            }
        }
    }
}

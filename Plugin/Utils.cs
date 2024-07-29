using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace JavascriptForGrasshopper
{
    /// <summary>
    /// Utilities for checking dependencies and prompting the user to install them.
    /// </summary>
    internal static class Utils
    {
        /// <summary>
        /// Creates a zip file of a folder
        /// </summary>
        /// <param name="folder">The folder to zip</param>
        /// <returns></returns>
        public static byte[] ZipFolder(string folder, string[] ignoreFolderNames)
        {
            // Create a temp folder to copy the source to before creating a zip.
            // This is so we can exclude node_modules etc. from the zip
            string tmpDir = Path.Combine(JSComponent.WorkingDir, "Temp");
            Directory.CreateDirectory(tmpDir);
            CopyDirectoryRecursive(folder, tmpDir, folder => !ignoreFolderNames.Contains(Path.GetFileName(folder)));
            string zipFile = Path.Combine(JSComponent.WorkingDir, "tmp_source.zip");
            ZipFile.CreateFromDirectory(tmpDir, zipFile);

            byte[] zip;
            using (FileStream fs = File.OpenRead(zipFile))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    fs.CopyTo(ms);
                    zip = ms.ToArray();
                }
            };
            File.Delete(zipFile);
            Directory.Delete(tmpDir, true);
            return zip;
        }

        /// <summary>
        /// Extracts a zip byte array to a destination
        /// </summary>
        /// <param name="zip">The zip file</param>
        /// <param name="destination">The destination to extract to</param>
        public static void UnzipFolder(byte[] zip, string destination)
        {
            string tmpZipFile = Path.Combine(JSComponent.WorkingDir, "tmp_source_restore.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(tmpZipFile));
            File.WriteAllBytes(tmpZipFile, zip);
            Directory.CreateDirectory(destination);
            ZipFile.ExtractToDirectory(tmpZipFile, destination);
            File.Delete(tmpZipFile);
        }

        /// <summary>
        /// Copies a directory recursively with a filter to exclude certain folders
        /// </summary>
        /// <param name="sourceDir">The source to copy</param>
        /// <param name="destinationDir">The destination</param>
        /// <param name="folderNameFilter">A function which returns true if a folder should be included. If null, all folders are included</param>
        public static void CopyDirectoryRecursive(string sourceDir, string destinationDir, Func<string, bool> folderNameFilter = null)
        {
            if (folderNameFilter != null && !folderNameFilter(sourceDir))
            {
                return;
            }

            DirectoryInfo dir = new DirectoryInfo(sourceDir);

            DirectoryInfo[] dirs = dir.GetDirectories();

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectoryRecursive(subDir.FullName, newDestinationDir, folderNameFilter);
            }
        }

        /// <summary>
        /// Attempts to launch a code editor.  
        /// If it fails, just open file.
        /// </summary>
        public static void LaunchCodeEditor(string entryPoint)
        {
            Debug.Assert(File.Exists(entryPoint), "Entry point did not exist. Unable to launch code editor.");

            bool launchedEditor = false;
            try
            {
                // Attempt to launch visual studio code
#if DEBUG
                string prefix = "";
#else
                string prefix = "--reuse-window ";
#endif
                Process launchCodeProcess = Process.Start(new ProcessStartInfo()
                {
                    FileName = "code",
                    Arguments = $"{prefix}\"{Path.GetDirectoryName(entryPoint)}\" \"{entryPoint}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                launchCodeProcess.WaitForExit();
                launchedEditor = launchCodeProcess.ExitCode == 0;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to launch code editor due to exception: " + e.Message);
            }

            if (!launchedEditor)
            {
                // If code fails to open, just open the file however the system wants to.
                Process.Start(new ProcessStartInfo()
                {
                    FileName = entryPoint,
                    UseShellExecute = true
                });
            }
        }

        /// <summary>
        /// Attempts to run npm install, in case the user is using npm packages.
        /// </summary>
        /// <param name="workingDir">The working directory to run in.</param>
        /// <returns>True if npm ran and returned exit code 0</returns>
        internal static bool RunNpmInstall(string workingDir)
        {
            try
            {
                Process process = Process.Start(new ProcessStartInfo()
                {
                    WorkingDirectory = workingDir,
                    UseShellExecute = true,
                    FileName = "npm",
                    Arguments = "install",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });

                process.WaitForExit();
                Debug.WriteLine($"npm install exit with code {process.ExitCode}");
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to run npm install: " + ex.Message);
                return false;
            }

        }
    }
}

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Installer;

public class ShortcutHelper
{
    
    /// <summary>
        /// Creates a shortcut file (.lnk) using WshShell.
        /// </summary>
        /// <param name="shortcutPath">The full path where the shortcut will be created (e.g., "C:\Path\To\MyShortcut.lnk").</param>
        /// <param name="targetPath">The full path to the target executable or file.</param>
        /// <param name="arguments">Optional command-line arguments for the target.</param>
        /// <param name="workingDirectory">Optional working directory for the target.</param>
        /// <param name="description">Optional description for the shortcut.</param>
        /// <param name="iconLocation">Optional path to an icon file (e.g., "C:\Path\To\Icon.ico,0" or "C:\Path\To\App.exe,0").</param>
        /// <param name="windowStyle">Optional window style for the shortcut (1 = Normal, 3 = Maximized, 7 = Minimized).</param>
        public static void Create(
            string shortcutPath,
            string targetPath,
            string arguments = null,
            string workingDirectory = null,
            string description = null,
            string iconLocation = null,
            int windowStyle = 1) // Default to normal window
        {
            if (string.IsNullOrWhiteSpace(shortcutPath))
                throw new ArgumentNullException(nameof(shortcutPath));
            if (!Path.GetExtension(shortcutPath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Shortcut path must have a .lnk extension.", nameof(shortcutPath));
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentNullException(nameof(targetPath));

            // Type for WScript.Shell, using late binding
            Type wshShellType = null;
            object wshShell = null;
            object shortcut = null;

            try
            {
                // Get the WScript.Shell type.
                // This is equivalent to: var wshShell = new WshShell(); if you have the COM reference.
                wshShellType = Type.GetTypeFromProgID("WScript.Shell");
                if (wshShellType == null)
                {
                    throw new InvalidOperationException("WScript.Shell is not available on this system.");
                }
                wshShell = Activator.CreateInstance(wshShellType);

                // Create a shortcut object.
                // This is equivalent to: IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(shortcutPath);
                shortcut = wshShellType.InvokeMember("CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    wshShell,
                    new object[] { shortcutPath });

                // Set shortcut properties using reflection or dynamic.
                // Using dynamic for easier property access:
                dynamic dynamicShortcut = shortcut;

                dynamicShortcut.TargetPath = targetPath;

                if (!string.IsNullOrWhiteSpace(arguments))
                {
                    dynamicShortcut.Arguments = arguments;
                }

                if (!string.IsNullOrWhiteSpace(workingDirectory))
                {
                    dynamicShortcut.WorkingDirectory = workingDirectory;
                }
                else // Default working directory to the target's directory if not specified
                {
                     dynamicShortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    dynamicShortcut.Description = description;
                }

                if (!string.IsNullOrWhiteSpace(iconLocation))
                {
                    dynamicShortcut.IconLocation = iconLocation;
                }

                // Set window style (1: Normal, 3: Maximized, 7: Minimized/NoActivate)
                // See IWshShortcut.WindowStyle documentation for more values.
                dynamicShortcut.WindowStyle = windowStyle;

                // Save the shortcut.
                // This is equivalent to: shortcut.Save();
                dynamicShortcut.Save();

                Console.WriteLine($"Shortcut created successfully using WshShell at: {shortcutPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating shortcut with WshShell: {ex.Message}");
                // Consider more specific error handling or re-throwing
                throw;
            }
            finally
            {
                // Release COM objects to prevent memory leaks
                if (shortcut != null)
                {
                    Marshal.ReleaseComObject(shortcut);
                }
                if (wshShell != null)
                {
                    Marshal.ReleaseComObject(wshShell);
                }
            }
        }
    
}
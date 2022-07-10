using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace MapleStory.Common
{
    /// <summary>
    /// Helper class for finding MapleStory's path. 
    /// </summary>
    public static class MapleStoryPathHelper
    {
        private static readonly string REG_KEY_PATH_CMS =
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\MapleStory"; // Tested for CMS
        private static readonly string REG_VALUE_NAME_CMS = @"UninstallString";

        private static readonly string REG_KEY_PATH_GMS = @"SOFTWARE\WOW6432Node\Wizet\MapleStory";
        private static readonly string REG_VALUE_NAME_GMS = @"ExecPath";

        public static readonly string MapleStoryExecutableName = @"MapleStory.exe";

        public static readonly string MapleStoryBaseWzName = @"Data\Base\Base.wz";

        private static readonly Lazy<string> _mapleStoryInstallDirectory = new Lazy<string>(() =>
        {
            // For CMS
            try
            {
                RegistryKey key = Registry.LocalMachine.OpenSubKey(REG_KEY_PATH_CMS); // Possible null
                string uninstallPath = key.GetValue(REG_VALUE_NAME_CMS) as string; // Possible invalid cast
                return Path.GetDirectoryName(uninstallPath); // Possible corrupted path
            }
            catch (Exception ex) when (ex is NullReferenceException || ex is InvalidCastException || ex is ArgumentException)
            {
                // ignored, not found
            }

            // For GMS
            try
            {
                RegistryKey key = Registry.LocalMachine.OpenSubKey(REG_KEY_PATH_GMS); // Possible null
                string execPath = key.GetValue(REG_VALUE_NAME_GMS) as string; // Possible invalid cast
                return execPath; 
            }
            catch (Exception ex) when (ex is NullReferenceException || ex is InvalidCastException)
            {
                // ignored, not found
            }
            return string.Empty;
        });

        /// <summary>
        /// Indicate if MapleStory installed location was found.
        /// </summary>
        public static bool FoundMapleStoryInstalled => Directory.Exists(_mapleStoryInstallDirectory.Value);

        /// <summary>
        /// Get MapleStory installed folder based on registry, might fail if no elevate privilege, in which case it will return empty string.
        /// </summary>
        public static string MapleStoryInstallDirectory => _mapleStoryInstallDirectory.Value;

    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common
{
    internal class AppConstants
    {
        #pragma warning disable CS8601 // Possible null reference assignment.
        public static readonly string AppExecutableLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        #pragma warning restore CS8601 // Possible null reference assignment.
        public static readonly string AppSettingsFileName = "lpsSettings.json";
        public static readonly string AppSettingsFileLocation = Path.Combine(AppExecutableLocation, "config", AppConstants.AppSettingsFileName);
    }
}

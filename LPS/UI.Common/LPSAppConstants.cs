using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common
{
    internal class LPSAppConstants
    {
        public static readonly string AppExecutableLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        public static readonly string AppSettingsFileName = "lpsSettings.json";
        public static readonly string AppSettingsFileLocation = Path.Combine(LPSAppConstants.AppExecutableLocation, "config", LPSAppConstants.AppSettingsFileName);
    }
}

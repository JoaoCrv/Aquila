using System;
using System.Reflection;
using System.Diagnostics; // Para FileVersionInfo

namespace Aquila.Services
{
    public static class AppInfo
    {
        public static string GetApplicationVersion()
        {
            // Get the assembly version
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            // If the assembly version is null, return a default value
            if (version == null)
            {
                return "0.0.0";
            }
            else
            {
                return version.ToString();
            }
        }
    }
}
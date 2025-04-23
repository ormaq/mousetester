using System.Management;

namespace MouseTester
{
    /// <summary>Safely gathers mouse-device data via WMI.  
    /// Some properties (e.g. InterfaceType, DriverVersion) aren’t present on every OS,
    /// so every lookup is wrapped in a try/catch to avoid “Not found” exceptions.</summary>
    internal static class MouseInfoProvider
    {
        internal sealed record MouseInfo(
            string Name,
            string Manufacturer,
            string Description,
            string PNPDeviceID,
            string InterfaceType,
            string DriverVersion);

        private static string SafeGet(ManagementBaseObject obj, string prop)
        {
            try
            {
                return obj.Properties[prop]?.Value?.ToString() ?? "";
            }
            catch (ManagementException)          // property missing on this platform
            {
                return "";
            }
        }

        public static IEnumerable<MouseInfo> GetConnectedMice()
        {
            using var searcher =
                new ManagementObjectSearcher("SELECT * FROM Win32_PointingDevice");

            foreach (ManagementObject m in searcher.Get())
            {
                yield return new MouseInfo(
                    SafeGet(m, "Name"),
                    SafeGet(m, "Manufacturer"),
                    SafeGet(m, "Description"),
                    SafeGet(m, "PNPDeviceID"),
                    SafeGet(m, "InterfaceType"),   // often absent
                    SafeGet(m, "DriverVersion")); // often absent
            }
        }
    }
}

using System.Runtime.InteropServices;
using System.Text;

namespace DCWallet;

internal static class DistributionPolicy
{
    public static bool IsStoreBuild
    {
        get
        {
#if DC_STORE
            return true;
#else
            return false;
#endif
        }
    }
    public static bool UsesStoreUpdates => IsStoreBuild || HasWindowsPackageIdentity();
    static bool HasWindowsPackageIdentity()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try { uint length = 0; var result = GetCurrentPackageFullName(ref length, null); return result is 0 or 122; }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { return false; }
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, StringBuilder? packageFullName);
}

using System.Reflection;
namespace DCWallet;
internal static class AppInfo
{
    public static string Version => typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "alpha";
}

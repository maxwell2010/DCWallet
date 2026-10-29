using System.Globalization;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DCWallet.Core;

public sealed record ReleaseUpdateInfo(string CurrentVersion, string NewVersion, Uri ReleaseUri,
    IReadOnlyDictionary<string, Uri> Downloads)
{
    public Uri? GetDownloadUri(string runtimeIdentifier) => Downloads.GetValueOrDefault(runtimeIdentifier);
}

public sealed class ReleaseUpdateClient(HttpClient? client = null)
{
    const string Repository = "https://github.com/maxwell2010/DCWallet";
    static readonly HttpClient DefaultClient = new() { Timeout = TimeSpan.FromSeconds(7) };
    static readonly IReadOnlyDictionary<string, string> Assets = new Dictionary<string, string>
    {
        ["win-x64"] = "DCWallet-win-x64.exe", ["win-arm64"] = "DCWallet-win-arm64.exe",
        ["linux-x64"] = "DCWallet-x86_64.AppImage", ["linux-arm64"] = "DCWallet-linux-arm64.tar.gz",
        ["osx-x64"] = "DCWallet-osx-x64.app.zip", ["osx-arm64"] = "DCWallet-osx-arm64.app.zip"
    };

    public async Task<ReleaseUpdateInfo?> CheckAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!SemanticVersion.TryParse(currentVersion, out var current)) return null;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(7));
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/maxwell2010/DCWallet/releases/tags/alpha");
            request.Headers.UserAgent.ParseAdd("DCWallet-update-check/1.0");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await (client ?? DefaultClient).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 1048576) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            // Bound even chunked responses before deserializing external JSON.
            using var bytes = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(buffer, deadline.Token)) > 0)
            {
                if (bytes.Length + count > 1048576) return null;
                bytes.Write(buffer, 0, count);
            }
            using var json = JsonDocument.Parse(bytes.ToArray());
            var root = json.RootElement;
            if (root.GetProperty("draft").GetBoolean() || root.GetProperty("tag_name").GetString() != "alpha" ||
                root.GetProperty("html_url").GetString() != Repository + "/releases/tag/alpha") return null;
            var title = root.GetProperty("name").GetString();
            const string prefix = "DC WALLET ";
            if (title is null || !title.StartsWith(prefix, StringComparison.Ordinal)) return null;
            var version = title[prefix.Length..];
            if (!SemanticVersion.TryParse(version, out var available) || available!.CompareTo(current!) <= 0) return null;
            var downloads = new Dictionary<string, Uri>();
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                foreach (var entry in Assets)
                {
                    var url = Repository + "/releases/download/alpha/" + entry.Value;
                    if (asset.GetProperty("name").GetString() == entry.Value && asset.GetProperty("browser_download_url").GetString() == url)
                        downloads.TryAdd(entry.Key, new Uri(url));
                }
            }
            if (downloads.Count == 0) return null;
            return new(currentVersion, version, new Uri(Repository + "/releases/tag/alpha"), downloads);
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or IOException)
        {
            return null;
        }
    }

    sealed record SemanticVersion(BigInteger Major, BigInteger Minor, BigInteger Patch, string[]? Pre) : IComparable<SemanticVersion>
    {
        static readonly Regex Pattern = new(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant);
        public static bool TryParse(string value, out SemanticVersion? parsed)
        {
            parsed = null;
            if (value.Length > 200) return false;
            var match = Pattern.Match(value);
            if (!match.Success) return false;
            var pre = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : null;
            if (pre?.Any(p => p.All(char.IsAsciiDigit) && p.Length > 1 && p[0] == '0') == true) return false;
            parsed = new(BigInteger.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), BigInteger.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), BigInteger.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), pre);
            return true;
        }
        public int CompareTo(SemanticVersion? other)
        {
            if (other is null) return 1;
            var result = Major.CompareTo(other.Major);
            if (result == 0) result = Minor.CompareTo(other.Minor);
            if (result == 0) result = Patch.CompareTo(other.Patch);
            if (result != 0) return result;
            if (Pre is null) return other.Pre is null ? 0 : 1;
            if (other.Pre is null) return -1;
            for (var i = 0; i < Math.Min(Pre.Length, other.Pre.Length); i++)
            {
                var leftNumber = BigInteger.TryParse(Pre[i], NumberStyles.None, CultureInfo.InvariantCulture, out var left);
                var rightNumber = BigInteger.TryParse(other.Pre[i], NumberStyles.None, CultureInfo.InvariantCulture, out var right);
                result = leftNumber && rightNumber ? left.CompareTo(right) : leftNumber ? -1 : rightNumber ? 1 : string.CompareOrdinal(Pre[i], other.Pre[i]);
                if (result != 0) return result;
            }
            return Pre.Length.CompareTo(other.Pre.Length);
        }
    }
}

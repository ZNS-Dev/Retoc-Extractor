using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace RetocExtractor.RetocExtractor.Codes
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppSettings settings = SettingsStore.Load();
            try
            {

                RetocBootstrapper.EnsureRetocAvailable();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Retoc Extractor could not download the latest Windows version of retoc. Check your internet connection and try again, or place retoc.exe in the Data folder manually. Details: {ex.Message}",
                    "Could not download retoc",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            Application.Run(new MainForm(settings));
        }
    }

    internal static class RetocBootstrapper
    {
        private const string LatestReleaseApi = "https://api.github.com/repos/trumank/retoc/releases/latest";
        private const string UserAgent = "RetocExtractor/1.0";
        private const long MaximumArchiveBytes = 100L * 1024L * 1024L;

        public static void EnsureRetocAvailable()
        {
            string applicationDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string dataDirectory = Path.Combine(applicationDirectory, "Data");
            string exportDirectory = Path.Combine(applicationDirectory, "Export");

            Directory.CreateDirectory(dataDirectory);
            Directory.CreateDirectory(exportDirectory);

            string retocPath = Path.Combine(dataDirectory, "retoc.exe");
            string zipPath = Path.Combine(dataDirectory, "retoc-download.tmp.zip");
            string extractedPath = Path.Combine(dataDirectory, "retoc-download.tmp.exe");

            TryDelete(zipPath);
            TryDelete(extractedPath);

            if (IsUsableExecutable(retocPath))
                return;

            if (!Environment.Is64BitOperatingSystem)
                throw new PlatformNotSupportedException("The selected retoc Windows release requires a 64-bit version of Windows.");

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            try
            {
                ReleaseInfo release = FindLatestWindowsRelease();
                using (var client = CreateWebClient())
                    client.DownloadFile(release.DownloadUrl, zipPath);

                var archiveInfo = new FileInfo(zipPath);
                if (!archiveInfo.Exists || archiveInfo.Length <= 0 || archiveInfo.Length > MaximumArchiveBytes)
                    throw new InvalidDataException("The downloaded retoc archive is empty or larger than the allowed size.");

                string expectedHash = release.Sha256;
                if (string.IsNullOrWhiteSpace(expectedHash) && !string.IsNullOrWhiteSpace(release.ChecksumUrl))
                {
                    try
                    {
                        using var client = CreateWebClient();
                        string checksumText = client.DownloadString(release.ChecksumUrl);
                        Match checksumMatch = Regex.Match(checksumText ?? string.Empty, @"\b[a-fA-F0-9]{64}\b");
                        if (checksumMatch.Success) expectedHash = checksumMatch.Value;
                    }
                    catch
                    {
                        // Checksum sidecars are optional; the release asset is still downloaded over HTTPS.
                    }
                }

                if (!string.IsNullOrWhiteSpace(expectedHash))
                {
                    string actualHash = ComputeSha256(zipPath);
                    if (!string.Equals(NormalizeHash(expectedHash), actualHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The retoc archive SHA-256 checksum does not match the release metadata.");
                }

                using (var stream = File.OpenRead(zipPath))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    ZipArchiveEntry executable = archive.Entries
                        .Where(entry => !string.IsNullOrWhiteSpace(entry.Name) &&
                                        string.Equals(Path.GetExtension(entry.Name), ".exe", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(entry => string.Equals(entry.Name, "retoc.exe", StringComparison.OrdinalIgnoreCase) ? 3 :
                                                    string.Equals(entry.Name, "retoc_cli.exe", StringComparison.OrdinalIgnoreCase) ? 2 :
                                                    entry.Name.IndexOf("retoc", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0)
                        .FirstOrDefault() ?? throw new InvalidDataException("The latest Windows retoc ZIP did not contain an executable.");
                    if (executable.Length <= 100000 || executable.Length > MaximumArchiveBytes)
                        throw new InvalidDataException("The retoc executable in the release archive has an unexpected size.");

                    using Stream input = executable.Open();
                    using var output = new FileStream(extractedPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    input.CopyTo(output);
                }

                if (!IsUsableExecutable(extractedPath))
                    throw new InvalidDataException("The extracted retoc executable is missing or incomplete.");

                if (File.Exists(retocPath)) File.Delete(retocPath);
                File.Move(extractedPath, retocPath);
            }
            finally
            {
                TryDelete(zipPath);
                TryDelete(extractedPath);
            }
        }

        private static ReleaseInfo FindLatestWindowsRelease()
        {
            string json;
            var request = (HttpWebRequest)WebRequest.Create(LatestReleaseApi);
            request.Method = "GET";
            request.UserAgent = UserAgent;
            request.Accept = "application/vnd.github+json";
            request.Timeout = 20000;
            request.ReadWriteTimeout = 30000;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
                json = reader.ReadToEnd();

            var serializer = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
            if (serializer.DeserializeObject(json) is not IDictionary<string, object> root)
                throw new InvalidDataException("GitHub did not return a valid retoc release response.");

            if (!root.TryGetValue("assets", out object assetsValue))
                throw new InvalidDataException("GitHub did not return a valid retoc release response.");

            if (assetsValue is not IEnumerable assetEntries || assetsValue is string)
                throw new InvalidDataException("GitHub returned no release assets for retoc.");

            var candidates = new List<ReleaseAsset>();
            foreach (object entry in assetEntries)
            {
                if (entry is not IDictionary<string, object> asset) continue;

                string name = GetJsonString(asset, "name");
                string url = GetJsonString(asset, "browser_download_url");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) continue;
                candidates.Add(new ReleaseAsset
                {
                    Name = name,
                    DownloadUrl = url,
                    Digest = GetJsonString(asset, "digest")
                });
            }

            ReleaseAsset windowsZip = candidates
                .Where(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                                asset.Name.IndexOf("windows-msvc", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                asset.Name.IndexOf("x86_64", StringComparison.OrdinalIgnoreCase) >= 0)
                .FirstOrDefault() ?? throw new InvalidDataException("The latest retoc release does not provide a Windows MSVC ZIP asset.");
            string digest = windowsZip.Digest ?? string.Empty;
            if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                digest = digest.Substring("sha256:".Length);

            ReleaseAsset checksum = candidates.FirstOrDefault(asset =>
                string.Equals(asset.Name, windowsZip.Name + ".sha256", StringComparison.OrdinalIgnoreCase));

            return new ReleaseInfo
            {
                DownloadUrl = windowsZip.DownloadUrl,
                Sha256 = NormalizeHash(digest),
                ChecksumUrl = checksum == null ? string.Empty : checksum.DownloadUrl
            };
        }

        private static string GetJsonString(IDictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) && value is string v
                ? v
                : string.Empty;
        }

        private static WebClient CreateWebClient()
        {
            var client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = UserAgent;
            client.Headers[HttpRequestHeader.Accept] = "application/octet-stream";
            return client;
        }

        private static string ComputeSha256(string filePath)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(filePath);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string NormalizeHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash)) return string.Empty;
            hash = hash.Trim();
            if (hash.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) hash = hash.Substring(7);
            Match match = Regex.Match(hash, @"[a-fA-F0-9]{64}");
            return match.Success ? match.Value.ToLowerInvariant() : string.Empty;
        }

        private static bool IsUsableExecutable(string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length <= 100000) return false;
                using var stream = File.OpenRead(path);
                using var reader = new BinaryReader(stream);
                // Validate the DOS/PE signature and require the AMD64 machine type.
                if (reader.ReadUInt16() != 0x5A4D) return false;
                stream.Position = 0x3C;
                int peOffset = reader.ReadInt32();
                if (peOffset < 64 || peOffset > stream.Length - 6) return false;
                stream.Position = peOffset;
                if (reader.ReadUInt32() != 0x00004550) return false;
                return reader.ReadUInt16() == 0x8664;
            }
            catch { return false; }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private sealed class ReleaseInfo
        {
            public string DownloadUrl { get; set; }
            public string Sha256 { get; set; }
            public string ChecksumUrl { get; set; }
        }

        private sealed class ReleaseAsset
        {
            public string Name { get; set; }
            public string DownloadUrl { get; set; }
            public string Digest { get; set; }
        }
    }
}

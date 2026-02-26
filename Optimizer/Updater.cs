using System;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Net.Cache;

namespace Optimizer   // ⚠ IMPORTANT: must match your project namespace
{
    public static class Updater
    {
        private const string UpdateInfoUrl =
            "https://raw.githubusercontent.com/MrPcGamerYT/Optimizer/main/update.json";

        public static void CheckAndUpdate()
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 |
                    SecurityProtocolType.Tls11;

                using (WebClient wc = new WebClient())
                {
                    wc.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
                    wc.Headers.Add("Cache-Control", "no-cache");

                    string json = wc.DownloadString(UpdateInfoUrl);

                    string latestVersion = ExtractValue(json, "version");
                    string installerUrl = ExtractValue(json, "url");

                    string currentVersion = Application.ProductVersion;

                    if (CompareVersions(latestVersion, currentVersion) <= 0)
                        return;

                    DialogResult result = MessageBox.Show(
                        $"New version {latestVersion} available.\n\nYour version: {currentVersion}\n\nUpdate now?",
                        "Optimizer Update",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (result != DialogResult.Yes)
                        return;

                    string installerPath = Path.Combine(
                        Path.GetTempPath(),
                        "OptimizerSetup.exe");

                    if (File.Exists(installerPath))
                        File.Delete(installerPath);

                    wc.DownloadFile(installerUrl, installerPath);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = installerPath,
                        UseShellExecute = true,
                        Verb = "runas"
                    });

                    Environment.Exit(0);
                }
            }
            catch (WebException)
            {
                // silent fail (no internet or server down)
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Updater Error:\n" + ex.Message,
                    "Updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static string ExtractValue(string json, string key)
        {
            Match match = Regex.Match(
                json,
                $"\"{key}\"\\s*:\\s*\"([^\"]+)\"",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                throw new Exception("Invalid update.json format");

            return match.Groups[1].Value;
        }

        private static int CompareVersions(string latest, string current)
        {
            Version vLatest = new Version(latest);
            Version vCurrent = new Version(current);

            return vLatest.CompareTo(vCurrent);
        }
    }
}

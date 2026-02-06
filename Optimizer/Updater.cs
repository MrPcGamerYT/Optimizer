using System;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace Optimizer
{
    public static class Updater
    {
        private const string UpdateInfoUrl =
            "https://raw.githubusercontent.com/MrPcGamerYT/Optimizer/main/update.json";

        private static bool hasCheckedThisSession = false;

        /// <summary>
        /// Call this method once on app startup to check for updates.
        /// </summary>
        public static void CheckAndUpdate()
{
    if (hasCheckedThisSession) return;
    hasCheckedThisSession = true;

    // Fixes connection issues with GitHub
    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

    try
    {
        using (WebClient wc = new WebClient())
        {
            wc.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            string json = wc.DownloadString(UpdateInfoUrl);

            string latestVersionText = ExtractJsonValue(json, "version");
            string installerUrl = ExtractJsonValue(json, "url");

            // Get the version of the app currently running
            string currentVersionText = Application.ProductVersion;

            // If Latest is NOT greater than Current, stop here
            if (CompareVersions(latestVersionText, currentVersionText) <= 0)
                return;

            // Updated message box to show both versions for debugging
            DialogResult dr = MessageBox.Show(
                $"A new version is available!\n\n" +
                $"Latest: {latestVersionText}\n" +
                $"Your Version: {currentVersionText}\n\n" +
                $"Download and install now?",
                "Optimizer Update",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information
            );

            if (dr != DialogResult.Yes) return;

            string installerPath = Path.Combine(Path.GetTempPath(), "OptimizerSetup.exe");
            if (File.Exists(installerPath)) File.Delete(installerPath);

            wc.DownloadFile(installerUrl, installerPath);

            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true,
                Verb = "runas"
            });

            Application.Exit(); 
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show("Update error: " + ex.Message);
    }
}
        // Extract a JSON value by key (simple regex, no library)
        private static string ExtractJsonValue(string json, string key)
        {
            var match = Regex.Match(
                json,
                $"\"{key}\"\\s*:\\s*\"([^\"]+)\"",
                RegexOptions.IgnoreCase
            );

            if (!match.Success)
                throw new Exception($"Missing '{key}' in update.json");

            return match.Groups[1].Value;
        }

        // Compare semantic versions: 1 = latest > current, -1 = latest < current, 0 = equal
        private static int CompareVersions(string vLatest, string vCurrent)
        {
            int[] latestParts = ParseVersionParts(vLatest);
            int[] currentParts = ParseVersionParts(vCurrent);

            int maxLength = Math.Max(latestParts.Length, currentParts.Length);

            for (int i = 0; i < maxLength; i++)
            {
                int latest = (i < latestParts.Length) ? latestParts[i] : 0;
                int current = (i < currentParts.Length) ? currentParts[i] : 0;

                if (latest > current) return 1;
                if (latest < current) return -1;
            }

            return 0;
        }

        private static int[] ParseVersionParts(string v)
        {
            var match = Regex.Match(v, @"\d+(\.\d+)*");
            if (!match.Success)
                throw new Exception("Invalid version format: " + v);

            string[] parts = match.Value.Split('.');
            int[] numbers = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                numbers[i] = int.Parse(parts[i]);

            return numbers;
        }
    }
}

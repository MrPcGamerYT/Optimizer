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
            // Prevent multiple checks in the same app session
            if (hasCheckedThisSession) return;
            hasCheckedThisSession = true;

            try
            {
                using (WebClient wc = new WebClient())
                {
                    // Always get the latest JSON (no caching)
                    wc.CachePolicy = new System.Net.Cache.RequestCachePolicy(
                        System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

                    string json = wc.DownloadString(UpdateInfoUrl);

                    string latestVersionText = ExtractJsonValue(json, "version");
                    string installerUrl = ExtractJsonValue(json, "url");

                    // Compare versions: skip if current is up-to-date
                    if (CompareVersions(latestVersionText, Application.ProductVersion) <= 0)
                        return;

                    // Ask user to update
                    DialogResult dr = MessageBox.Show(
                        $"A new version {latestVersionText} is available.\n\nUpdate now?",
                        "Optimizer Update",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information
                    );

                    if (dr != DialogResult.Yes) return;

                    string installerPath = Path.Combine(
                        Path.GetTempPath(),
                        "OptimizerSetup.exe"
                    );

                    // Remove previous installer if exists
                    if (File.Exists(installerPath))
                        File.Delete(installerPath);

                    // Download latest installer
                    wc.DownloadFile(installerUrl, installerPath);

                    // Run installer with admin rights
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = installerPath,
                        UseShellExecute = true,
                        Verb = "runas"
                    });

                    // Exit current app after starting installer
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Update failed:\n" + ex.Message,
                    "Update Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
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

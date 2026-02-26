using System;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Text.RegularExpressions;

class Updater
{
    private const string UpdateInfoUrl =
        "https://raw.githubusercontent.com/MrPcGamerYT/Optimizer/main/update.json";

    public static void CheckAndUpdate()
    {
        try
        {
            using (WebClient wc = new WebClient())
            {
                // Disable caching to always get the latest JSON
                wc.CachePolicy = new System.Net.Cache.RequestCachePolicy(
                    System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

                string json = wc.DownloadString(UpdateInfoUrl);

                string latestVersionText = ExtractJsonValue(json, "version");
                string installerUrl = ExtractJsonValue(json, "url");

                // Compare versions: if latest <= current, do nothing
                if (CompareVersions(latestVersionText, Application.ProductVersion) <= 0)
                    return; // already up-to-date

                // Ask user to update
                if (MessageBox.Show(
                    $"New version {latestVersionText} is available.\n\nUpdate now?",
                    "Optimizer Update",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information) != DialogResult.Yes)
                    return;

                string installerPath = Path.Combine(
                    Path.GetTempPath(),
                    "OptimizerSetup.exe"
                );

                // Remove previous installer if exists
                if (File.Exists(installerPath))
                    File.Delete(installerPath);

                // Download latest installer
                wc.DownloadFile(installerUrl, installerPath);

                // Run installer only (never the main exe)
                Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true,
                    Verb = "runas" // prompt for admin
                });

                // Force exit the current app immediately
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

    // Extract a JSON value by key (no external library required)
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

    // Compare semantic versions: returns 1 if latest > current, -1 if latest < current, 0 if equal
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

        return 0; // equal
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

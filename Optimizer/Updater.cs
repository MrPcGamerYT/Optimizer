using System;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Forms;

class Updater
{
    public static void CheckAndUpdate()
    {
        try
        {
            using (WebClient wc = new WebClient())
            {
                string json = wc.DownloadString(
                    "https://raw.githubusercontent.com/MrPcGamerYT/Optimizer/refs/heads/main/update.json"
                );

                using JsonDocument doc = JsonDocument.Parse(json);
                string latestVersion = doc.RootElement.GetProperty("version").GetString();
                string installerUrl  = doc.RootElement.GetProperty("url").GetString();

                string currentVersion = Application.ProductVersion;

                if (latestVersion != currentVersion)
                {
                    if (MessageBox.Show(
                        $"New version {latestVersion} is available.\n\nUpdate now?",
                        "Optimizer Update",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        string installerPath = Path.Combine(
                            Path.GetTempPath(),
                            "OptimizerSetup.exe"
                        );

                        wc.DownloadFile(installerUrl, installerPath);

                        // ✅ RUN INSTALLER ONLY
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = installerPath,
                            UseShellExecute = true,
                            Verb = "runas"
                        });

                        // ✅ EXIT CURRENT APP
                        Application.Exit();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Could not check for updates.\n" + ex.Message,
                "Update Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }
}

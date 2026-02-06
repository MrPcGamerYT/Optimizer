using System;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

class Updater
{
    public static void CheckAndUpdate()
    {
        try
        {
            using (WebClient wc = new WebClient())
            {
                string versionInfo = wc.DownloadString(
                    "https://raw.githubusercontent.com/MrPcGamerYT/Optimizer/refs/heads/main/update.json"
                );

                string[] lines = versionInfo
                    .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                string latestVersion = lines[0].Trim();
                string installerUrl = lines[1].Trim();

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

                        // 🔥 RUN INSTALLER ONLY
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = installerPath,
                            UseShellExecute = true,
                            Verb = "runas" // admin
                        });

                        // 🔴 EXIT APP IMMEDIATELY
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

using System;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading;

class Updater
{
    // Call this method to check for updates
    public static void CheckAndUpdate()
    {
        try
        {
            WebClient wc = new WebClient();
            string versionInfo = wc.DownloadString("https://raw.githubusercontent.com/MrPcGamerYT/Optimizer/refs/heads/main/version.txt");
            string[] lines = versionInfo.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            string latestVersion = lines[0].Trim();
            string downloadUrl = lines[1].Trim();

            string currentVersion = Application.ProductVersion;

            if (latestVersion != currentVersion)
            {
                if (MessageBox.Show($"New version {latestVersion} is available. Update now?", "Update", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    string tempFile = Path.Combine(Path.GetTempPath(), "Optimizer_new.exe");
                    wc.DownloadFile(downloadUrl, tempFile);

                    // Launch the updater process
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = tempFile,
                        Arguments = "/update",
                        UseShellExecute = true
                    });

                    // Close current app
                    Application.Exit();
                }
            }
        }
        catch
        {
            MessageBox.Show("Could not check for updates.");
        }
    }
}

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MEmarket.Services
{
    public class MEinstaller
    {
        public Delegate output;
        public Delegate progress;
        public TelnetClient telnetClient;
        private StorageFolder LocalData;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private string latestMessage = "";

        public async Task<bool> InstallApp(StorageFile XAPFile, bool InstallInSDCard = false)
        {
            this.showLoader(true);
            this.latestMessage = "";
            if (this.LocalData == null)
                this.LocalData = await ApplicationData.Current.LocalFolder.CreateFolderAsync("xapInstallers", CreationCollisionOption.OpenIfExists);

            bool state = false;
            string Parameter2 = "-di";
            string fileExt = Path.GetExtension(XAPFile.Name).ToLower();
            if (fileExt == ".appx" || fileExt == ".appxbundle")
            {
                Parameter2 = "";
            }
            else if (InstallInSDCard)
            {
                Parameter2 = "-dis";
            }   

            string InstallerName = Path.GetFileNameWithoutExtension(XAPFile.Name) + ".bat";
            string InstallerConfirmName = Path.GetFileNameWithoutExtension(XAPFile.Name) + ".txt";
            string InstallerOutputName = Path.GetFileNameWithoutExtension(XAPFile.Name) + "Output.txt";
            StorageFile storageFile = await this.LocalData.CreateFileAsync(InstallerName, CreationCollisionOption.ReplaceExisting);

            if (storageFile != null)
            {
                string InstallCommand = $"th \"{XAPFile.Path}\" -u\n\r";
                InstallCommand += "sleep 3\n\r";
                InstallCommand += $"th \"{XAPFile.Path}\" %1> \"{this.LocalData.Path}\\{InstallerOutputName}\"\n\r";
                InstallCommand += $"echo \"All done\"> \"{this.LocalData.Path}\\{InstallerConfirmName}\"";
                await FileIO.AppendTextAsync(storageFile, InstallCommand);
                string InstallerPath = storageFile.Path;
                string command = "\"" + InstallerPath + "\" " + Parameter2;
                try
                {
                    this.cancellationTokenSource.Cancel();
                }
                catch (Exception)
                {
                }
                await this.SendCommand(command);

                string installationConfirmationFile = this.GetInstallationConfirmFileByXAP(XAPFile.Path);
                
                while (!File.Exists(installationConfirmationFile))
                    await Task.Delay(1000);
                
                string outputFileName = this.GetInstallationOutputFileByXAP(XAPFile.Path);
                await this.ShowInstallationOutPut(outputFileName);

                if (this.latestMessage != null && (this.latestMessage.ToLower().Contains("error") || this.latestMessage.ToLower().Contains("failed")))
                {
                    this.showOutput("Installation failed");
                }
                else
                {
                    state = true;
                    this.showOutput("Installation done");
                }
            }
            this.telnetClient.Disconnect();
            this.showOutput("Cleaning temp files..");
            await this.DeleteInstallationConfirmFileByXAP(XAPFile.Path);
            this.showLoader(false);
            return state;
        }

        public void Cancel() => this.cancellationTokenSource.Cancel();

        public async Task SendCommand(string command, bool closeAfterCommand = false)
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            if (this.telnetClient != null)
            {
                try
                {
                    this.telnetClient.Disconnect();
                    this.telnetClient.Dispose();
                }
                catch (Exception)
                {
                }
            }
            this.telnetClient = new TelnetClient(TimeSpan.FromSeconds(3.0), this.cancellationTokenSource.Token);
            this.telnetClient.ConnectionClosed += new EventHandler(this.HandleConnectionClosed);
            this.telnetClient.MessageReceived += new EventHandler<string>(this.HandleMessageReceived);
            this.telnetClient.ErrorReceived += new EventHandler<string>(this.HandleErrorReceived);
            await Task.Run((Func<Task>)(async () =>
            {
                try
                {
                    this.showOutput("Connecting to telnet..");
                    this.telnetClient.Connect("127.0.0.1", 23);
                    await Task.Delay(1000);
                    this.showOutput("Telnet Connected");
                    this.showOutput("Executing installation command..");
                    this.telnetClient.Send(command);
                    this.showOutput("Command sent..");
                    if (!closeAfterCommand)
                        return;
                    await Task.Delay(2000);
                    this.telnetClient.Disconnect();
                }
                catch (Exception ex)
                {
                    this.showOutput(ex.Message);
                }
            }));
        }

        private void showLoader(bool state)
        {
            if ((object)this.progress == null)
                return;
            this.progress.DynamicInvoke((object)state);
        }

        private void showOutput(string message)
        {
            if ((object)this.output == null)
                return;
            this.output.DynamicInvoke((object)message);
        }

        public async Task ShowInstallationOutPut(string InstallerOutputPath)
        {
            try
            {
                StorageFile statusFile = await this.LocalData.GetFileAsync(InstallerOutputPath);
                if (statusFile == null)
                    return;
                byte[] resultInBytes;
                using (IRandomAccessStream targetStream = await statusFile.OpenAsync(FileAccessMode.Read))
                {
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        await targetStream.AsStreamForRead().CopyToAsync(memoryStream);
                        resultInBytes = memoryStream.ToArray();
                    }
                }
                string textRead = Encoding.UTF8.GetString(resultInBytes, 0, resultInBytes.Length);
                if (textRead == null || textRead.Length <= 0)
                    return;
                if (this.cancellationTokenSource.IsCancellationRequested)
                    return;
                try
                {
                    this.showOutput(textRead);
                    this.latestMessage = textRead;
                }
                catch (Exception ex)
                {
                    this.showOutput(ex.Message);
                }
            }
            catch (Exception)
            {
            }
        }

        private void HandleConnectionClosed(object sender, EventArgs e)
        {
            this.showOutput("Telnet connection closed");
        }

        private void HandleMessageReceived(object sender, string e)
        {
            if (e.Length <= 0 || !Regex.IsMatch(e, "[a-zA-Z0-9]+"))
                return;
            this.showOutput(e);
        }

        private void HandleErrorReceived(object sender, string e)
        {
            if (e.Length <= 0 || e.Contains("WaitForMessage completed: Calling Disconnect") || e.Contains("Calling Disconnect") || !Regex.IsMatch(e, "[a-zA-Z0-9]+"))
                return;
            this.showOutput(e);
        }

        private string GetInstallationFileByXAP(string XAPLocation)
        {
            return this.LocalData.Path + "\\" + Path.GetFileNameWithoutExtension(XAPLocation) + ".bat";
        }

        private string GetInstallationConfirmFileByXAP(string XAPLocation)
        {
            return this.LocalData.Path + "\\" + Path.GetFileNameWithoutExtension(XAPLocation) + ".txt";
        }

        private string GetInstallationOutputFileByXAP(string XAPLocation)
        {
            return Path.GetFileNameWithoutExtension(XAPLocation) + "Output.txt";
        }

        private async Task DeleteInstallationConfirmFileByXAP(string XAPLocation)
        {
            string InstallerPath = Path.GetFileNameWithoutExtension(XAPLocation) + ".txt";
            try
            {
                StorageFile testFile = await this.LocalData.GetFileAsync(InstallerPath);
                if (testFile != null)
                    await testFile.DeleteAsync();
            }
            catch (Exception)
            {
            }
            try
            {
                await this.DeleteInstallationOutputFileByXAP(XAPLocation);
            }
            catch (Exception)
            {
            }
            try
            {
                await this.DeleteInstallationBatFileByXAP(XAPLocation);
            }
            catch (Exception)
            {
            }
        }

        private string GetInstallationFileByXAP(StorageFile XAPFile)
        {
            return XAPFile != null ? this.LocalData.Path + "\\" + Path.GetFileNameWithoutExtension(XAPFile.Path) + ".bat" : "File is null";
        }

        private async Task DeleteInstallationOutputFileByXAP(string XAPLocation)
        {
            string InstallerPath = Path.GetFileNameWithoutExtension(XAPLocation) + "Output.txt";
            try
            {
                StorageFile testFile = await this.LocalData.GetFileAsync(InstallerPath);
                if (testFile == null)
                    return;
                await testFile.DeleteAsync();
            }
            catch (Exception)
            {
            }
        }

        private async Task DeleteInstallationBatFileByXAP(string XAPLocation)
        {
            string InstallerPath = Path.GetFileNameWithoutExtension(XAPLocation) + ".bat";
            try
            {
                StorageFile testFile = await this.LocalData.GetFileAsync(InstallerPath);
                if (testFile == null)
                    return;
                await testFile.DeleteAsync();
            }
            catch (Exception)
            {
            }
        }
    }
}
using MEmarket.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace MEmarket.Services
{
    public static class AppUpdateService
    {
        public static async Task<InstallResult> UpdateAsync(
            UpdateableApp app,
            Action<string> statusChanged,
            Action<bool> progressStateChanged)
        {
            if (app == null) throw new ArgumentNullException("app");
            if (string.IsNullOrWhiteSpace(app.AppUrl))
            {
                throw new ArgumentException("Не указан адрес приложения.", "app");
            }

            StorageFile packageFile = null;
            try
            {
                if (statusChanged != null) statusChanged(MEmarket.Resources.AppResources.LoadingText);

                string entryUrl = app.AppUrl.TrimEnd('/') + "/entry.json";
                string entryJson;
                using (var client = new HttpClient())
                {
                    entryJson = await client.GetStringAsync(new Uri(entryUrl));
                }

                JsonObject entryRoot;
                if (!JsonObject.TryParse(entryJson, out entryRoot) || !entryRoot.ContainsKey("versions"))
                {
                    throw new InvalidDataException("Не удалось найти список версий приложения.");
                }

                string appFileName = null;
                foreach (var version in entryRoot.GetNamedArray("versions"))
                {
                    var versionObject = version.GetObject();
                    if (versionObject.GetNamedString("version") == app.NewVersion)
                    {
                        appFileName = versionObject.GetNamedString("app_file");
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(appFileName))
                {
                    throw new InvalidDataException("Не удалось найти пакет выбранной версии приложения.");
                }

                var packageUri = new Uri(app.AppUrl.TrimEnd('/') + "/" + appFileName);
                string extension = Path.GetExtension(packageUri.AbsolutePath).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension)) extension = ".xap";

                if (statusChanged != null) statusChanged(MEmarket.Resources.AppResources.InstallProgressTextDownloading + app.Name + "...");
                packageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                    "temp_update_package" + extension,
                    CreationCollisionOption.ReplaceExisting);

                using (var client = new HttpClient())
                {
                    byte[] packageBytes = await client.GetByteArrayAsync(packageUri);
                    await FileIO.WriteBytesAsync(packageFile, packageBytes);
                }

                if (statusChanged != null) statusChanged(MEmarket.Resources.AppResources.InstallProgressTextUpdating + app.Name + "...");
                var installer = new MEinstaller();
                if (progressStateChanged != null)
                {
                    installer.ProgressStateChanged += progressStateChanged;
                }

                var result = await installer.UpdateAppAsync(packageFile);
                if (result.IsSuccess)
                {
                    LocalAppsManager.RegisterInstalledApp(
                        app.Id,
                        app.Name,
                        app.Icon,
                        app.NewVersion,
                        app.RepositoryUrl,
                        app.AppUrl);
                }

                return result;
            }
            finally
            {
                if (packageFile != null)
                {
                    try
                    {
                        await packageFile.DeleteAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Не удалось удалить временный пакет обновления: " + ex.Message);
                    }
                }
            }
        }
    }
}

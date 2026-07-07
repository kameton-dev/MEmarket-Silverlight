using MEmarket.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Runtime.Serialization.Json;

namespace MEmarket.Services
{
    public static class LocalAppsManager
    {
        private const string FileName = "installed_apps.json";

        public static void SaveApps(List<TrackedApp> apps)
        {
            try
            {
                using (var storage = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    using (var stream = new IsolatedStorageFileStream(FileName, FileMode.Create, storage))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(List<TrackedApp>));
                        serializer.WriteObject(stream, apps);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка сохранения БД: " + ex.Message);
            }
        }

        public static List<TrackedApp> LoadApps()
        {
            try
            {
                using (var storage = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (!storage.FileExists(FileName))
                        return new List<TrackedApp>();

                    using (var stream = new IsolatedStorageFileStream(FileName, FileMode.Open, storage))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(List<TrackedApp>));
                        return (List<TrackedApp>)serializer.ReadObject(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка загрузки БД: " + ex.Message);
                return new List<TrackedApp>();
            }
        }

        public static void RegisterInstalledApp(string id, string name, string icon, string version, string repoUrl, string appUrl)
        {
            var apps = LoadApps();
            apps.RemoveAll(a => a.Id == id);

            apps.Add(new TrackedApp
            {
                Id = id,
                Name = name,
                Icon = icon,
                InstalledVersion = version,
                RepositoryUrl = repoUrl,
                AppUrl = appUrl,
                InstallDate = DateTime.Now
            });

            SaveApps(apps);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using Microsoft.Phone.Shell;
using MEmarket.Models;

namespace MEmarket.Services
{
    public static class TileManager
    {
        private const string SettingKey = "ShowUpdatesOnTile";
        
        public static void UpdateLiveTile(List<UpdateableApp> updates)
        {
            try
            {
                var settings = IsolatedStorageSettings.ApplicationSettings;
                bool isEnabled = false;
                if (settings.Contains(SettingKey))
                {
                    isEnabled = (bool)settings[SettingKey];
                }

                if (!isEnabled) return;
                
                ShellTile primaryTile = ShellTile.ActiveTiles.FirstOrDefault();
                if (primaryTile == null) return;

                if (updates != null && updates.Count > 0)
                {
                    var firstUpdate = updates[0];

                    string updateMessage = string.Format(
                        MEmarket.Resources.AppResources.LiveTileContent,
                        firstUpdate.Name
                    );

                    string versionsMessage = string.Format(
                        "{0} ➔ {1}",
                        firstUpdate.InstalledVersion,
                        firstUpdate.NewVersion
                    );

                    FlipTileData tileData = new FlipTileData
                    {
                        Count = updates.Count,
                        BackTitle = MEmarket.Resources.AppResources.LiveTileTitle,
                        BackContent = updateMessage + "\n" + versionsMessage
                    };

                    primaryTile.Update(tileData);
                }
                else
                {
                    ClearTile();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка обновления живой плитки: " + ex.Message);
            }
        }
        
        public static void ClearTile()
        {
            try
            {
                ShellTile primaryTile = ShellTile.ActiveTiles.FirstOrDefault();
                if (primaryTile != null)
                {
                    FlipTileData tileData = new FlipTileData
                    {
                        Count = 0,
                        BackTitle = "",
                        BackContent = ""
                    };
                    primaryTile.Update(tileData);
                }
            }
            catch { }
        }
    }
}
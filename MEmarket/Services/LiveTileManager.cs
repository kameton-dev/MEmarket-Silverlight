using System;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using Microsoft.Phone.Shell;
using MEmarket.Models;

namespace MEmarket.Services
{
    public static class LiveTileManager
    {
        private const string LiveTileUpdatesKey = "ShowLiveTileUpdates";
        
        public static bool IsLiveTileEnabled()
        {
            if (IsolatedStorageSettings.ApplicationSettings.Contains(LiveTileUpdatesKey))
            {
                return (bool)IsolatedStorageSettings.ApplicationSettings[LiveTileUpdatesKey];
            }
            return true;
        }
        
        public static void SetLiveTileEnabled(bool enabled)
        {
            IsolatedStorageSettings.ApplicationSettings[LiveTileUpdatesKey] = enabled;
            IsolatedStorageSettings.ApplicationSettings.Save();
            
            if (!enabled)
            {
                ResetLiveTile();
            }
        }
        
        public static void UpdateLiveTile(List<UpdateableApp> updates)
        {
            if (!IsLiveTileEnabled())
            {
                ResetLiveTile();
                return;
            }

            ShellTile primaryTile = ShellTile.ActiveTiles.FirstOrDefault();
            if (primaryTile == null) return;

            if (updates != null && updates.Count > 0)
            {
                var firstUpdate = updates[0];
                int count = updates.Count;
                
                FlipTileData tileData = new FlipTileData
                {
                    Title = "MEmarket",
                    Count = count,
                    
                    BackTitle = "Обновления",
                    BackContent = "Доступно обновлений: " + count,

                    WideBackContent = string.Format("[{0}]: доступно обновление!\n{1} ➔ {2}",
                        firstUpdate.Name,
                        firstUpdate.InstalledVersion,
                        firstUpdate.NewVersion)
                };

                primaryTile.Update(tileData);
            }
            else
            {
                ResetLiveTile();
            }
        }
        
        public static void ResetLiveTile()
        {
            ShellTile primaryTile = ShellTile.ActiveTiles.FirstOrDefault();
            if (primaryTile == null) return;

            FlipTileData tileData = new FlipTileData
            {
                Title = "MEmarket",
                Count = 0,
                BackTitle = "",
                BackContent = "",
                WideBackContent = ""
            };

            primaryTile.Update(tileData);
        }
    }
}
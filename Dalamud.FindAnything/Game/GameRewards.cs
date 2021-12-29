using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Logging;
using Microsoft.Win32;

namespace Dalamud.FindAnything.Game;

public static class GameRewards
{
    public interface IRewardItem
    {
        public string Name { get; }
        public float Cost { get; }

        public void Bought();
    }

    public class EmoteSetReward : IRewardItem
    {
        public string Name => "a special set of bespoke, handcrafted DN emojis";
        public float Cost => 1_800_000;
        public void Bought()
        {
            var tmpFolder = Path.Combine(Path.GetTempPath(), "Noses");
            Directory.CreateDirectory(tmpFolder);

            var noses = Directory.GetFiles(Path.Combine(FindAnythingPlugin.PluginInterface.AssemblyLocation.Directory!.FullName, "noses"), "*.png");
            foreach (var nose in noses)
            {
                File.Copy(nose, Path.Combine(tmpFolder, Path.GetFileName(nose)), true);
            }

            var si = new ProcessStartInfo(tmpFolder);
            si.UseShellExecute = true;
            Process.Start(si);
        }
    }

    public class WallpaperReward : IRewardItem
    {
        public string Name => "DN Desktop Wallpaper";
        public float Cost => 1_900_000;

        public sealed class Wallpaper
        {
            Wallpaper() { }

            const int SPI_SETDESKWALLPAPER = 20;
            const int SPIF_UPDATEINIFILE = 0x01;
            const int SPIF_SENDWININICHANGE = 0x02;

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

            public enum Style : int
            {
                Tile,
                Center,
                Stretch,
                Span,
                Fit,
                Fill,
            }

            public static void Set(string path, Style style)
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
                if (style == Style.Fill)
                {
                    key.SetValue(@"WallpaperStyle", 10.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                }
                if (style == Style.Fit)
                {
                    key.SetValue(@"WallpaperStyle", 6.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                }
                if (style == Style.Span) // Windows 8 or newer only!
                {
                    key.SetValue(@"WallpaperStyle", 22.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                }
                if (style == Style.Stretch)
                {
                    key.SetValue(@"WallpaperStyle", 2.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                }
                if (style == Style.Tile)
                {
                    key.SetValue(@"WallpaperStyle", 0.ToString());
                    key.SetValue(@"TileWallpaper", 1.ToString());
                }
                if (style == Style.Center)
                {
                    key.SetValue(@"WallpaperStyle", 0.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                }

                SystemParametersInfo(SPI_SETDESKWALLPAPER,
                    0,
                    path,
                    SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            }
        }


        public void Bought()
        {
            try
            {
                Wallpaper.Set(Path.Combine(FindAnythingPlugin.PluginInterface.AssemblyLocation.Directory!.FullName, "noses", "wallpaper.jpg"), Wallpaper.Style.Fill);
                FindAnythingPlugin.PluginInterface.UiBuilder.AddNotification("Wallpaper bought!", "Congrats!", NotificationType.Success);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Could not set Desktop Wallpaper.");
            }
        }
    }

    public static readonly IReadOnlyDictionary<uint, IRewardItem> Rewards = new Dictionary<uint, IRewardItem>
    {
        { 0x00, new WallpaperReward() },
        { 0x01, new EmoteSetReward() }
    };
}
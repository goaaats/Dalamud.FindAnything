using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Data;
using Dalamud.Interface;
using Dalamud.Logging;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;

namespace Dalamud.FindAnything
{
    public class TextureCache : IDisposable
    {
        private readonly UiBuilder uiBuilder;
        private readonly DataManager data;

        public IReadOnlyDictionary<uint, TextureWrap> MainCommandIcons { get; init; }
        public IReadOnlyDictionary<uint, TextureWrap> GeneralActionIcons { get; init; }
        public IReadOnlyDictionary<uint, TextureWrap> ContentTypeIcons { get; init; }
        public IReadOnlyDictionary<uint, TextureWrap> EmoteIcons { get; init; }
        public IReadOnlyDictionary<uint, TextureWrap> ClassJobIcons { get; init; }
        public IReadOnlyDictionary<uint, TextureWrap> MountIcons { get; init; }
        public IReadOnlyDictionary<uint, TextureWrap> MinionIcons { get; init; }

        public Dictionary<uint, TextureWrap> ExtraIcons { get; private set; }

        public TextureWrap AetheryteIcon { get; init; }
        public TextureWrap WikiIcon { get; init; }
        public TextureWrap PluginInstallerIcon { get; init; }
        public TextureWrap LogoutIcon { get; init; }
        public TextureWrap EmoteIcon { get; init; }
        public TextureWrap HintIcon { get; set; }
        public TextureWrap ChatIcon { get; set; }
        public TextureWrap MathsIcon { get; set; }

        public TextureWrap GameIcon { get; set; }

        private TextureCache(UiBuilder uiBuilder, DataManager data)
        {
            this.uiBuilder = uiBuilder;
            this.data = data;

            var mainCommands = new Dictionary<uint, TextureWrap>();
            foreach (var mainCommand in data.GetExcelSheet<MainCommand>()!)
            {
                mainCommands.Add(mainCommand.RowId, data!.GetImGuiTextureHqIcon((uint) mainCommand.Icon)!);
            }
            MainCommandIcons = mainCommands;

            var generalActions = new Dictionary<uint, TextureWrap>();
            foreach (var action in data.GetExcelSheet<GeneralAction>()!)
            {
                generalActions.Add(action.RowId, data!.GetImGuiTextureHqIcon((uint) action.Icon)!);
            }
            GeneralActionIcons = generalActions;

            var contentTypes = new Dictionary<uint, TextureWrap>();
            foreach (var cType in data.GetExcelSheet<ContentType>()!)
            {
                if (cType.Icon == 0)
                    continue;

                contentTypes.Add(cType.RowId, data!.GetImGuiTextureHqIcon((uint) cType.Icon)!);
            }
            ContentTypeIcons = contentTypes;

            var emotes = new Dictionary<uint, TextureWrap>();
            foreach (var emote in data.GetExcelSheet<Emote>()!)
            {
                var icon = data!.GetImGuiTextureHqIcon((uint)emote.Icon);
                if (icon == null)
                    continue;

                emotes.Add(emote.RowId, icon);
            }
            EmoteIcons = emotes;

            var cjIcons = new Dictionary<uint, TextureWrap>();
            foreach (var classJob in data.GetExcelSheet<ClassJob>()!)
            {
                TextureWrap? icon = null;
                if (classJob.JobIndex != 0)
                {
                    icon = data.GetImGuiTextureHqIcon(062400 + (uint)classJob.JobIndex);
                }
                else
                {
                    var offset = classJob.RowId switch
                    {
                        1 => 1,
                        2 => 2,
                        3 => 3,
                        4 => 4,
                        5 => 5,
                        6 => 6,
                        7 => 7,
                        8 => 10,
                        9 => 11,
                        10 => 12,
                        11 => 13,
                        12 => 14,
                        13 => 15,
                        14 => 16,
                        15 => 17,
                        16 => 18,
                        17 => 19,
                        18 => 20,

                        26 => 8,
                        29 => 9,
                        _ => 0,
                    };

                    icon = data.GetImGuiTextureHqIcon(062300 + (uint) offset);
                }

                if (icon != null)
                    cjIcons.Add(classJob.RowId, icon);
            }
            ClassJobIcons = cjIcons;
            PluginLog.Information(ClassJobIcons.Count + " class jobs loaded.");

            var mountIcons = new Dictionary<uint, TextureWrap>();
            foreach (var mount in data.GetExcelSheet<Mount>()!)
            {
                var icon = data!.GetImGuiTextureHqIcon(mount.Icon);
                if (icon == null)
                    continue;

                mountIcons.Add(mount.RowId, icon);
            }
            MountIcons = mountIcons;
            
            var minionIcons = new Dictionary<uint, TextureWrap>();
            foreach (var minion in data.GetExcelSheet<Companion>()!)
            {
                var icon = data!.GetImGuiTextureHqIcon(minion.Icon);
                if (icon == null)
                    continue;

                minionIcons.Add(minion.RowId, icon);
            }
            MinionIcons = minionIcons;

            AetheryteIcon = data.GetImGuiTextureHqIcon(066417)!;
            WikiIcon = data.GetImGuiTextureHqIcon(066404)!;
            PluginInstallerIcon = data.GetImGuiTextureHqIcon(066472)!;
            LogoutIcon = data.GetImGuiTextureHqIcon(066403)!;
            EmoteIcon = data.GetImGuiTextureHqIcon(066420)!;
            HintIcon = data.GetImGuiTextureHqIcon(066453)!;
            ChatIcon = data.GetImGuiTextureHqIcon(066473)!;
            MathsIcon = data.GetImGuiTextureHqIcon(062409)!;

            this.ExtraIcons = new Dictionary<uint, TextureWrap>();

            GameIcon = FindAnythingPlugin.PluginInterface.UiBuilder.LoadImage(Path.Combine(
                FindAnythingPlugin.PluginInterface.AssemblyLocation.Directory!.FullName, "noses", "Normal.png"));

            ReloadMacroIcons();
        }

        public void ReloadMacroIcons()
        {
            foreach (var macroLink in FindAnythingPlugin.Configuration.MacroLinks)
            {
                EnsureExtraIcon((uint) macroLink.IconId);
            }
        }

        public void EnsureExtraIcon(uint iconId)
        {
            if (this.ExtraIcons.ContainsKey(iconId))
                return;

            var tex = this.data.GetImGuiTextureHqIcon(iconId);

            if (tex != null)
                this.ExtraIcons[iconId] = tex;
        }

        public static TextureCache Load(UiBuilder uiBuilder, DataManager data) => new TextureCache(uiBuilder, data);

        public void Dispose()
        {
            foreach (var icon in MainCommandIcons)
            {
                icon.Value.Dispose();
            }

            foreach (var icon in GeneralActionIcons)
            {
                icon.Value.Dispose();
            }

            foreach (var icon in ContentTypeIcons)
            {
                icon.Value.Dispose();
            }

            foreach (var icon in EmoteIcons)
            {
                icon.Value.Dispose();
            }

            foreach (var icon in this.ExtraIcons)
            {
                icon.Value.Dispose();
            }

            foreach (var icon in this.ClassJobIcons)
            {
                icon.Value.Dispose();
            }
            
            foreach (var icon in this.MountIcons)
            {
                icon.Value.Dispose();
            }

            foreach (var icon in this.MinionIcons)
            {
                icon.Value.Dispose();
            }

            WikiIcon.Dispose();
            AetheryteIcon.Dispose();
            PluginInstallerIcon.Dispose();
            LogoutIcon.Dispose();
            EmoteIcon.Dispose();
            HintIcon.Dispose();
            ChatIcon.Dispose();
            MathsIcon.Dispose();
            GameIcon.Dispose();
        }
    }
}
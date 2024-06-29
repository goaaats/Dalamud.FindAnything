using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;

namespace Dalamud.FindAnything
{
    public class TextureCache
    {
        private readonly ITextureProvider textureProvider;

        /*
        
        
        public IReadOnlyDictionary<uint, ISharedImmediateTexture> ContentTypeIcons { get; }
        public IReadOnlyDictionary<uint, ISharedImmediateTexture> EmoteIcons { get; }
        
        public IReadOnlyDictionary<uint, ISharedImmediateTexture> MountIcons { get; }
        public IReadOnlyDictionary<uint, ISharedImmediateTexture> MinionIcons { get; }
        public IReadOnlyDictionary<uint, ISharedImmediateTexture> FashionAccessoryIcons { get; }
        public IReadOnlyDictionary<uint, ISharedImmediateTexture> CollectionIcons { get; }

        public Dictionary<uint, ISharedImmediateTexture> ExtraIcons { get; }
         */

        public IReadOnlyDictionary<uint, ISharedImmediateTexture> MainCommandIcons { get; }
        public IReadOnlyDictionary<uint, ISharedImmediateTexture> GeneralActionIcons { get; }
        public IReadOnlyDictionary<uint, ISharedImmediateTexture> ClassJobIcons { get; }

        public ISharedImmediateTexture AetheryteIcon { get; }
        public ISharedImmediateTexture WikiIcon { get; }
        public ISharedImmediateTexture PluginInstallerIcon { get; }
        public ISharedImmediateTexture LogoutIcon { get; }
        public ISharedImmediateTexture EmoteIcon { get; }
        public ISharedImmediateTexture HintIcon { get; }
        public ISharedImmediateTexture ChatIcon { get; }
        public ISharedImmediateTexture MathsIcon { get; }
        public ISharedImmediateTexture InnRoomIcon { get; }
        public ISharedImmediateTexture RoulettesIcon { get; }

        public ISharedImmediateTexture GameIcon { get; }
       

        public ISharedImmediateTexture GetIcon(uint iconId)
        {
            return textureProvider.GetFromGameIcon(new GameIconLookup
            {
                HiRes = false,
                IconId = iconId,
            });
        }

        private TextureCache(IDataManager data, ITextureProvider textureProvider)
        {
            this.textureProvider = textureProvider;

            /*
            



            var contentTypes = new Dictionary<uint, IDalamudTextureWrap>();
            foreach (var cType in data.GetExcelSheet<ContentType>()!)
            {
                if (cType.Icon == 0)
                    continue;

                contentTypes.Add(cType.RowId, GetIconTexture(cType.Icon)!);
            }
            ContentTypeIcons = contentTypes;

            var emotes = new Dictionary<uint, IDalamudTextureWrap>();
            foreach (var emote in data.GetExcelSheet<Emote>()!)
            {
                var icon = GetIconTexture(emote.Icon);
                if (icon == null)
                    continue;

                emotes.Add(emote.RowId, icon);
            }
            EmoteIcons = emotes;

            

            var mountIcons = new Dictionary<uint, IDalamudTextureWrap>();
            foreach (var mount in data.GetExcelSheet<Mount>()!)
            {
                var icon = GetIconTexture(mount.Icon);
                if (icon == null)
                    continue;

                mountIcons.Add(mount.RowId, icon);
            }
            MountIcons = mountIcons;

            var minionIcons = new Dictionary<uint, IDalamudTextureWrap>();
            foreach (var minion in data.GetExcelSheet<Companion>()!)
            {
                var icon = GetIconTexture(minion.Icon);
                if (icon == null)
                    continue;

                minionIcons.Add(minion.RowId, icon);
            }
            MinionIcons = minionIcons;

            var fashionAccessoryIcons = new Dictionary<uint, IDalamudTextureWrap>();
            foreach (var ornament in data.GetExcelSheet<Ornament>()!)
            {
                var icon = GetIconTexture(ornament.Icon);
                if (icon == null)
                    continue;

                fashionAccessoryIcons.Add(ornament.RowId, icon);
            }
            FashionAccessoryIcons = fashionAccessoryIcons;

            var collectionIcons = new Dictionary<uint, IDalamudTextureWrap>();
            foreach (var mcGuffin in data.GetExcelSheet<McGuffinUIData>()!)
            {
                var icon = GetIconTexture(mcGuffin.Icon);
                if (icon == null)
                    continue;

                collectionIcons.Add(mcGuffin.RowId, icon);
            }
            CollectionIcons = collectionIcons;
            */
            
            var mainCommands = new Dictionary<uint, ISharedImmediateTexture>();
            foreach (var mainCommand in data.GetExcelSheet<MainCommand>()!)
            {
                mainCommands.Add(mainCommand.RowId, GetIcon((uint) mainCommand.Icon)!);
            }
            MainCommandIcons = mainCommands;
            
            var generalActions = new Dictionary<uint, ISharedImmediateTexture>();
            foreach (var action in data.GetExcelSheet<GeneralAction>()!)
            {
                generalActions.Add(action.RowId, GetIcon((uint) action.Icon)!);
            }
            GeneralActionIcons = generalActions;
            
            var cjIcons = new Dictionary<uint, ISharedImmediateTexture>();
            foreach (var classJob in data.GetExcelSheet<ClassJob>()!)
            {
                ISharedImmediateTexture? icon;
                if (classJob.JobIndex != 0)
                {
                    icon = GetIcon(062400 + (uint)classJob.JobIndex);
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

                    icon = GetIcon(062300 + (uint) offset);
                }

                cjIcons.Add(classJob.RowId, icon);
            }
            ClassJobIcons = cjIcons;
            FindAnythingPlugin.Log.Information(ClassJobIcons.Count + " class jobs loaded.");

            AetheryteIcon = GetIcon(066417);
            WikiIcon = GetIcon(066404);
            PluginInstallerIcon = GetIcon(066472);
            LogoutIcon = GetIcon(066403);
            EmoteIcon = GetIcon(066420);
            HintIcon = GetIcon(066453);
            ChatIcon = GetIcon(066473);
            MathsIcon = GetIcon(062409);
            InnRoomIcon = GetIcon(066419);
            RoulettesIcon = GetIcon(data.GetExcelSheet<ContentType>()!.GetRow(1)!.Icon);

            var gameIconPath = new FileInfo(Path.Combine(
                FindAnythingPlugin.PluginInterface.AssemblyLocation.Directory!.FullName, "noses", "Normal.png"));
            GameIcon = textureProvider.GetFromFile(gameIconPath);
        }
        
        public static TextureCache Load(IDataManager data, ITextureProvider textureProvider) => new(data, textureProvider);
    }
}
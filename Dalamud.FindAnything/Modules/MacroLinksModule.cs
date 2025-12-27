using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using System;
using System.Linq;

namespace Dalamud.FindAnything.Modules;

public sealed class MacroLinksModule : SearchModule
{
    public override Configuration.SearchSetting SearchSetting => Configuration.SearchSetting.MacroLinks;

    public override void Search(SearchContext ctx, Normalizer normalizer, FuzzyMatcher matcher, GameState gameState) {
        var macroLinks = FindAnythingPlugin.Configuration.MacroLinks.AsEnumerable();
        if (FindAnythingPlugin.Configuration.MacroLinksSearchDirection == Configuration.MacroSearchDirection.TopToBottom) {
            macroLinks = macroLinks.Reverse();
        }

        foreach (var macroLink in macroLinks) {
            var score = matcher.Matches(normalizer.Searchable(macroLink.SearchName));
            if (score > 0) {
                ctx.AddResult(new MacroLinkSearchResult {
                    Score = score * Weight,
                    Entry = macroLink,
                });
            }
        }
    }

    private class MacroLinkSearchResult : ISearchResult
    {
        public string CatName => "Macros";
        public string Name => Entry.SearchName.Split(';', StringSplitOptions.TrimEntries).First();
        public ISharedImmediateTexture Icon => FindAnythingPlugin.TexCache.GetIcon((uint)Entry.IconId);
        public required int Score { get; init; }
        public required Configuration.MacroEntry Entry { get; init; }

        public object Key {
            get {
                return Entry.Kind switch {
                    Configuration.MacroEntry.MacroEntryKind.Id => (Entry.SearchName, Entry.Shared, Entry.Id),
                    Configuration.MacroEntry.MacroEntryKind.SingleLine => (Entry.SearchName, Entry.Line),
                    _ => throw new ArgumentOutOfRangeException($"Unknown MacroEntryKind: {Entry.Kind}"),
                };
            }
        }

        public unsafe void Selected() {
            switch (Entry.Kind) {
                case Configuration.MacroEntry.MacroEntryKind.Id:
                    var macro = RaptureMacroModule.Instance()->GetMacro(Entry.Shared ? 1u : 0u, (uint)Entry.Id);
                    // Service.Log.Debug($"Macro: 0x{(IntPtr)macro:X} / name[{macro->Name}] iconId[{macro->IconId}] iconRowId[{macro->MacroIconRowId}]");
                    if (macro->IconId == 0 && macro->MacroIconRowId == 0) {
                        FindAnythingPlugin.Instance.UserError($"Invalid macro: {Entry.Id} ({(Entry.Shared ? "Shared" : "Individual")})");
                        return;
                    }
                    RaptureShellModule.Instance()->ExecuteMacro(macro);
                    break;
                case Configuration.MacroEntry.MacroEntryKind.SingleLine:
                    if (!Entry.Line.StartsWith('/')) {
                        FindAnythingPlugin.Instance.UserError("Invalid slash command:" + Entry.Line);
                        return;
                    }

                    if (Entry.Line.Length > 100) {
                        FindAnythingPlugin.Instance.UserError("Slash command too long:" + Entry.Line);
                        return;
                    }

                    Chat.ExecuteCommand(Entry.Line);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
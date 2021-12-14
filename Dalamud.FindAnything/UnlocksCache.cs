using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using Framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace Dalamud.FindAnything;

public unsafe class UnlocksCache
{
    // E9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B7 57 3C 48 8B C8 E8 ?? ?? ?? ?? 84 C0 75 D9
    [return:MarshalAs(UnmanagedType.U1)]
    private delegate byte IsDutyUnlockedDelegate(uint contentId);

    private readonly IsDutyUnlockedDelegate? _isDutyUnlocked;

    // E8 ?? ?? ?? ?? 84 C0 74 A4
    private delegate byte IsEmoteUnlockedDelegate(UIState* uiState, uint emoteId, byte unk);

    private readonly IsEmoteUnlockedDelegate? _isEmoteUnlocked;
    
    public IReadOnlyList<uint> UnlockedDutyKeys { get; private set; }
    public IReadOnlyList<uint> UnlockedEmoteKeys { get; private set; }
    
    internal bool IsDutyUnlocked(uint contentId) {
        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.LookingForGroup);
        return this._isDutyUnlocked(contentId) > 0;
    }
    
    private UnlocksCache()
    {
        if (FindAnythingPlugin.TargetScanner.TryScanText("E9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B7 57 3C 48 8B C8 E8 ?? ?? ?? ?? 84 C0 75 D9", out var dutyUnlockedPtr)) {
            PluginLog.Information($"dutyUnlockedPtr: {dutyUnlockedPtr:X}");
            this._isDutyUnlocked = Marshal.GetDelegateForFunctionPointer<IsDutyUnlockedDelegate>(dutyUnlockedPtr);
        }
        
        if (FindAnythingPlugin.TargetScanner.TryScanText("E8 ?? ?? ?? ?? 84 C0 74 A4", out var emoteUnlockedPtr)) {
            PluginLog.Information($"emoteUnlockedPtr: {emoteUnlockedPtr:X}");
            this._isEmoteUnlocked = Marshal.GetDelegateForFunctionPointer<IsEmoteUnlockedDelegate>(emoteUnlockedPtr);
        }
    }

    public void Refresh()
    {
        if (this._isDutyUnlocked != null)
        {
            UnlockedDutyKeys = FindAnythingPlugin.Data.GetExcelSheet<ContentFinderCondition>()!.Where(x => IsDutyUnlocked(x.Content)).Select(x => x.RowId).ToList();
        }
        
        if (this._isEmoteUnlocked != null)
        {
            var emotes = new List<uint>();
            foreach (var emote in FindAnythingPlugin.Data.GetExcelSheet<Emote>()!.Where(x => x.Order != 0)) 
            {
                if (emote.UnlockLink == 0 || this._isEmoteUnlocked(UIState.Instance(), emote.UnlockLink, 1) > 0)
                {
                    emotes.Add(emote.RowId);
                }
            }
            UnlockedEmoteKeys = emotes;
        }
        
        PluginLog.LogVerbose($"{UnlockedDutyKeys.Count} duties unlocked.");
        PluginLog.LogVerbose($"{UnlockedEmoteKeys.Count} emotes unlocked.");
    }
    
    public static UnlocksCache Load() => new UnlocksCache();
}
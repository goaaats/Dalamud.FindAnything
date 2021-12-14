using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Logging;
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

    public IReadOnlyList<uint> UnlockedDutyKeys { get; private set; }
    
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
    }

    public void Refresh()
    {
        if (this._isDutyUnlocked != null)
        {
            UnlockedDutyKeys = FindAnythingPlugin.Data.GetExcelSheet<ContentFinderCondition>()!.Where(x => IsDutyUnlocked(x.Content)).Select(x => x.RowId).ToList();
        }
        
        PluginLog.LogVerbose($"{UnlockedDutyKeys.Count} duties unlocked.");
    }
    
    public static UnlocksCache Load() => new UnlocksCache();
}
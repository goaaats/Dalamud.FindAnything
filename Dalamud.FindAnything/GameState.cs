using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Dalamud.FindAnything;

public class GameState
{
    public bool IsInDuty() {
        if (Service.ClientState.TerritoryType == 1055) return false;

        return Service.Condition[ConditionFlag.BoundByDuty] || Service.Condition[ConditionFlag.BoundByDuty56] ||
               Service.Condition[ConditionFlag.BoundByDuty95];
    }

    public unsafe bool IsInExplorerMode() {
        var activeContentDirector = EventFramework.Instance()->DirectorModule.ActiveContentDirector;
        if (activeContentDirector == null) {
            return false;
        }

        return (activeContentDirector->Director.ContentFlags & 1) != 0;
    }

    public unsafe bool IsInLeve() {
        var director = UIState.Instance()->DirectorTodo.Director;
        if (director == null) return false;

        return director->Info.EventId.ContentId is EventHandlerContent.GatheringLeveDirector
            or EventHandlerContent.BattleLeveDirector;
    }

    public bool IsInEvent() {
        return Service.Condition[ConditionFlag.Occupied] ||
               Service.Condition[ConditionFlag.OccupiedInCutSceneEvent];
    }

    public bool IsInCombat() {
        return Service.Condition[ConditionFlag.InCombat];
    }

    public bool IsInCombatDuty() => IsInDuty() && !IsInExplorerMode() && !IsInLeve();
}
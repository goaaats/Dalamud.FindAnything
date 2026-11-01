using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Dalamud.FindAnything;

public class GameState {
    public static bool IsInDuty()
        => Service.Condition.Any(ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95);

    public static unsafe bool IsInExplorerMode() {
        var activeContentDirector = EventFramework.Instance()->DirectorModule.ActiveContentDirector;
        if (activeContentDirector == null) {
            return false;
        }

        return (activeContentDirector->Director.ContentFlags & 1) != 0;
    }

    public static unsafe bool IsInLeve() {
        var director = UIState.Instance()->DirectorTodo.Director;
        if (director == null) return false;

        return director->Info.EventId.ContentId is EventHandlerContent.GatheringLeveDirector
            or EventHandlerContent.BattleLeveDirector;
    }

    public static bool IsInEvent() {
        return Service.Condition[ConditionFlag.Occupied] ||
               Service.Condition[ConditionFlag.OccupiedInCutSceneEvent];
    }

    public static bool IsInCombat() {
        return Service.Condition[ConditionFlag.InCombat];
    }

    public static bool IsInCombatDuty() => IsInDuty() && !IsInExplorerMode() && !IsInLeve();
}

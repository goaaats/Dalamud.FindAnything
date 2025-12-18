using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace Dalamud.FindAnything;

public static class MathAux
{
    public static int GetNeededExpForLevel(short level)
    {
        var paramGrow = FindAnythingPlugin.Data.GetExcelSheet<ParamGrow>();
        if (paramGrow.Count < level)
        {
            return 0;
        }

        return paramGrow.GetRow((uint)level).ExpToNext;
    }

    public static int GetNeededExpForCurrentLevel()
    {
        if (!FindAnythingPlugin.PlayerState.IsLoaded)
            return 0;

        if (!FindAnythingPlugin.PlayerState.IsLoaded)
            return 0;

        return GetNeededExpForLevel(FindAnythingPlugin.PlayerState.Level);
    }

    public static unsafe int GetCurrentExp()
    {
        if (!FindAnythingPlugin.PlayerState.IsLoaded)
            return 0;

        if (FindAnythingPlugin.PlayerState.ClassJob.ValueNullable is not { } classJob)
            return 0;

        return UIState.Instance()->PlayerState.ClassJobExperience[classJob.ExpArrayIndex];
    }

    public static int GetExpLeft()
    {
        if (!FindAnythingPlugin.PlayerState.IsLoaded)
            return 0;

        return GetNeededExpForCurrentLevel() - GetCurrentExp();
    }

    public static int GetLevel()
    {
        if (!FindAnythingPlugin.PlayerState.IsLoaded)
            return 0;

        return FindAnythingPlugin.PlayerState.Level;
    }
}
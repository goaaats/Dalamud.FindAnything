using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.GeneratedSheets;

namespace Dalamud.FindAnything;

public static class MathAux
{
    public static int GetNeededExpForLevel(uint level)
    {
        if (FindAnythingPlugin.ClientState.LocalPlayer == null)
            return 0;

        var paramGrow = FindAnythingPlugin.Data.GetExcelSheet<ParamGrow>()!;
        if (paramGrow.RowCount < level)
        {
            return 0;
        }

        return paramGrow.GetRow(level)!.ExpToNext;
    }

    public static int GetNeededExpForCurrentLevel()
    {
        if (FindAnythingPlugin.ClientState.LocalPlayer == null)
            return 0;

        return GetNeededExpForLevel(FindAnythingPlugin.ClientState.LocalPlayer.Level);
    }

    public static unsafe int GetCurrentExp()
    {
        if (FindAnythingPlugin.ClientState.LocalPlayer == null)
            return 0;

        var cjIndex = FindAnythingPlugin.ClientState.LocalPlayer.ClassJob.GameData.ExpArrayIndex;
        return UIState.Instance()->PlayerState.ClassJobExperience[cjIndex];
    }

    public static int GetExpLeft()
    {
        if (FindAnythingPlugin.ClientState.LocalPlayer == null)
            return 0;

        return GetNeededExpForCurrentLevel() - GetCurrentExp();
    }

    public static int GetLevel()
    {
        if (FindAnythingPlugin.ClientState.LocalPlayer == null)
            return 0;

        return FindAnythingPlugin.ClientState.LocalPlayer.Level;
    }
}
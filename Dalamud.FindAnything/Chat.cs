using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Dalamud.FindAnything;

public static class Chat {
    public static unsafe void ExecuteCommand(string command) {
        using var cmd = new Utf8String(command);
        cmd.SanitizeString((AllowedEntities)0x27F);
        UIModule.Instance()->ProcessChatBoxEntry(&cmd);
    }
}

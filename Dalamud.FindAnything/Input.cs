using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using System.Runtime.InteropServices;

namespace Dalamud.FindAnything;

// Most of this is stolen from QoLBar
public static unsafe class Input {
    private static bool IsGameFocused => !Framework.Instance()->WindowInactive;
    private static bool IsGameTextInputActive => RaptureAtkModule.Instance()->AtkModule.IsTextInputActive();

    public static bool Disabled => IsGameTextInputActive || !IsGameFocused || ImGui.GetIO().WantCaptureKeyboard;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState(byte[] lpKeyState);
    private static readonly byte[] KeyboardState = new byte[256];

    public static void Update() {
        GetKeyboardState(KeyboardState);
    }

    public static bool IsDown(VirtualKey key) => (KeyboardState[(int)key] & 0x80) != 0;
}

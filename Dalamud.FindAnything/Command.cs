using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Dalamud.FindAnything;

[StructLayout(LayoutKind.Explicit)]
[SuppressMessage("ReSharper", "PrivateFieldCanBeConvertedToLocalVariable")]
internal readonly struct ChatPayload : IDisposable 
{
    [FieldOffset(0)]
    private readonly nint textPtr;

    [FieldOffset(16)]
    private readonly ulong textLen;

    [FieldOffset(8)]
    private readonly ulong unk1;

    [FieldOffset(24)]
    private readonly ulong unk2;

    internal ChatPayload(byte[] stringBytes) {
        textPtr = Marshal.AllocHGlobal(stringBytes.Length + 30);
        Marshal.Copy(stringBytes, 0, textPtr, stringBytes.Length);
        Marshal.WriteByte(textPtr + stringBytes.Length, 0);

        textLen = (ulong) (stringBytes.Length + 1);

        unk1 = 64;
        unk2 = 0;
    }

    public void Dispose() {
        Marshal.FreeHGlobal(textPtr);
    }
}

public class Command
{
    private static Command? instance = null;
    public static Command Instance => instance ??= new Command();
    
    private delegate void ProcessChatBoxDelegate(nint uiModule, nint message, nint unused, byte a4);

    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 48 8B F2 48 8B F9 45 84 C9")]
    private readonly ProcessChatBoxDelegate? processChatBox = null!;

    public Command()
    {
        FindAnythingPlugin.GameInteropProvider.InitializeFromAttributes(this);
    }
    
    public unsafe void SendChatUnsafe(string command)
    {
        if (processChatBox == null) {
            throw new InvalidOperationException("Could not find signature for chat sending");
        }

        var uiModule = (nint)UIModule.Instance();

        using var payload = new ChatPayload(Encoding.UTF8.GetBytes($"{command}"));
        var mem1 = Marshal.AllocHGlobal(400);
        Marshal.StructureToPtr(payload, mem1, false);

        processChatBox(uiModule, mem1, nint.Zero, 0);

        Marshal.FreeHGlobal(mem1);
    }
}
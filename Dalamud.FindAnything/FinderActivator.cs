using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Dalamud.FindAnything;

public sealed class FinderActivator : IDisposable
{
    private readonly Finder finder;
    private readonly Input input;
    private IDoubleTapTrigger doubleTapTrigger;

    public FinderActivator(Finder finder) {
        this.finder = finder;
        input = new Input();

        Configure(FindAnythingPlugin.Configuration);
        FindAnythingPlugin.ConfigManager.OnChange += Configure;

        Service.Framework.Update += FrameworkOnUpdate;
    }

    public void Dispose() {
        Service.Framework.Update -= FrameworkOnUpdate;
    }

    [MemberNotNull(nameof(doubleTapTrigger))]
    private void Configure(Configuration config) {
        Service.Log.Debug($"Configuring {nameof(FinderActivator)}");

        var delay = FindAnythingPlugin.Configuration.ShiftShiftDelay;
        var openAction = () => finder.Open();

        doubleTapTrigger = FindAnythingPlugin.Configuration.ShiftShiftUnit switch {
            Configuration.DoubleTapUnit.Frames => new FrameBasedDoubleTapTrigger(delay, openAction),
            Configuration.DoubleTapUnit.Milliseconds => new TimeBasedDoubleTapTrigger(delay, openAction),
            _ => throw new ArgumentOutOfRangeException($"Unknown DoubleTapUnit: {FindAnythingPlugin.Configuration.ShiftShiftUnit}")
        };
    }

    private void FrameworkOnUpdate(IFramework framework) {
        if (Input.Disabled)
            return;

        if (FindAnythingPlugin.Configuration.NotInCombat && Service.Condition[ConditionFlag.InCombat])
            return;

        input.Update();

        if (input.IsDown(VirtualKey.ESCAPE)) {
            finder.Close();
        } else {
            if (finder.IsOpen)
                return;

            switch (FindAnythingPlugin.Configuration.Open) {
                case Configuration.OpenMode.Combo:
                    CheckOpenWithCombo();
                    break;
                case Configuration.OpenMode.ShiftShift:
                    doubleTapTrigger.Update(input.IsDown(FindAnythingPlugin.Configuration.ShiftShiftKey));
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown OpenMode: {FindAnythingPlugin.Configuration.Open}");
            }
        }
    }

    private void CheckOpenWithCombo() {
        var config = FindAnythingPlugin.Configuration;

        var mod = config.ComboModifier == VirtualKey.NO_KEY || input.IsDown(config.ComboModifier);
        var mod2 = config.ComboModifier2 == VirtualKey.NO_KEY || input.IsDown(config.ComboModifier2);
        var key = config.ComboKey == VirtualKey.NO_KEY || input.IsDown(config.ComboKey);

        var wiki = config.WikiComboKey != VirtualKey.NO_KEY && input.IsDown(config.WikiComboKey);

        if (mod && mod2 && key) {
            if (wiki) {
                finder.Open(openToWiki: true);
            } else {
                finder.Open();
            }

            // We do not skip these even if finderOpen is true since we need to cancel keys still held after open
            if (config.PreventPassthrough) {
                UnsetKey(config.ComboModifier);
                UnsetKey(config.ComboModifier2);
                UnsetKey(config.ComboKey);

                if (wiki)
                    UnsetKey(config.WikiComboKey);
            }
        }
    }

    private static void UnsetKey(VirtualKey key) {
        if ((int)key <= 0 || (int)key >= 240)
            return;

        Service.Keys[key] = false;
    }
}
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Dalamud.FindAnything;

public sealed class FinderActivator : IDisposable
{
    private readonly Finder finder;
    private IDoubleTapDetector doubleTapDetector;

    public FinderActivator(Finder finder) {
        this.finder = finder;

        Configure(FindAnythingPlugin.Configuration);
        FindAnythingPlugin.ConfigManager.OnChange += Configure;

        Service.Framework.Update += FrameworkOnUpdate;
    }

    public void Dispose() {
        Service.Framework.Update -= FrameworkOnUpdate;
    }

    [MemberNotNull(nameof(doubleTapDetector))]
    private void Configure(Configuration config) {
        Service.Log.Debug($"Configuring {nameof(FinderActivator)}");

        var delay = FindAnythingPlugin.Configuration.ShiftShiftDelay;

        doubleTapDetector = FindAnythingPlugin.Configuration.ShiftShiftUnit switch {
            Configuration.DoubleTapUnit.Frames => new FrameBasedDoubleTapDetector(delay),
            Configuration.DoubleTapUnit.Milliseconds => new TimeBasedDoubleTapDetector(delay),
            _ => throw new ArgumentOutOfRangeException($"Unknown DoubleTapUnit: {FindAnythingPlugin.Configuration.ShiftShiftUnit}")
        };
    }

    private void FrameworkOnUpdate(IFramework framework) {
        if (Input.Disabled)
            return;

        Input.Update();

        if (Input.IsDown(VirtualKey.ESCAPE)) {
            finder.Close();
            return;
        }

        // Always do this even if the finder is closed or unopenable, because CheckOpenWithCombo needs to prevent input passthrough
        var openType = FindAnythingPlugin.Configuration.Open switch {
            Configuration.OpenMode.Combo => CheckOpenWithCombo(),
            Configuration.OpenMode.ShiftShift => CheckOpenWithDoubleTap(),
            _ => throw new ArgumentOutOfRangeException($"Unknown OpenMode: {FindAnythingPlugin.Configuration.Open}")
        };

        if (FindAnythingPlugin.Configuration.NotInCombat && Service.Condition[ConditionFlag.InCombat])
            return;

        if (openType == OpenAction.Normal) {
            finder.Open();
        } else if (openType == OpenAction.Wiki) {
            finder.Open(openToWiki: true);
        }
    }

    private OpenAction CheckOpenWithCombo() {
        var config = FindAnythingPlugin.Configuration;

        var mod = config.ComboModifier == VirtualKey.NO_KEY || Input.IsDown(config.ComboModifier);
        var mod2 = config.ComboModifier2 == VirtualKey.NO_KEY || Input.IsDown(config.ComboModifier2);
        var key = config.ComboKey == VirtualKey.NO_KEY || Input.IsDown(config.ComboKey);
        var wiki = config.WikiComboKey != VirtualKey.NO_KEY && Input.IsDown(config.WikiComboKey);

        if (mod && mod2 && key) {
            if (config.PreventPassthrough) {
                // Service.Log.Debug("Preventing passthrough...");
                UnsetKey(config.ComboModifier);
                UnsetKey(config.ComboModifier2);
                UnsetKey(config.ComboKey);

                if (wiki)
                    UnsetKey(config.WikiComboKey);
            }

            return wiki ? OpenAction.Wiki : OpenAction.Normal;
        }

        return OpenAction.None;
    }

    private OpenAction CheckOpenWithDoubleTap() {
        if (finder.IsOpen)
            return OpenAction.None;

        return doubleTapDetector.Update(Input.IsDown(FindAnythingPlugin.Configuration.ShiftShiftKey))
            ? OpenAction.Normal
            : OpenAction.None;
    }

    private enum OpenAction
    {
        None,
        Normal,
        Wiki,
    }

    private static void UnsetKey(VirtualKey key) {
        // Service.Log.Debug($"Unsetting key {key}...");
        if ((int)key <= 0 || (int)key >= 240) {
            // Service.Log.Debug($"  Skipped");
            return;
        }

        // var oldValue = Service.Keys[key];
        Service.Keys[key] = false;
        // Service.Log.Debug($"  Unset ({oldValue} -> {Service.Keys[key]})");
    }
}
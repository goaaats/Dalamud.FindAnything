using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using ImGuiScene;

namespace Dalamud.FindAnything.Game;

public class GameWindow : Window, IDisposable
{
    public enum NoseKind
    {
        Normal,
        Farmer,
        Robo,
        Weird,
        Agent,
        CEO,
        Thancred,
        Magical,
        Eternity,
        End
    }

    private readonly IReadOnlyDictionary<NoseKind, uint> noseCosts = new Dictionary<NoseKind, uint>
    {
        { NoseKind.Normal, 15 },
        { NoseKind.Farmer, 500 },
        { NoseKind.Robo, 3000 },
        { NoseKind.Weird, 13000 },
        { NoseKind.Agent, 30000 },
        { NoseKind.CEO, 150000 },
        { NoseKind.Thancred, 450000 },
        { NoseKind.Magical, 600000 },
        { NoseKind.Eternity, 1000000 },
        { NoseKind.End, 2500000 },
    };

    private readonly IReadOnlyDictionary<NoseKind, string> noseDesc = new Dictionary<NoseKind, string>
    {
        { NoseKind.Normal, "Your normal, cute and loyal dog." },
        { NoseKind.Farmer, "These dogs like to get their paws dirty." },
        { NoseKind.Robo, "Automated DNs, delivered by drone strike." },
        { NoseKind.Weird, "???" },
        { NoseKind.Agent, "Licensed to sniff. (This dog will protect you from thieves - but there's a chance he won't survive it!)" },
        { NoseKind.CEO, "They know how to lead!" },
        { NoseKind.Thancred, "This is dognose." },
        { NoseKind.Magical, "Dog Nose Power, Make-Up!" },
        { NoseKind.Eternity, "bB0W B3F=RE Th§M (This dog transcended reality - it will generate DNs, even when DN Farm is closed)" },
        { NoseKind.End, "From the deepest pits of the universe, they have come to bring you the final DN." },
    };

    private readonly IReadOnlyDictionary<NoseKind, float> nosePassiveEarningPerSecond = new Dictionary<NoseKind, float>
    {
        { NoseKind.Normal, 0.1f },
        { NoseKind.Farmer, 1f },
        { NoseKind.Robo, 3f },
        { NoseKind.Weird, 7f },
        { NoseKind.Agent, 12f },
        { NoseKind.CEO, 20f },
        { NoseKind.Thancred, 28f },
        { NoseKind.Magical, 40f },
        { NoseKind.Eternity, 10f },
        { NoseKind.End, 999f },
    };

    private const double ETERNITY_DN_PER_HOUR = 20;

    public enum FarmUpgrade
    {
        Base,
        FarmHouse,
        TempleOfDog,
        Casino,
        Restaurant,
        SpaceCentre,
        ThemePark,
        SingularityReactor,
        Cinema,
        DimensionalGate,
    }

    private static readonly IReadOnlyDictionary<FarmUpgrade, float> farmUpgradeCost = new Dictionary<FarmUpgrade, float>
    {
        { FarmUpgrade.Base, 0f },
        { FarmUpgrade.FarmHouse, 10000f },
        { FarmUpgrade.TempleOfDog, 49000f },
        { FarmUpgrade.Casino, 100000f },
        { FarmUpgrade.Restaurant, 220000f },
        { FarmUpgrade.SpaceCentre, 500000f },
        { FarmUpgrade.ThemePark, 1224500f },
        { FarmUpgrade.SingularityReactor, 2000000f },
        { FarmUpgrade.Cinema, 2500000f },
        { FarmUpgrade.DimensionalGate, 10000000f },
    };

    private static readonly IReadOnlyDictionary<FarmUpgrade, int> farmUpgradeCap = new Dictionary<FarmUpgrade, int>
    {
        { FarmUpgrade.Base, 100 },
        { FarmUpgrade.FarmHouse, 150 },
        { FarmUpgrade.TempleOfDog, 225 },
        { FarmUpgrade.Casino, 400 },
        { FarmUpgrade.Restaurant, 600 },
        { FarmUpgrade.SpaceCentre, 720 },
        { FarmUpgrade.ThemePark, 1000 },
        { FarmUpgrade.SingularityReactor, 2000 },
        { FarmUpgrade.Cinema, 2500 },
        { FarmUpgrade.DimensionalGate, 999999 },
    };

    private static readonly IReadOnlyDictionary<FarmUpgrade, string> farmUpgradeName = new Dictionary<FarmUpgrade, string>
    {
        { FarmUpgrade.Base, "BASE UPGRADE" },
        { FarmUpgrade.FarmHouse, "Farm House" },
        { FarmUpgrade.TempleOfDog, "Temple of Dog" },
        { FarmUpgrade.Casino, "Dog City Casino" },
        { FarmUpgrade.Restaurant, "Doggy Dognose's Pizza" },
        { FarmUpgrade.SpaceCentre, "DN Space Agency" },
        { FarmUpgrade.ThemePark, "Puppy Resort" },
        { FarmUpgrade.SingularityReactor, "Singularity Reactor" },
        { FarmUpgrade.Cinema, "Dog Nose Cinema" },
        { FarmUpgrade.DimensionalGate, "Dogmensional Gate" },
    };

    private struct Bonus
    {
        public float Multiplier { get; set; }
        public string Name { get; set; }
        public float Cost { get; set; }
    }

    private readonly IReadOnlyDictionary<uint, Bonus> bonuses = new Dictionary<uint, Bonus>
    {
        { 1, new Bonus
        {
            Name = "Squeaky Toy Dispenser",
            Multiplier = .1f,
            Cost = 200_000,
        }},
        { 2, new Bonus
        {
            Name = "HUGE Frisbee",
            Multiplier = .5f,
            Cost = 2500_000,
        }},
        { 3, new Bonus
        {
            Name = "Scooby Snacks",
            Multiplier = .2f,
            Cost = 500_000,
        }},
        { 4, new Bonus
        {
            Name = "Water Hose + Pool set",
            Multiplier = .1f,
            Cost = 700_000,
        }},
        { 5, new Bonus
        {
            Name = "Endless supply of Premium Dog Food",
            Multiplier = .3f,
            Cost = 1500_000,
        }},
    };

    private Dictionary<NoseKind, TextureWrap> noseTextures;
    private TextureWrap thiefTexture;
    private TextureWrap clerkNeedsTexture;
    private TextureWrap clerkBoutiqueTexture;
    private TextureWrap clerkBuilderTexture;
    private TextureWrap sageTexture;

    public class SimulationState
    {
        public DateTimeOffset LastSaved { get; set; }

        public ulong TotalSteps { get; set; }
        public double CurrentDn { get; set; }
        public double TotalEarned { get; set; }
        
        public int GottemCount { get; set; }

        public Dictionary<NoseKind, ulong> NumNoses { get; set; }
        public List<uint> RewardsGained { get; set; }
        public List<uint> BonusesGained { get; set; }

        public FarmUpgrade FarmUpgrade { get; set; }
        public bool UpgradePurchased { get; set; }
        public DateTimeOffset UpgradeFinishesAt { get; set; }
        public int FarmCap => farmUpgradeCap[this.FarmUpgrade];
        public ulong DogCount => this.NumNoses.Aggregate(0ul, (acc, kvp) => acc + kvp.Value);

        public bool GameComplete => this.NumNoses.TryGetValue(NoseKind.End, out var cnt) && cnt > 0;
    }

    public static SimulationState GameState;

    private const string SomethingPlaySoundSig = "E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? FE C2 ";
    private delegate void PlaySoundDelegate(IntPtr path, byte shit);

    private PlaySoundDelegate _playSoundFun;

    public GameWindow() : base("DN Farm###dnwindow")
    {
        var assetPath = FindAnythingPlugin.PluginInterface.AssemblyLocation.Directory!.FullName;

        noseTextures = new Dictionary<NoseKind, TextureWrap>();
        foreach (var noseKind in Enum.GetValues<NoseKind>())
        {
            var path = Path.Combine(assetPath, "noses", noseKind + ".png");
            noseTextures.Add(noseKind, FindAnythingPlugin.PluginInterface.UiBuilder.LoadImage(path));
        }

        thiefTexture = FindAnythingPlugin.PluginInterface.UiBuilder.LoadImage(Path.Combine(assetPath, "noses", "Thief.png"));
        clerkNeedsTexture = FindAnythingPlugin.PluginInterface.UiBuilder.LoadImage(Path.Combine(assetPath, "noses", "ClerkNeeds.png"));
        clerkBoutiqueTexture = FindAnythingPlugin.PluginInterface.UiBuilder.LoadImage(Path.Combine(assetPath, "noses", "ClerkBoutique.png"));
        clerkBuilderTexture = FindAnythingPlugin.PluginInterface.UiBuilder.LoadImage(Path.Combine(assetPath, "noses", "ClerkBuilder.png"));
        sageTexture = FindAnythingPlugin.PluginInterface.UiBuilder.LoadImage(Path.Combine(assetPath, "noses", "Sage.png"));

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 200),
            MaximumSize = new Vector2(1000, 800),
        };

        try
        {
            var addr = FindAnythingPlugin.TargetScanner.ScanText(SomethingPlaySoundSig);
            _playSoundFun = Marshal.GetDelegateForFunctionPointer<PlaySoundDelegate>(addr);
        }
        catch(Exception e)
        {
            PluginLog.Error(e, "Failed to find play sound function");
        }

        Load();
    }

    public void PlayMyTurn()
    {
        if (_playSoundFun == null)
            return;

        var textBytes = Encoding.ASCII.GetBytes("sound/voice/Vo_Line/8202048_en.scd");
        var ptr = Marshal.AllocHGlobal(textBytes.Length + 1);
        Marshal.Copy(textBytes, 0, ptr, textBytes.Length);
        Marshal.WriteByte(ptr + textBytes.Length, 0);
        _playSoundFun(ptr, 1);
    }

    public void Load()
    {
        if (FindAnythingPlugin.Configuration.SimulationState == null)
        {
            NewGame();
        }
        else
        {
            GameState = FindAnythingPlugin.Configuration.SimulationState;
        }
    }

    public void NewGame()
    {
        GameState = new SimulationState();
        GameState.NumNoses = new Dictionary<NoseKind, ulong>();
        GameState.RewardsGained = new List<uint>();
        GameState.BonusesGained = new List<uint>();
        GameState.CurrentDn = 15;
        GameState.FarmUpgrade = FarmUpgrade.Base;

        this.thiefActive = false;
        this.thiefMessageDismissed = true;

        FindAnythingPlugin.Configuration.SimulationState = GameState;
        FindAnythingPlugin.Configuration.Save();
    }

    private void EarnRestedDn()
    {
        var timeSinceSave = DateTimeOffset.Now - GameState.LastSaved;
        var numHoursSpent = Math.Floor(timeSinceSave.TotalHours);
        PluginLog.Verbose($"{numHoursSpent} hours since last save");
        if (numHoursSpent > 0 && GameState.NumNoses.TryGetValue(NoseKind.Eternity, out var numEternityDogs))
        {
            var earnedRestedDn = (ETERNITY_DN_PER_HOUR * numHoursSpent) * numEternityDogs;
            GameState.CurrentDn += earnedRestedDn;
            FindAnythingPlugin.PluginInterface.UiBuilder.AddNotification($"You earned {earnedRestedDn:N0} DN from resting for {numHoursSpent} hours.", "DN Farm", NotificationType.Info, 10000);
        }
    }

    public override void OnOpen()
    {
        saidNoToSage = false;
        clicks = 0;
        EarnRestedDn();
        base.OnOpen();
    }

    public override void Update()
    {
        Simulate();
        WindowName = $"DN Farm ({GameState.CurrentDn:N0} DN)";

        if (thiefActive)
        {
            WindowName += " (Thief!!!!!)";
        }

        WindowName += "###dnwindow";
    }

    private double GetDps()
    {
        double dps = 0;
        foreach (var kind in Enum.GetValues<NoseKind>())
        {
            if (GameState.NumNoses.TryGetValue(kind, out var num))
            {
                dps += nosePassiveEarningPerSecond[kind] * num;
            }
        }

        return dps;
    }

    private const int AGENT_THIEF_PROTECT_DEATH_CHANCE = 80;

    private bool CheckAgentThiefProtect()
    {
        if (GameState.NumNoses.TryGetValue(NoseKind.Agent, out var num) && num > 1)
        {
            if (this.random.Next(1, 100) <= AGENT_THIEF_PROTECT_DEATH_CHANCE)
            {
                GameState.NumNoses[NoseKind.Agent] = num - 1;
            }

            return true;
        }

        return false;
    }

    private double GetAdjustedCost(NoseKind kind)
    {
        var baseCost = noseCosts[kind];
        if (!GameState.NumNoses.TryGetValue(kind, out var num))
            return baseCost;

        var multiplier = 1.0;
        multiplier += num * (kind == NoseKind.Normal ? 0.02 : 0.018);

        return baseCost * multiplier;
    }

    private string GetDogName(NoseKind kind) => kind != NoseKind.Thancred ? kind.ToString() + " Dog" : "Dogcred";

    private float GetMultiplier() => GameState.BonusesGained.Select(x => bonuses[x].Multiplier).Aggregate(1f, (x, y) => x + y);

    private Random random = new();

    private double thiefStolenDn = 0;
    private bool thiefActive = false;
    private bool thiefMessageDismissed = true;
    private float thiefWillSteal;
    private DateTimeOffset thiefWillStealAt;

    private void Simulate()
    {
        GameState.TotalSteps++;

        var dps = GetDps();
        var fps = ImGui.GetIO().Framerate;
        var earned = dps / fps;

        earned *= GetMultiplier();

        GameState.CurrentDn += earned;
        GameState.TotalEarned += earned;

        if (GameState.UpgradePurchased && DateTimeOffset.Now > GameState.UpgradeFinishesAt)
        {
            GameState.UpgradePurchased = false;
            GameState.FarmUpgrade++;
        }

        if (GameState.TotalSteps % 1000 == 0)
        {
            if (!this.thiefActive && this.random.Next(0, 300) < 5 && !GameState.GameComplete)
            {
                this.thiefWillSteal = random.Next(1, 70) / 100f;
                this.thiefActive = true;
                this.thiefMessageDismissed = false;
                this.thiefWillStealAt = DateTimeOffset.Now.AddSeconds(random.Next(8, 15));
                PluginLog.Information($"[DN] Thief triggered! Steals: {this.thiefWillSteal} at {this.thiefWillStealAt}");
            }

            GameState.LastSaved = DateTimeOffset.Now;
            FindAnythingPlugin.Configuration.SimulationState = GameState;
            FindAnythingPlugin.Configuration.Save();
        }
    }

    public void Cheat()
    {
        GameState.CurrentDn = GameState.TotalEarned = 1000000f;
    }

    private int clicks = 0;
    private bool saidNoToSage = false;

    public override void Draw()
    {
        ImGui.TextUnformatted($"{GameState.CurrentDn:N2} DN");
        ImGui.SameLine();
        ImGui.Image(noseTextures[NoseKind.Normal].ImGuiHandle, new Vector2(16, 16));
        if (ImGui.IsItemClicked())
        {
            GameState.CurrentDn += 0.1;
            GameState.TotalEarned += 0.1;
        }
        ImGui.SameLine();
        ImGuiHelpers.ScaledDummy(0, 5);
        ImGui.SameLine();
        if (GameState.BonusesGained.Count > 0)
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey, $"({GetDps():N2}/s x {GetMultiplier():N2})");
        }
        else
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey, $"({GetDps():N2}/s)");
        }

        var numDogs = GameState.DogCount;
        var dogCap = (ulong)GameState.FarmCap;
        ImGui.TextColored(ImGuiColors.DalamudGrey, $"Your farm has space for {numDogs}/{dogCap} dogs.");

        if (GameState.GottemCount > 0)
        {
            ImGui.TextColored(new Vector4((float) random.NextDouble(), (float) random.NextDouble(), (float) random.NextDouble(), 1), "Gottem! - " + GameState.GottemCount + " times");
        }

        ImGuiHelpers.ScaledDummy(5);

        if (ImGui.BeginTabBar("###noseTabs"))
        {
            if (ImGui.BeginTabItem("Farm"))
            {
                if (ImGui.BeginChild("NoseShop", new Vector2(-1, 500 * ImGuiHelpers.GlobalScale), false))
                {
                    var windowSize = ImGui.GetWindowSize();

                    var didAny = false;
                    var lastHadDn = NoseKind.Normal;
                    foreach (var kind in Enum.GetValues<NoseKind>())
                    {
                        var cost = GetAdjustedCost(kind);
                        var name = GetDogName(kind);
                        if (GameState.NumNoses.TryGetValue(kind, out var num))
                        {
                            didAny = true;
                            var desc = noseDesc[kind];
                            var cursorStart = ImGui.GetCursorPos();

                            ImGui.Text($"{name}");
                            ImGui.SameLine();
                            ImGuiHelpers.ScaledDummy(0, 5);
                            ImGui.SameLine();
                            ImGui.TextColored(ImGuiColors.DalamudGrey, $"x{num}");

                            ImGui.TextColored(ImGuiColors.DalamudGrey, desc);

                            var buyText = $"Buy {name} ({cost:N0} DN)";
                            if (GameState.CurrentDn >= cost && numDogs < dogCap)
                            {
                                if (ImGui.Button(buyText))
                                {
                                    if (FindAnythingPlugin.Keys[VirtualKey.SHIFT])
                                    {
                                        for (var i = 0; i < 10; i++)
                                        {
                                            BuyDog(kind);
                                        }
                                    }
                                    else
                                    {
                                        clicks++;
                                        BuyDog(kind);
                                    }

                                    if (kind == NoseKind.Thancred)
                                        PlayMyTurn();
                                }
                            }
                            else
                            {
                                ImGuiComponents.DisabledButton(buyText);
                            }

                            ImGui.SameLine();

                            var btnReleaseText = $"Release from duty###release{kind}";
                            if (num > 1)
                            {
                                if (ImGui.Button(btnReleaseText))
                                {
                                    GameState.NumNoses[kind]--;
                                }
                            }
                            else
                            {
                                ImGuiComponents.DisabledButton(btnReleaseText);
                            }

                            cursorStart.X = windowSize.X - 64 - 30;
                            ImGui.SetCursorPos(cursorStart);
                            ImGui.Image(noseTextures[kind].ImGuiHandle, new Vector2(64, 64) * ImGuiHelpers.GlobalScale);

                            lastHadDn = kind;

                            ImGui.Separator();
                        }
                        else if (lastHadDn == kind - 1 && kind != NoseKind.Normal && didAny)
                        {
                            var btnText = $"Buy your first {GetDogName(kind)} ({noseCosts[kind]} DN)";
                            if (GameState.CurrentDn >= cost && numDogs < dogCap)
                            {
                                if (ImGui.Button(btnText))
                                {
                                    BuyDog(kind);

                                    if (kind == NoseKind.Thancred)
                                        PlayMyTurn();
                                }
                            }
                            else
                            {
                                ImGuiComponents.DisabledButton(btnText);
                            }

                            if (clicks >= 50)
                            {
                                ImGui.SameLine();
                                ImGui.TextColored(ImGuiColors.DalamudGrey, "Only pro dogs know this: You can hold SHIFT to buy 10 at once!");
                            }

                            ImGui.Separator();
                        }
                    }

                    if (!didAny)
                    {
                        if (ImGui.Button($"Buy your first dog ({noseCosts[NoseKind.Normal]} DN)"))
                        {
                            BuyDog(NoseKind.Normal);
                        }
                    }
                }

                ImGui.EndChild();

                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(10);

                if (GameState.GameComplete)
                {
                    var name = "my love";
                    if (FindAnythingPlugin.ClientState.LocalPlayer != null)
                        name = FindAnythingPlugin.ClientState.LocalPlayer.Name.TextValue.Split()[0];

                    ImGui.Image(this.noseTextures[NoseKind.Magical].ImGuiHandle, new Vector2(128, 128) * ImGuiHelpers.GlobalScale);
                    ImGui.SameLine();
                    ImGui.TextWrapped($"\"Peace has returned to the DN hills.\nEveryone is living happily.\nYou did it.\nThank you, {name}.\"");
                }
                else if (this.thiefActive)
                {
                    ImGui.Image(this.thiefTexture.ImGuiHandle, new Vector2(128, 128) * ImGuiHelpers.GlobalScale);
                    ImGui.SameLine();

                    var timeLeft = this.thiefWillStealAt - DateTimeOffset.Now;
                    if (timeLeft.TotalSeconds > 0)
                    {
                        ImGui.SameLine();
                        ImGui.TextWrapped($"Oh no! A thief is here to rob you!\nYou have to stop it!\n\nYou have {timeLeft.TotalSeconds:N0} seconds left until he'll get away!");
                    }
                    else
                    {
                        if (CheckAgentThiefProtect())
                        {
                            this.thiefActive = false;
                            this.thiefMessageDismissed = true;
                            this.thiefWillSteal = 0;
                        }
                        else
                        {
                            var steals = GameState.CurrentDn * this.thiefWillSteal;
                            GameState.CurrentDn -= steals;
                            this.thiefStolenDn = steals;
                            this.thiefActive = false;
                        }
                    }

                    ImGuiHelpers.ScaledDummy(5);

                    if (ImGui.Button("Call the cops"))
                    {
                        this.thiefActive = false;
                        this.thiefMessageDismissed = true;
                        this.thiefWillSteal = 0;
                    }
                }
                else if (!this.thiefActive && !this.thiefMessageDismissed)
                {
                    ImGui.Image(this.thiefTexture.ImGuiHandle, new Vector2(128, 128) * ImGuiHelpers.GlobalScale);
                    ImGui.SameLine();
                    ImGui.TextWrapped($"Oh no! A thief has robbed your farm and you didn't catch it!\nYou lost {this.thiefStolenDn:N0} DN, that's a lot...");

                    if (ImGui.Button("OK"))
                    {
                        this.thiefMessageDismissed = true;
                    }
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
                    ImGui.TextWrapped("Keep your eyes peeled! A thief may appear...");
                    ImGui.PopStyleColor();
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("City"))
            {
                if (ImGui.CollapsingHeader("DN Construction Co."))
                {
                    ImGui.Image(this.clerkBuilderTexture.ImGuiHandle, new Vector2(64, 64) * ImGuiHelpers.GlobalScale);
                    ImGui.SameLine();

                    if (GameState.UpgradePurchased)
                    {
                        var minutesLeft = (GameState.UpgradeFinishesAt - DateTimeOffset.Now).TotalMinutes;
                        ImGui.TextWrapped($"\"Working on it! We'll be done in {minutesLeft:N0} minutes.\"");
                        ImGui.SetNextItemWidth(10);
                    }
                    else if (GameState.FarmUpgrade != FarmUpgrade.DimensionalGate)
                    {
                        ImGui.TextWrapped($"\"Hey, what's up. Need a bigger farm?\nYou'll be able to get more dogs if you upgrade it.\"");

                        ImGuiHelpers.ScaledDummy(5);

                        var nextUpgrade = GameState.FarmUpgrade + 1;
                        var nextUpgradeName = farmUpgradeName[nextUpgrade];
                        var nextUpgradeCost = farmUpgradeCost[nextUpgrade];
                        var btnText = $"Buy Farm Upgrade: {nextUpgradeName} ({nextUpgradeCost:N0} DN)";
                        if (GameState.CurrentDn >= nextUpgradeCost)
                        {
                            if (ImGui.Button(btnText))
                            {
                                GameState.CurrentDn -= nextUpgradeCost;
                                GameState.UpgradePurchased = true;
                                GameState.UpgradeFinishesAt = DateTimeOffset.Now + TimeSpan.FromMinutes(random.Next(3, 14));
                            }
                        }
                        else
                        {
                            ImGuiComponents.DisabledButton(btnText);
                        }
                    }
                    else
                    {
                        ImGui.TextWrapped($"\"Sorry, your farm is as big as it gets.\nNothing I can do, we don't have the permit to make it any bigger!\"");
                    }
                }

                ImGuiHelpers.ScaledDummy(10);

                if (ImGui.CollapsingHeader("DN Needs & Utilities"))
                {
                    ImGui.Image(this.clerkNeedsTexture.ImGuiHandle, new Vector2(64, 64) * ImGuiHelpers.GlobalScale);
                    ImGui.SameLine();
                    ImGui.TextWrapped($"\"Hi, welcome to DN Needs & Utilities. Here you can buy stuff that your dogs will love.\nWhat can I get you?\"");

                    ImGuiHelpers.ScaledDummy(5);

                    foreach (var bonus in bonuses)
                    {
                        var available = !GameState.BonusesGained.Contains(bonus.Key) && GameState.CurrentDn >= bonus.Value.Cost;
                        var btnText = $"Buy {bonus.Value.Name} ({bonus.Value.Cost:N0} DN)";
                        if (available)
                        {
                            if (ImGui.Button(btnText))
                            {
                                GameState.CurrentDn -= bonus.Value.Cost;
                                GameState.BonusesGained.Add(bonus.Key);
                            }
                        }
                        else
                        {
                            ImGuiComponents.DisabledButton(btnText);
                        }

                        ImGui.SameLine();
                        ImGui.TextColored(ImGuiColors.DalamudGrey, $"This will increase DN gain by {bonus.Value.Multiplier:N2}");
                    }
                }

                ImGuiHelpers.ScaledDummy(10);

                if (ImGui.CollapsingHeader("DN Boutique"))
                {
                    ImGui.Image(this.clerkBoutiqueTexture.ImGuiHandle, new Vector2(64, 64) * ImGuiHelpers.GlobalScale);
                    ImGui.SameLine();

                    if (!GameState.GameComplete)
                    {
                        ImGui.TextWrapped($"\"The DN boutique is still under renovation, we're very sorry!\nPlease come back when you cleared the game, we should be done by then!\"");
                    }
                    else
                    {
                        ImGui.TextWrapped($"\"Hello! Welcome to the DN Boutique!\nWe offer a wide variety of handcrafted DN accessories to improve your living space!\"");

                        ImGuiHelpers.ScaledDummy(5);

                        foreach (var reward in GameRewards.Rewards)
                        {
                            var btnText = $"Buy {reward.Value.Name} ({reward.Value.Cost:N0} DN)";
                            if (GameState.CurrentDn >= reward.Value.Cost && !GameState.RewardsGained.Contains(reward.Key))
                            {
                                if (ImGui.Button(btnText))
                                {
                                    reward.Value.Bought();
                                    GameState.CurrentDn -= reward.Value.Cost;

                                    if (reward.Key != 0x02)
                                    {
                                        GameState.RewardsGained.Add(reward.Key);
                                    }
                                }
                            }
                            else
                            {
                                ImGuiComponents.DisabledButton(btnText);
                            }
                        }
                    }
                }

                ImGuiHelpers.ScaledDummy(10);

                if (!saidNoToSage && ImGui.CollapsingHeader("DN Sage"))
                {
                    ImGui.Image(this.sageTexture.ImGuiHandle, new Vector2(64, 64) * ImGuiHelpers.GlobalScale);
                    ImGui.SameLine();
                    ImGui.TextWrapped($"\"If you are weary of this world, you may start anew.\nConsider your life a journey, and you will find your way.\nAltogether, you have earned {GameState.TotalEarned:N0} DN.\n\nIs this what you want?\"");

                    if (ImGui.Button("Yes"))
                    {
                        IsOpen = false;
                        NewGame();
                    }

                    ImGui.SameLine();

                    if (ImGui.Button("No"))
                    {
                        saidNoToSage = true;
                    }
                }

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void BuyDog(NoseKind kind)
    {
        var cost = GetAdjustedCost(kind);
        if (GameState.CurrentDn >= cost && GameState.DogCount < (uint)GameState.FarmCap)
        {
            GameState.CurrentDn -= cost;

            if (!GameState.NumNoses.ContainsKey(kind))
            {
                GameState.NumNoses.Add(kind, 1);
            }
            else
            {
                GameState.NumNoses[kind]++;
            }
        }
    }

    public void Dispose()
    {
        foreach (var noseTexture in noseTextures)
        {
            noseTexture.Value.Dispose();
        }

        thiefTexture.Dispose();
        clerkNeedsTexture.Dispose();
        clerkBoutiqueTexture.Dispose();
        clerkBuilderTexture.Dispose();
        sageTexture.Dispose();
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
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
        { NoseKind.Agent, "Licensed to sniff." },
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
        { NoseKind.Eternity, 45f },
        { NoseKind.End, 999f },
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
            Cost = 1500_000,
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
    private TextureWrap sageTexture;

    public class SimulationState
    {
        public DateTimeOffset LastSaved { get; set; }

        public ulong TotalSteps { get; set; }
        public double CurrentDn { get; set; }
        public double TotalEarned { get; set; }

        public Dictionary<NoseKind, ulong> NumNoses { get; set; }
        public List<uint> RewardsGained { get; set; }
        public List<uint> BonusesGained { get; set; }

        public bool GameComplete => this.NumNoses.TryGetValue(NoseKind.End, out var cnt) && cnt > 0;
    }

    private SimulationState state;

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
        sageTexture = FindAnythingPlugin.PluginInterface.UiBuilder.LoadImage(Path.Combine(assetPath, "noses", "Sage.png"));

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 200),
            MaximumSize = new Vector2(1000, 800),
        };

        Load();
    }

    public void Load()
    {
        if (FindAnythingPlugin.Configuration.SimulationState == null)
        {
            NewGame();
        }
        else
        {
            state = FindAnythingPlugin.Configuration.SimulationState;
        }
    }

    public void NewGame()
    {
        state = new SimulationState();
        state.NumNoses = new Dictionary<NoseKind, ulong>();
        state.RewardsGained = new List<uint>();
        state.BonusesGained = new List<uint>();
        state.CurrentDn = 15;

        this.thiefActive = false;
        this.thiefMessageDismissed = true;

        FindAnythingPlugin.Configuration.SimulationState = state;
        FindAnythingPlugin.Configuration.Save();
    }

    public override void OnOpen()
    {
        saidNoToSage = false;
        clicks = 0;
        base.OnOpen();
    }

    public override void Update()
    {
        Simulate();
        WindowName = $"DN Farm ({this.state.CurrentDn:N0} DN)";

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
            if (state.NumNoses.TryGetValue(kind, out var num))
            {
                dps += nosePassiveEarningPerSecond[kind] * num;
            }
        }

        return dps;
    }

    private float GetMultiplier() => this.state.BonusesGained.Select(x => bonuses[x].Multiplier).Aggregate(1f, (x, y) => x + y);

    private Random random = new();

    private double thiefStolenDn = 0;
    private bool thiefActive = false;
    private bool thiefMessageDismissed = true;
    private float thiefWillSteal;
    private DateTimeOffset thiefWillStealAt;

    private void Simulate()
    {
        state.TotalSteps++;

        var dps = GetDps();
        var fps = ImGui.GetIO().Framerate;
        var earned = dps / fps;

        earned *= GetMultiplier();

        this.state.CurrentDn += earned;
        this.state.TotalEarned += earned;

        if (this.state.TotalSteps % 1000 == 0)
        {
            if (!this.thiefActive && this.random.Next(0, 300) < 5 && !this.state.GameComplete)
            {
                this.thiefWillSteal = random.Next(1, 70) / 100f;
                this.thiefActive = true;
                this.thiefMessageDismissed = false;
                this.thiefWillStealAt = DateTimeOffset.Now.AddSeconds(random.Next(8, 15));
                PluginLog.Information($"[DN] Thief triggered! Steals: {this.thiefWillSteal} at {this.thiefWillStealAt}");
            }

            this.state.LastSaved = DateTimeOffset.Now;
            FindAnythingPlugin.Configuration.SimulationState = state;
            FindAnythingPlugin.Configuration.Save();
        }
    }

    public void Cheat()
    {
        this.state.CurrentDn = this.state.TotalEarned = 1000000f;
    }

    private int clicks = 0;
    private bool saidNoToSage = false;

    public override void Draw()
    {
        ImGui.TextUnformatted($"{state.CurrentDn:N2} DN");
        ImGui.SameLine();
        ImGui.Image(noseTextures[NoseKind.Normal].ImGuiHandle, new Vector2(16, 16));
        if (ImGui.IsItemClicked())
        {
            state.CurrentDn += 0.1;
            state.TotalEarned += 0.1;
        }
        ImGui.SameLine();
        ImGuiHelpers.ScaledDummy(0, 5);
        ImGui.SameLine();
        if (this.state.BonusesGained.Count > 0)
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey, $"({GetDps():N2}/s x {GetMultiplier():N2})");
        }
        else
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey, $"({GetDps():N2}/s)");
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
                        var cost = noseCosts[kind];
                        if (state.NumNoses.TryGetValue(kind, out var num))
                        {
                            didAny = true;
                            var desc = noseDesc[kind];
                            var cursorStart = ImGui.GetCursorPos();

                            ImGui.Text($"{kind} Dog");
                            ImGui.SameLine();
                            ImGuiHelpers.ScaledDummy(0, 5);
                            ImGui.SameLine();
                            ImGui.TextColored(ImGuiColors.DalamudGrey, $"x{num}");

                            ImGui.TextColored(ImGuiColors.DalamudGrey, desc);

                            var buyText = $"Buy {kind} Dog ({cost} DN)";
                            if (state.CurrentDn >= cost)
                            {
                                if (ImGui.Button($"Buy {kind} Dog ({cost} DN)"))
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
                                }
                            }
                            else
                            {
                                ImGuiComponents.DisabledButton(buyText);
                            }

                            var lastCursorPos = ImGui.GetCursorPos();
                            cursorStart.X = windowSize.X - 64 - 30;
                            ImGui.SetCursorPos(cursorStart);
                            ImGui.Image(noseTextures[kind].ImGuiHandle, new Vector2(64, 64) * ImGuiHelpers.GlobalScale);

                            lastHadDn = kind;

                            ImGui.Separator();
                        }
                        else if (lastHadDn == kind - 1 && kind != NoseKind.Normal && didAny)
                        {
                            var btnText = $"Buy your first {kind} Dog ({noseCosts[kind]} DN)";
                            if (this.state.CurrentDn >= cost)
                            {
                                if (ImGui.Button(btnText))
                                {
                                    BuyDog(kind);
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

                if (this.state.GameComplete)
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
                        var steals = this.state.CurrentDn * this.thiefWillSteal;
                        this.state.CurrentDn -= steals;
                        this.thiefStolenDn = steals;
                        this.thiefActive = false;
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
                if (ImGui.CollapsingHeader("DN Needs & Utilities"))
                {
                    ImGui.Image(this.clerkNeedsTexture.ImGuiHandle, new Vector2(64, 64) * ImGuiHelpers.GlobalScale);
                    ImGui.SameLine();
                    ImGui.TextWrapped($"\"Hi, welcome to DN Needs & Utilities. Here you can buy stuff that your dogs will love.\nWhat can I get you?\"");

                    ImGuiHelpers.ScaledDummy(5);

                    foreach (var bonus in bonuses)
                    {
                        var available = !this.state.BonusesGained.Contains(bonus.Key) && this.state.CurrentDn >= bonus.Value.Cost;
                        var btnText = $"Buy {bonus.Value.Name} ({bonus.Value.Cost:N0} DN)";
                        if (available)
                        {
                            if (ImGui.Button(btnText))
                            {
                                state.CurrentDn -= bonus.Value.Cost;
                                state.BonusesGained.Add(bonus.Key);
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

                if (ImGui.CollapsingHeader("DN Boutique"))
                {
                    ImGui.Image(this.clerkBoutiqueTexture.ImGuiHandle, new Vector2(64, 64) * ImGuiHelpers.GlobalScale);
                    ImGui.SameLine();

                    if (!this.state.GameComplete)
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
                            if (state.CurrentDn >= reward.Value.Cost && !state.RewardsGained.Contains(reward.Key))
                            {
                                if (ImGui.Button(btnText))
                                {
                                    reward.Value.Bought();
                                    state.CurrentDn -= reward.Value.Cost;
                                    state.RewardsGained.Add(reward.Key);
                                }
                            }
                            else
                            {
                                ImGuiComponents.DisabledButton(btnText);
                            }
                        }
                    }
                }

                if (!saidNoToSage && ImGui.CollapsingHeader("DN Sage"))
                {
                    ImGui.Image(this.sageTexture.ImGuiHandle, new Vector2(64, 64) * ImGuiHelpers.GlobalScale);
                    ImGui.SameLine();
                    ImGui.TextWrapped($"\"If you are weary of this world, you may start anew.\nConsider your life a journey, and you will find your way.\nAltogether, you have earned {state.TotalEarned:N0} DN.\n\nIs this what you want?\"");

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
        var cost = noseCosts[kind];
        if (state.CurrentDn >= cost)
        {
            state.CurrentDn -= cost;

            if (!state.NumNoses.ContainsKey(kind))
            {
                state.NumNoses.Add(kind, 1);
            }
            else
            {
                state.NumNoses[kind]++;
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
        sageTexture.Dispose();
    }
}
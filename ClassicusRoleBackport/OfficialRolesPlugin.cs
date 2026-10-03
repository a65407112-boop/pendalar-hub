using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using HarmonyLib;
using UnityEngine;

namespace ClassicUs.OfficialRolesBackport
{
    [BepInPlugin(Guid, "Classic Us Official Roles Backport", Version)]
    [BepInDependency(ManactorPlugin.Guid)]
    [BepInDependency(ManuAPIPlugin.Guid)]
    public sealed class OfficialRolesPlugin : BasePlugin
    {
        public const string Guid = "classicus.officialroles.backport";
        public const string ModName = "ClassicUsOfficialRolesBackport";
        public const string Version = "0.1.0";

        public static ManualLogSource Log;

        public override void Load()
        {
            Log = base.Log;
            RoleSettings.Init(Config);

            ManactorAPI.Register(ModName, Version);

            // Every networked system owns named, host-authoritative Manactor RPCs.
            ManactorAPI.RegisterRpcMethods(typeof(NoisemakerSystem));
            ManactorAPI.RegisterRpcMethods(typeof(DetectiveSystem));
            ManactorAPI.RegisterRpcMethods(typeof(JudgeSystem));
            ManactorAPI.RegisterRpcMethods(typeof(PhantomSystem));
            ManactorAPI.RegisterRpcMethods(typeof(ShapeshifterSystem));
            ManactorAPI.RegisterRpcMethods(typeof(ViperSystem));
            ManactorAPI.RegisterRpcMethods(typeof(GhostRoleSystem));
            ManactorAPI.RegisterRpcMethods(typeof(GuardianAngelSystem));
            ManactorAPI.RegisterRpcMethods(typeof(InfluencerSystem));

            // Newest official roles go first so they are not starved in small lobbies.
            RoleRegistry.RegisterVirtual(new ViperRole());
            RoleRegistry.RegisterVirtual(new PhantomRole());
            RoleRegistry.RegisterVirtual(new ShapeshifterRole());

            RoleRegistry.RegisterVirtual(new JudgeRole());
            RoleRegistry.RegisterVirtual(new DetectiveRole());
            RoleRegistry.RegisterVirtual(new NoisemakerRole());
            RoleRegistry.RegisterVirtual(new TrackerRole());
            RoleRegistry.RegisterVirtual(new ScientistRole());
            RoleRegistry.RegisterVirtual(new EngineerRole());

            GameEvents.BeforeMurder += GuardianAngelSystem.OnBeforeMurder;
            GameEvents.AfterMurder += OnAfterMurder;
            GameEvents.TaskCompleted += OnTaskCompleted;
            GameEvents.AtMeeting += e => JudgeSystem.OnMeeting(e.Meeting);
            GameEvents.AfterMeeting += _ => JudgeSystem.AfterMeeting();
            GameEvents.GameStarted += _ => RuntimeState.ResetForGame();
            GameEvents.GameEnded += _ => RuntimeState.ResetForGame();
            ManactorAPI.OnPlayerDied += GhostRoleSystem.OnPlayerDied;

            ModBadgeAPI.RegisterLoadedModBadge("Official Roles", Version, new Color(0.55f, 0.8f, 1f, 1f));
            ModBadgeAPI.RegisterPrelobbyTag("Official Roles Backport", "#8CCBFF");

            new Harmony(Guid).PatchAll(typeof(OfficialRolesPlugin).Assembly);

            Log.LogInfo("Official Roles Backport loaded: Scientist, Engineer, Tracker, Noisemaker, Detective, Judge, " +
                        "Guardian Angel, Influencer, Shapeshifter, Phantom, Viper.");
        }

        private static void OnAfterMurder(MurderEventArgs e)
        {
            try { NoisemakerSystem.OnMurder(e); } catch (Exception ex) { Log.LogError("Noisemaker: " + ex); }
            try { DetectiveSystem.OnMurder(e); } catch (Exception ex) { Log.LogError("Detective: " + ex); }
            try { ViperSystem.OnMurder(e); } catch (Exception ex) { Log.LogError("Viper: " + ex); }
        }

        private static void OnTaskCompleted(TaskCompletedEventArgs e)
        {
            try { ScientistSystem.OnTask(e.Player); } catch (Exception ex) { Log.LogError("Scientist recharge: " + ex); }
            try { JudgeSystem.OnTask(e.Player); } catch (Exception ex) { Log.LogError("Judge tasks: " + ex); }
        }
    }

    internal static class RuntimeState
    {
        public static void ResetForGame()
        {
            try { RoleAbilities.Reset(); } catch { }
            try { Overlay.Reset(); } catch { }
            try { TargetHighlight.Clear(); } catch { }
            try { ScientistSystem.Reset(); } catch { }
            try { TrackerSystem.Reset(); } catch { }
            try { DetectiveSystem.Reset(); } catch { }
            try { JudgeSystem.Reset(); } catch { }
            try { PhantomSystem.Reset(); } catch { }
            try { ShapeshifterSystem.Reset(); } catch { }
            try { ViperSystem.Reset(); } catch { }
            try { GhostRoleSystem.Reset(); } catch { }
            try { GuardianAngelSystem.Reset(); } catch { }
            try { InfluencerSystem.Reset(); } catch { }
            try { RoleIntroSystem.Reset(); } catch { }
        }
    }

    internal static class RoleIntroSystem
    {
        private static string _shownRole;

        public static void Tick()
        {
            var p = PlayerControl.LocalPlayer;
            if (p?.Data == null || p.Data.IsDead) return;

            string id = null;
            string title = null;
            string text = null;

            if (RoleChecks.Is(p, RoleIds.Judge)) { id = RoleIds.Judge; title = "JUDGE"; text = "Complete tasks, then Overrule one meeting."; }
            else if (RoleChecks.Is(p, RoleIds.Detective)) { id = RoleIds.Detective; title = "DETECTIVE"; text = "Investigate the active case and interrogate suspects."; }
            else if (RoleChecks.Is(p, RoleIds.Noisemaker)) { id = RoleIds.Noisemaker; title = "NOISEMAKER"; text = "If you die, the living are alerted."; }
            else if (RoleChecks.Is(p, RoleIds.Tracker)) { id = RoleIds.Tracker; title = "TRACKER"; text = "Track another living player."; }
            else if (RoleChecks.Is(p, RoleIds.Scientist)) { id = RoleIds.Scientist; title = "SCIENTIST"; text = "Check vitals from anywhere; tasks recharge battery."; }
            else if (RoleChecks.Is(p, RoleIds.Engineer)) { id = RoleIds.Engineer; title = "ENGINEER"; text = "Use vents as a Crewmate."; }
            else if (RoleChecks.Is(p, RoleIds.Viper)) { id = RoleIds.Viper; title = "VIPER"; text = "Your victims' bodies dissolve."; }
            else if (RoleChecks.Is(p, RoleIds.Phantom)) { id = RoleIds.Phantom; title = "PHANTOM"; text = "Turn invisible for a limited time."; }
            else if (RoleChecks.Is(p, RoleIds.Shapeshifter)) { id = RoleIds.Shapeshifter; title = "SHAPESHIFTER"; text = "Disguise yourself as another player."; }

            if (id == null || id == _shownRole) return;
            _shownRole = id;
            Overlay.Show("RoleIntro", title + "\n" + text, 6f, 0.7f, 2.15f);
        }

        public static void Reset() => _shownRole = null;
    }
}

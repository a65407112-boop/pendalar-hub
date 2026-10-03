using System;
using HarmonyLib;

namespace ClassicUs.OfficialRolesBackport
{
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.FixedUpdate))]
    internal static class HudManager_FixedUpdate_OfficialRolesPatch
    {
        private static void Postfix(HudManager __instance)
        {
            try { RoleAbilities.Tick(__instance); }
            catch (Exception e) { OfficialRolesPlugin.Log.LogError("RoleAbilities.Tick: " + e); }

            try { ScientistSystem.Tick(); } catch (Exception e) { OfficialRolesPlugin.Log.LogError("Scientist.Tick: " + e); }
            try { TrackerSystem.Tick(); } catch (Exception e) { OfficialRolesPlugin.Log.LogError("Tracker.Tick: " + e); }
            try { DetectiveSystem.Tick(); } catch (Exception e) { OfficialRolesPlugin.Log.LogError("Detective.Tick: " + e); }
            try { JudgeSystem.Tick(); } catch (Exception e) { OfficialRolesPlugin.Log.LogError("Judge.Tick: " + e); }
            try { PhantomSystem.Tick(); } catch (Exception e) { OfficialRolesPlugin.Log.LogError("Phantom.Tick: " + e); }
            try { ShapeshifterSystem.Tick(); } catch (Exception e) { OfficialRolesPlugin.Log.LogError("Shapeshifter.Tick: " + e); }
            try { ViperSystem.Tick(); } catch (Exception e) { OfficialRolesPlugin.Log.LogError("Viper.Tick: " + e); }
            try { GuardianAngelSystem.Tick(); } catch (Exception e) { OfficialRolesPlugin.Log.LogError("Guardian.Tick: " + e); }
            try { InfluencerSystem.Tick(); } catch (Exception e) { OfficialRolesPlugin.Log.LogError("Influencer.Tick: " + e); }
            try { RoleIntroSystem.Tick(); } catch (Exception e) { OfficialRolesPlugin.Log.LogError("RoleIntro.Tick: " + e); }
            try { Overlay.Tick(); } catch (Exception e) { OfficialRolesPlugin.Log.LogError("Overlay.Tick: " + e); }
        }
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
    internal static class HudManager_Start_OfficialRolesPatch
    {
        private static void Prefix()
        {
            try
            {
                RoleAbilities.Reset();
                Overlay.Reset();
                TargetHighlight.Clear();
            }
            catch (Exception e)
            {
                OfficialRolesPlugin.Log.LogError("HUD reset: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerPhysics), "HandleAnimation")]
    internal static class PlayerPhysics_HandleAnimation_OfficialRolesPatch
    {
        private static void Postfix(PlayerPhysics __instance)
        {
            try { PhantomSystem.Reapply(__instance != null ? __instance.myPlayer : null); }
            catch (Exception e) { OfficialRolesPlugin.Log.LogError("Phantom.Reapply: " + e); }
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    internal static class MeetingHud_Start_OfficialRolesPatch
    {
        private static void Prefix()
        {
            // Active transformations/invisibility end when a meeting starts, matching modern-role pacing.
            try { PhantomSystem.Reset(); } catch { }
            try { ShapeshifterSystem.Reset(); } catch { }
            try { ScientistSystem.Reset(); } catch { }
            try { TrackerSystem.Reset(); } catch { }
            try { RoleAbilities.Reset(); } catch { }
        }
    }
}

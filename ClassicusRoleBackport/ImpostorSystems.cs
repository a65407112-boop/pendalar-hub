using System;
using System.Collections.Generic;
using ClassicUs.Manactor;
using UnityEngine;

namespace ClassicUs.OfficialRolesBackport
{
    internal static class PhantomSystem
    {
        private const string RequestRpc = "classicus.officialroles.PhantomRequest";
        private const string StartRpc = "classicus.officialroles.PhantomStart";
        private static readonly Dictionary<byte, float> EndsAt = new();

        public static bool IsInvisible(PlayerControl p) =>
            p != null && p.Data != null && EndsAt.ContainsKey(p.Data.PlayerId);

        public static void Request()
        {
            var local = PlayerControl.LocalPlayer;
            if (local?.Data == null) return;
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                Start(local.Data.PlayerId, 10f, true);
            else
                ManactorAPI.SendRpcMethod(RequestRpc, local.Data.PlayerId);
        }

        [ManactorRpc(RequestRpc)]
        private static void OnRequest(byte senderId, byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost || senderId != playerId) return;
            var p = Players.Find(playerId);
            if (!RoleChecks.Is(p, RoleIds.Phantom) || p.Data.IsDead) return;
            Start(playerId, 10f, true);
        }

        [ManactorRpc(StartRpc)]
        private static void OnStart(byte senderId, byte playerId, float seconds)
        {
            if (!NetworkAuth.IsFromHost(senderId)) return;
            Start(playerId, seconds, false);
        }

        private static void Start(byte playerId, float seconds, bool broadcast)
        {
            EndsAt[playerId] = Time.time + seconds;
            Apply(Players.Find(playerId), true);
            if (broadcast) ManactorAPI.SendRpcMethod(StartRpc, playerId, seconds);
        }

        public static void Tick()
        {
            if (EndsAt.Count == 0) return;
            List<byte> expired = null;
            foreach (var kv in EndsAt)
            {
                var p = Players.Find(kv.Key);
                if (Time.time >= kv.Value || p == null || p.Data == null || p.Data.IsDead)
                    (expired ??= new List<byte>()).Add(kv.Key);
                else
                    Apply(p, true);
            }

            if (expired == null) return;
            foreach (var id in expired)
            {
                EndsAt.Remove(id);
                Apply(Players.Find(id), false);
            }
        }

        public static void Reapply(PlayerControl player)
        {
            if (IsInvisible(player)) Apply(player, true);
        }

        private static void Apply(PlayerControl player, bool hidden)
        {
            if (player == null) return;
            if (player.MyPhysics != null && player.MyPhysics.rend != null) player.MyPhysics.rend.enabled = !hidden;
            if (player.HatRenderer != null) player.HatRenderer.SetEnabled(!hidden);
            if (player.nameText != null) player.nameText.gameObject.SetActive(!hidden);
            if (player.CurrentPet != null) player.CurrentPet.Visible = !hidden;
        }

        public static void Reset()
        {
            foreach (var id in new List<byte>(EndsAt.Keys)) Apply(Players.Find(id), false);
            EndsAt.Clear();
        }
    }

    internal sealed class ShiftState
    {
        public byte ShifterId;
        public byte TargetId;
        public float EndsAt;
    }

    internal static class ShapeshifterSystem
    {
        private const string RequestRpc = "classicus.officialroles.ShiftRequest";
        private const string StartRpc = "classicus.officialroles.ShiftStart";
        private const string EndRpc = "classicus.officialroles.ShiftEnd";
        private static readonly Dictionary<byte, ShiftState> States = new();

        public static void Request()
        {
            var local = PlayerControl.LocalPlayer;
            var target = Players.NearestLiving(local, 4f);
            if (local?.Data == null || target?.Data == null)
            {
                Overlay.Show("ShiftMessage", "SHAPESHIFTER: no target nearby", 2f);
                return;
            }

            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                Resolve(local.Data.PlayerId, target.Data.PlayerId);
            else
                ManactorAPI.SendRpcMethod(RequestRpc, local.Data.PlayerId, target.Data.PlayerId);
        }

        [ManactorRpc(RequestRpc)]
        private static void OnRequest(byte senderId, byte shifterId, byte targetId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost || senderId != shifterId) return;
            Resolve(shifterId, targetId);
        }

        private static void Resolve(byte shifterId, byte targetId)
        {
            var shifter = Players.Find(shifterId);
            var target = Players.Find(targetId);
            if (!RoleChecks.Is(shifter, RoleIds.Shapeshifter) || shifter?.Data == null || target?.Data == null ||
                shifter.Data.IsDead || target.Data.IsDead) return;

            ManactorAPI.SendRpcMethod(StartRpc, shifterId, targetId, 15f);
            Start(shifterId, targetId, 15f);
        }

        [ManactorRpc(StartRpc)]
        private static void OnStart(byte senderId, byte shifterId, byte targetId, float duration)
        {
            if (!NetworkAuth.IsFromHost(senderId)) return;
            Start(shifterId, targetId, duration);
        }

        private static void Start(byte shifterId, byte targetId, float duration)
        {
            States[shifterId] = new ShiftState { ShifterId = shifterId, TargetId = targetId, EndsAt = Time.time + duration };
            ApplyLook(Players.Find(shifterId), Players.Find(targetId));
        }

        public static void Tick()
        {
            if (States.Count == 0) return;
            List<byte> expired = null;
            foreach (var kv in States)
            {
                var state = kv.Value;
                var shifter = Players.Find(state.ShifterId);
                var target = Players.Find(state.TargetId);
                if (Time.time >= state.EndsAt || shifter == null || shifter.Data == null || shifter.Data.IsDead || target == null)
                    (expired ??= new List<byte>()).Add(kv.Key);
                else
                    ApplyLook(shifter, target);
            }

            if (expired == null) return;
            foreach (var id in expired)
            {
                if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                    ManactorAPI.SendRpcMethod(EndRpc, id);
                Restore(Players.Find(id));
                States.Remove(id);
            }
        }

        [ManactorRpc(EndRpc)]
        private static void OnEnd(byte senderId, byte shifterId)
        {
            if (!NetworkAuth.IsFromHost(senderId)) return;
            Restore(Players.Find(shifterId));
            States.Remove(shifterId);
        }

        private static void ApplyLook(PlayerControl shifter, PlayerControl target)
        {
            if (shifter?.Data == null || target?.Data == null) return;
            try
            {
                shifter.RawSetColor(target.Data.ColorId);
                shifter.RawSetName(target.Data.PlayerName);
                shifter.RawSetHat(target.Data.HatId);
                shifter.RawSetSkin(target.Data.SkinId);
                shifter.RawSetPet(target.Data.PetId);
            }
            catch (Exception e)
            {
                OfficialRolesPlugin.Log.LogError("Shapeshifter ApplyLook failed: " + e);
            }
        }

        private static void Restore(PlayerControl shifter)
        {
            if (shifter?.Data == null) return;
            try
            {
                shifter.RawSetColor(shifter.Data.ColorId);
                shifter.RawSetName(shifter.Data.PlayerName);
                shifter.RawSetHat(shifter.Data.HatId);
                shifter.RawSetSkin(shifter.Data.SkinId);
                shifter.RawSetPet(shifter.Data.PetId);
            }
            catch (Exception e)
            {
                OfficialRolesPlugin.Log.LogError("Shapeshifter restore failed: " + e);
            }
        }

        public static void Reset()
        {
            foreach (var state in States.Values) Restore(Players.Find(state.ShifterId));
            States.Clear();
        }
    }

    internal sealed class DissolveState
    {
        public byte VictimId;
        public float Start;
        public float Duration;
    }

    internal static class ViperSystem
    {
        private const string StartRpc = "classicus.officialroles.ViperDissolve";
        private static readonly Dictionary<byte, DissolveState> States = new();

        public static void OnMurder(ClassicUs.ManuAPI.MurderEventArgs e)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost || e?.Killer?.Data == null || e.Target?.Data == null) return;
            if (!RoleChecks.Is(e.Killer, RoleIds.Viper)) return;

            byte victim = e.Target.Data.PlayerId;
            ManactorAPI.SendRpcMethod(StartRpc, victim, 12f);
            Start(victim, 12f);
        }

        [ManactorRpc(StartRpc)]
        private static void OnStart(byte senderId, byte victimId, float duration)
        {
            if (!NetworkAuth.IsFromHost(senderId)) return;
            Start(victimId, duration);
        }

        private static void Start(byte victimId, float duration)
        {
            States[victimId] = new DissolveState { VictimId = victimId, Start = Time.time, Duration = duration };
        }

        public static void Tick()
        {
            if (States.Count == 0) return;
            List<byte> done = null;
            foreach (var kv in States)
            {
                var s = kv.Value;
                DeadBody body = null;
                foreach (var b in UnityEngine.Object.FindObjectsOfType<DeadBody>())
                {
                    if (b != null && b.ParentId == s.VictimId) { body = b; break; }
                }

                float t = Mathf.Clamp01((Time.time - s.Start) / Mathf.Max(0.1f, s.Duration));
                if (body != null && body.MyRend != null)
                {
                    var c = body.MyRend.color;
                    float alpha = t < 0.33f ? 1f : (t < 0.66f ? 0.62f : Mathf.Lerp(0.35f, 0f, (t - 0.66f) / 0.34f));
                    body.MyRend.color = new Color(Mathf.Lerp(c.r, 0.35f, t), Mathf.Lerp(c.g, 1f, t * 0.45f),
                        Mathf.Lerp(c.b, 0.2f, t), alpha);
                }

                if (t >= 1f)
                {
                    if (body != null) UnityEngine.Object.Destroy(body.gameObject);
                    (done ??= new List<byte>()).Add(kv.Key);
                }
            }

            if (done != null) foreach (var id in done) States.Remove(id);
        }

        public static void Reset() => States.Clear();
    }
}

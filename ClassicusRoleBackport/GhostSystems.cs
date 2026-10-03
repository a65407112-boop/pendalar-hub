using System;
using System.Collections.Generic;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using UnityEngine;

namespace ClassicUs.OfficialRolesBackport
{
    internal enum GhostRoleKind : byte
    {
        None = 0,
        GuardianAngel = 1,
        Influencer = 2
    }

    internal static class GhostRoleSystem
    {
        private const string AssignRpc = "classicus.officialroles.AssignGhostRole";
        private static readonly Dictionary<byte, GhostRoleKind> Roles = new();
        private static int _guardianCount;
        private static int _influencerCount;

        public static bool Is(PlayerControl p, GhostRoleKind role) =>
            p != null && p.Data != null && Roles.TryGetValue(p.Data.PlayerId, out var r) && r == role;

        public static void OnPlayerDied(byte playerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;

            var p = Players.Find(playerId);
            if (p == null || p.Data == null || !p.Data.IsDead || !RoleChecks.IsCrew(p)) return;
            if (Roles.ContainsKey(playerId)) return;

            var candidates = new List<GhostRoleKind>();
            if (_influencerCount < RoleSettings.Count(RoleSettings.Influencer) &&
                UnityEngine.Random.Range(0f, 100f) < RoleSettings.Chance(RoleSettings.Influencer))
                candidates.Add(GhostRoleKind.Influencer);
            if (_guardianCount < RoleSettings.Count(RoleSettings.GuardianAngel) &&
                UnityEngine.Random.Range(0f, 100f) < RoleSettings.Chance(RoleSettings.GuardianAngel))
                candidates.Add(GhostRoleKind.GuardianAngel);

            if (candidates.Count == 0) return;
            var selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            Assign(playerId, selected, true);
        }

        private static void Assign(byte playerId, GhostRoleKind role, bool broadcast)
        {
            Roles[playerId] = role;
            if (role == GhostRoleKind.GuardianAngel) _guardianCount++;
            if (role == GhostRoleKind.Influencer) _influencerCount++;

            if (broadcast) ManactorAPI.SendRpcMethod(AssignRpc, playerId, (byte)role);

            var local = PlayerControl.LocalPlayer;
            if (local?.Data != null && local.Data.PlayerId == playerId)
            {
                string name = role == GhostRoleKind.GuardianAngel ? "GUARDIAN ANGEL" : "INFLUENCER";
                string sub = role == GhostRoleKind.GuardianAngel ? "Protect a living Crewmate." : "Send image clues to the living.";
                Overlay.Show("GhostRoleIntro", name + "\n" + sub, 6f, 0.8f, 2.2f);
            }
        }

        [ManactorRpc(AssignRpc)]
        private static void OnAssign(byte senderId, byte playerId, byte role)
        {
            if (!NetworkAuth.IsFromHost(senderId)) return;
            Assign(playerId, (GhostRoleKind)role, false);
        }

        public static void Reset()
        {
            Roles.Clear();
            _guardianCount = 0;
            _influencerCount = 0;
        }
    }

    internal sealed class ShieldState
    {
        public byte TargetId;
        public float EndsAt;
    }

    internal static class GuardianAngelSystem
    {
        private const string RequestRpc = "classicus.officialroles.GuardianRequest";
        private const string StartRpc = "classicus.officialroles.GuardianStart";
        private const string EndRpc = "classicus.officialroles.GuardianEnd";
        private static readonly Dictionary<byte, ShieldState> Shields = new();

        public static bool CanProtect()
        {
            var local = PlayerControl.LocalPlayer;
            return GhostRoleSystem.Is(local, GhostRoleKind.GuardianAngel) && local.Data != null && local.Data.IsDead &&
                   Players.NearestLiving(local, 5f) != null;
        }

        public static void Request()
        {
            var local = PlayerControl.LocalPlayer;
            var target = Players.NearestLiving(local, 5f);
            if (local?.Data == null || target?.Data == null) return;

            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                Resolve(local.Data.PlayerId, target.Data.PlayerId);
            else
                ManactorAPI.SendRpcMethod(RequestRpc, local.Data.PlayerId, target.Data.PlayerId);
        }

        [ManactorRpc(RequestRpc)]
        private static void OnRequest(byte senderId, byte angelId, byte targetId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost || senderId != angelId) return;
            Resolve(angelId, targetId);
        }

        private static void Resolve(byte angelId, byte targetId)
        {
            var angel = Players.Find(angelId);
            var target = Players.Find(targetId);
            if (!GhostRoleSystem.Is(angel, GhostRoleKind.GuardianAngel) || angel?.Data == null || !angel.Data.IsDead ||
                target?.Data == null || target.Data.IsDead) return;

            ManactorAPI.SendRpcMethod(StartRpc, targetId, 10f);
            Start(targetId, 10f);
        }

        [ManactorRpc(StartRpc)]
        private static void OnStart(byte senderId, byte targetId, float duration)
        {
            if (!NetworkAuth.IsFromHost(senderId)) return;
            Start(targetId, duration);
        }

        private static void Start(byte targetId, float duration)
        {
            Shields[targetId] = new ShieldState { TargetId = targetId, EndsAt = Time.time + duration };
            var local = PlayerControl.LocalPlayer;
            if (local?.Data != null && local.Data.PlayerId == targetId)
                Overlay.Show("GuardianProtected", "GUARDIAN ANGEL PROTECTED YOU", duration, 1.4f, 1.7f);
        }

        public static void OnBeforeMurder(MurderEventArgs e)
        {
            if (e?.Target?.Data == null) return;
            byte id = e.Target.Data.PlayerId;
            if (!Shields.TryGetValue(id, out var s) || Time.time >= s.EndsAt) return;

            e.Cancelled = true;
            Shields.Remove(id);

            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                ManactorAPI.SendRpcMethod(EndRpc, id, true);

            if (e.Target.AmOwner)
                Overlay.Show("GuardianBlock", "GUARDIAN ANGEL BLOCKED A KILL", 4f, 1.2f, 1.8f);
        }

        [ManactorRpc(EndRpc)]
        private static void OnEnd(byte senderId, byte targetId, bool consumed)
        {
            if (!NetworkAuth.IsFromHost(senderId)) return;
            Shields.Remove(targetId);
            var local = PlayerControl.LocalPlayer;
            if (consumed && local?.Data != null && local.Data.PlayerId == targetId)
                Overlay.Show("GuardianBlock", "GUARDIAN ANGEL BLOCKED A KILL", 4f, 1.2f, 1.8f);
        }

        public static void Tick()
        {
            if (Shields.Count == 0) return;
            List<byte> expired = null;
            foreach (var kv in Shields)
                if (Time.time >= kv.Value.EndsAt)
                    (expired ??= new List<byte>()).Add(kv.Key);
            if (expired == null) return;

            foreach (var id in expired)
            {
                Shields.Remove(id);
                if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                    ManactorAPI.SendRpcMethod(EndRpc, id, false);
            }
        }

        public static void Reset() => Shields.Clear();
    }

    internal static class InfluencerSystem
    {
        private const string MessageRpc = "classicus.officialroles.InfluencerMessage";
        private static readonly string[] Cards =
        {
            "VENT", "BODY", "RED", "BLUE", "GREEN", "TASK", "EYE", "RUN", "LEFT", "RIGHT", "MEETING", "DANGER"
        };

        private static bool _composing;
        private static readonly byte[] Picks = new byte[3];
        private static int _refreshes;
        private static readonly List<byte> Targets = new();
        private static int _targetIndex;

        private static readonly SimpleHudButton TargetButton = new();
        private static readonly SimpleHudButton RefreshButton = new();
        private static readonly SimpleHudButton SendButton = new();
        private static readonly SimpleHudButton CancelButton = new();

        public static bool CanCompose()
        {
            var local = PlayerControl.LocalPlayer;
            return GhostRoleSystem.Is(local, GhostRoleKind.Influencer) && local.Data != null && local.Data.IsDead &&
                   Players.LivingCount() > 0 && !_composing;
        }

        public static void Begin()
        {
            if (!CanCompose()) return;
            _composing = true;
            _refreshes = 2;
            _targetIndex = 0;
            BuildTargets();
            RefreshCards();
            Render();
        }

        private static void BuildTargets()
        {
            Targets.Clear();
            foreach (var p in PlayerControl.AllPlayerControls)
                if (p != null && p.Data != null && !p.Data.IsDead && !p.Data.Disconnected)
                    Targets.Add(p.Data.PlayerId);
            if (_targetIndex >= Targets.Count) _targetIndex = 0;
        }

        private static void RefreshCards()
        {
            for (int i = 0; i < Picks.Length; i++)
                Picks[i] = (byte)UnityEngine.Random.Range(0, Cards.Length);
        }

        private static void CycleTarget()
        {
            BuildTargets();
            if (Targets.Count == 0) return;
            _targetIndex = (_targetIndex + 1) % Targets.Count;
            Render();
        }

        private static void DoRefresh()
        {
            if (_refreshes <= 0) return;
            _refreshes--;
            RefreshCards();
            Render();
        }

        private static void DoSend()
        {
            BuildTargets();
            if (Targets.Count == 0) return;
            if (_targetIndex >= Targets.Count) _targetIndex = 0;
            var local = PlayerControl.LocalPlayer;
            if (local?.Data == null) return;

            byte target = Targets[_targetIndex];
            Send(local.Data.PlayerId, target, Picks[0], Picks[1], Picks[2]);
            Cancel();
        }

        private static void Render()
        {
            if (!_composing)
            {
                HideButtons();
                Overlay.Hide("InfluencerCompose");
                return;
            }

            BuildTargets();
            string target = Targets.Count == 0 ? "nobody" : Players.Name(Targets[_targetIndex]);
            Overlay.Persistent("InfluencerCompose",
                "INFLUENCER\nTarget: " + target +
                "\n[" + Cards[Picks[0]] + "]   [" + Cards[Picks[1]] + "]   [" + Cards[Picks[2]] + "]" +
                "\nCycle target / Refresh (" + _refreshes + ") / Send",
                0.9f, 1.3f);

            TargetButton.Show("InfluencerTarget", RoleIconFactory.Get("influencer_target", new Color(0.55f, 0.85f, 1f, 1f)),
                AbilityButtonGrid.SlotB, CycleTarget);
            RefreshButton.Show("InfluencerRefresh", RoleIconFactory.Get("influencer_refresh", new Color(0.7f, 1f, 0.7f, 1f)),
                AbilityButtonGrid.SlotC, DoRefresh);
            SendButton.Show("InfluencerSend", RoleIconFactory.Get("influencer_send", new Color(1f, 0.55f, 0.85f, 1f)),
                AbilityButtonGrid.SlotA, DoSend);
            CancelButton.Show("InfluencerCancel", RoleIconFactory.Get("influencer_cancel", new Color(1f, 0.4f, 0.4f, 1f)),
                new Vector3(2.7f, 2.65f, 0f), Cancel);
        }

        public static void Tick()
        {
            if (!_composing) return;
            if (!GhostRoleSystem.Is(PlayerControl.LocalPlayer, GhostRoleKind.Influencer))
            {
                Cancel();
                return;
            }

            Render();
        }

        private static void Send(byte influencerId, byte targetId, byte a, byte b, byte c)
        {
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                Resolve(influencerId, targetId, a, b, c);
            else
                ManactorAPI.SendRpcMethod(MessageRpc + ".Request", influencerId, targetId, a, b, c);
        }

        [ManactorRpc(MessageRpc + ".Request")]
        private static void OnRequest(byte senderId, byte influencerId, byte targetId, byte a, byte b, byte c)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost || senderId != influencerId) return;
            Resolve(influencerId, targetId, a, b, c);
        }

        private static void Resolve(byte influencerId, byte targetId, byte a, byte b, byte c)
        {
            var influencer = Players.Find(influencerId);
            var target = Players.Find(targetId);
            if (!GhostRoleSystem.Is(influencer, GhostRoleKind.Influencer) || influencer?.Data == null || !influencer.Data.IsDead ||
                target?.Data == null || target.Data.IsDead) return;

            ManactorAPI.SendRpcMethod(MessageRpc, influencerId, targetId, a, b, c);
            ApplyMessage(influencerId, targetId, a, b, c);
        }

        [ManactorRpc(MessageRpc)]
        private static void OnMessage(byte senderId, byte influencerId, byte targetId, byte a, byte b, byte c)
        {
            if (!NetworkAuth.IsFromHost(senderId)) return;
            ApplyMessage(influencerId, targetId, a, b, c);
        }

        private static void ApplyMessage(byte influencerId, byte targetId, byte a, byte b, byte c)
        {
            var local = PlayerControl.LocalPlayer;
            if (local?.Data == null || local.Data.PlayerId != targetId) return;

            string A = Cards[Mathf.Clamp(a, 0, Cards.Length - 1)];
            string B = Cards[Mathf.Clamp(b, 0, Cards.Length - 1)];
            string C = Cards[Mathf.Clamp(c, 0, Cards.Length - 1)];
            Overlay.Show("InfluencerMessage",
                "MESSAGE FROM THE INFLUENCER\n[" + A + "]   [" + B + "]   [" + C + "]",
                8f, 1.3f, 1.8f);
        }

        private static void HideButtons()
        {
            TargetButton.Hide();
            RefreshButton.Hide();
            SendButton.Hide();
            CancelButton.Hide();
        }

        private static void Cancel()
        {
            _composing = false;
            Targets.Clear();
            HideButtons();
            Overlay.Hide("InfluencerCompose");
        }

        public static void Reset()
        {
            _composing = false;
            _refreshes = 0;
            Targets.Clear();
            TargetButton.Destroy();
            RefreshButton.Destroy();
            SendButton.Destroy();
            CancelButton.Destroy();
            Overlay.Hide("InfluencerCompose");
        }
    }

}

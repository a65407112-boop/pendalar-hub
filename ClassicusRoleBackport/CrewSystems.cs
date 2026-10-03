using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using UnityEngine;

namespace ClassicUs.OfficialRolesBackport
{
    internal static class ScientistSystem
    {
        private const float MaxBattery = 20f;
        private const float TaskRecharge = 5f;
        private static float _battery = 8f;
        private static bool _open;

        public static bool CanOpen()
        {
            var p = PlayerControl.LocalPlayer;
            return RoleChecks.Is(p, RoleIds.Scientist) && p.Data != null && !p.Data.IsDead && _battery > 0f;
        }

        public static void Toggle()
        {
            if (!CanOpen()) return;
            _open = !_open;
            if (!_open) Overlay.Hide("ScientistVitals");
        }

        public static void OnTask(PlayerControl player)
        {
            if (player == PlayerControl.LocalPlayer && RoleChecks.Is(player, RoleIds.Scientist))
                _battery = Mathf.Min(MaxBattery, _battery + TaskRecharge);
        }

        public static void Tick()
        {
            if (!_open) return;
            if (!CanOpen())
            {
                _open = false;
                Overlay.Hide("ScientistVitals");
                return;
            }

            _battery = Mathf.Max(0f, _battery - Time.fixedDeltaTime);
            var lines = new List<string> { "SCIENTIST VITALS   battery " + _battery.ToString("0.0") + "s" };
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p.Data == null || p.Data.Disconnected) continue;
                lines.Add((p.Data.IsDead ? "[DEAD] " : "[ALIVE] ") + p.Data.PlayerName);
            }
            Overlay.Persistent("ScientistVitals", string.Join("\n", lines), 1.8f, 1.35f);
        }

        public static void Reset()
        {
            _battery = 8f;
            _open = false;
            Overlay.Hide("ScientistVitals");
        }
    }

    internal static class TrackerSystem
    {
        private static byte _targetId = byte.MaxValue;
        private static float _endsAt;

        public static void Start()
        {
            var local = PlayerControl.LocalPlayer;
            var target = Players.NearestLiving(local);
            if (target == null || target.Data == null)
            {
                Overlay.Show("TrackerMessage", "TRACKER: no valid target", 2f);
                return;
            }

            _targetId = target.Data.PlayerId;
            _endsAt = Time.time + 30f;
            Overlay.Show("TrackerMessage", "TRACKING " + target.Data.PlayerName, 2f);
        }

        public static void Tick()
        {
            if (_targetId == byte.MaxValue) return;
            if (Time.time >= _endsAt)
            {
                Reset();
                return;
            }

            var local = PlayerControl.LocalPlayer;
            var target = Players.Find(_targetId);
            if (local == null || target == null || target.Data == null || target.Data.IsDead)
            {
                Reset();
                return;
            }

            float distance = Vector2.Distance(local.GetTruePosition(), target.GetTruePosition());
            Vector2 delta = target.GetTruePosition() - local.GetTruePosition();
            string arrow = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? (delta.x >= 0 ? ">" : "<")
                : (delta.y >= 0 ? "^" : "v");

            Overlay.Persistent("TrackerHud",
                "TRACKER  " + arrow + "  " + target.Data.PlayerName + "  " + distance.ToString("0.0") + "m  " +
                Mathf.Max(0f, _endsAt - Time.time).ToString("0") + "s", 2.5f, 1.45f);
        }

        public static void Reset()
        {
            _targetId = byte.MaxValue;
            _endsAt = 0f;
            Overlay.Hide("TrackerHud");
        }
    }

    internal static class NoisemakerSystem
    {
        private const string AlertRpc = "classicus.officialroles.NoisemakerAlert";

        public static void OnMurder(MurderEventArgs e)
        {
            if (e?.Target == null || e.Target.Data == null) return;
            if (!RoleChecks.Is(e.Target, RoleIds.Noisemaker)) return;
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;

            byte id = e.Target.Data.PlayerId;
            ManactorAPI.SendRpcMethod(AlertRpc, id);
            ApplyAlert(id);
        }

        [ManactorRpc(AlertRpc)]
        private static void OnAlertRpc(byte senderId, byte victimId)
        {
            if (!ManactorAPI.IsFromHost(senderId)) return;
            ApplyAlert(victimId);
        }

        private static void ApplyAlert(byte victimId)
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || local.Data.IsDead) return;
            Overlay.Show("NoisemakerAlert",
                "!!! NOISEMAKER ALERT !!!\n" + Players.Name(victimId) + " was killed", 5f, 1.6f, 2.15f);
        }
    }

    internal sealed class DetectiveCase
    {
        public int Id;
        public byte VictimId;
        public Dictionary<byte, float> Distances = new();
    }

    internal static class DetectiveSystem
    {
        private const string CaseRpc = "classicus.officialroles.DetectiveCase";
        private const string RequestRpc = "classicus.officialroles.DetectiveInterrogateRequest";
        private const string ResultRpc = "classicus.officialroles.DetectiveInterrogateResult";

        private static DetectiveCase _hostCase;
        private static int _caseCounter;
        private static int _localCaseId;
        private static byte _localVictimId = byte.MaxValue;
        private static readonly Dictionary<int, int> HostUses = new();
        private static readonly List<string> LocalNotes = new();

        public static void OnMurder(MurderEventArgs e)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost || e?.Target?.Data == null) return;

            var c = new DetectiveCase { Id = ++_caseCounter, VictimId = e.Target.Data.PlayerId };
            Vector2 victimPos = e.Target.GetTruePosition();
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p.Data == null || p.Data.IsDead || p.Data.Disconnected) continue;
                c.Distances[p.Data.PlayerId] = Vector2.Distance(victimPos, p.GetTruePosition());
            }
            _hostCase = c;
            HostUses.Clear();

            ManactorAPI.SendRpcMethod(CaseRpc, c.Id, c.VictimId);
            ApplyCase(c.Id, c.VictimId);
        }

        [ManactorRpc(CaseRpc)]
        private static void OnCaseRpc(byte senderId, int caseId, byte victimId)
        {
            if (!ManactorAPI.IsFromHost(senderId)) return;
            ApplyCase(caseId, victimId);
        }

        private static void ApplyCase(int caseId, byte victimId)
        {
            _localCaseId = caseId;
            _localVictimId = victimId;
            LocalNotes.Clear();
            if (RoleChecks.Is(PlayerControl.LocalPlayer, RoleIds.Detective))
                Overlay.Show("DetectiveNewCase", "DETECTIVE: New case - " + Players.Name(victimId), 4f);
        }

        public static bool CanInterrogate()
        {
            var local = PlayerControl.LocalPlayer;
            return RoleChecks.Is(local, RoleIds.Detective) && local.Data != null && !local.Data.IsDead &&
                   _localCaseId > 0 && Players.NearestLiving(local, 3f) != null;
        }

        public static void Interrogate()
        {
            var local = PlayerControl.LocalPlayer;
            var suspect = Players.NearestLiving(local, 3f);
            if (local?.Data == null || suspect?.Data == null || _localCaseId <= 0) return;

            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                ResolveInterrogate(local.Data.PlayerId, suspect.Data.PlayerId, _localCaseId);
            else
                ManactorAPI.SendRpcMethod(RequestRpc, local.Data.PlayerId, suspect.Data.PlayerId, _localCaseId);
        }

        [ManactorRpc(RequestRpc)]
        private static void OnRequestRpc(byte senderId, byte detectiveId, byte suspectId, int caseId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost || senderId != detectiveId) return;
            ResolveInterrogate(detectiveId, suspectId, caseId);
        }

        private static void ResolveInterrogate(byte detectiveId, byte suspectId, int caseId)
        {
            var detective = Players.Find(detectiveId);
            if (!RoleChecks.Is(detective, RoleIds.Detective) || _hostCase == null || _hostCase.Id != caseId) return;

            int key = caseId * 256 + detectiveId;
            HostUses.TryGetValue(key, out int uses);
            if (uses >= 3) return;
            HostUses[key] = uses + 1;

            bool near = _hostCase.Distances.TryGetValue(suspectId, out float d) && d <= 3.5f;
            byte remaining = (byte)Mathf.Max(0, 2 - uses);
            ManactorAPI.SendRpcMethod(ResultRpc, detectiveId, suspectId, _hostCase.VictimId, near, remaining);
            ApplyResult(detectiveId, suspectId, _hostCase.VictimId, near, remaining);
        }

        [ManactorRpc(ResultRpc)]
        private static void OnResultRpc(byte senderId, byte detectiveId, byte suspectId, byte victimId, bool near, byte remaining)
        {
            if (!ManactorAPI.IsFromHost(senderId)) return;
            ApplyResult(detectiveId, suspectId, victimId, near, remaining);
        }

        private static void ApplyResult(byte detectiveId, byte suspectId, byte victimId, bool near, byte remaining)
        {
            var local = PlayerControl.LocalPlayer;
            if (local?.Data == null || local.Data.PlayerId != detectiveId) return;

            string note = Players.Name(suspectId) + (near ? " WAS near " : " was NOT near ") + Players.Name(victimId);
            LocalNotes.Add(note);
            Overlay.Show("DetectiveResult", "INTERROGATION\n" + note + "\n" + remaining + " interrogations left", 5f);
        }

        public static void Tick()
        {
            var local = PlayerControl.LocalPlayer;
            if (!RoleChecks.Is(local, RoleIds.Detective) || _localCaseId <= 0)
            {
                Overlay.Hide("DetectiveNotes");
                return;
            }

            var lines = new List<string> { "DETECTIVE NOTES", "Victim: " + Players.Name(_localVictimId) };
            lines.AddRange(LocalNotes);
            Overlay.Persistent("DetectiveNotes", string.Join("\n", lines), -2.5f, 1.15f);
        }

        public static void Reset()
        {
            _hostCase = null;
            _caseCounter = 0;
            _localCaseId = 0;
            _localVictimId = byte.MaxValue;
            HostUses.Clear();
            LocalNotes.Clear();
            Overlay.Hide("DetectiveNotes");
        }
    }

    internal static class JudgeSystem
    {
        private const string RequestRpc = "classicus.officialroles.JudgeOverruleRequest";
        private const string ResultRpc = "classicus.officialroles.JudgeOverruleResult";
        private const int RequiredTasks = 2;

        private static readonly Dictionary<byte, int> Tasks = new();
        private static readonly HashSet<byte> Used = new();
        private static MeetingHud _meeting;
        private static List<byte> _targets = new();

        public static void OnTask(PlayerControl player)
        {
            if (player?.Data == null) return;
            byte id = player.Data.PlayerId;
            Tasks.TryGetValue(id, out int n);
            Tasks[id] = n + 1;
        }

        public static void OnMeeting(MeetingHud meeting)
        {
            _meeting = meeting;
            RebuildTargets();
        }

        public static void AfterMeeting()
        {
            _meeting = null;
            _targets.Clear();
            Overlay.Hide("JudgeMenu");
        }

        private static void RebuildTargets()
        {
            _targets = new List<byte>();
            foreach (var p in PlayerControl.AllPlayerControls)
                if (p != null && p.Data != null && !p.Data.IsDead && !p.Data.Disconnected)
                    _targets.Add(p.Data.PlayerId);
        }

        public static void Tick()
        {
            var local = PlayerControl.LocalPlayer;
            if (_meeting == null || local?.Data == null || !RoleChecks.Is(local, RoleIds.Judge) || local.Data.IsDead)
            {
                Overlay.Hide("JudgeMenu");
                return;
            }

            byte judgeId = local.Data.PlayerId;
            Tasks.TryGetValue(judgeId, out int done);
            if (Used.Contains(judgeId))
            {
                Overlay.Persistent("JudgeMenu", "JUDGE: Overrule already used", 2.45f, 1.25f);
                return;
            }

            if (done < RequiredTasks)
            {
                Overlay.Persistent("JudgeMenu", "JUDGE: Overrule locked (" + done + "/" + RequiredTasks + " tasks)", 2.45f, 1.25f);
                return;
            }

            if (_targets.Count == 0) RebuildTargets();
            var lines = new List<string> { "JUDGE OVERRULE - press a number" };
            int shown = Math.Min(9, _targets.Count);
            for (int i = 0; i < shown; i++) lines.Add((i + 1) + ": " + Players.Name(_targets[i]));
            lines.Add("Wrong target = YOU are ejected");
            Overlay.Persistent("JudgeMenu", string.Join("\n", lines), 2.0f, 1.15f);

            for (int i = 0; i < shown; i++)
            {
                KeyCode key = (KeyCode)((int)KeyCode.Alpha1 + i);
                if (Input.GetKeyDown(key))
                {
                    Request(judgeId, _targets[i]);
                    break;
                }
            }
        }

        private static void Request(byte judgeId, byte targetId)
        {
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                Resolve(judgeId, targetId);
            else
                ManactorAPI.SendRpcMethod(RequestRpc, judgeId, targetId);
        }

        [ManactorRpc(RequestRpc)]
        private static void OnRequest(byte senderId, byte judgeId, byte targetId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost || senderId != judgeId) return;
            Resolve(judgeId, targetId);
        }

        private static void Resolve(byte judgeId, byte targetId)
        {
            if (Used.Contains(judgeId)) return;
            var judge = Players.Find(judgeId);
            var target = Players.Find(targetId);
            if (!RoleChecks.Is(judge, RoleIds.Judge) || judge?.Data == null || target?.Data == null || target.Data.IsDead) return;
            Tasks.TryGetValue(judgeId, out int done);
            if (done < RequiredTasks) return;

            Used.Add(judgeId);
            bool correct = RoleChecks.IsImpostor(target);
            byte ejectedId = correct ? targetId : judgeId;

            ManactorAPI.SendRpcMethod(ResultRpc, judgeId, ejectedId, correct);
            ApplyResult(judgeId, ejectedId, correct);
        }

        [ManactorRpc(ResultRpc)]
        private static void OnResult(byte senderId, byte judgeId, byte ejectedId, bool correct)
        {
            if (!ManactorAPI.IsFromHost(senderId)) return;
            ApplyResult(judgeId, ejectedId, correct);
        }

        private static void ApplyResult(byte judgeId, byte ejectedId, bool correct)
        {
            Used.Add(judgeId);
            var ejected = Players.Find(ejectedId);
            try { ejected?.Exiled(); }
            catch (Exception e) { OfficialRolesPlugin.Log.LogError("Judge Exiled failed: " + e); }

            Overlay.Show("JudgeResult",
                correct ? "OVERRULE SUCCESS: " + Players.Name(ejectedId) + " was ejected"
                        : "OVERRULE FAILED: Judge " + Players.Name(judgeId) + " was ejected",
                5f, 1.4f, 1.7f);

            try { _meeting?.Close(); }
            catch (Exception e) { OfficialRolesPlugin.Log.LogError("Judge meeting close failed: " + e); }
        }

        public static void Reset()
        {
            Tasks.Clear();
            Used.Clear();
            _meeting = null;
            _targets.Clear();
            Overlay.Hide("JudgeMenu");
        }
    }
}

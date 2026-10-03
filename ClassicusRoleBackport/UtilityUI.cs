using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ClassicUs.OfficialRolesBackport
{
    internal static class Players
    {
        public static PlayerControl Find(byte id)
        {
            foreach (var p in PlayerControl.AllPlayerControls)
                if (p != null && p.Data != null && p.Data.PlayerId == id)
                    return p;
            return null;
        }

        public static PlayerControl NearestLiving(PlayerControl from, float maxDistance = 999f, bool includeImpostors = true)
        {
            if (from == null) return null;
            Vector2 at = from.GetTruePosition();
            PlayerControl best = null;
            float bestDist = maxDistance;
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p == from || p.Data == null || p.Data.IsDead || p.Data.Disconnected) continue;
                if (!includeImpostors && RoleChecks.IsImpostor(p)) continue;
                float d = Vector2.Distance(at, p.GetTruePosition());
                if (d >= bestDist) continue;
                bestDist = d;
                best = p;
            }
            return best;
        }

        public static string Name(byte id)
        {
            var p = Find(id);
            return p?.Data?.PlayerName ?? ("Player " + id);
        }

        public static int LivingCount()
        {
            int n = 0;
            foreach (var p in PlayerControl.AllPlayerControls)
                if (p != null && p.Data != null && !p.Data.IsDead && !p.Data.Disconnected)
                    n++;
            return n;
        }
    }

    internal sealed class OverlayEntry
    {
        public GameObject Go;
        public TextMeshPro Text;
        public float Until;
        public bool Persistent;
    }

    internal static class Overlay
    {
        private static readonly Dictionary<string, OverlayEntry> Entries = new();

        public static void Show(string key, string text, float seconds = 4f, float y = 2.2f, float fontSize = 2.1f)
        {
            Set(key, text, false, Time.time + seconds, y, fontSize);
        }

        public static void Persistent(string key, string text, float y = 2.2f, float fontSize = 2.1f)
        {
            Set(key, text, true, 0f, y, fontSize);
        }

        private static void Set(string key, string text, bool persistent, float until, float y, float fontSize)
        {
            var hud = HudManager.Instance;
            if (hud == null) return;

            if (!Entries.TryGetValue(key, out var e) || e.Go == null || e.Text == null)
            {
                var go = new GameObject("OfficialRoleOverlay_" + key);
                go.transform.SetParent(hud.transform, false);
                go.transform.localPosition = new Vector3(0f, y, -20f);
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = fontSize;
                tmp.sortingOrder = short.MaxValue;
                tmp.color = Color.white;
                tmp.enableWordWrapping = false;
                e = new OverlayEntry { Go = go, Text = tmp };
                Entries[key] = e;
            }

            e.Go.transform.localPosition = new Vector3(0f, y, -20f);
            e.Text.fontSize = fontSize;
            e.Text.text = text;
            e.Persistent = persistent;
            e.Until = until;
            e.Go.SetActive(true);
        }

        public static void Hide(string key)
        {
            if (!Entries.TryGetValue(key, out var e)) return;
            if (e.Go != null) UnityEngine.Object.Destroy(e.Go);
            Entries.Remove(key);
        }

        public static void Tick()
        {
            if (Entries.Count == 0) return;
            List<string> remove = null;
            foreach (var kv in Entries)
            {
                var e = kv.Value;
                if (e.Go == null)
                {
                    (remove ??= new List<string>()).Add(kv.Key);
                    continue;
                }
                if (!e.Persistent && e.Until > 0f && Time.time >= e.Until)
                {
                    UnityEngine.Object.Destroy(e.Go);
                    (remove ??= new List<string>()).Add(kv.Key);
                }
            }
            if (remove != null)
                foreach (var key in remove) Entries.Remove(key);
        }

        public static void Reset()
        {
            foreach (var e in Entries.Values)
                if (e.Go != null) UnityEngine.Object.Destroy(e.Go);
            Entries.Clear();
        }
    }

    internal static class TargetHighlight
    {
        private static PlayerControl _target;
        private static Material _material;
        private static float _oldOutline;
        private static Color _oldColor;
        private static bool _hadOutline;
        private static bool _hadColor;

        public static void Set(PlayerControl target, Color color)
        {
            if (_target != target) Clear();
            if (target == null || target.MyPhysics == null || target.MyPhysics.rend == null) return;

            _target = target;
            _material = target.MyPhysics.rend.material;
            if (_material == null) return;

            if (!_hadOutline && _material.HasProperty("_Outline"))
            {
                _oldOutline = _material.GetFloat("_Outline");
                _hadOutline = true;
            }
            if (!_hadColor && _material.HasProperty("_OutlineColor"))
            {
                _oldColor = _material.GetColor("_OutlineColor");
                _hadColor = true;
            }

            if (_material.HasProperty("_Outline")) _material.SetFloat("_Outline", 1f);
            if (_material.HasProperty("_OutlineColor")) _material.SetColor("_OutlineColor", color);
        }

        public static void Clear()
        {
            if (_material != null)
            {
                if (_hadOutline && _material.HasProperty("_Outline")) _material.SetFloat("_Outline", _oldOutline);
                if (_hadColor && _material.HasProperty("_OutlineColor")) _material.SetColor("_OutlineColor", _oldColor);
            }
            _target = null;
            _material = null;
            _hadOutline = false;
            _hadColor = false;
        }
    }
}

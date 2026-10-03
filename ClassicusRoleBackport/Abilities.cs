using System.Collections.Generic;
using ClassicUs.ManuAPI;
using UnityEngine;

namespace ClassicUs.OfficialRolesBackport
{
    internal static class RoleIconFactory
    {
        private static readonly Dictionary<string, Sprite> Cache = new();

        public static Sprite Get(string key, Color color)
        {
            if (Cache.TryGetValue(key, out var sprite) && sprite != null) return sprite;

            if (ModernAssets.TryGet(key, out sprite) && sprite != null)
            {
                Cache[key] = sprite;
                return sprite;
            }

            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    tex.SetPixel(x, y, Color.clear);

            // Fallback only. Exact official art is loaded from the user's own modern Among Us installation when available.
            for (int y = 4; y < s - 4; y++)
            {
                for (int x = 4; x < s - 4; x++)
                {
                    float dx = x - s / 2f;
                    float dy = y - s / 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 25f && d < 29f) tex.SetPixel(x, y, color);
                }
            }

            int code = 0;
            foreach (char c in key) code = (code * 31 + c) & 0x7fffffff;
            for (int bit = 0; bit < 5; bit++)
            {
                if (((code >> bit) & 1) == 0) continue;
                int yy = 18 + bit * 7;
                for (int x = 18; x <= 46; x++) tex.SetPixel(x, yy, color);
            }
            for (int y = 18; y <= 46; y++) tex.SetPixel(32, y, color);

            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 64f);
            sprite.name = "OfficialRoleIcon_" + key;
            Cache[key] = sprite;
            return sprite;
        }

        public static void Reset()
        {
            Cache.Clear();
        }
    }

    internal sealed class ScientistAbility : CustomAbility
    {
        protected override string Name => "ScientistVitalsButton";
        protected override float Cooldown => 0f;
        protected override Vector3 DistanceFromEdge => AbilityButtonGrid.SlotA;
        protected override Sprite CreateIcon(Sprite original) => RoleIconFactory.Get("scientist", new Color(0.25f, 0.85f, 1f, 1f));
        protected override bool IsVisible()
        {
            var p = PlayerControl.LocalPlayer;
            return RoleChecks.Is(p, RoleIds.Scientist) && p.Data != null && !p.Data.IsDead;
        }
        protected override bool CanActivate() => ScientistSystem.CanOpen();
        protected override void OnActivate() => ScientistSystem.Toggle();
    }

    internal sealed class TrackerAbility : CustomAbility
    {
        protected override string Name => "TrackerButton";
        protected override float Cooldown => 20f;
        protected override Vector3 DistanceFromEdge => AbilityButtonGrid.SlotA;
        protected override Sprite CreateIcon(Sprite original) => RoleIconFactory.Get("tracker", new Color(0.25f, 1f, 0.55f, 1f));
        protected override bool IsVisible()
        {
            var p = PlayerControl.LocalPlayer;
            return RoleChecks.Is(p, RoleIds.Tracker) && p.Data != null && !p.Data.IsDead;
        }
        protected override bool CanActivate() => Players.NearestLiving(PlayerControl.LocalPlayer) != null;
        protected override void OnActivate() => TrackerSystem.Start();
    }

    internal sealed class DetectiveAbility : CustomAbility
    {
        protected override string Name => "DetectiveInterrogateButton";
        protected override float Cooldown => 10f;
        protected override Vector3 DistanceFromEdge => AbilityButtonGrid.SlotA;
        protected override Sprite CreateIcon(Sprite original) => RoleIconFactory.Get("detective", new Color(0.35f, 0.95f, 0.9f, 1f));
        protected override bool IsVisible()
        {
            var p = PlayerControl.LocalPlayer;
            return RoleChecks.Is(p, RoleIds.Detective) && p.Data != null && !p.Data.IsDead;
        }
        protected override bool CanActivate() => DetectiveSystem.CanInterrogate();
        protected override void OnActivate() => DetectiveSystem.Interrogate();
    }

    internal sealed class ShapeshifterAbility : CustomAbility
    {
        protected override string Name => "ShapeshifterButton";
        protected override float Cooldown => 25f;
        protected override Vector3 DistanceFromEdge => AbilityButtonGrid.SlotA;
        protected override Sprite CreateIcon(Sprite original) => RoleIconFactory.Get("shapeshifter", new Color(1f, 0.22f, 0.22f, 1f));
        protected override bool IsVisible()
        {
            var p = PlayerControl.LocalPlayer;
            return RoleChecks.Is(p, RoleIds.Shapeshifter) && p.Data != null && !p.Data.IsDead;
        }
        protected override bool CanActivate() => Players.NearestLiving(PlayerControl.LocalPlayer, 4f) != null;
        protected override void OnActivate() => ShapeshifterSystem.Request();
    }

    internal sealed class PhantomAbility : CustomAbility
    {
        protected override string Name => "PhantomButton";
        protected override float Cooldown => 30f;
        protected override Vector3 DistanceFromEdge => AbilityButtonGrid.SlotA;
        protected override Sprite CreateIcon(Sprite original) => RoleIconFactory.Get("phantom", new Color(0.8f, 0.25f, 1f, 1f));
        protected override bool IsVisible()
        {
            var p = PlayerControl.LocalPlayer;
            return RoleChecks.Is(p, RoleIds.Phantom) && p.Data != null && !p.Data.IsDead;
        }
        protected override bool CanActivate() => !PhantomSystem.IsInvisible(PlayerControl.LocalPlayer);
        protected override void OnActivate() => PhantomSystem.Request();
    }

    internal sealed class GuardianAngelAbility : CustomAbility
    {
        protected override string Name => "GuardianAngelButton";
        protected override float Cooldown => 20f;
        protected override Vector3 DistanceFromEdge => AbilityButtonGrid.SlotA;
        protected override Sprite CreateIcon(Sprite original) => RoleIconFactory.Get("guardian", new Color(0.75f, 0.95f, 1f, 1f));
        protected override bool IsVisible()
        {
            var p = PlayerControl.LocalPlayer;
            return GhostRoleSystem.Is(p, GhostRoleKind.GuardianAngel) && p.Data != null && p.Data.IsDead;
        }
        protected override bool CanActivate() => GuardianAngelSystem.CanProtect();
        protected override void OnActivate() => GuardianAngelSystem.Request();
    }

    internal sealed class InfluencerAbility : CustomAbility
    {
        protected override string Name => "InfluencerButton";
        protected override float Cooldown => 30f;
        protected override Vector3 DistanceFromEdge => AbilityButtonGrid.SlotA;
        protected override Sprite CreateIcon(Sprite original) => RoleIconFactory.Get("influencer", new Color(1f, 0.55f, 0.85f, 1f));
        protected override bool IsVisible()
        {
            var p = PlayerControl.LocalPlayer;
            return GhostRoleSystem.Is(p, GhostRoleKind.Influencer) && p.Data != null && p.Data.IsDead;
        }
        protected override bool CanActivate() => InfluencerSystem.CanCompose();
        protected override void OnActivate() => InfluencerSystem.Begin();
    }

    internal static class RoleAbilities
    {
        private static readonly ScientistAbility Scientist = new();
        private static readonly TrackerAbility Tracker = new();
        private static readonly DetectiveAbility Detective = new();
        private static readonly ShapeshifterAbility Shapeshifter = new();
        private static readonly PhantomAbility Phantom = new();
        private static readonly GuardianAngelAbility Guardian = new();
        private static readonly InfluencerAbility Influencer = new();

        public static void Tick(HudManager hud)
        {
            Scientist.Tick(hud);
            Tracker.Tick(hud);
            Detective.Tick(hud);
            Shapeshifter.Tick(hud);
            Phantom.Tick(hud);
            Guardian.Tick(hud);
            Influencer.Tick(hud);
        }

        public static void Reset()
        {
            Scientist.Reset();
            Tracker.Reset();
            Detective.Reset();
            Shapeshifter.Reset();
            Phantom.Reset();
            Guardian.Reset();
            Influencer.Reset();
        }
    }
}

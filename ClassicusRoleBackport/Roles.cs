using ClassicUs.ManuAPI;
using UnityEngine;

namespace ClassicUs.OfficialRolesBackport
{
    internal static class RoleIds
    {
        public const string Scientist = "classicus.officialroles.Scientist";
        public const string Engineer = "classicus.officialroles.Engineer";
        public const string Tracker = "classicus.officialroles.Tracker";
        public const string Noisemaker = "classicus.officialroles.Noisemaker";
        public const string Detective = "classicus.officialroles.Detective";
        public const string Judge = "classicus.officialroles.Judge";

        public const string Shapeshifter = "classicus.officialroles.Shapeshifter";
        public const string Phantom = "classicus.officialroles.Phantom";
        public const string Viper = "classicus.officialroles.Viper";

        public const string GuardianAngel = "classicus.officialroles.GuardianAngel";
        public const string Influencer = "classicus.officialroles.Influencer";
    }

    internal abstract class OfficialCrewRole : CustomCrewmateRole
    {
        protected abstract RoleOption Option { get; }
        public override int Count => RoleSettings.Count(Option);
        public override float RoleChancePercent => RoleSettings.Chance(Option);
        public override Color TeamColor => new(0.35f, 0.75f, 1f, 1f);
    }

    internal abstract class OfficialImpostorRole : CustomImpostorRole
    {
        protected abstract RoleOption Option { get; }
        public override int Count => RoleSettings.Count(Option);
        public override float RoleChancePercent => RoleSettings.Chance(Option);
        public override Color TeamColor => new(1f, 0.15f, 0.15f, 1f);
    }

    internal sealed class ScientistRole : OfficialCrewRole
    {
        protected override RoleOption Option => RoleSettings.Scientist;
        public override string DisplayName => "Scientist";
        public override string RoleTypeName => RoleIds.Scientist;
        public override string Description => "Access vitals from anywhere. Complete tasks to recharge your battery.";
        public override string DescriptionShort => "Check vitals from anywhere.";
        public override string EjectionText(string n) => n + " was the Scientist.";
    }

    internal sealed class EngineerRole : OfficialCrewRole
    {
        protected override RoleOption Option => RoleSettings.Engineer;
        public override string DisplayName => "Engineer";
        public override string RoleTypeName => RoleIds.Engineer;
        public override bool CanVent => true;
        public override string Description => "Use vents as a Crewmate.";
        public override string DescriptionShort => "You can use vents.";
        public override string EjectionText(string n) => n + " was the Engineer.";
    }

    internal sealed class TrackerRole : OfficialCrewRole
    {
        protected override RoleOption Option => RoleSettings.Tracker;
        public override string DisplayName => "Tracker";
        public override string RoleTypeName => RoleIds.Tracker;
        public override string Description => "Track another living player for a limited time.";
        public override string DescriptionShort => "Track a player's position.";
        public override string EjectionText(string n) => n + " was the Tracker.";
    }

    internal sealed class NoisemakerRole : OfficialCrewRole
    {
        protected override RoleOption Option => RoleSettings.Noisemaker;
        public override string DisplayName => "Noisemaker";
        public override string RoleTypeName => RoleIds.Noisemaker;
        public override string Description => "When killed, alert living players to your death.";
        public override string DescriptionShort => "Your death makes noise.";
        public override string EjectionText(string n) => n + " was the Noisemaker.";
    }

    internal sealed class DetectiveRole : OfficialCrewRole
    {
        protected override RoleOption Option => RoleSettings.Detective;
        public override string DisplayName => "Detective";
        public override string RoleTypeName => RoleIds.Detective;
        public override string Description => "Investigate the active murder case and interrogate suspects.";
        public override string DescriptionShort => "Interrogate suspects.";
        public override string EjectionText(string n) => n + " was the Detective.";
    }

    internal sealed class JudgeRole : OfficialCrewRole
    {
        protected override RoleOption Option => RoleSettings.Judge;
        public override string DisplayName => "Judge";
        public override string RoleTypeName => RoleIds.Judge;
        public override string Description => "Complete tasks to unlock one Overrule. A wrong Overrule ejects you instead.";
        public override string DescriptionShort => "Overrule a meeting once.";
        public override string EjectionText(string n) => n + " was the Judge.";
    }

    internal sealed class ShapeshifterRole : OfficialImpostorRole
    {
        protected override RoleOption Option => RoleSettings.Shapeshifter;
        public override string DisplayName => "Shapeshifter";
        public override string RoleTypeName => RoleIds.Shapeshifter;
        public override string Description => "Disguise yourself as another living player.";
        public override string DescriptionShort => "Shift into another player.";
        public override string EjectionText(string n) => n + " was the Shapeshifter.";
    }

    internal sealed class PhantomRole : OfficialImpostorRole
    {
        protected override RoleOption Option => RoleSettings.Phantom;
        public override string DisplayName => "Phantom";
        public override string RoleTypeName => RoleIds.Phantom;
        public override string Description => "Turn invisible for a limited time.";
        public override string DescriptionShort => "Become invisible.";
        public override string EjectionText(string n) => n + " was the Phantom.";
    }

    internal sealed class ViperRole : OfficialImpostorRole
    {
        protected override RoleOption Option => RoleSettings.Viper;
        public override string DisplayName => "Viper";
        public override string RoleTypeName => RoleIds.Viper;
        public override string Description => "Bodies from your kills dissolve until nothing remains.";
        public override string DescriptionShort => "Your victims dissolve.";
        public override string EjectionText(string n) => n + " was the Viper.";
    }

    internal static class RoleChecks
    {
        public static bool Is(PlayerControl p, string id) =>
            p != null && p.Data != null && RoleRegistry.IsAssigned(p, id);

        public static bool IsCrew(PlayerControl p)
        {
            if (p == null || p.Data == null || p.Data.myRole == null) return false;
            return p.Data.myRole.RoleTeamType == RoleTeamTypes.Crewmate;
        }

        public static bool IsImpostor(PlayerControl p)
        {
            if (p == null || p.Data == null || p.Data.myRole == null) return false;
            return p.Data.myRole.RoleTeamType == RoleTeamTypes.Impostor;
        }
    }
}

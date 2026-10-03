using System.Collections.Generic;
using BepInEx.Configuration;
using ClassicUs.ManuAPI;

namespace ClassicUs.OfficialRolesBackport
{
    internal sealed class RoleOption
    {
        public string Key;
        public string Label;
        public ConfigEntry<int> Count;
        public ConfigEntry<float> Chance;
    }

    internal static class RoleSettings
    {
        private static readonly Dictionary<string, RoleOption> Options = new();

        public static RoleOption Scientist;
        public static RoleOption Engineer;
        public static RoleOption Tracker;
        public static RoleOption Noisemaker;
        public static RoleOption Detective;
        public static RoleOption Judge;
        public static RoleOption Shapeshifter;
        public static RoleOption Phantom;
        public static RoleOption Viper;
        public static RoleOption GuardianAngel;
        public static RoleOption Influencer;

        public static void Init(ConfigFile config)
        {
            // Newest roles are deliberately first in assignment order elsewhere so a small lobby can still see them.
            Scientist = Add(config, RoleIds.Scientist, "Scientist", 1, 100f);
            Engineer = Add(config, RoleIds.Engineer, "Engineer", 1, 100f);
            Tracker = Add(config, RoleIds.Tracker, "Tracker", 1, 100f);
            Noisemaker = Add(config, RoleIds.Noisemaker, "Noisemaker", 1, 100f);
            Detective = Add(config, RoleIds.Detective, "Detective", 1, 100f);
            Judge = Add(config, RoleIds.Judge, "Judge", 1, 100f);
            Shapeshifter = Add(config, RoleIds.Shapeshifter, "Shapeshifter", 1, 100f);
            Phantom = Add(config, RoleIds.Phantom, "Phantom", 1, 100f);
            Viper = Add(config, RoleIds.Viper, "Viper", 1, 100f);
            GuardianAngel = Add(config, RoleIds.GuardianAngel, "Guardian Angel", 1, 100f);
            Influencer = Add(config, RoleIds.Influencer, "Influencer", 1, 100f);

            SettingsMenuAPI.Register(Options.Count * 2, builder =>
            {
                foreach (var pair in Options)
                {
                    var o = pair.Value;
                    builder.AddNumeric("ORB_" + o.Key + "_Count", o.Label + " Count", 1f, 0f, 5f, "0",
                        () => o.Count.Value,
                        v => { o.Count.Value = (int)v; o.Count.ConfigFile.Save(); });
                    builder.AddNumeric("ORB_" + o.Key + "_Chance", o.Label + " Chance", 10f, 0f, 100f, "0\\%",
                        () => o.Chance.Value,
                        v => { o.Chance.Value = v; o.Chance.ConfigFile.Save(); });
                }
                builder.ExpandScroller(Options.Count * 2 + 2);
            });
        }

        private static RoleOption Add(ConfigFile config, string key, string label, int count, float chance)
        {
            var option = new RoleOption
            {
                Key = key.Replace("classicus.officialroles.", ""),
                Label = label,
                Count = config.Bind("Roles", label.Replace(" ", "") + "Count", count,
                    new ConfigDescription("Maximum " + label + " roles per match.", new AcceptableValueRange<int>(0, 5))),
                Chance = config.Bind("Roles", label.Replace(" ", "") + "Chance", chance,
                    new ConfigDescription(label + " assignment chance.", new AcceptableValueRange<float>(0f, 100f)))
            };
            Options[key] = option;
            return option;
        }

        public static int Count(RoleOption o) => o?.Count?.Value ?? 0;
        public static float Chance(RoleOption o) => o?.Chance?.Value ?? 0f;
    }
}

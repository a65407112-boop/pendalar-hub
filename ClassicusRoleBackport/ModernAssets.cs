using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace ClassicUs.OfficialRolesBackport
{
    // Exact-only loader. No filename matching, no scoring, no "closest" asset selection.
    // Extract-OfficialAssets.ps1 writes exact-assets.tsv by following serialized Unity PPtr
    // references from the actual modern role objects.
    internal static class ModernAssets
    {
        private sealed class ExactEntry
        {
            public string Key;
            public string RelativePath;
            public float PixelsPerUnit = 100f;
            public string Source;
        }

        private static readonly Dictionary<string, ExactEntry> Entries =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Sprite> Sprites =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> MissingLogged =
            new(StringComparer.OrdinalIgnoreCase);

        private static string _assetFolder;
        private static bool _initialized;

        public static void Init(ConfigFile config)
        {
            _assetFolder = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                "OfficialAssets");

            try { Directory.CreateDirectory(_assetFolder); } catch { }
            LoadManifest();
            _initialized = true;
        }

        public static bool TryGet(string key, out Sprite sprite)
        {
            sprite = null;
            if (!_initialized) return false;

            if (Sprites.TryGetValue(key, out sprite) && sprite != null)
                return true;

            if (!Entries.TryGetValue(key, out var entry))
            {
                if (MissingLogged.Add(key))
                    OfficialRolesPlugin.Log?.LogWarning(
                        "MISSING EXACT ASSET: " + key +
                        ". Run Extract-OfficialAssets.cmd. The mod will not substitute a different modern-game asset.");
                return false;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(Path.Combine(_assetFolder, entry.RelativePath));
                string root = Path.GetFullPath(_assetFolder) + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Manifest path escapes OfficialAssets.");
            }
            catch (Exception e)
            {
                OfficialRolesPlugin.Log?.LogWarning("Exact asset path rejected for " + key + ": " + e.Message);
                return false;
            }

            if (!File.Exists(fullPath))
            {
                OfficialRolesPlugin.Log?.LogWarning("Exact asset file missing for " + key + ": " + fullPath);
                return false;
            }

            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                var bytes = Il2CppSystem.IO.File.ReadAllBytes(fullPath);
                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    UnityEngine.Object.Destroy(texture);
                    return false;
                }

                texture.name = "ExactOfficial_" + key;
                float ppu = entry.PixelsPerUnit > 0.01f ? entry.PixelsPerUnit : 100f;
                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    ppu);
                sprite.name = "ExactOfficial_" + key;
                Sprites[key] = sprite;

                OfficialRolesPlugin.Log?.LogInfo(
                    "Exact official asset loaded: " + key + " <- " + entry.Source);
                return true;
            }
            catch (Exception e)
            {
                OfficialRolesPlugin.Log?.LogWarning("Exact asset load failed for " + key + ": " + e.Message);
                return false;
            }
        }

        private static void LoadManifest()
        {
            Entries.Clear();
            Sprites.Clear();
            MissingLogged.Clear();

            string manifest = Path.Combine(_assetFolder, "exact-assets.tsv");
            if (!File.Exists(manifest))
            {
                OfficialRolesPlugin.Log?.LogWarning(
                    "OfficialAssets/exact-assets.tsv not found. No modern asset will be guessed.");
                return;
            }

            int loaded = 0;
            try
            {
                foreach (string raw in File.ReadAllLines(manifest))
                {
                    if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#")) continue;
                    string[] parts = raw.Split('\t');
                    if (parts.Length < 2) continue;

                    string key = parts[0].Trim();
                    string rel = parts[1].Trim();
                    if (key.Length == 0 || rel.Length == 0) continue;

                    float ppu = 100f;
                    if (parts.Length >= 3)
                        float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out ppu);
                    if (ppu <= 0.01f) ppu = 100f;

                    Entries[key] = new ExactEntry
                    {
                        Key = key,
                        RelativePath = rel,
                        PixelsPerUnit = ppu,
                        Source = parts.Length >= 4 ? parts[3].Trim() : rel
                    };
                    loaded++;
                }

                OfficialRolesPlugin.Log?.LogInfo(
                    "Exact official asset manifest loaded: " + loaded + " entries.");
            }
            catch (Exception e)
            {
                OfficialRolesPlugin.Log?.LogWarning("Could not read exact-assets.tsv: " + e.Message);
            }
        }
    }
}

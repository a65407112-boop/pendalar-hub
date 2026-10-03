using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace ClassicUs.OfficialRolesBackport
{
    internal static class ModernAssets
    {
        private sealed class Candidate
        {
            public string Key;
            public string[] Terms;
        }

        private static readonly Candidate[] Candidates =
        {
            new() { Key = "scientist", Terms = new[] { "scientist", "vitals" } },
            new() { Key = "engineer", Terms = new[] { "engineer", "vent" } },
            new() { Key = "tracker", Terms = new[] { "tracker", "track" } },
            new() { Key = "noisemaker", Terms = new[] { "noisemaker", "noise", "alert" } },
            new() { Key = "detective", Terms = new[] { "detective", "interrogate", "case" } },
            new() { Key = "judge", Terms = new[] { "judge", "overrule" } },
            new() { Key = "guardian", Terms = new[] { "guardianangel", "guardian", "protect" } },
            new() { Key = "influencer", Terms = new[] { "spiritguide", "influencer", "message" } },
            new() { Key = "shapeshifter", Terms = new[] { "shapeshifter", "shapeshift", "shift" } },
            new() { Key = "phantom", Terms = new[] { "phantom", "vanish", "invisible" } },
            new() { Key = "viper", Terms = new[] { "viper", "dissolve", "acid" } },
        };

        private static readonly Dictionary<string, Sprite> Sprites = new(StringComparer.OrdinalIgnoreCase);
        private static ConfigEntry<string> _modernContentPath;
        private static bool _initialized;
        private static bool _bundleScanAttempted;
        private static string _assetFolder;

        public static void Init(ConfigFile config)
        {
            _modernContentPath = config.Bind(
                "Assets",
                "ModernAmongUsContentPath",
                @"C:\XboxGames\Among Us\Content",
                "Path to the current official Among Us Content folder. The mod only reads local assets from your own installation.");

            _assetFolder = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                "OfficialAssets");

            try { Directory.CreateDirectory(_assetFolder); } catch { }
            LoadExtractedImages();
            _initialized = true;
        }

        public static bool TryGet(string key, out Sprite sprite)
        {
            sprite = null;
            if (!_initialized) return false;

            if (Sprites.TryGetValue(key, out sprite) && sprite != null)
                return true;

            if (!_bundleScanAttempted)
            {
                _bundleScanAttempted = true;
                ScanModernAddressableBundles();
                if (Sprites.TryGetValue(key, out sprite) && sprite != null)
                    return true;
            }

            return false;
        }

        private static void LoadExtractedImages()
        {
            if (string.IsNullOrEmpty(_assetFolder) || !Directory.Exists(_assetFolder)) return;

            try
            {
                foreach (var file in Directory.EnumerateFiles(_assetFolder, "*.*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;

                    string key = MatchKey(file);
                    if (key == null) continue;

                    var sprite = LoadSpriteFromImage(file);
                    if (sprite == null) continue;

                    MaybeTake(key, file, sprite);
                }
            }
            catch (Exception e)
            {
                OfficialRolesPlugin.Log?.LogWarning("OfficialAssets image scan failed: " + e.Message);
            }
        }

        private static Sprite LoadSpriteFromImage(string path)
        {
            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                var bytes = Il2CppSystem.IO.File.ReadAllBytes(path);
                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                texture.name = "OfficialRoleAsset_" + Path.GetFileNameWithoutExtension(path);
                var sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    Math.Max(texture.width, texture.height));
                sprite.name = texture.name;
                return sprite;
            }
            catch
            {
                return null;
            }
        }

        private static void ScanModernAddressableBundles()
        {
            string root = FindModernContentPath();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                OfficialRolesPlugin.Log?.LogWarning("OfficialAssets: modern Among Us Content folder was not found. Using fallback icons.");
                return;
            }

            string streaming = Path.Combine(root, "Among Us_Data", "StreamingAssets");
            if (!Directory.Exists(streaming))
            {
                OfficialRolesPlugin.Log?.LogWarning("OfficialAssets: no StreamingAssets folder in " + root + ".");
                return;
            }

            OfficialRolesPlugin.Log?.LogInfo("OfficialAssets: scanning local modern Among Us bundles from " + streaming + ".");

            int filesTried = 0;
            int assetsSeen = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(streaming, "*", SearchOption.AllDirectories))
                {
                    if (filesTried >= 350) break;
                    var info = new FileInfo(file);
                    if (info.Length < 1024 || info.Length > 128L * 1024L * 1024L) continue;

                    string ext = info.Extension.ToLowerInvariant();
                    string lowerPath = file.ToLowerInvariant();
                    if (ext != ".bundle" && !lowerPath.Contains("standalonewindows") && !lowerPath.Contains("windows")) continue;

                    AssetBundle bundle = null;
                    try
                    {
                        bundle = AssetBundle.LoadFromFile(file);
                        if (bundle == null) continue;
                        filesTried++;

                        var names = bundle.GetAllAssetNames();
                        for (int i = 0; i < names.Length; i++)
                        {
                            string assetName = names[i];
                            if (string.IsNullOrEmpty(assetName)) continue;
                            string key = MatchKey(assetName);
                            if (key == null) continue;
                            assetsSeen++;

                            Sprite sprite = null;
                            try { sprite = bundle.LoadAsset<Sprite>(assetName); } catch { }
                            if (sprite == null)
                            {
                                try
                                {
                                    var tex = bundle.LoadAsset<Texture2D>(assetName);
                                    if (tex != null)
                                        sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                            new Vector2(0.5f, 0.5f), Math.Max(tex.width, tex.height));
                                }
                                catch { }
                            }

                            if (sprite != null)
                                MaybeTake(key, assetName, sprite);
                        }
                    }
                    catch { }
                    finally
                    {
                        try { if (bundle != null) bundle.Unload(false); } catch { }
                    }
                }
            }
            catch (Exception e)
            {
                OfficialRolesPlugin.Log?.LogWarning("OfficialAssets bundle scan failed: " + e.Message);
            }

            OfficialRolesPlugin.Log?.LogInfo("OfficialAssets: scan complete. Matched role assets=" + Sprites.Count + ", candidate assets seen=" + assetsSeen + ".");
        }

        private static void MaybeTake(string key, string sourceName, Sprite sprite)
        {
            if (sprite == null) return;

            int score = Score(sourceName, key);
            if (Sprites.TryGetValue(key, out var existing) && existing != null)
            {
                int oldScore = Score(existing.name ?? string.Empty, key);
                if (score <= oldScore) return;
            }

            sprite.name = "OfficialRoleAsset_" + key + "_" + Path.GetFileNameWithoutExtension(sourceName);
            Sprites[key] = sprite;
            OfficialRolesPlugin.Log?.LogInfo("OfficialAssets: " + key + " <- " + sourceName);
        }

        private static int Score(string sourceName, string key)
        {
            string s = Normalize(sourceName);
            int score = 0;
            if (s.Contains(Normalize(key))) score += 50;
            if (s.Contains("button")) score += 40;
            if (s.Contains("ability")) score += 35;
            if (s.Contains("icon")) score += 30;
            if (s.Contains("hud")) score += 15;
            if (s.Contains("role")) score += 10;
            if (s.Contains("banner") || s.Contains("portrait") || s.Contains("background")) score -= 20;
            return score;
        }

        private static string MatchKey(string sourceName)
        {
            string s = Normalize(sourceName);
            foreach (var c in Candidates)
                foreach (var term in c.Terms)
                    if (s.Contains(Normalize(term)))
                        return c.Key;
            return null;
        }

        private static string Normalize(string value)
        {
            if (value == null) return string.Empty;
            return value.Replace("\\", "/").Replace("_", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
        }

        private static string FindModernContentPath()
        {
            var checks = new List<string>();

            if (_modernContentPath != null && !string.IsNullOrWhiteSpace(_modernContentPath.Value))
                checks.Add(_modernContentPath.Value);

            checks.Add(@"C:\XboxGames\Among Us\Content");
            checks.Add(@"C:\Program Files (x86)\Steam\steamapps\common\Among Us");
            checks.Add(@"C:\Program Files\Steam\steamapps\common\Among Us");

            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            checks.Add(Path.Combine(pluginDir, "ModernContent"));
            checks.Add(Path.Combine(pluginDir, "Content"));

            foreach (var path in checks)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    string full = Path.GetFullPath(path);
                    if (File.Exists(Path.Combine(full, "GameAssembly.dll")) &&
                        Directory.Exists(Path.Combine(full, "Among Us_Data")))
                        return full;
                }
                catch { }
            }

            return null;
        }
    }
}

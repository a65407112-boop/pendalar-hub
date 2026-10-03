# Classic Us Official Roles Backport

Target: Classic Us 2026.9.20 (Windows, IL2CPP)

This mod recreates the official modern Among Us roles inside the older Classic Us client using ClassicUs.Manactor + ClassicUs.ManuAPI.

Implemented roles:
- Crewmate: Scientist, Engineer, Tracker, Noisemaker, Detective, Judge
- Ghost Crewmate: Guardian Angel, Influencer
- Impostor: Shapeshifter, Phantom, Viper

Important design choice:
- Gameplay is recreated for the old client instead of trying to inject modern v19.0.0 IL2CPP classes directly. Modern role classes depend on game systems that do not exist in the old client.
- Networking is host-authoritative through Manactor named RPCs so every modded Classic Us client sees the same results.
- No Innersloth artwork/audio is redistributed. Ability icons are generated at runtime from clean-room geometric glyphs.

Installation:
1. Install a working IL2CPP BepInEx setup for Classic Us.
2. Copy the included BepInEx folder over your Classic Us directory.
3. Make sure every player in the lobby has the same mod version.
4. Start Classic Us. Role counts/chances appear in the game settings menu.

Role behavior:
- Scientist: portable vitals overlay; battery drains while open, tasks recharge it, and meetings close the panel without refilling the battery.
- Engineer: can vent as a Crewmate.
- Tracker: tracks the nearest selected living player for 30 seconds.
- Noisemaker: alerts living players when murdered.
- Detective: receives murder cases and can interrogate up to three nearby suspects for crime-scene proximity clues.
- Judge: after completing two tasks, can use one Overrule during a meeting. Choosing a non-Impostor ejects the Judge.
- Guardian Angel: dead Crewmate ghost role; shields a living player and blocks one kill.
- Influencer: dead Crewmate ghost role; composes three randomized clue cards, can refresh twice, cycles recipient with TAB, and sends the message to a living player.
- Shapeshifter: temporarily copies the target player's visible color/name.
- Phantom: becomes invisible for a limited time.
- Viper: killed bodies dissolve in stages and eventually disappear.

Current clean-room limitations:
- Shapeshifter copies name, color, hat, skin and pet. Classic Us 2026.9.20's exposed player API predates the modern visor field, so visor copying is not available in this target.
- Tracker uses a direction/distance HUD instead of opening the modern map tracker panel.
- Influencer uses generated text cards rather than copyrighted modern image assets.
- Judge uses number keys 1-9 in the meeting overlay instead of the modern v18 UI.
- Detective clues are reconstructed from player distance at the murder moment rather than the exact v17 notebook implementation.

Those differences are UI/compatibility compromises; role state, assignment and multiplayer effects are synchronized through the host.

Source branch:
classicus-official-roles-backport

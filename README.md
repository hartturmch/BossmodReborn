# [![](https://raw.githubusercontent.com/FFXIV-CombatReborn/RebornAssets/main/IconAssets/BMR_Icon.png)](https://github.com/FFXIV-CombatReborn/BossmodReborn)

**BossmodReborn**

![Github Latest Releases](https://img.shields.io/github/downloads/FFXIV-CombatReborn/BossmodReborn/latest/total.svg?style=for-the-badge)
![Github License](https://img.shields.io/github/license/FFXIV-CombatReborn/BossmodReborn.svg?label=License&style=for-the-badge)
[![](https://dcbadge.limes.pink/api/server/p54TZMPnC9)](https://discord.gg/p54TZMPnC9)

BossmodReborn is a community-driven fork of the original Bossmod plugin for Final Fantasy XIV. It aims to enhance your gameplay by providing real-time tactical guidance and tools that simplify complex raid mechanics. This tool is invaluable for optimizing in-game strategies, ensuring precise positioning, and enhancing overall raid performance.

This is extra work for changes to the original BMR, and it does not mean every change I make here will be implemented in the original project. If you want to see the original, check this repository:

https://github.com/FFXIV-CombatReborn/BossmodReborn

## Installation (Dalamud)

1. Open Dalamud settings in-game with `/xlsettings`.
2. Go to `Experimental`.
3. Add this custom plugin repository:
   `https://raw.githubusercontent.com/hartturmch/BossmodReborn/main/pluginmaster.json`
4. Save, then open `/xlplugins`.
5. Search for `BossMod Reborn` and install it.

## Changes

1. Alteração: Added Feint to Melee AI DPS. The `VBM AI` preset now enables Feint for `Melee DPS AI`, and the AI uses it before predicted raidwide/shared damage or tankbusters when the boss is in range.

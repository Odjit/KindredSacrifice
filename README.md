# KindredSacrifice

<p align="center"><em>Offer the living, awaken the altar.</em></p>
<p align="center">
<a href="https://www.youtube.com/watch?v=ENx_fQuE6OI">
  <img src="logo.png" width="320" alt="Watch the trailer" />
    </a>
</p>

<p align="center">
  Server-side blood sacrifice rituals for V Rising: sacrifice prisoners, build blood moon progress, and earn blood-type rewards.
</p>
<hr>

## Features

• **Ritual Sacrifice Cage** lets you place a ritual altar anywhere.  
• **Blood Moon Progression** means sacrifices build toward a blood moon, with quality heavily rewarded.  
• **Blood-Type Rewards** grant unique bonuses for 100% sacrifices.  
• **Lockout Control** enforces a configurable cooldown after a blood moon.


<p align="right"><sub>Questions? Ping <b>odjit</b> on Discord.</sub></p>


## Commands

### General Commands

| Command | Description |
|---|---|
| `.sacrifice place` | Spawn the sacrifice cage at your cursor position. Only one cage may exist at a time. (Admin Only) |
| `.sacrifice goto` | Teleport to the sacrifice cage. Shortcut: `.sac g`  (Admin Only)|
| `.sacrifice remove` | Remove the sacrifice cage. (Admin Only) |
| `.sacrifice reload` | Reload the sacrifice configuration. (Admin Only) |
| `.sacrifice status` | Show blood moon progress and lockout status. (Player Command) |

---

### Settings Commands (Admin Only)

| Command | Description |
|---|---|
| `.sacrifice settings` | Show global settings and reward summary. |
| `.sacrifice reward <bloodType>` | Show reward details for a blood type. Shortcut: `.sac r warrior` |
| `.sacrifice setmessages <true\|false>` | Toggle sacrifice messages. Shortcut: `.sac sm true` |
| `.sacrifice setaccumulation` | Toggle lower-quality blood contributing to blood moon progress. |
| `.sacrifice setlockout <nights>` | Set blood moon lockout nights (minimum 2). Shortcut: `.sac sl 3` |
| `.sacrifice setrewardtype <bloodType> <rewardType>` | Set reward type (None, BloodMoon, Buff, DropItems, DropBloodMerlots). |
| `.sacrifice setbuff <bloodType> <prefabGuid> [duration]` | Configure buff reward. Duration defaults to 3600 seconds. |
| `.sacrifice setmerlot <bloodType> <quality\|prisoner> <min> <max> [type\|prisoner]` | Configure merlot drops. |
| `.sacrifice adddrop <bloodType> <prefabGuid> [min] [max]` | Add an item drop. |
| `.sacrifice removedrop <bloodType> <index>` | Remove an item drop by 1-based index. |
| `.sacrifice cleardrops <bloodType>` | Clear all item drops from a blood type. |



## How to use


1. Spawn the sacrifice cage with `.sac place` while aiming at the ground
2. Perform a sacrifice by bringing a dominated prisoner to the altar cage.
3. Place the dominated prisoner in the cage
4. The sacrifice will automatically trigger.

### Blood Quality & Rewards

**100% Quality**  
Immediate blood moon and blood type based rewards.

**Below 100%**  
No rewards. Progress only to accumulator.


### Blood Moon Accumulator 

The blood moon accumulator uses squared scaling, heavily rewarding high-quality sacrifices:

| Blood Quality | Points Gained | Sacrifices |
|---------------|--------------|-------------------|
| 100% | 10,000 | 1 |
| 71%  | 5,000  | 2 |
| 50%  | 2,500  | 4 |
| 25%  | 625    | 16 |
| 5%   | 25     | 400 |



When total progress reaches **10,000 points**:

1. A blood moon is scheduled for the current or next night.
2. The accumulator resets to 0.
3. Sacrifices enter lockout for a configurable number of nights (default: 3).

**During Lockout:**
- No prisoner is able to be added to the cage
- Use `.sac status` to check remaining lockout time

---
## Configuration

Configuration file is located in `BepInEx/config/KindredSacrifice/`. 
I advise using in-game commands in order to not cause json errors.


<details>
<summary><strong>Configuration Examples</strong></summary>

### Example 1: Different Buffs Per Blood Type
```json
"BloodTypeRewards": {
  "Warrior": {
    "RewardType": "Buff",
    "BuffPrefabGuid": -1006733024,
    "BuffDuration": 3600
  },
  "Scholar": {
    "RewardType": "Buff",
    "BuffPrefabGuid": -238197495,
    "BuffDuration": 7200
  },
  "Brute": {
    "RewardType": "Buff",
    "BuffPrefabGuid": 1068709119,
    "BuffDuration": 3600
  }
}
```

### Example 2: Item Drops for Corrupted Blood
```json
"Corrupted": {
  "RewardType": "DropItems",
  "ItemDrops": [
    { "ItemPrefabGuid": -257494203, "MinQuantity": 5, "MaxQuantity": 10 },
    { "ItemPrefabGuid": 28358550, "MinQuantity": 10, "MaxQuantity": 20 }
  ]
}
```

### Example 3: Prisoner-Quality Merlots for Draculin
```json
"Draculin": {
  "RewardType": "DropBloodMerlots",
  "MerlotMinQuantity": 2,
  "MerlotMaxQuantity": 5,
  "UsePrisonerBloodType": true,    // Use Draculin blood type
  "UsePrisonerQuality": true       // Use 100% quality
}
```

### Example 4: Fixed-Quality Warrior Merlots for Scholar
```json
"Scholar": {
  "RewardType": "DropBloodMerlots",
  "MerlotMinQuantity": 3,
  "MerlotMaxQuantity": 6,
  "UsePrisonerBloodType": false,
  "MerlotBloodType": "Warrior",    // Override to Warrior blood
  "UsePrisonerQuality": false,
  "MerlotQuality": 100.0           // Fixed 100% quality
}
```
</details>
<hr>

## Installation

1. Install [BepInEx 6 (IL2CPP)](https://v-rising.thunderstore.io/package/BepInEx/BepInExPack_V_Rising/) on your V Rising dedicated server
2. Install [VampireCommandFramework](https://v-rising.thunderstore.io/package/deca/VampireCommandFramework/)
3. Copy `KindredSacrifice.dll` to `BepInEx/plugins/`
4. Start the server: configuration files will be created in `BepInEx/config/KindredSacrifice/`

## Credits
- Special thanks to an early playtester for consulting and design feedback.
- [V Rising Modding Community](https://vrisingmods.com) for support and ideas.

## License

This project is licensed under the AGPL-3.0 license.
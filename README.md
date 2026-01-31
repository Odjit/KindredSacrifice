![](logo.png)
# KindredSacrifice for V Rising

KindredSacrifice is a server modification for V Rising that adds a blood sacrifice system with a ritual, blood moon mechanics, and bloodtype based rewards. This gives players something else do to with additional prisoners.
- **Ritual Sacrifice Cage**: Spawn a sacrifice altar wherever you choose.
- **Blood Moon Accumulator**: Sacrifices contribute to triggering blood moons based on quality
- **Blood Rewards**: Get unique rewards for 100% quality sacrifices based on blood type
- **Blood Moon Lockout**: Prevent sacrifices for configurable nights after triggering a blood moon

Feel free to reach out to me on Discord (odjit) if you have any questions or need help with the mod.

## Commands

### Core Commands
- `.sacrifice place`
  - Spawn the sacrifice cage at your cursor position. Only one sacrifice cage can exist at a time. (admin only)
- `.sacrifice goto`
  - Teleport to the sacrifice cage (admin only)
  - Shortcut: `.sac g`
- `.sacrifice remove`
  - Remove the sacrifice cage (admin only)
- `.sacrifice status`
  - Check blood moon progress and lockout status
- `.sacrifice reload`
  - Reload the sacrifice configuration (admin only)


### Settings Commands (admin only)
- `.sacrifice settings`
  - Show global settings and reward type summary 
- `.sacrifice reward <bloodType>`
  - Show full reward details for a blood type 
  - Shortcut: `.sac r warrior`
- `.sacrifice setmessages <true|false>`
  - Toggle sacrifice messages on/off 
  - Shortcut: `.sac sm true`
- `.sacrifice setlockout <nights>`
  - Set blood moon lockout nights, minimum 2 
  - Shortcut: `.sac sl 3`
- `.sacrifice setrewardtype <bloodType> <rewardType>`
  - Set reward type for a blood type 
  - Reward types: None, BloodMoon, Buff, DropItems, DropBloodMerlots
  - Shortcut: `.sac srt warrior Buff`
- `.sacrifice setbuff <bloodType> <prefabGuid> [duration]`
  - Set buff reward for a blood type 
  - Duration defaults to 3600 seconds
  - Shortcut: `.sac sb warrior -1006733024 7200`
- `.sacrifice setmerlot <bloodType> <quality|prisoner> <min> <max> [type|prisoner]`
  - Set merlot drop config for a blood type 
  - Quality: 0-100 or "prisoner" to use prisoner's quality
  - Type: blood type name or "prisoner" to use prisoner's type (default)
  - Shortcut: `.sac smer warrior prisoner 2 5` or `.sac smer scholar 100 3 6 Warrior`
- `.sacrifice adddrop <bloodType> <prefabGuid> [min] [max]`
  - Add an item drop to a blood type 
  - Min/max default to 1
  - Shortcut: `.sac ad creature 28358550 5 10`
- `.sacrifice removedrop <bloodType> <index>`
  - Remove an item drop by index, 1-based 
  - Shortcut: `.sac rd creature 1`
- `.sacrifice cleardrops <bloodType>`
  - Clear all item drops from a blood type 
  - Shortcut: `.sac cd creature`


## Usage

1. Spawn the sacrifice cage with `.sac place` while aiming at the ground
2. Perform a sacrifice by bringing a dominated prisoner to the altar cage.
3. Place the dominated prisoner in the cage
4. The sacrifice will automatically trigger.

### Blood Quality & Rewards
What happens based on blood quality:

- **100% Quality (Perfect Sacrifice)**:
  - Blood type-specific reward (buff, items, or merlots)
  - 10,000 points to blood moon (triggers immediately)

- **Below 100% (Regular Sacrifice)**:
  - NO blood type-specific reward
  - Points to blood moon accumulator (scaled by quality)

### Blood Moon Accumulator

The blood moon accumulator uses squared scaling:
- 100% quality → 10,000 points (instant blood moon)
- 71% quality → 5,000 points (need 2 to trigger)
- 50% quality → 2,500 points (need 4 to trigger)
- 25% quality → 625 points (need 16 to trigger)
- 5% quality → 25 points (need 400 to trigger)

This heavily rewards high-quality sacrifices.

**When accumulated points reach 10,000:**

1. A blood moon is triggered for the current/next night
2. The accumulator is reset to 0
3. Sacrifices are locked out for X nights (configurable, default: 3)

**During Lockout:**
- No prisoner is able to be added to the cage
- Use `.sac status` to check remaining lockout time


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
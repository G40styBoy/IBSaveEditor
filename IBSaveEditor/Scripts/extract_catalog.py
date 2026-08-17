#!/usr/bin/env python3
"""
Generates IBSaveEditor item catalogs from the games' shipped config files.

Input   Catalog/src/<GAME>/*.int    UE3 localization -- display names, descriptions
        Catalog/src/<GAME>/*.ini    UE3 config       -- stats, cost, rarity, sockets, gem effects

Output  Catalog/<GAME>.json            merged item catalog
        Catalog/<GAME>.gems.json       gem name/description composition tokens

Both file families use the same INI-like section convention:

    [Sword_1 SwordInventoryItem]
    FriendlyName=Steel Sword          <- from the .int
    DamageBonus=6                     <- from the .ini

The token after the internal name is the UnrealScript class, which is the
authoritative source of an item's category. Joining the two families on the
internal name yields a catalog with both the player-facing name and the
underlying stats.

Sections with no class token are UI/config string tables, not items. The one
exception is [Gems] in the .int, which holds the format templates the game uses
to COMPOSE gem names and effect descriptions at runtime -- gems are not
enumerated by name, they are built from their own field values.

Re-run after changing anything under Catalog/src. Never hand-edit the output.
"""
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Catalog" / "src"
OUT = ROOT / "Catalog"

# UnrealScript class -> catalog category. None means "refine by name prefix".
CLASS_CATEGORY = {
    "SwordInventoryItem": None,
    "SwordInventoryMPItem": None,
    "SwordInventorySPItem": None,
    "SwordInventoryBossItem": "bossItem",
    "SwordInventoryItemGem": "gem",
    "SwordInventoryItemKey": "key",
    "SwordInventoryItemPotion": "potion",
    "SwordInventoryItemRandom": "treasure",
    "SwordInventoryItemWorld": "ingredient",
    "SwordInventoryPlayerAbility": "ability",
    "SwordInventoryBattleChallenges": "challenge",
    "SwordQuestData": "quest",
}

PREFIX_CATEGORY = {
    "Sword": "weapon",
    "Shield": "shield",
    "Helmet": "helmet",
    "Armor": "armor",
    "Magic": "magic",
}

# Gem ItemSubType is the socket shape the gem requires. Legend is documented in
# the comment header of DefaultGems.ini.
SOCKET_TYPES = {
    1: "Stats",
    2: "Elemental Damage",
    3: "General",
    4: "Combat",
    5: "Uber",
    6: "Light",
    7: "Heavy",
    8: "Dual",
}

NAME_KEYS = ("FriendlyName", "DisplayName")
TEXT_KEYS = ("Description", "FriendlyNamePlural", "PotionName", "PotionDescription")

STAT_KEYS = {
    "DamageBonus": "damage", "HealthBonus": "health", "ShieldBonus": "shield",
    "MagicBonus": "magic", "StaminaBonus": "stamina",
    "FireBonus": "fire", "IceBonus": "ice", "ElecBonus": "elec",
    "PoisonBonus": "poison", "LightBonus": "light", "DarkBonus": "dark",
    "WindBonus": "wind", "WaterBonus": "water",
}

CORE_KEYS = {
    "ItemType": "itemType", "ItemSubType": "itemSubType", "BaseLevel": "baseLevel",
    "Cost": "cost", "FixedBaseLevelCost": "fixedBaseLevelCost",
    "ItemRare": "itemRare", "HiddenLevel": "hiddenLevel",
    "LevelXPScale": "levelXpScale", "BonusCombo": "bonusCombo",
}

GEM_KEYS = {
    "BattleTrigger": "battleTrigger", "BattleEffect": "battleEffect",
    "BattleEffectValue": "battleEffectValue", "BattleEffectDuration": "battleEffectDuration",
    "BattleEffectRetriggerTime": "battleEffectRetriggerTime",
    "BattleTriggerRequiredCount": "battleTriggerRequiredCount",
    "BattleEffectRewardType": "battleEffectRewardType",
    "RecipeMatch": "recipeMatch", "RecipeBoostAmount": "recipeBoostAmount",
    "MPParent": "mpParent", "PotionType": "potionType", "Restrict": "restrict",
    "MaxRandomAddPct": "maxRandomAddPct", "FillColor": "fillColor",
    "PerBloodlineCost": "perBloodlineCost",
}

VISUAL_KEYS = {"IconPath": "iconPath", "IconUV": "iconUV", "MeshPath": "meshPath"}

BOOL_KEYS = {"bUniqueItem": "unique", "bHidden": "hidden", "IsSaber": "isSaber"}

INDEXED_KEYS = {"Socket": "sockets", "UpgradeTier": "upgradeTiers",
                "MagicSpells": "magicSpells", "MagicSpellLevel": "magicSpellLevels"}


def load_text(path: Path) -> str:
    """UE3 ships these as UTF-16LE with BOM or plain ANSI, inconsistently."""
    raw = path.read_bytes()
    if raw[:2] in (b"\xff\xfe", b"\xfe\xff"):
        return raw.decode("utf-16")
    try:
        return raw.decode("utf-8-sig")
    except UnicodeDecodeError:
        return raw.decode("latin-1")


def parse_sections(text: str):
    """Yields (header, {key: value}). Comment lines are dropped -- the config
    files carry large commented-out example blocks that are not live data."""
    sections, current = [], None
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith(";"):
            continue
        header = re.match(r"^\[([^\]]+)\]$", line)
        if header:
            current = (header.group(1), {})
            sections.append(current)
            continue
        if current and "=" in line:
            key, value = line.split("=", 1)
            current[1].setdefault(key.strip(), value.strip())
    return sections


def coerce(value: str):
    """Config values are untyped strings. Narrow the obvious ones."""
    if re.fullmatch(r"-?\d+", value):
        return int(value)
    if re.fullmatch(r"-?\d*\.\d+", value):
        return float(value)
    low = value.lower()
    if low in ("true", "false"):
        return low == "true"
    return value


def categorize(internal_name: str, cls: str) -> str:
    category = CLASS_CATEGORY.get(cls, "unknown")
    if category is None:
        prefix = re.match(r"^([A-Za-z]+)", internal_name)
        category = PREFIX_CATEGORY.get(prefix.group(1) if prefix else "", "item")
    return category


def collect(game_dir: Path):
    """Returns (names, defs, gem_tokens, classes) keyed by internal name."""
    names, defs, classes, gem_tokens = {}, {}, {}, {}

    for path in sorted(game_dir.iterdir()):
        if path.suffix.lower() not in (".int", ".ini"):
            continue
        is_loc = path.suffix.lower() == ".int"

        for header, kv in parse_sections(load_text(path)):
            parts = header.split()

            if len(parts) != 2:
                if is_loc and parts and parts[0] == "Gems":
                    gem_tokens.update(kv)
                continue

            internal_name, cls = parts
            # Only inventory-ish classes are items; this filters out input zones,
            # scene definitions and other engine config that shares the format.
            if not (cls.startswith("SwordInventory") or cls in CLASS_CATEGORY):
                continue

            classes.setdefault(internal_name, cls)
            target = names if is_loc else defs
            target.setdefault(internal_name, {}).update(kv)

    return names, defs, gem_tokens, classes


def build_entry(internal_name, cls, loc_kv, def_kv):
    entry = {
        "internalName": internal_name,
        "displayName": next((loc_kv[k] for k in NAME_KEYS if k in loc_kv), None),
        "category": categorize(internal_name, cls),
        "class": cls,
    }

    variant = re.match(r"^(.*?)_(\d+)$", internal_name)
    if variant:
        entry["nameBase"] = variant.group(1)

    for key in TEXT_KEYS:
        if key in loc_kv:
            entry[key[0].lower() + key[1:]] = loc_kv[key]

    for src, dst in CORE_KEYS.items():
        if src in def_kv:
            entry[dst] = coerce(def_kv[src])

    if entry.get("category") == "gem" and "itemSubType" in entry:
        entry["socketType"] = SOCKET_TYPES.get(entry["itemSubType"])

    stats = {dst: coerce(def_kv[src]) for src, dst in STAT_KEYS.items() if src in def_kv}
    if stats:
        entry["stats"] = stats

    gem = {dst: coerce(def_kv[src]) for src, dst in GEM_KEYS.items() if src in def_kv}
    if gem:
        entry["gem"] = gem

    visual = {dst: def_kv[src] for src, dst in VISUAL_KEYS.items() if src in def_kv}
    if visual:
        entry["visual"] = visual

    flags = {dst: coerce(def_kv[src]) for src, dst in BOOL_KEYS.items() if src in def_kv}
    if flags:
        entry["flags"] = flags

    # Indexed keys (Socket[0], UpgradeTier[1], ...) collapse into ordered lists.
    indexed = defaultdict(dict)
    for key, value in def_kv.items():
        match = re.fullmatch(r"([A-Za-z]+)\[(\d+)\]", key)
        if match and match.group(1) in INDEXED_KEYS:
            indexed[INDEXED_KEYS[match.group(1)]][int(match.group(2))] = coerce(value)
    for name, by_index in indexed.items():
        entry[name] = [by_index[i] for i in sorted(by_index)]

    return entry


def build(game: str, game_dir: Path):
    names, defs, gem_tokens, classes = collect(game_dir)

    items = []
    for internal_name in sorted(set(names) | set(defs)):
        items.append(build_entry(internal_name, classes.get(internal_name, "unknown"),
                                 names.get(internal_name, {}), defs.get(internal_name, {})))

    items.sort(key=lambda e: (e["category"], e["internalName"]))

    named = sum(1 for e in items if e["displayName"])
    statted = sum(1 for e in items if "stats" in e or "cost" in e)

    catalog = {
        "game": game,
        "catalogVersion": 2,
        "entryCount": len(items),
        "withDisplayName": named,
        "withDefinition": statted,
        "categoryCounts": dict(sorted(Counter(e["category"] for e in items).items())),
        "socketTypes": {str(k): v for k, v in SOCKET_TYPES.items()},
        "items": items,
    }
    (OUT / f"{game}.json").write_text(
        json.dumps(catalog, indent=2, ensure_ascii=False), encoding="utf-8")

    if gem_tokens:
        (OUT / f"{game}.gems.json").write_text(json.dumps({
            "game": game,
            "catalogVersion": 2,
            "note": ("Runtime composition tokens. Gem display names and effect text are BUILT "
                     "from a gem's own field values (ItemSubType, stat bonuses, BattleTrigger, "
                     "BattleEffect) using these templates -- they are not looked up by name."),
            "tokens": gem_tokens,
        }, indent=2, ensure_ascii=False), encoding="utf-8")

    return len(items), named, statted, len(gem_tokens), catalog["categoryCounts"]


def main():
    if not SRC.is_dir():
        sys.exit(f"missing source directory: {SRC}")
    game_dirs = sorted(d for d in SRC.iterdir() if d.is_dir())
    if not game_dirs:
        sys.exit(f"no per-game source directories under {SRC}")

    for game_dir in game_dirs:
        total, named, statted, tokens, counts = build(game_dir.name, game_dir)
        print(f"{game_dir.name:5s} {total:5d} entries  {named:5d} named  "
              f"{statted:5d} defined  {tokens:4d} gem tokens")
        print("      " + "  ".join(f"{k}={v}" for k, v in counts.items()))


if __name__ == "__main__":
    main()

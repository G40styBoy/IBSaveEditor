
# Save Editing Guide

Reference example structure: Deserialized Save Data.json

---

# ⚠️ Before You Start

- Always create a backup of your original save file.
- Only edit the deserialized JSON file.
- Use a proper editor (VS Code, Notepad++, etc.).
- Do not break JSON formatting.
- Do not add comments (JSON does not support comments).

If the JSON structure breaks, the save will fail to load.

---

# JSON Structure Basics

## Objects

```json
{
  "PropertyA": 10,
  "PropertyB": true
}
```

Curly braces `{}` group properties together.

---

## Arrays

```json
"SomeArray": [
  { "Value": 1 },
  { "Value": 2 }
]
```

Square brackets `[]` represent a list of values or objects.

Rules:
- Items must be separated by commas.
- Do not remove brackets.
- Do not leave trailing commas.

---

# Data Types

---

## 1. Integers (Whole Numbers)

Format:

```json
"SomeValue": 123
```

Rules:
- No quotes.
- No decimal points.
- Maximum safe value: 2147483647 (32-bit limit).

---

## 2. Floats (Decimal Numbers)

Format:

```json
"SomeFloat": 0.25
```

Rules:
- Must include a decimal point.
- No quotes.
- Avoid unrealistic values unless testing.

---

## 3. Booleans

Format:

```json
"SomeFlag": true
```

Only valid values:
- true
- false

Rules:
- Lowercase only.
- No quotes.

---

## 4. Bytes (Small Integers, Often Prefixed with `b`)

Format:

```json
"bSomeTier": 5
```

Rules:
- Integer only.
- Typically 0–255.
- No decimal.
- No quotes.

---

## 5. Strings (Normal Text Values)

Format:

```json
"PreReqValue": ""
```

Rules:
- Must always be wrapped in quotes.
- Can contain text, numbers, or be empty.
- Empty string is valid: ""

---

## 6. FNames (Special Game Identifiers)

Example:

```json
"ini_ItemName": "Sword_350"
```

Rules:
- Case-sensitive.
- Must match valid internal identifiers.
- Invalid names may cause crashes or loading errors.

---

## 7. Enums

Format:

```json
"SomeEnumProperty": {
  "Enum": "EnumTypeName",
  "Enum Value": "SomeValue"
}
```

Rules:
- Do not remove the object structure.
- Do not change "Enum" unless you know what you're doing.
- Change only "Enum Value" to another valid value.
- Values must remain quoted strings.

---

## 8. Nested Objects

Format:

```json
"SomeObject": {
  "InnerValue": 5,
  "InnerFlag": false
}
```

Rules:
- Keep curly braces.
- Separate properties with commas.
- Edit internal values normally based on type.

---

# Editing Inside Arrays

Example:

```json
"PlayerInventory": [
  {
    "ini_ItemName": "Sword_350",
    "NumberPlayerHas": 1
  }
]
```

You may:
- Modify values.
- Rename variable names inside array objects if supported.
- Add additional objects if the structure supports it.

You must:
- Keep proper commas.
- Maintain valid JSON formatting.
- Keep required properties unless you know they are optional.

---

# Critical JSON Rules

Never:
- Remove commas between properties.
- Remove quotation marks from strings.
- Add comments.
- Leave trailing commas.
- Break bracket structure.

Correct:

```json
{
  "ValueA": 10,
  "ValueB": 20
}
```

Incorrect:

```json
{
  "ValueA": 10
  "ValueB": 20
}
```

---

# Safe Editing Workflow

1. Backup original save.
2. Modify one value at a time.
3. Save.
4. Validate JSON formatting.
5. Re-import.
6. Test in game.

If broken:
- Restore backup.
- Undo last change.

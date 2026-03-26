# Infinity Blade Save Editor

A desktop save editor for the Infinity Blade Trilogy. Load, inspect, and modify save data through a clean UI.

---

## How It Works

Save files for Infinity Blade games are stored as binary `.bin` packages. The editor works in three steps:

**1. Deserialize**
Click **Deserialize .bin** and select a save file. The editor reads the binary package, decrypts it if necessary, and writes a `.json` file to the `OUTPUT` folder.

**2. Edit**
Click **Open** and load the exported `.json`. All save properties appear in the tree on the left. Click any property to view and edit its value on the right. Structs and arrays can be expanded to browse and modify their members.

**3. Serialize**
Click **Save** to write your changes, then click **Serialize .json** to rebuild the `.bin` package. The output is written to the `OUTPUT` folder.

---

## Interface Overview

| Panel | Purpose |
|---|---|
| Left tree | All save properties, expandable by clicking the arrow |
| Right panel | Value editor for the selected property |
| Log panel | Timestamped record of all file operations |
| Toolbar | Open, Save, Reload, Deserialize .bin, Serialize .json |

---

## Features

- **Deserialize and serialize** — Convert between `.bin` save packages and editable JSON
- **Full property editing** — Edit integers, floats, booleans, strings, bytes, enums, structs, and arrays
- **Add and remove entries** — Add new members to structs and arrays with type selection, or remove existing ones
- **Duplicate entries** — Clone any struct member or array item with a single click
- **Encrypted save support** — Detects and handles encrypted save packages automatically

---

## Supported Property Types

| Type | Description |
|---|---|
| `int` | Integer |
| `float` | Floating point number |
| `bool` | True or false |
| `byte` | Value from 0 to 255 |
| `string` | Plain text |
| `name` | Friendly name reference |
| `enum` | Named enum type and value pair |
| `struct` | Nested group of named properties |
| `array` | Ordered list of items |

---

## Output

All processed files are written to the `OUTPUT` directory located beside the executable. Existing files with the same name will be overwritten.

---

## Notes

- This tool is still under active development
- Error messages are surfaced in the log panel.

---

## Credits

- **Hox8** -> AES keys for each game

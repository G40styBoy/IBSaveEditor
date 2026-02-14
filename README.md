# Infinity Blade Save Editor

**IBSaveEditor** is a simple tool for handling save file packages for the Infinity Blade Trilogy. It allows you to extract, modify, and repackage save data with ease.

---

## Features
- **Simple interface**: Drag-and-drop support for `.bin` and `.json` files.  
- **Encrypted save support**: Recognizes and decrypts encrypted save packages.  
- **Deserialize saves**: Converts `.bin` save files into readable `.json` files.  
- **Repackage saves**: Recalculates and repackages the deserialized file back into its original format.

---

## Documentation

- [Save Editing Guide](Save-Editing-Guide.md)

This guide explains how to safely edit the deserialized `.json` file, including data types, arrays, enums, and formatting rules.

---

## How it Works
1. Drag and drop a `.bin` save file into the program.
2. The program exports the save data as a `.json` file in the `OUTPUT` folder.
3. Modify the `.json` file as needed (see **Save Editing Guide**).
4. Drag and drop the modified `.json` file into the program.
5. The program transforms your modified data back into the original save format.
   
---

## Notes
- Tool is still in early stages of development. If bugs are encountered please create an issue.
- Always create backups before modifying save data.
- Invalid JSON formatting will prevent repackaging.

---

## Credits
- Hox8 for sending me all AES keys for each game.

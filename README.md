# Infinity Blade Save Editor

**IBSaveEditor** is a simple tool for handling save file packages for the Infinity Blade Trilogy. It allows you to extract, modify, and repackage save data with ease.

---

## Features
- **Simple interface**: Drag-and-drop support for `.bin` and `.json` files.  
- **Encrypted save support**: Recognizes and decrypts encrypted save packages.  
- **Deserialize saves**: Converts `.bin` save files into readable `.json` files.  
- **Repackage saves**: Recalculates and repackages the deserialized file back into its original format.

---

## How it Works
1. Drag and drop a `.bin` save file into the program.
2. The program exports the save data as a `.json` file in the `OUTPUT` folder.
3. Modify the `.json` file as needed.
4. Drag and drop the modified `.json` file into the program.
5. The program transforms your modified data back into the original save format.

---

## ❓ Not sure what save files are supported? Check here!
- Unencrypted IB3 ✅
- Unencrypted/Encrypted IB2 ✅
- Unencrypted/Encrypted IB1 ❌

---

## ⚠️ Notes
- Tool is still in early stages of development. If bugs are encountered please create an issue. This will be released whenever im satisfied with the build quality.

---
## Credits
- Hox8 for sending me all AES keys for each game.

# Infinity Blade Save Editor

**IBSaveEditor** is a utility for working with save packages from the Infinity Blade Trilogy. It allows you to convert, inspect, modify, and rebuild save data through a structured JSON format.

---

## Features

- **Drag-and-drop input**: Drop either a `.bin` or `.json` file directly into the application.  
- **Automatic processing**: The tool detects the file type and performs the correct operation automatically.  
- **Encrypted save support**: Detects and decrypts encrypted save packages when necessary.  
- **Binary to JSON conversion**: Converts `.bin` save packages into a structured, editable `.json` file.  
- **JSON to binary rebuild**: Reconstructs a valid save package from a modified `.json` file.  
- **Structured JSON envelope**: Outputs save data in a clear `metadata` and `data` format for safer editing.  
- **Validation and diagnostics**: Includes strong JSON validation and detailed error messages to help identify formatting or structural issues.  

---

## Usage

- Drop a `.bin` file into the program to extract it.  
- Drop a `.json` file into the program to rebuild it.  
- All generated files are written automatically to the `OUTPUT` folder.  

No manual step switching or mode selection is required. The tool determines the correct action based on the file extension.

---

## JSON Format

Exported save files use a structured envelope:

- `metadata`: Contains package information such as game type, encryption state, version, and identifiers.  
- `data`: Contains the full serialized save data.  

When editing:

- Preserve the overall structure.  
- Do not remove required metadata fields.  
- Follow correct data types for properties.  

---

## Documentation

- [Save Editing Guide](GUIDE.md)

The guide explains:

- Property types (int, float, bool, string, byte, enum, struct, array)  
- Enum formatting rules  
- Array structure requirements  
- Common editing mistakes to avoid  

---

## Output

All processed files are placed in the `OUTPUT` directory located beside the executable. Existing files may be overwritten if names match.

---

## Notes

- This tool is still under active development.  
- Error messages are designed to surface structural or formatting problems clearly.  
- Invalid JSON structure or mismatched metadata may prevent rebuilds.  

---

## Credits

- Hox8 for providing AES keys for each game.

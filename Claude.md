## WHAT
IBSaveEditor is a desktop tool for deserializing, editing, and reserializing Infinity Blade save files (Unreal Engine 3 `.bin` packages). The frontend is built in Avalonia (MVVM, ReactiveUI). The backend handles the full round-trip pipeline for UProperty-based save data.

### Pipeline (in order)
1. **Deserialize** — `UnrealPackage` reads the `.bin` file, handles decryption if necessary, and produces a list of `UProperty` objects (intermediate format)
2. **Write JSON** — `JsonDataParser` takes the `UProperty` list and writes a structured `.json` file with a `metadata` + `data` envelope
3. **Read JSON** — `JsonDataCruncher` reads the `.json` back into the intermediate format
4. **Serialize** — `Serializer` takes the intermediate format and rebuilds the `.bin` package, performing any necessary recalculations

### UI Layer
The UI allows the user to open the exported `.json`, browse and edit all save properties, and save changes back. The edited JSON is then serialized back to `.bin` via the toolbar.

**Important:** The UI stores the raw JSON string on load (`_originalJson`) and re-parses it fresh on every save. This preserves the `metadata` envelope intact — only the `data` section is replaced with the edited node tree. Do not change this to use a `JObject` reference — shared `JToken` references cause silent mutations.

## WHY
+
## Project Structure
```
IBSaveEditor/
  Package/         Decryption, encryption, package type identification, reading package data
  Serialize/       Deserialization and serialization of UProperty data
  Json/            JSON read/write pipeline (JsonDataParser, JsonDataCruncher, JsonUtils)
  UProperty/       UProperty base class, definitions, and helper logic
    UArray/        Array-specific UProperty types and registry
  Wrappers/        UnrealBinaryWriter — BinaryWriter extended for Unreal package format
  Util/            General utilities used across the codebase
  Models/          SaveNode types used by the UI node tree (PrimitiveNode, StructNode, etc.)
  Services/        JsonToNodeTree and NodeTreeToJson — convert between SaveNode tree and JObject
  ViewModels/      MVVM ViewModels (MainWindowViewModel, NodeViewModel, converters)
  Views/           Avalonia AXAML views and code-behind
  Styles/          EditorStyles.axaml — all visual styling in one file
```

## Games Supported
IB1, IB2, IB3, VOTE. All games share the same architectural pipeline — the only difference between them is encryption magic values, which are constants.

## DO NOT TOUCH
- **`Package/PackageConstants.cs`** — Contains game-specific constants including AES keys. Do not modify.

## Build & Run
```bash
# Build
dotnet build

# Run
dotnet run --project IBSaveEditor/IBSaveEditor.csproj

# Test
dotnet test

# Publish (single file, self-contained, Windows x64)
dotnet publish IBSaveEditor/IBSaveEditor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Dependencies
- [Avalonia 11.2.3](https://avaloniaui.net/) — cross-platform UI framework
- [ReactiveUI](https://reactiveui.net/) — MVVM framework used with Avalonia
- [Newtonsoft.Json](https://www.newtonsoft.com/json) — JSON parsing and serialization
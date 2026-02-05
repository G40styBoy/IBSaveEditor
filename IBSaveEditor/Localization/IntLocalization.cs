using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace IBSaveEditor.Localization;

/// <summary>
/// loads SwordGame.int and provides lookup tables for internal IDs & display names.
/// </summary>
public static class IntLocalization
{
    public static readonly Dictionary<string, string> KeyToName = new();

    private static bool loaded = false;

    public static void LoadFromExecutableDirectory(string fileName = "SwordGame.int")
    {
        string fullPath = Path.Combine(FilePaths.Localization, fileName);
        File.ReadAllText(fullPath);
    }

    
}

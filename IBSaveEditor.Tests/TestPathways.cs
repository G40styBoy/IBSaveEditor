namespace IBSaveEditor.Tests
{
    public static class TestPathways
    {
        private readonly static string projectRoot = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..")
        );        
        private readonly static string filesPath = Path.Combine(projectRoot, "Files");
        private readonly static string repoRoot  = Path.GetFullPath(Path.Combine(projectRoot, ".."));

        /// <summary>
        /// The user's personal real-save archive, a sibling of this test project
        /// (gitignored as "SAVE STORAGE LOCATION/"). Not present for most
        /// contributors : callers must check <see cref="Directory.Exists(string)"/>
        /// and no-op rather than fail when it's missing.
        /// </summary>
        private readonly static string saveStorageLocation = Path.Combine(repoRoot, "SAVE STORAGE LOCATION");

        /// <summary>Where corpus-derived reports (census, catalog coverage, ...) get written.</summary>
        private readonly static string docsSchemaDir = Path.Combine(repoRoot, "docs", "schema");

        public static string GetFileLocation() => filesPath;

        public static string GetRepoRoot() => repoRoot;

        public static string GetSaveStorageLocation() => saveStorageLocation;

        public static string GetDocsSchemaDir() => docsSchemaDir;
    }

    
}
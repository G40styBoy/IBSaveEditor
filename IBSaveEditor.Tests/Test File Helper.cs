namespace IBSaveEditor.Tests
{
    public static class TestPathways
    {
        private readonly static string projectRoot = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..")
        );        
        private readonly static string filesPath = Path.Combine(projectRoot, "Files");

        public static string GetFileLocation() => filesPath;
    }

    
}
namespace eShop.Catalog.Api.Tests.Support;

internal static class RepoRoot
{
    public static string Path
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(System.IO.Path.Combine(dir.FullName, "eShop.slnx")))
                {
                    return dir.FullName;
                }
            }

            throw new InvalidOperationException("Could not find the repository root from the test output directory.");
        }
    }
}

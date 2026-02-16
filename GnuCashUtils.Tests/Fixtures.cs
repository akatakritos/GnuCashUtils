namespace GnuCashUtils.Tests;

public static class Fixtures
{
    public static string File(string name)
    {
        var baseDir = Path.GetDirectoryName(typeof(Fixtures).Assembly.Location)!;
        return Path.Combine(baseDir, "Fixtures", name);
    }
}

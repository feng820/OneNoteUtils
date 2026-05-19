using System.Reflection;

namespace OneNoteUtils.Core.Tests;

internal static class FixtureHelper
{
    public static string LoadFixture(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"OneNoteUtils.Core.Tests.Fixtures.{name}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Fixture '{resourceName}' not found. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

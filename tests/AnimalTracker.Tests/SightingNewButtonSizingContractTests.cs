namespace AnimalTracker.Tests;

public sealed class SightingNewButtonSizingContractTests
{
    [Fact]
    public void Add_sighting_page_does_not_use_small_buttons_for_actions()
    {
        var repoRoot = GetRepoRoot();
        var path = Path.Combine(repoRoot, "src", "AnimalTracker", "Components", "Pages", "Sightings", "New.razor");
        var content = File.ReadAllText(path);

        // Contract: keep action buttons consistent size (no UiButtonSize.Sm) on this page.
        Assert.DoesNotContain("Size=\"UiButtonSize.Sm\"", content, StringComparison.Ordinal);
    }

    private static string GetRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Directory.GetParent(dir)?.FullName;
            if (candidate is null)
                break;

            if (Directory.Exists(Path.Combine(candidate, "src"))
                && Directory.Exists(Path.Combine(candidate, "tests")))
            {
                return candidate;
            }

            dir = candidate;
        }

        throw new InvalidOperationException("Could not locate repository root from test base directory.");
    }
}


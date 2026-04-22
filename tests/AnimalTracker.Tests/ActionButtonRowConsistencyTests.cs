namespace AnimalTracker.Tests;

public sealed class ActionButtonRowConsistencyTests
{
    private const string ActionRowClass =
        "flex flex-col-reverse gap-3 pt-2 sm:flex-row sm:flex-wrap sm:justify-end sm:gap-3";

    [Theory]
    [InlineData("src/AnimalTracker/Components/Pages/Sightings/Import.razor")]
    [InlineData("src/AnimalTracker/Components/Pages/Sightings/New.razor")]
    [InlineData("src/AnimalTracker/Components/Pages/Sightings/Edit.razor")]
    [InlineData("src/AnimalTracker/Components/Pages/Animals/New.razor")]
    [InlineData("src/AnimalTracker/Components/Pages/Animals/Edit.razor")]
    public void Primary_secondary_action_row_uses_standard_UIButton_variants(string relativePath)
    {
        var repoRoot = GetRepoRoot();
        var path = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var content = File.ReadAllText(path);

        var row = ExtractActionRow(content);

        // Contract:
        // - Secondary action uses UIButton Outline + w-full sm:w-auto
        // - Primary action uses UIButton Primary + w-full sm:w-auto
        // This catches drift like raw <button>, missing variants, or missing layout classes.
        Assert.Contains("<UIButton", row, StringComparison.Ordinal);
        Assert.Contains("Variant=\"UiButtonVariant.Outline\"", row, StringComparison.Ordinal);
        Assert.Contains("Variant=\"UiButtonVariant.Primary\"", row, StringComparison.Ordinal);
        Assert.Contains("Class=\"w-full sm:w-auto\"", row, StringComparison.Ordinal);
    }

    private static string ExtractActionRow(string content)
    {
        var idx = content.IndexOf(ActionRowClass, StringComparison.Ordinal);
        if (idx < 0)
            throw new Xunit.Sdk.XunitException($"Expected standard action row class not found: '{ActionRowClass}'.");

        // Find the opening <div ...> containing that class, then return until its closing </div>.
        var open = content.LastIndexOf("<div", idx, StringComparison.Ordinal);
        if (open < 0)
            throw new Xunit.Sdk.XunitException("Could not locate opening <div> for action row.");

        var close = content.IndexOf("</div>", idx, StringComparison.Ordinal);
        if (close < 0)
            throw new Xunit.Sdk.XunitException("Could not locate closing </div> for action row.");

        return content.Substring(open, (close - open) + "</div>".Length);
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


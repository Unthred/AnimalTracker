using System.Text;

namespace AnimalTracker.Tests;

public sealed class FormStylingContractStaticTests
{
    private const string StandardControlClass =
        "w-full min-w-0 rounded-xl border border-slate-200/90 bg-white px-3 py-2.5 text-sm shadow-sm transition duration-200 ease-in-out hover:border-slate-300 hover:shadow-sm focus:border-slate-300 focus:outline-none focus:ring-4 focus:ring-slate-200/70 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-50 dark:hover:border-slate-600 dark:focus:ring-slate-800/80";

    [Fact]
    public void Razor_forms_do_not_introduce_unstyled_text_inputs()
    {
        var repoRoot = GetRepoRoot();
        var srcRoot = Path.Combine(repoRoot, "src", "AnimalTracker");
        var razorFiles = Directory.GetFiles(srcRoot, "*.razor", SearchOption.AllDirectories);

        var failures = new List<string>();

        foreach (var file in razorFiles)
        {
            var content = File.ReadAllText(file);
            var tags = ExtractTags(content);

            foreach (var tag in tags)
            {
                if (!IsControlWeCareAbout(tag))
                    continue;

                if (IsExplicitlyExcluded(tag))
                    continue;

                if (!TagHasStandardClass(tag))
                {
                    failures.Add($"{MakeRelative(repoRoot, file)}: {Summarize(tag)}");
                }
            }
        }

        if (failures.Count > 0)
        {
            var message = "Form styling contract violated. These controls are missing the standard control class.\n"
                          + "Expected class (copy exactly):\n"
                          + StandardControlClass
                          + "\n\nOffenders:\n- "
                          + string.Join("\n- ", failures);
            throw new Xunit.Sdk.XunitException(message);
        }
    }

    private static bool IsControlWeCareAbout(string tag)
    {
        // We check common text-ish controls that frequently regress to default styling.
        // Note: shared UI components (e.g. UIDatePicker) are not matched here.
        return tag.StartsWith("<InputText", StringComparison.Ordinal)
               || tag.StartsWith("<InputNumber", StringComparison.Ordinal)
               || tag.StartsWith("<InputTextArea", StringComparison.Ordinal)
               || tag.StartsWith("<InputSelect", StringComparison.Ordinal)
               || tag.StartsWith("<select", StringComparison.Ordinal)
               || tag.StartsWith("<textarea", StringComparison.Ordinal);
    }

    private static bool IsExplicitlyExcluded(string tag)
    {
        // These are intentionally styled differently or not text-like.
        if (tag.StartsWith("<InputFile", StringComparison.Ordinal))
            return true;
        if (tag.StartsWith("<InputCheckbox", StringComparison.Ordinal))
            return true;

        // Allow explicit opt-out for rare cases (e.g., third-party component wrappers).
        // Usage: add data-style-contract="ignore" on the element.
        if (tag.Contains("data-style-contract=\"ignore\"", StringComparison.OrdinalIgnoreCase))
            return true;

        // SearchableCombobox / UISpeciesSelect / UILocationNameCombobox / UIDatePicker are components,
        // so they won't be matched by IsControlWeCareAbout anyway.
        return false;
    }

    private static bool TagHasStandardClass(string tag)
    {
        // Accept exact match or additional classes around it (e.g. extra spacing/width classes).
        // We require the exact standard string to appear as a contiguous substring.
        return tag.Contains($"class=\"{StandardControlClass}\"", StringComparison.Ordinal)
               || tag.Contains($"class='{StandardControlClass}'", StringComparison.Ordinal)
               || tag.Contains(StandardControlClass, StringComparison.Ordinal)
               // Allow shared constant bindings used in a few pages.
               || tag.Contains("class=\"@StandardInputClass\"", StringComparison.Ordinal)
               || tag.Contains("class='@StandardInputClass'", StringComparison.Ordinal)
               || tag.Contains("class=\"@StandardControlClass\"", StringComparison.Ordinal)
               || tag.Contains("class='@StandardControlClass'", StringComparison.Ordinal);
    }

    private static IEnumerable<string> ExtractTags(string content)
    {
        // Lightweight tag extractor: finds `<...>` blocks (including multiline) and returns them.
        // Good enough for our contract checks without adding an HTML parser dependency.
        var tags = new List<string>();
        var sb = new StringBuilder();
        var inTag = false;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (!inTag)
            {
                if (c == '<')
                {
                    inTag = true;
                    sb.Clear();
                    sb.Append(c);
                }
                continue;
            }

            sb.Append(c);

            if (c == '>')
            {
                inTag = false;
                tags.Add(sb.ToString().Trim());
            }
        }

        return tags;
    }

    private static string Summarize(string tag)
    {
        tag = tag.Replace("\r", " ").Replace("\n", " ");
        if (tag.Length <= 240)
            return tag;
        return tag[..240] + "...";
    }

    private static string GetRepoRoot()
    {
        // Test assembly runs from tests/bin/... so climb until we find /src and /tests.
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

    private static string MakeRelative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}


namespace AnimalTracker.Components.UI;

/// <summary>Single option in a <see cref="SearchableCombobox{TValue}"/> list (value + visible label).</summary>
public readonly record struct ComboboxOption<TValue>(TValue Value, string Text);

/// <summary>Helpers to build common option lists for <see cref="SearchableCombobox{TValue}"/>.</summary>
public static class ComboboxOptionList
{
    public static ComboboxOption<TEnum?>[] ForNullableEnum<TEnum>(string emptyLabel = "Not specified")
        where TEnum : struct, Enum
    {
        var list = new List<ComboboxOption<TEnum?>> { new(default, emptyLabel) };
        foreach (var e in Enum.GetValues<TEnum>())
            list.Add(new ComboboxOption<TEnum?>(e, e.ToString() ?? ""));
        return list.ToArray();
    }

    public static ComboboxOption<TEnum?>[] ForNullableEnumValues<TEnum>()
        where TEnum : struct, Enum
    {
        var list = new List<ComboboxOption<TEnum?>>();
        foreach (var e in Enum.GetValues<TEnum>())
            list.Add(new ComboboxOption<TEnum?>(e, e.ToString() ?? ""));
        return list.ToArray();
    }

    /// <summary>Theme mode values for user/admin settings (matches stored strings: system, light, dark).</summary>
    public static ComboboxOption<string>[] UserThemeModes { get; } =
    [
        new("system", "Follow system"),
        new("light", "Light"),
        new("dark", "Dark")
    ];

    public static ComboboxOption<int>[] TimelinePageSizes { get; } =
    [
        new(25, "25"),
        new(50, "50"),
        new(100, "100")
    ];
}

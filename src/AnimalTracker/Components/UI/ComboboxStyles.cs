namespace AnimalTracker.Components.UI;

/// <summary>Shared Tailwind tokens for combobox trigger, chevron, and list items.</summary>
internal static class ComboboxStyles
{
    internal const string Input =
        "w-full min-w-0 rounded-xl border border-slate-200/90 bg-white py-2.5 pl-3 pr-16 text-sm font-medium text-slate-900 shadow-sm transition duration-200 ease-in-out " +
        "placeholder:font-normal placeholder:text-slate-500 hover:border-slate-300 hover:shadow-sm focus:border-slate-300 focus:outline-none focus:ring-4 focus:ring-slate-200/70 " +
        "disabled:cursor-not-allowed disabled:opacity-60 " +
        "dark:border-slate-700 dark:bg-slate-900 dark:text-slate-50 dark:placeholder:font-normal dark:placeholder:text-slate-400 dark:hover:border-slate-600 dark:focus:ring-slate-800/80";

    internal const string ClearButton =
        "absolute inset-y-0 end-9 z-[2] flex w-7 shrink-0 items-center justify-center rounded-lg border-0 bg-transparent p-0 text-slate-500 transition duration-200 ease-in-out " +
        "hover:bg-slate-100/80 hover:text-slate-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-slate-300/80 " +
        "dark:text-slate-300 dark:hover:bg-slate-800/60 dark:hover:text-slate-100 dark:focus-visible:ring-slate-600/80";

    internal const string Chevron =
        "absolute inset-y-0 end-0 z-[2] flex w-9 shrink-0 items-center justify-center border-0 bg-transparent p-0 text-slate-600 transition duration-200 ease-in-out " +
        "hover:bg-slate-100/80 hover:text-slate-800 focus:outline-none focus-visible:ring-2 focus-visible:ring-slate-300/80 " +
        "disabled:cursor-not-allowed disabled:opacity-60 " +
        "dark:text-slate-200 dark:hover:bg-slate-800/60 dark:hover:text-slate-50 dark:focus-visible:ring-slate-600/80";

    /// <summary>
    /// List: <c>absolute</c> keeps the panel out of document flow so opening it does not push rows below (JS then switches to <c>fixed</c> for viewport stacking).
    /// </summary>
    internal const string Listbox =
        "absolute left-0 right-0 top-[calc(100%+4px)] z-[200000] max-h-60 w-full min-w-0 overflow-auto rounded-xl border border-slate-200/80 bg-white py-1 shadow-xl dark:border-slate-800/80 dark:bg-slate-900";

    internal static string OptionRow(bool highlighted, bool selected)
    {
        const string Base =
            "cursor-pointer px-3 py-2 text-sm font-medium text-slate-900 dark:text-slate-100";
        if (highlighted)
            return $"{Base} bg-slate-100 dark:bg-slate-800/80";
        if (selected)
            return $"{Base} bg-slate-200/60 dark:bg-slate-700/50";
        return $"{Base} hover:bg-slate-50 dark:hover:bg-slate-800/50";
    }

    internal const string EmptyListHint = "px-3 py-2 text-sm font-medium text-slate-500 dark:text-slate-400";
}

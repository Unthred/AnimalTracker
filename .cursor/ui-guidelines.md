# UI / Design Guidelines

## Overall Goal
The UI must look modern, clean, and professional — similar to a SaaS dashboard.
Avoid anything that looks like default HTML or basic styling.

---

## Tech Stack
- Use Tailwind CSS for all styling
- Use the project's shared Blazor UI components where possible
- Avoid custom CSS unless absolutely necessary

---

## Layout
- Use a card-based layout for content
- Maintain consistent spacing using an 8px scale
- Add generous padding to all sections
- Avoid cramped layouts

---

## Typography
- Use a clean modern font (e.g. Inter)
- Clear hierarchy:
  - Large bold headings
  - Medium subheadings
  - Readable body text
- Increase line height for readability

---

## Colours
- Use a neutral base (whites, greys)
- Use ONE primary accent colour
- Avoid overly bright or clashing colours
- Ensure good contrast and readability

---

## Components
- All UI must use reusable components:
  - Buttons
  - Cards
  - Inputs
- Components must be consistent across the app

---

## Styling
- Use rounded corners (lg or xl)
- Use soft shadows for depth
- Avoid harsh borders
- Keep design minimal and uncluttered

---

## Interactions
- Add hover effects to interactive elements
- Use subtle transitions (200ms ease-in-out)
- Buttons and cards should feel responsive

---

## Do NOT
- Do not use default browser styles
- Do not mix inconsistent spacing
- Do not use random colours
- Do not create one-off styles for elements

---

## Design Inspiration
Aim for a modern SaaS look similar to:
- Clean dashboards
- Minimalist admin panels
- Professional web apps

---

## Rule
If unsure, choose the more minimal, clean, and spacious design.

---

## Form Styling Contract (Required)
When adding or editing any form field, you MUST follow this contract.

- Do not use unstyled native controls.
- Every new/changed input/select/textarea must either:
  - use an existing shared UI component, or
  - use the exact standard input class string below.

### Standard control class (copy exactly)
`w-full min-w-0 rounded-xl border border-slate-200/90 bg-white px-3 py-2.5 text-sm shadow-sm transition duration-200 ease-in-out hover:border-slate-300 hover:shadow-sm focus:border-slate-300 focus:outline-none focus:ring-4 focus:ring-slate-200/70 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-50 dark:hover:border-slate-600 dark:focus:ring-slate-800/80`

### Required structure
- Wrap fields in `UIField`.
- Add `ValidationMessage` for validated fields.
- Keep spacing/layout consistent with neighboring fields.
- If adding actions near fields (e.g. Add location), style with existing `UIButton` variants.
- If a shared component already exists for a field type, use it instead of raw/native controls when the component supports the required binding type and behavior (example: prefer `UIDatePicker` for non-nullable `DateTime` selection).

### Done criteria for any form change
- No default browser-styled controls remain in touched sections.
- New fields visually match existing form controls in light/dark mode.
- Focus, hover, and disabled states match existing patterns.
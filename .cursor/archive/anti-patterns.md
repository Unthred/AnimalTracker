# Anti-Patterns

Avoid the following:

## Over-Engineering

- Do not introduce unnecessary layers (e.g., repositories over EF Core unless justified)
- Avoid excessive abstraction

## Bloated Components

- Do not place business logic inside Blazor components

## State Misuse

- Do not use static/global state
- Do not manually call StateHasChanged() unless necessary

## UI Libraries

- Do not introduce MudBlazor, Telerik, or other UI frameworks

## Premature Optimization

- Do not optimize code unless there is a clear performance issue

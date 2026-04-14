# Animal Tracker - Specification

## Summary
Animal Tracker is a small, fast app for recording animal sightings at one or more locations, identifying repeat visitors (starting with squirrels), and reviewing basic trends over time.

This project is a Blazor Web App using .NET 10 with server-side interactivity, Tailwind CSS, and EF Core as the only data access layer.

## Goals
- **Quick capture**: Add a sighting in a few taps/clicks (time, species, notes, optional photo).
- **Identify individuals**: Optionally associate sightings with a specific animal profile when known.
- **Review history**: Browse, search, and filter sightings over time.
- **Simple insights**: Basic stats like “most frequent visitors” and visit frequency by time window.

## Non-goals (for initial versions)
- **Automated identification** (AI recognition) or camera integrations.
- **Complex mapping/heatmaps** beyond simple location selection.
- **Multi-tenant / public social sharing** features.
- **Offline-first PWA guarantees** (can be considered later).

---

## Primary Users & Use Cases
### Primary user
- **Home observer**: A person tracking wildlife visits at home (garden/yard).

### User stories (MVP)
- **Record a sighting**: As a user, I can add a sighting with species, time (default: now), notes, and optional photo.
- **Mark unknown vs known**: As a user, I can leave the animal unknown or link it to an existing animal profile.
- **Create an animal profile**: As a user, I can create an animal profile with identifying features and notes.
- **Link later**: As a user, I can later link previously-unknown sightings to an animal profile.
- **View timeline**: As a user, I can view recent sightings in reverse chronological order.
- **Filter/search**: As a user, I can filter sightings by date range, species, location, and (when known) animal.
- **Simple stats**: As a user, I can see counts per species and most frequent identified animals.

---

## Domain Model (Conceptual)
### Species
Represents a general classification (e.g. squirrel, fox, crow).

Fields (conceptual):
- **Name** (required): e.g. “Squirrel”
- **Description** (optional)
- **Default tags** (optional, later)

### Location
Represents where sightings occur.

Initial assumption:
- Support **one default location** (“Home”) but model supports more than one.

Fields (conceptual):
- **Name** (required): “Home”, “Front yard”, “Park”
- **Notes** (optional)

### Animal (Individual)
Represents a distinct individual animal (optional to use).

Fields (conceptual):
- **SpeciesId** (required)
- **DisplayName** (optional): “Bob”
- **IdentifyingFeatures** (optional): freeform text and/or structured traits (later)
- **Notes** (optional)
- **Status** (optional, later): active/inactive (e.g., “no longer seen”)

### Sighting
Represents an observation event.

Fields (conceptual):
- **OccurredAt** (required): date/time (defaults to now)
- **LocationId** (required)
- **SpeciesId** (required)
- **AnimalId** (optional): null when unknown/unidentified
- **Notes** (optional)
- **Photos** (optional): one or more images (storage approach TBD)

### Tag (Optional, post-MVP)
Allows classifying sightings/animals with labels (e.g. “aggressive”, “injured”, “frequent visitor”).

---

## Key UX Flows
### Add sighting (primary flow)
- Entry point: “Add Sighting” button from home/timeline.
- Defaults:
  - **Time** defaults to now, editable.
  - **Location** defaults to last used (or “Home”).
  - **Species** defaults to last used (optional).
- Inputs:
  - Species (required)
  - Location (required)
  - Animal (optional): pick known animal or leave unknown
  - Notes (optional)
  - Photo(s) (optional)
- Save → return to timeline with new item visible.

### Identify animal later
- From a sighting detail page: “Link to animal”
- Choose existing animal or “Create new animal” then link.

### Review timeline
- Default view: last 50 sightings, newest first.
- Filter bar (collapsible on mobile):
  - Date range
  - Species
  - Location
  - Animal (only shows when identified animals exist)
  - “Unknown only” toggle

### Simple insights
- “Stats” page shows:
  - sightings per species (selected date range)
  - most frequently seen animals (identified only)
  - sightings over time (simple daily/weekly buckets)

---

## Pages / Routes (Proposed)
- **`/`**: Timeline (recent sightings + filters + quick add)
- **`/sightings/new`**: Add sighting
- **`/sightings/{id}`**: Sighting detail (edit, link animal, manage photos)
- **`/animals`**: Animal list (by species, searchable)
- **`/animals/new`**: Create animal
- **`/animals/{id}`**: Animal detail (profile + linked sightings)
- **`/stats`**: Basic statistics
- **`/settings`**: Locations management (and future settings)

---

## Data & Storage
### Persistence
- Use **EF Core** as the only data access layer.
- Initial DB can be:
  - **SQLite** for local/dev simplicity, or
  - a server DB later (Postgres/SQL Server) without changing app semantics.

### Photos
Store photos in a way that avoids bloating database rows:
- Prefer **file/blob storage** with DB metadata pointing to the stored asset (exact approach TBD).

### Auditing (nice-to-have)
- Track `CreatedAt`, `UpdatedAt` for major entities.

---

## Services & Responsibilities (Proposed)
Keep components UI-only; business logic in services.

- **`SightingService`**:
  - create/edit/delete sightings
  - attach/detach photo metadata
  - link/unlink animal to a sighting
  - query sightings with filters/paging
- **`AnimalService`**:
  - CRUD animals
  - merge animals (future: if duplicates created)
  - query animals by species/name
- **`SpeciesService`**:
  - seed common species
  - manage species list (optional in MVP)
- **`LocationService`**:
  - manage locations
  - provide default/last-used location
- **State containers (scoped)**:
  - timeline filter state
  - last-used selections for quick entry

---

## UI & Accessibility Requirements
- Tailwind CSS only (no third-party component libraries).
- Mobile-first layout for quick entry.
- Accessibility:
  - labeled form fields, focus states, keyboard navigation
  - sufficient contrast in default theme
  - responsive tables/lists (avoid dense grids on narrow screens)

---

## Performance & Reliability Targets (MVP)
- Timeline loads quickly with paging (avoid loading all sightings).
- Filters applied server-side via EF Core queries.
- Photos loaded lazily and/or via thumbnails to avoid heavy pages.

---

## Security & Privacy (MVP)
- Default stance: this is a personal tracker; do not expose data publicly.
- If authentication is added later:
  - use built-in ASP.NET Core auth patterns
  - ensure per-user data isolation (future)

---

## Acceptance Criteria (MVP)
- Can create/edit an animal profile and associate it with sightings.
- Can record a sighting with species, occurred time, location, optional notes.
- Can view a timeline of sightings and filter by date/species/location.
- Can view an animal detail page showing its linked sightings.
- Can see basic statistics for a selected date range.

---

## Open Questions (To Be Answered)
- **Identification granularity**: Do we require individual animals in MVP, or keep it optional (recommended: optional)?
- **Differentiation approach**: Do we want structured traits (checkboxes) vs freeform features text initially?
- **Tagging**: Do we need tags in MVP, or can notes cover this until later?
- **Photo handling**: What storage target (local filesystem, cloud blob, DB blobs) is preferred?
- **Locations**: Single default location only in MVP, or support multiple from day one?
- **Stats depth**: Which stats are must-have beyond counts (e.g., “most frequent visitor”, “time-of-day histogram”)?

---

## Future Ideas
- Camera integration
- AI image recognition
- Notifications (“Bob is back again”)
- Heatmaps / activity graphs
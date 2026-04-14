# Development Workflow

This project follows a spec-first approach.

Before implementing new features, the AI should:

1. Clarify requirements
2. Identify edge cases
3. Propose a simple design
4. Confirm assumptions
5. Only then generate code

When requirements are unclear:
- Ask questions instead of making assumptions
- Prefer simple solutions over complex ones

# Application Context

This is a Blazor Web App using .NET 10 and server-side interactivity.

## Core Principles

- Components are UI-focused and lightweight
- Business logic lives in services
- Data access is handled via EF Core DbContext
- State is managed via scoped state containers

## Data Flow

UI (Components)
→ Services
→ DbContext
→ Database

## Folder Structure

/Components
Reusable UI components

/Pages
Page-level components

/Services
Business logic

/Data
EF Core models and DbContext
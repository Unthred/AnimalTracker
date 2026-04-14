# Architecture Decisions

## Dependency Injection

- Use primary constructors for injecting services
- Avoid service locators

## State Management

- Use scoped state containers
- Do not use global static state

## UI Strategy

- Tailwind CSS only
- No third-party UI component libraries

## Real-Time Updates

- Prefer SignalR
- Use SSE only when simpler

## Database

- EF Core is the single data access layer
- No raw SQL unless necessary for performance
# Recommended Patterns

## Service Pattern

- Services handle business logic
- Services are injected via primary constructors
- Services should be stateless where possible

Example:

public class WeatherService(HttpClient http)
{
    public async Task<WeatherForecast[]> GetForecastAsync()
    {
        return await http.GetFromJsonAsync<WeatherForecast[]>("weather");
    }
}

---

## Component Pattern

- Components should not contain complex logic
- Use services for data access
- Use parameters and event callbacks for communication

---

## State Container Pattern

- Use scoped services to store UI state
- Notify components via events or bindings

---

## LINQ Usage

- Prefer readable LINQ over complex chained expressions
- Use modern operators (LeftJoin/RightJoin) where they improve clarity
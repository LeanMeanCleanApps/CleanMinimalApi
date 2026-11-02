---
name: logging
description: How logging works with Serilog - structured properties, sinks per environment (console locally, Seq in Docker) and levels. Use when adding a log line, changing log configuration, or looking for where logs go.
metadata:
  scope: shared
---

# Logging

## The rule

Serilog is the logger. It's configured from the `Serilog` section of the appsettings files and wired up in the Presentation project's startup. Logs are structured: values go in as named properties, never pasted into the message.

| Environment | Sink |
| --- | --- |
| `local` | Console, with the output template in `appsettings.local.json` |
| `docker` | Seq at `http://seq:5341`, browsable at <http://localhost:5341> |

Every event carries the application name, environment name, machine name and assembly version.

## Steps

1. Take `ILogger<T>` through the constructor where you need it. Application code can use `Microsoft.Extensions.Logging.ILogger<T>` (the abstraction), never Serilog types.
2. Write message templates with named holes: `logger.LogInformation("Created review {ReviewId} for movie {MovieId}", review.Id, movieId);`
3. Pick the level for the reader at 3am: `Information` for things that happened, `Warning` for something odd that was handled, `Error` for a failed request, `Debug` for detail you'd only want while developing.
4. Change levels or sinks in the appsettings files, not in code.
5. Name new properties in PascalCase with no spaces, so they can be queried in Seq.

## Don't

- Use string interpolation in a log message (`$"Created {id}"`). It loses the property and allocates on every call.
- Log tokens, connection strings, passwords or personal data.
- Use `Console.WriteLine`.
- Reference Serilog from Application or Infrastructure.

## Check

``` bash
dotnet run --project ./src/Presentation   # watch the console
docker compose up -d --build              # then open http://localhost:5341
```

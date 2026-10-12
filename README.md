# FitnessTracker

.NET MAUI aplikace pro evidenci tréninků, vyvíjená postupně s vysvětlením návrhu a komentováním podstatné logiky.

## Dokumentace

- [Architektura a pravidla práce s kódem](docs/architecture.md)
- [Datový model a ukládání](docs/data-model.md)
- [Roadmapa a stav vývoje](docs/roadmap.md)

## Ověření SQLite

```powershell
dotnet run --project tests/FitnessTracker.PersistenceChecks
```

Podrobnosti a rozsah ověření jsou v [README integračního projektu](tests/FitnessTracker.PersistenceChecks/README.md).
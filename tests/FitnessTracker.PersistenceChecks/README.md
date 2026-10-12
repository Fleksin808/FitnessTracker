# Integrační ověření SQLite

Spuštění z kořene repozitáře:

```powershell
dotnet run --project tests/FitnessTracker.PersistenceChecks
```

Samostatný konzolový projekt používá skutečný datový kód aplikace a stejnou knihovnu SQLite. Nepotřebuje MAUI okno ani telefon. Při úspěchu vypíše `PASS`, při porušení pravidla skončí chybou.

Ověřuje první a opakované otevření databáze, zachování ID, názvu a přesného času, pořadí a samostatnost seznamu, parametry SQL, odmítnutí duplicit, souběh, ochranu novější verze, neúspěšnou migraci, opakování po chybě a poškozený záznam. Zápis a čtení ve dvou samostatných procesech ověřují uložení mezi spuštěními.

Každý běh vytváří vlastní dočasnou databázi pod výstupní složkou testu a po skončení ji odstraní. Databázi MAUI aplikace neotevírá ani neupravuje. Ověření UI, navigace a skutečného telefonu je samostatný krok.

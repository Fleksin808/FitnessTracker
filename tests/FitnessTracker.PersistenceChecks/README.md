# Integrační ověření SQLite

Spuštění z kořene repozitáře:

```powershell
dotnet run --project tests/FitnessTracker.PersistenceChecks
```

Samostatný konzolový projekt používá skutečný datový kód aplikace a stejnou knihovnu SQLite. Nepotřebuje MAUI okno ani telefon. Při úspěchu vypíše `PASS`, při porušení pravidla skončí chybou.

Ověřuje první a opakované otevření databáze, zachování ID, názvu a přesného času, pořadí a samostatnost seznamu, parametry SQL, odmítnutí duplicit, souběh, ochranu novější verze, neúspěšnou migraci, opakování po chybě a poškozený záznam. Zápis a čtení ve dvou samostatných procesech ověřují uložení mezi spuštěními.

`ExerciseChecks.cs` ověřuje i katalog: platnost modelu, přejmenování se zachováním ID, neměnnost původního snímku, archivaci a obnovení, výchozí skrytí archivu, řazení názvů, neexistující aktualizaci, databázové omezení archivace na 0/1 a migraci 1 → 2 se zachováním tréninků. Test restartu ověřuje také uložený název a archivaci cviku mezi procesy. Konflikt ve druhé migraci nesmí přepsat existující údaje ani verzi.

Každý běh vytváří vlastní dočasnou databázi pod výstupní složkou testu a po skončení ji odstraní. Databázi MAUI aplikace neotevírá ani neupravuje. Ověření UI, navigace a skutečného telefonu je samostatný krok.

`WorkoutLogChecks.cs` ověřuje záznam tréninku: kopii názvu po změně katalogu, opakované výskyty stejného cviku, série a přesnost gramů, null/0, přesuny a rollback při selhání uprostřed prohození, souběžné zápisy, ukončení a zákaz dalších změn, cizí klíče a kaskády. Migrace 2 → 3 musí zachovat tréninky a katalog; její chyba vrací i přidaný sloupec. Zápis a čtení mezi procesy nyní ověřují také cviky, série a konec tréninku.

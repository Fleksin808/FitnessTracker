# Datový model a ukládání

## Stav a rozsah

Toto je návrh první verze deníku silových tréninků. Implementován je základ modelu `Workout` s `Id`, `Name` a `StartedAt`, formulář, seznam a detail. Tréninky se ukládají do SQLite tabulky `Workouts`. Ostatní entity, zbývající pole a tabulky přidáváme postupně podle [roadmapy](roadmap.md).

Aplikace ukládá hlavní data lokálně do SQLite. Databázový soubor `fitness-tracker.db3` je v datovém adresáři aplikace získaném přes MAUI `FileSystem.AppDataDirectory`, nikoliv pomocí pevné cesty na Windows nebo Androidu. Vytváří se při první práci s úložištěm. Běžné zavření aplikace soubor zachová; odinstalace nebo vymazání dat aplikace může data odstranit. Dosavadní čistě paměťové záznamy nejsou zdrojem pro automatickou migraci.

Drobná nastavení patří do MAUI Preferences. JSON je plánovaný formát exportu a importu; není hlavním úložištěm.

## Entity a vztahy

```text
Exercise (definice cviku)
  1 → N WorkoutExercise (výskyt cviku v tréninku)

Workout (trénink)
  1 → N WorkoutExercise
          1 → N WorkoutSet (série)
```

Stejný cvik se může objevit ve více trénincích i vícekrát v jednom tréninku. Definice cviku neobsahuje opakování ani zátěž; ty patří konkrétní sérii.

### Workout

Konstruktor `Workout(string name, DateTimeOffset startedAt)` vytvoří nové ID, odmítne prázdný název, odstraní jeho okrajové mezery a převede předaný čas do UTC. `Workout.Restore(Guid id, string name, DateTimeOffset startedAt)` zachová existující ID a použije stejnou validaci. Prázdné ID není platné. Vlastnosti mají zatím pouze getter, aby nebylo možné obejít kontrolu dodatečným přiřazením. Změnu názvu navrhneme při příslušné operaci. `FinishedAt` a `Notes` v tabulce níže jsou plánovaná pole.

| Pole | Typ v C# | Význam |
| --- | --- | --- |
| Id | Guid | Stabilní identifikátor |
| Name | string | Povinný název bez okrajových mezer |
| StartedAt | DateTimeOffset | Začátek tréninku |
| FinishedAt | DateTimeOffset? | Konec; `null` znamená nedokončený trénink |
| Notes | string? | Volitelná poznámka |

Trénink může být prázdný rozpracovaný záznam. Konec nesmí předcházet začátku. Ukončení tréninku automaticky nemaže ani neupravuje jeho série.

### Exercise

| Pole | Typ v C# | Význam |
| --- | --- | --- |
| Id | Guid | Stabilní identifikátor |
| Name | string | Povinný název cviku |
| IsArchived | bool | Skrytí z běžné nabídky nových cviků |

Název není identifikátor a může se změnit. Přejmenování definice nesmí přepsat názvy cviků v již zaznamenaných trénincích.

### WorkoutExercise

| Pole | Typ v C# | Význam |
| --- | --- | --- |
| Id | Guid | Identifikátor konkrétního výskytu |
| WorkoutId | Guid | Odkaz na trénink |
| ExerciseId | Guid | Odkaz na definici cviku |
| ExerciseNameSnapshot | string | Název zkopírovaný při přidání do tréninku |
| Position | int | Pořadí cviku v tréninku, od 1 |

### WorkoutSet

| Pole | Typ v C# | Význam |
| --- | --- | --- |
| Id | Guid | Identifikátor série |
| WorkoutExerciseId | Guid | Odkaz na cvik v tréninku |
| Position | int | Pořadí série v rámci cviku, od 1 |
| Repetitions | int | Počet opakování; uložená série musí mít alespoň 1 |
| LoadGrams | long? | Zaznamenaná externí zátěž v gramech |

`LoadGrams = null` znamená nezadanou zátěž. Hodnota `0` znamená výslovně bez externí zátěže. Záporná zátěž není v první verzi povolena. Podpora asistovaných cviků, intervalů a vzdáleností bude vyžadovat vlastní návrh, nikoliv přetížení významu tohoto pole.

Rozpracovaný text formuláře není platná uložená série. Při potvrzení ho převedeme a zvalidujeme.

## Jednotky a databázové typy

| Údaj | SQLite | Dohoda |
| --- | --- | --- |
| Guid | TEXT | Kanonický formát `D` |
| Názvy a poznámky | TEXT | Názvy trimujeme; prázdnou poznámku normalizujeme na `null` |
| Časový okamžik | TEXT | UTC, ISO 8601, pevný round-trip formát `O` |
| Opakování a pořadí | INTEGER | Celá čísla s validací |
| Zátěž | INTEGER nebo NULL | Celé gramy |
| Bool | INTEGER | `0` nebo `1` |

Datum a čas v doméně používají `DateTimeOffset`. Před uložením je normalizujeme do UTC a zobrazujeme v místním čase. Kalendářní datum bez času, například budoucí den měření, bude samostatný datový typ a nebude automaticky převáděno na UTC.

V rozhraní zadává uživatel kilogramy. Převod používá `decimal`: `62,5 kg → 62500 g`. Podporujeme přesnost do celých gramů; jemnější vstup odmítneme s vysvětlením. Převod textu respektuje kulturu uživatele, databázový formát je na kultuře nezávislý.

## Integrita a mazání

- Primární klíče jsou ID; návaznosti používají cizí klíče. V SQLite výslovně zapneme jejich vynucování pro každé připojení.
- Pořadí cviku je unikátní v rámci tréninku; pořadí série je unikátní v rámci výskytu cviku.
- Smazání tréninku odstraní jeho výskyty cviků a série.
- Smazání výskytu cviku odstraní jeho série.
- Definici cviku používanou v historii archivujeme. Fyzické smazání používané definice je zakázáno.
- Zápis celého tréninku a operace s více návaznými záznamy probíhají v transakci.
- Přesuny pořadí musí respektovat unikátnost i během transakce; konkrétní algoritmus ověříme při zavedení řazení.
- Databázové implementace ukládají běžné sloupce; nepoužívají serializovaný JSON celé objektové struktury.

## Verze databáze a migrace

### Implementovaná verze 1

Migrace `0 → 1` vytváří tabulku:

```sql
CREATE TABLE Workouts (
    Id TEXT NOT NULL PRIMARY KEY,
    Name TEXT NOT NULL CHECK (length(trim(Name)) > 0),
    StartedAt TEXT NOT NULL
);
PRAGMA user_version = 1;
```

Oba příkazy probíhají v jedné transakci. Záznamy ukládáme přes SQL parametry. Název není unikátní; duplicitní ID vyvolá `InvalidOperationException`. Výsledky řadíme podle časového okamžiku sestupně a při shodě podle `Guid` vzestupně, stejně jako paměťová implementace.

Příklad uloženého času: `2026-10-13T16:30:00.1234567+00:00`. Při čtení používáme přesný formát `O` a invariantní kulturu. Nesprávný formát ID nebo času vyvolá chybu, nesmí se považovat za nenalezený trénink.

První verze neobsahuje cizí klíče mezi tabulkami, protože existuje jen `Workouts`; jejich vynucování je už zapnuté na připojení pro budoucí migrace. Neznámou tabulku ve schématu verze 0 nepřepisujeme pomocí `IF NOT EXISTS`: konflikt migraci zastaví a zachová původní stav.

### Pravidla dalších migrací

První databáze obsahuje jen data potřebná v daném kroku. Další tabulky a pole přidáváme číslovanými migracemi.

Verzi schématu evidujeme pomocí SQLite `PRAGMA user_version`. Migrace probíhají před zpřístupněním úložišť, v pořadí a v transakci. Číslo verze změníme až po úspěšném dokončení. Opakovaný start již provedené migrace neopakuje.

Chyba migrace nesmí vést k automatickému smazání databáze. Databázi s novější nepodporovanou verzí aplikace odmítne otevřít a zobrazí srozumitelnou chybu. Konkrétní SQL a plán migrací zaznamenáme při implementaci.

## Export a import

Budoucí JSON export bude mít vlastní `formatVersion`, čas exportu v UTC a pole entit. Verze exportu je nezávislá na verzi databáze. GUID budou textové, časy ve stejném UTC formátu a zátěž v celých gramech.

Před implementací zálohy stanovíme přesný JSON kontrakt a pravidla obnovy. Import musí před zápisem ověřit verzi, ID, návaznosti a platnost hodnot. Obnova proběhne atomicky. Slučování více záloh není součástí první verze obnovy.

## Budoucí rozšíření

Měření tělesné váhy dostane samostatnou entitu a tabulku. Šablona tréninku bude samostatná definice, ze které vznikne konkrétní trénink. Statistiky budou nejprve vypočítané nad existující historií.

GUID usnadňují budoucí slučování dat, ale samy neřeší synchronizaci. Před cloudem musíme navrhnout vlastnictví dat, konflikty, evidenci změn a propagaci mazání.

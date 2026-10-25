# Datový model a ukládání

## Stav a rozsah

Toto je návrh první verze deníku silových tréninků. Implementovány jsou tréninky, katalog cviků, záznam cviků a sérií v tréninku a přehled historie s filtrem podle data. SQLite schéma verze 3 obsahuje `Workouts`, `Exercises`, `WorkoutExercises` a `WorkoutSets`. Historie filtruje načtené tréninky podle místního kalendářního dne jejich začátku; vlastní okamžik zůstává uložen v UTC. Úpravy a mazání historie jsou další částí kroku 6 [roadmapy](roadmap.md).

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

Konstruktor `Workout(string name, DateTimeOffset startedAt)` vytvoří nové ID, odmítne prázdný název, odstraní jeho okrajové mezery a převede předaný čas do UTC. `Workout.Restore` přijímá ID, název, začátek a volitelný konec. `Finish` vrátí ukončenou verzi stejného tréninku a ověří, že konec nepředchází začátku. Opakované ukončení zachová původní konec. Prázdné ID není platné. Vlastnosti mají pouze getter. Změnu názvu navrhneme při příslušné operaci; `Notes` je zatím plánované pole.

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

`Exercise(string name)` vytvoří nové ID a aktivní cvik; prázdné názvy včetně samotných bílých znaků jsou neplatné. `Restore` obnoví identitu a archivaci. `WithName` a `WithArchived` vracejí novou verzi stejného cviku s původním ID a nemění starý objekt. Názvy nejsou unikátní.

`IExerciseRepository.GetAllAsync()` vrací výchozí aktivní nabídku; `GetAllAsync(true)` zahrne i archiv. Výsledkem je samostatný snímek pouze pro čtení, seřazený podle názvu pomocí `StringComparer.OrdinalIgnoreCase`, při shodě podle ID. `GetByIdAsync` hledá i archivované cviky. Archivaci lze vrátit. Operaci fyzického smazání zatím nenabízíme.

`UpdateAsync` vyžaduje existující ID, jinak vyvolá `KeyNotFoundException`; nevytváří nové záznamy. Aktualizace ukládá název a archivaci z předaného modelu, tedy poslední úspěšný zápis platí. Řešení konfliktů více uživatelů nebo synchronizace není součástí této verze.

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
- Smazání tréninku odstraní jeho výskyty cviků a série. Repository provede příkaz v transakci; cizí klíče `ON DELETE CASCADE` smažou jen návazné řádky. Definice cviků v tabulce `Exercises` zůstávají.
- Smazání výskytu cviku odstraní jeho série.
- Definici cviku používanou v historii archivujeme. Fyzické smazání používané definice je zakázáno.
- Zápis celého tréninku a operace s více návaznými záznamy probíhají v transakci.
- Přesuny pořadí musí respektovat unikátnost i během transakce; konkrétní algoritmus ověříme při zavedení řazení.
- Databázové implementace ukládají běžné sloupce; nepoužívají serializovaný JSON celé objektové struktury.

## Verze databáze a migrace

### Záznam tréninku a pravidla zápisu

`IWorkoutLogRepository` načítá celý trénink jako neměnný `WorkoutLog` s kolekcí `WorkoutExerciseLog` a sériemi. Čtení všech částí probíhá v jedné transakci. `WorkoutExercise` obsahuje odkaz do katalogu i kopii názvu z okamžiku přidání. Stejný katalogový cvik lze přidat vícekrát; každý výskyt má vlastní ID a série.

Přidat lze jen aktivní cvik z katalogu, což repository zkontroluje ve stejné transakci jako zápis. Následné přejmenování či archivace definice neovlivňuje historický název a nebrání zapisovat série k již přidanému výskytu. Ukončený trénink nepřijímá nové série ani změny pořadí; oprava opakování a zátěže existující série je výslovně povolena i v historii. Repository pravidla vynucuje, nestačí pouze skrýt tlačítka v UI.

Přidávání používá další pozici v rámci vlastníka. Přesun má směr -1 nebo 1; za okrajem je beze změny. Prohození používá dočasnou volnou kladnou pozici, takže neporušuje UNIQUE ani CHECK během operace. Celý přesun je transakce a při chybě se vrátí zpět.

Zátěž se zadává v kg podle kultury zařízení. Formulář odmítá oddělovače tisíců, záporné hodnoty, přetečení a přesnost menší než gram. Prázdné pole uloží null, explicitní nula uloží 0 gramů. Zobrazení zpět do kg patří prezentační vrstvě.

### Verze 1: tréninky

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

### Verze 2: katalog cviků

Migrace `1 → 2` přidává:

```sql
CREATE TABLE Exercises (
    Id TEXT NOT NULL PRIMARY KEY,
    Name TEXT NOT NULL CHECK (length(trim(Name)) > 0),
    IsArchived INTEGER NOT NULL DEFAULT 0 CHECK (IsArchived IN (0, 1))
);
PRAGMA user_version = 2;
```

Tabulka `Workouts` a její záznamy se nemění. Nová databáze projde migracemi `0 → 1 → 2` v jedné transakci, starší databáze verze 1 provede pouze druhou migraci. Chyba migrace zachová předchozí verzi i data. Cizí klíče mezi tréninky a katalogem přidáme s `WorkoutExercise`; současné tabulky ještě nemají vzájemný vztah.

### Verze 3: záznam cviků a sérií (aktuální)

Migrace `2 → 3` přidává nullable sloupec `Workouts.FinishedAt` ve stejném UTC formátu jako začátek a tabulky:

```sql
CREATE TABLE WorkoutExercises (
    Id TEXT NOT NULL PRIMARY KEY,
    WorkoutId TEXT NOT NULL REFERENCES Workouts(Id) ON DELETE CASCADE,
    ExerciseId TEXT NOT NULL REFERENCES Exercises(Id) ON DELETE RESTRICT,
    ExerciseNameSnapshot TEXT NOT NULL CHECK (length(trim(ExerciseNameSnapshot)) > 0),
    Position INTEGER NOT NULL CHECK (Position > 0),
    UNIQUE (WorkoutId, Position)
);
CREATE TABLE WorkoutSets (
    Id TEXT NOT NULL PRIMARY KEY,
    WorkoutExerciseId TEXT NOT NULL REFERENCES WorkoutExercises(Id) ON DELETE CASCADE,
    Position INTEGER NOT NULL CHECK (Position > 0),
    Repetitions INTEGER NOT NULL CHECK (Repetitions > 0),
    LoadGrams INTEGER NULL CHECK (LoadGrams IS NULL OR LoadGrams >= 0),
    UNIQUE (WorkoutExerciseId, Position)
);
PRAGMA user_version = 3;
```

Původní tréninky mají `FinishedAt = null`, katalog a jeho archivace zůstávají zachované. Přidání sloupce, obou tabulek a změna verze probíhají atomicky. CHECK konce vyžaduje null nebo konec nejdříve v okamžiku začátku; porovnává pevný UTC formát textu. Mazání tréninku má připravené kaskády, katalogový cvik používaný v historii nelze smazat. Produktové mazání přidáme v kroku 6.

### Pravidla dalších migrací

První databáze obsahuje jen data potřebná v daném kroku. Další tabulky a pole přidáváme číslovanými migracemi.

Verzi schématu evidujeme pomocí SQLite `PRAGMA user_version`. Migrace probíhají před zpřístupněním úložišť, v pořadí a v transakci. Číslo verze změníme až po úspěšném dokončení. Opakovaný start již provedené migrace neopakuje.

Chyba migrace nesmí vést k automatickému smazání databáze. Databázi s novější nepodporovanou verzí aplikace odmítne otevřít a zobrazí srozumitelnou chybu. Konkrétní SQL a plán migrací zaznamenáme při implementaci.

## Export a import

Záloha je úplný snímek lokálních aplikačních dat ve verzovaném JSON souboru. Je určena pro přenos nebo obnovení téhož deníku; není synchronizačním formátem. `formatVersion` se mění nezávisle na `PRAGMA user_version` SQLite.

### JSON verze 1

- Kódování UTF-8; názvy vlastností jsou přesně camelCase a rozlišují velikost písmen.
- Kořen obsahuje `formatVersion` (celé číslo `1`), `exportedAtUtc` (čas exportu v UTC) a povinná pole `workouts`, `exercises`, `workoutExercises`, `workoutSets`.
- Každé pole je JSON array, i když je prázdné. Každá vlastnost z kontraktu musí být přítomná; nullable hodnoty jsou výslovně `null`.
- Import odmítne neznámou verzi i neznámé vlastnosti. Komentáře, koncové čárky a neplatný JSON nejsou povoleny.

```json
{
  "formatVersion": 1,
  "exportedAtUtc": "2026-10-25T12:00:00.0000000+00:00",
  "workouts": [
    { "id": "11111111-1111-1111-1111-111111111111", "name": "Trénink nohou", "startedAt": "2026-10-25T10:00:00.0000000+00:00", "finishedAt": null }
  ],
  "exercises": [
    { "id": "22222222-2222-2222-2222-222222222222", "name": "Dřep", "isArchived": false }
  ],
  "workoutExercises": [
    { "id": "33333333-3333-3333-3333-333333333333", "workoutId": "11111111-1111-1111-1111-111111111111", "exerciseId": "22222222-2222-2222-2222-222222222222", "exerciseNameSnapshot": "Dřep", "position": 1 }
  ],
  "workoutSets": [
    { "id": "44444444-4444-4444-4444-444444444444", "workoutExerciseId": "33333333-3333-3333-3333-333333333333", "position": 1, "repetitions": 8, "loadGrams": 62500 }
  ]
}
```

| Záznam | Povinné vlastnosti a význam |
| --- | --- |
| `workouts[]` | `id` GUID ve formátu `D`; `name` neprázdný; `startedAt` UTC round-trip `O`; `finishedAt` buď UTC `O`, nebo `null` |
| `exercises[]` | `id`; `name` neprázdný; `isArchived` boolean |
| `workoutExercises[]` | `id`; existující `workoutId` a `exerciseId`; `exerciseNameSnapshot`; kladné `position` |
| `workoutSets[]` | `id`; existující `workoutExerciseId`; kladné `position` a `repetitions`; `loadGrams` nezáporné celé číslo, nebo `null` |

Identifikátory jsou kanonické řetězce GUID `D`. Časové hodnoty musí mít nulový UTC offset a přesný round-trip formát `O`; čas exportu také. Pořadí série je unikátní v rámci výskytu cviku a pořadí cviku v rámci tréninku. ID jsou v každé tabulce unikátní. Název snímku cviku se nemusí rovnat aktuálnímu názvu katalogu: rozdíl uchovává historický název. Export neobsahuje cesty k souborům, UI stav, logy ani nastavení zařízení.

### Pravidla obnovy

Obnova **nahradí celý současný deník** obsahem zálohy. Sloučení do existujících dat není v první verzi podporováno. UI před importem výslovně potvrdí, že přepíše tréninky, katalog i historii; zrušení dialogu neprovede žádný zápis.

1. Soubor celý načteme a naparsujeme do přenosných DTO; databázi zatím neměníme.
2. Ověříme verzi, povinné vlastnosti, limity vstupu, unikátní ID, hodnoty polí, pořadí, časové vztahy a všechny odkazy mezi poli. Doménové modely znovu ověří pravidla entit. Neznámá či chybějící data odmítneme.
3. Teprve po kompletní validaci otevřeme jednu SQLite transakci. Odstraníme staré tréninky (kaskáda vyčistí jejich záznamy), poté staré definice cviků, a vložíme validované entity v pořadí `Exercises → Workouts → WorkoutExercises → WorkoutSets`.
4. Při jakékoliv chybě transakci vrátíme zpět, takže původní databáze zůstane celá. `user_version` se nemění; schéma zůstává odpovědností migrací aplikace.

Import odmítne nepodporovanou budoucí verzi, poškozené či duplicitní ID, neplatné hodnoty i neexistující odkaz. Maximální velikost souboru je 25 MiB a každé pole entit může obsahovat nejvýše 100 000 položek; limity chrání aplikaci před neúměrnou alokací paměti. Před zápisem se nesmí kvůli validaci mazat, přejmenovávat ani jinak měnit současná data.

## Budoucí rozšíření

Měření tělesné váhy dostane samostatnou entitu a tabulku. Šablona tréninku bude samostatná definice, ze které vznikne konkrétní trénink. Statistiky budou nejprve vypočítané nad existující historií.

GUID usnadňují budoucí slučování dat, ale samy neřeší synchronizaci. Před cloudem musíme navrhnout vlastnictví dat, konflikty, evidenci změn a propagaci mazání.

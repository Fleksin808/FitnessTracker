# Architektura FitnessTracker

## Cíl a aktuální stav

FitnessTracker je .NET MAUI aplikace pro evidenci tréninků, vyvíjená po malých krocích s vysvětlením návrhu a kódu. První použitelná verze má fungovat lokálně bez připojení k internetu.

Tento dokument popisuje cílovou architekturu. Formulář a seznam jsou v `Features/Workouts/WorkoutPage.xaml`, detail má vlastní stránku a ViewModel. Stránky používají MVVM a závislosti sestavuje DI. Model `Workout` se ukládá přes `IWorkoutRepository`; aplikace nyní používá SQLite. Úložiště v paměti zůstává jako alternativní implementace. Další moduly přibudou podle [roadmapy](roadmap.md).

## Rozdělení projektu

Zpočátku používáme jeden MAUI projekt s oddělenými složkami. Složky a abstrakce přidáváme až při jejich skutečném použití.

```text
FitnessTracker/
├── Domain/
│   ├── Workouts/
│   ├── Exercises/
│   └── BodyMeasurements/
├── Application/
│   ├── Workouts/
│   ├── Backup/
│   ├── Statistics/
│   ├── Exercises/
│   └── BodyMeasurements/
├── Features/
│   ├── Workouts/
│   ├── WorkoutHistory/
│   ├── Backup/
│   ├── Statistics/
│   ├── Exercises/
│   └── BodyMeasurements/
├── Infrastructure/
│   └── Persistence/
├── Resources/
├── AppShell.xaml
└── MauiProgram.cs
```

| Část | Odpovědnost | Omezení |
| --- | --- | --- |
| Domain | Datové modely a pravidla jejich platnosti | Nezná MAUI, databázi ani obrazovky |
| Application | Operace s daty a rozhraní úložišť | Nezná konkrétní databázovou implementaci ani ovládací prvky |
| Features | XAML stránky a jejich ViewModely, stav formulářů a zobrazení chyb | Nepouští SQL a neukládá přímo do souborů |
| Infrastructure | Implementace úložišť, mapování dat, připojení a migrace | Neřídí vzhled a navigaci obrazovek |
| Resources | Sdílené styly a vizuální prostředky | Neobsahuje aplikační logiku |
| AppShell | Navigace mezi stránkami | Neobsahuje pravidla tréninků |
| MauiProgram | Registrace a propojení závislostí | Neprovádí operace s tréninky |

## Závislosti a tok operace

- `Domain` je nezávislý základ.
- `Application` používá `Domain` a definuje potřebná rozhraní, například `IWorkoutRepository`.
- `Features` používají operace a rozhraní z `Application` a modely z `Domain`.
- `Infrastructure` implementuje rozhraní z `Application` a mapuje doménové modely na databázové záznamy.
- `MauiProgram` vybere konkrétní implementace a předá je přes konstruktory.

Příklad potvrzení tréninku:

```text
Tlačítko ve View
  → příkaz ve ViewModelu
  → operace vytvoření tréninku
  → IWorkoutRepository
  → implementace v paměti nebo SQLite
  → aktualizace stavu ViewModelu
  → zobrazení výsledku ve View
```

Samostatnou službu pro operaci vytvoříme, když operace obsahuje pravidla nebo koordinuje více kroků. Jednoduché načtení může ViewModel provést přes rozhraní úložiště. Nevytváříme automaticky službu pro každou metodu ani univerzální repository pro všechny entity.

## MVVM

- **View:** XAML popisuje vzhled. Code-behind může obsahovat čistě vizuální chování, například práci s focusem.
- **ViewModel:** poskytuje vlastnosti pro binding, příkazy a stav obrazovky, například načítání nebo chybu. Nepracuje s konkrétním `Entry` nebo `Label`.
- **Model:** popisuje data a jejich význam. Nesmí záviset na obrazovce.

Ukládání a načítání používá asynchronní API. ViewModel během operace brání opakovanému odeslání a zobrazí srozumitelnou chybu při selhání. Pravidla platnosti dat nesmějí existovat pouze ve formuláři.

První implementace používala paměť; nyní používáme SQLite. Přechod mění implementaci úložiště a registraci závislostí, nikoliv způsob ukládání ve stránkách.

## Aktuální implementace

### Rozhraní úložiště v kroku 2

`Application/Workouts/IWorkoutRepository.cs` definuje tři operace: `AddAsync`, `GetAllAsync` a `GetByIdAsync`. Implementují je `InMemoryWorkoutRepository` a `SqliteWorkoutRepository` v `Infrastructure/Persistence`. V DI je aktuálně registrovaná SQLite varianta. Oba ViewModely dostávají rozhraní konstruktorem.

Návratové typy používají `Task`, aby databázová implementace mohla pracovat asynchronně. To samo o sobě nevytváří nové vlákno. Seznam je snímek pouze pro čtení, seřazený podle začátku sestupně a při shodě podle ID vzestupně. Neexistující ID vrací `null`; selhání úložiště je výjimka. Přidání duplicitního ID nesmí přepsat záznam a vyvolá `InvalidOperationException`. Názvy tréninků mohou být stejné.

Při načtení nesmí implementace vytvářet nová ID místo uložených. Metoda `Workout.Restore` zachová ID a ověří platnost modelu stejným privátním konstruktorem jako vytvoření nového tréninku.

Nový jmenný prostor `FitnessTracker.Application` se jmenuje stejně jako základní třída MAUI `Application`. Proto má `App` výslovně uvedeného předka `Microsoft.Maui.Controls.Application`.

### Úložiště v paměti

`InMemoryWorkoutRepository` používá `Dictionary<Guid, Workout>` a chrání přístup pomocí `lock`. Vrací seřazený snímek zabalený do kolekce pouze pro čtení. Model `Workout` má zatím jen neměnné vlastnosti, takže nelze přes vrácený model změnit uložené údaje.

Operace jsou okamžité: `Task.CompletedTask` představuje dokončený zápis, `Task.FromResult` dokončené načtení s výsledkem. Není potřeba `async`, `await` ani `Task.Run`. Zámek se používá jen pro krátké operace v paměti; budoucí databázový kód bude mít vlastní způsob přístupu.

Původní registrace `AddSingleton<IWorkoutRepository, InMemoryWorkoutRepository>()` zajišťovala jednu instanci pro celý běh aplikace. Restart aplikace paměť vymaže; nejde o databázi ani zálohu. Tato varianta nyní není v aplikaci registrována.

### SQLite v kroku 3

Používáme `sqlite-net-pcl` ve verzi `1.11.285`. Je to lehká knihovna s asynchronním API, kterou používá také oficiální příklad MAUI. Nepotřebujeme další ORM ani SQL ve ViewModelech. Verzi máme připnutou v projektu aplikace i integračního ověření.

- `FitnessDatabase` dostane cestu z `MauiProgram` a vytváří sdílené připojení. Databáze se otevře při první operaci. `SemaphoreSlim` chrání inicializaci před souběhem a `finally` vždy uvolní čekající volání. `FullMutex` zajistí bezpečný režim připojení pro asynchronní API knihovny.
- Inicializace zapne cizí klíče a v transakci provede migrace do aktuální verze 5: `0 → 1` vytváří tréninky, `1 → 2` přidává katalog cviků, `2 → 3` záznam cviků, sérií a konec tréninku, `3 → 4` přidává šablony a `4 → 5` samostatná měření hmotnosti. Verzi eviduje `PRAGMA user_version`. Chyba neoznačí inicializaci za dokončenou a umožní další pokus. Novější verze se odmítne; data se nemažou ani neresetují.
- `WorkoutRow` reprezentuje databázové sloupce a převádí `Guid` a UTC čas na text. Pouze tento záznam má atribut SQLite a veřejné settery pro mapování. `Workout` nemá závislost na knihovně.
- `SqliteWorkoutRepository` implementuje stejné rozhraní jako paměťová varianta. Přidání kontroluje ID a zapisuje v jedné transakci pomocí parametrů. Čtení mapuje záznamy přes `Workout.Restore` a vrací seřazený snímek pouze pro čtení. Poškozený záznam vyvolá chybu.
- `MauiProgram` registruje databázi i repository jako singleton a předává cestu `Path.Combine(FileSystem.AppDataDirectory, FitnessDatabase.FileName)`. Připojení patří celému běhu aplikace. Záloha a výměna databázového souboru budou později vyžadovat koordinované uzavření připojení.
- Na hlavní stránce se zobrazuje `LoadError` a je dostupné opakované načtení. ViewModel zachovává rozlišení úspěšného zápisu od neúspěšné obnovy seznamu.

Tok dat: `WorkoutViewModel → IWorkoutRepository → SqliteWorkoutRepository → FitnessDatabase → soubor SQLite`. Při čtení proběhne `WorkoutRow → Workout.Restore → Workout → ViewModel`.

Opakovatelné integrační ověření je v [tests/FitnessTracker.PersistenceChecks](../tests/FitnessTracker.PersistenceChecks/README.md). Používá vlastní databáze a zahrnuje zápis a čtení ve dvou samostatných procesech. Praktický restart MAUI aplikace a telefon se ověřují zvlášť.

Podklady: [SQLite v MAUI](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/database-sqlite?view=net-maui-10.0), [sqlite-net-pcl](https://www.nuget.org/packages/sqlite-net-pcl/1.11.285).

### MVVM formuláře

- `WorkoutViewModel` ručně implementuje `INotifyPropertyChanged`. Settery oznamují změny bindingu; `CallerMemberName` doplňuje název vlastnosti.
- `ConfirmWorkoutCommand` používá vestavěný MAUI `Command`, vystavený přes `ICommand`. Asynchronní lambda volá `ConfirmWorkoutAsync`, která ošetřuje chyby úložiště. `CanExecute` závisí na `IsSaving` a při změně stavu se obnovuje přes `ChangeCanExecute`. Další NuGet balíček zatím nepotřebujeme.
- `WorkoutPage` dostane ViewModel konstruktorem a nastaví `BindingContext`. `x:DataType` deklaruje typ pro kontrolu bindingů při sestavení, ale instanci ViewModelu nevytváří.
- `Entry.Text` má explicitní `TwoWay` binding; výsledný `Label.Text` má `OneWay` binding.
- `MauiProgram` registruje Shell, stránku a ViewModel jako transient. `App` vyžádá nový Shell při vytváření okna; `AppShell` dostane stránku konstruktorem. Rozhodnutí o získání instancí z DI zůstává v těchto vstupních bodech, nikoliv ve ViewModelu.
- ViewModel validuje vstup, vytvoří `Workout` s aktuálním časem a čeká na `AddAsync`. Potvrzení zobrazí až po úspěšném přidání. `IsSaving` blokuje příkaz i opakované přímé zavolání metody. `finally` obnoví stav při úspěchu i chybě; chybu zaznamená injektovaný `ILogger<WorkoutViewModel>` a uživateli zobrazí srozumitelnou zprávu. Logger poskytuje standardní infrastruktura MAUI; další registrace není potřeba.

Podklady: [compiled bindings](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/data-binding/compiled-bindings?view=net-maui-10.0) a [dependency injection](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/dependency-injection?view=net-maui-10.0).

### Kolekce pro seznam tréninků

`WorkoutViewModel.Workouts` je `ObservableCollection<Workout>` se stálou instancí. Kolekce oznamuje změny položek přes `CollectionChanged`, zatímco `INotifyPropertyChanged` oznamuje změny jednotlivých vlastností ViewModelu. Načítání zachovává pořadí z repository a před opětovným naplněním odstraní staré položky, aby nevznikaly duplicity. Kolekce je stav obrazovky, nikoliv hlavní úložiště.

`WorkoutPage.OnAppearing` čeká na `LoadWorkoutsAsync`; chyby přístupu k repository zpracovává ViewModel. Volání probíhají z UI vlákna. `IsLoading` a `IsSaving` blokují oba příkazy i přímé spuštění další veřejné operace. Interní obnova po zápisu probíhá ještě při `IsSaving`, takže mezi zápisem a obnovením nemůže začít další operace z formuláře.

Neúspěšné čtení zachová předchozí kolekci a nastaví `LoadError`; úspěšné opakování chybu odstraní. Pokud se zápis podaří a obnova seznamu selže, výsledná zpráva výslovně potvrzuje zápis a doporučí opakovat načtení. Nesmí uživateli doporučovat znovu přidat stejný trénink.

Kontrakt pro chat UI: `Workouts` slouží jako `ItemsSource`, položka má typ `Workout` s `Name` a `StartedAt` (UTC; zobrazit místně). `IsLoading` řídí indikátor načítání, `LoadError` zprávu o chybě a `LoadWorkoutsCommand` opakované načtení. Počáteční načtení už řeší stránka; nepřidávat druhé souběžné načtení. Seznam v XAML je implementován uživatelem a jeho fungování při spuštění bylo potvrzeno. LoadError a tlačítko pro opakované načtení jsou zobrazené; indikátor IsLoading zatím ne.

### Detail a navigace

`WorkoutDetailViewModel` navazuje na původní uživatelský kód. Vlastnost `Workout` má vlastní setter, který vyvolává `PropertyChanged` i pro odvozené vlastnosti `HasWorkout`, `WorkoutName` a `StartedAtText`. Čas se převádí do místního pásma pouze pro zobrazení. `LoadAsync` řeší prázdné a nenalezené ID odděleně od chyby úložiště. `finally` obnoví `IsLoading`; `RetryCommand` znovu načte poslední platné ID. Při novém načtení se starý detail vyčistí.

`WorkoutDetailPage` přijímá parametr `workoutId` přes `IQueryAttributable`, převede ho na `Guid` a v `OnAppearing` předá ViewModelu. Shell route registruje `AppShell`. Detailní stránka i její ViewModel jsou transient; repository zůstává singleton. Předává se jen ID, data načítá repository. Používáme běžnou route s query parametrem podporovanou v MAUI 10.

`WorkoutPage` obsluhuje výběr položky a navigaci v code-behind. Jde o navigační chování stránky; není zde ukládání ani doménová logika. Výběr se vyčistí, aby stejná položka šla po návratu znovu otevřít. Příznak `isNavigating` blokuje více souběžných přechodů. Výjimky při navigaci se logují a zobrazí se krátká zpráva. Při návratu seznam znovu načte sdílená data. Shell poskytne standardní navigaci zpět.

Podklady: [Shell navigace a parametry](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/shell/navigation?view=net-maui-10.0), [výběr v CollectionView](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/collectionview/selection?view=net-maui-10.0).

## Přidání modulu

### Záznam tréninku v kroku 5

- `WorkoutExercise` a `WorkoutSet` jsou neměnné doménové záznamy s pravidly pro ID, pořadí, opakování a zátěž. `WorkoutLog` a `WorkoutExerciseLog` tvoří konzistentní snímek pro detail.
- `IWorkoutRepository` nadále slouží základnímu seznamu tréninků. `IWorkoutLogRepository` přidává operace záznamu: načtení celku, přidání cviku, série, přesuny a ukončení. `SqliteWorkoutLogRepository` sdílí `FitnessDatabase` a chrání návaznosti i otevřený stav transakcemi.
- `WorkoutDetailViewModel` dostává rozhraní záznamu a katalogu. Načítá aktivní nabídku, parsuje vstup podle kultury zařízení a blokuje souběžné operace. `needsReload` po úspěšném zápisu blokuje další zápis, dokud se nepodaří načíst čerstvý snímek. Chyba obnovy se odlišuje od chyby zápisu.
- `WorkoutExerciseItem` a `WorkoutSetItem` jsou prezentační obálky pro popisky a převod gramů na kg. Katalogový výběr používá `Exercise`, výběr pro sérii konkrétní `WorkoutExerciseItem`, takže opakovaný výskyt téhož cviku má vlastní série.
- Detail poskytuje funkční ovládání pro výběr cviku, přidání série, posun cviků a sérií a ukončení. Po ukončení jsou nové série a změny pořadí zakázané; opravy existujících hodnot série popisuje následující pravidlo. Vizuální úpravy odkládáme podle přání uživatele.
- V kroku 6 `IWorkoutLogRepository.UpdateSetAsync` opravuje opakování a zátěž existující série v transakci. Jako výjimka dovoluje opravu i po ukončení; přidávání a řazení zůstává zakázané. Doménový konstruktor ověří hodnoty před UPDATE a ViewModel používá stejné parsování kg jako při přidání.
- Přejmenování a archivace katalogu zůstávají nezávislé na zaznamenaném názvu. Cizí klíče chrání existující historii; fyzické mazání v produktu přijde později.

Automatické ověření zahrnuje přesnost a null/0, změny katalogu, souběh, rollback přesunu i migrace, cizí klíče, kaskády a záznam mezi procesy. ViewModel byl ověřen se skutečnou SQLite databází včetně kultur cs-CZ/en-US, neplatného vstupu, přetečení, retry a blokace zápisu po ukončení. Ruční ověření nového detailu uživatel potvrdil.

### Historie tréninků v kroku 6

- `Features/WorkoutHistory` má vlastní stránku, ViewModel a prezentační položku. Používá `IWorkoutRepository`; nevytváří druhé úložiště ani kopii doménového modelu.
- Historie načte stejný seřazený snímek jako seznam. Volitelné meze filtru porovnávají místní kalendářní den `StartedAt`, protože uživatel pracuje s datem zobrazeným na zařízení. Samotný okamžik se v SQLite dál ukládá v UTC.
- Řádek historie předává do stávajícího detailu pouze ID přes Shell navigaci. Filtr je v paměti, protože aktuální lokální deník načítá celý seznam; při růstu dat lze později přidat dotaz podle rozsahu do repository.
- `AppShell` zpřístupňuje Historii vlastní záložkou vedle Tréninků a Cviků. Stránka a ViewModel jsou transient, sdílené repository zůstává singleton.
- Detail si před smazáním vyžádá potvrzení v UI. `IWorkoutRepository.DeleteAsync` odstraňuje trénink v transakci; kaskády databáze mažou zaznamenané cviky a série, zatímco katalogové definice zůstávají.

### Záloha a obnova v kroku 7

- JSON v3 je přenosový kontrakt; databáze zůstává SQLite. Import podporuje v1 a v2. Přenosné DTO a validace jsou oddělené od doménových modelů a řádků SQLite.
- Import nejprve validuje celý soubor mimo databázový zápis. Po potvrzení uživatele nahradí celý lokální deník jedinou transakcí; zrušení nebo validační chyba současná data nemění.
- Obnova zapisuje do stávajícího připojení v pořadí závislostí. Není potřeba vyměňovat soubor databáze ani obcházet `FitnessDatabase`; rollback transakce zachová původní obsah při chybě.
- `IFitnessBackupService` odděluje souborový JSON kontrakt od obrazovky. `BackupPage` používá systémový výběr souboru a sdílení; uživatel sám zvolí cílové umístění exportu.

### Statistiky v kroku 8

- `IWorkoutStatisticsRepository` načte all-time souhrn v jedné SQLite transakci. Statistiky jsou čtecí modul; nezasahují do modelu ani zápisu tréninků.
- Objem vychází jen z uložených opakování a zátěže v gramech. Jeho jednotka je kg × opakování; null zátěž se vynechává a nula zůstává nulovým příspěvkem.
- `StatisticsPage` a `StatisticsViewModel` zobrazují základní počty a objem. V nové statistice nejprve popíšeme její význam a ověříme výpočet na známých datech.

### Šablony v kroku 8

- `Domain/WorkoutTemplates` obsahuje neměnný plán s názvem a seřazenými odkazy na cviky. Šablona neukládá výkon ani série; stejné cviky se v jednom plánu neopakují.
- `Application/WorkoutTemplates/IWorkoutTemplateRepository` odděluje seznam, uložení, úpravu, smazání i použití plánu od SQLite a UI.
- `SqliteWorkoutTemplateRepository` ukládá šablonu a její položky v transakci. Použití šablony v jediné transakci založí nový trénink a nové zaznamenané cviky bez sérií; přenese aktuální názvy cviků jako snímky. Archivovaný cvik operaci zastaví před potvrzením transakce.
- SQLite schéma v4 přidává `WorkoutTemplates` a `WorkoutTemplateExercises`; položky se mažou kaskádou při smazání šablony a použitý cvik nelze fyzicky odstranit.
- `SqliteFitnessBackupService` exportuje JSON v2, před obnovou validuje šablony a jejich odkazy a importuje také JSON v1. Obnova v1 nahradí celý lokální deník a odstraní i místní šablony.
- `WorkoutTemplatesPage` a ViewModel tvoří vlastní záložku. Stránka zajišťuje navigaci a potvrzení smazání; operace a jejich stav řeší ViewModel.

### Měření tělesných údajů v kroku 9

- Modul měření tělesné hmotnosti má vlastní `Domain/BodyMeasurements`, `Application/BodyMeasurements`, `Features/BodyMeasurements` a implementaci v `Infrastructure/Persistence`. Stávající moduly tréninků a šablon zůstávají bez přímých závislostí na novém UI.
- `BodyWeightMeasurementsPage` a ViewModel nabízejí záznam, přehled a smazání chybného měření s potvrzením. `BodyWeightMeasurement` uchovává ID, okamžik pořízení a hmotnost v celých gramech; více měření během jednoho dne je povoleno.
- UI přijímá kg, ověřuje přesný převod na gramy a ukládá okamžik v UTC. Seznam řadí od nejnovějšího měření a zobrazuje čas v místním pásmu zařízení. DI přidává nové rozhraní, ViewModel a stránku jako vlastní záložku.
- SQLite migrace `4 → 5` a `SqliteBodyWeightMeasurementRepository` ukládají měření do samostatné tabulky. JSON záloha přechází na v3 a zachovává import v1 i v2; starší verze nemají měření, proto jejich import vytvoří deník bez těchto záznamů.

### Katalog cviků v kroku 4

- `Domain/Exercises/Exercise` je neměnná definice cviku s ID, názvem a archivací. `WithName` a `WithArchived` vytvářejí nové verze stejné identity, takže se nemění dřívější snímky.
- `Application/Exercises/IExerciseRepository` definuje přidání, hledání, načtení aktivního nebo celého katalogu a aktualizaci. Samostatnou službu pro prosté předání těchto operací nevytváříme.
- `Infrastructure/Persistence/SqliteExerciseRepository` sdílí `FitnessDatabase` s tréninky. `ExerciseRow` odděluje databázové mapování od domény. Aktualizace používá UPDATE a při neexistujícím ID hlásí chybu; nikdy nepoužívá upsert. Fyzické mazání zatím není dostupné.
- `Features/Exercises/ExerciseCatalogViewModel` poskytuje kolekci, editor, příkazy a stav `IsBusy`. Stejný editor řeší vytvoření i přejmenování. Během operace blokuje příkazy i přímá volání. Zápis a obnova seznamu mají odlišené výsledky: chyba čtení nesmí doporučit znovu uložit již úspěšnou změnu.
- Přepínač zobrazení archivu při chybě načtení vrátí původní filtr a zachová starou kolekci. Archivace upravovaného cviku ukončí jeho editaci, aby se pozdější uložená změna nevrátila ke starému stavu.
- `ExerciseCatalogPage` řeší jen binding a načtení při otevření. Příkazy v šabloně položky odkazují přes `x:Reference` na typovaný `ViewModel` stránky; `CommandParameter` je konkrétní cvik. Typ položky a typ zdroje příkazu jsou v XAML uvedené samostatně.
- `AppShell` má dvě hlavní záložky: Tréninky a Cviky. Obě stránky získá konstruktorem z DI, databáze a repository jsou singleton, stránky a ViewModely transient. Stávající route detailu tréninku zůstává dostupná nad seznamem.

Ruční ověření: vytvořit cvik, přejmenovat ho, zrušit rozpracovanou úpravu, archivovat, zobrazit archiv a obnovit. Restart musí zachovat cviky i původní tréninky. Datové operace a ViewModel jsou automaticky ověřené; samotná nová obrazovka zatím nebyla prakticky vyzkoušena.

Podklady k UI: [Shell záložky](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/shell/tabs?view=net-maui-10.0), [typované bindingy a jejich zdroje](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/data-binding/compiled-bindings?view=net-maui-10.0).

### Obecný postup

Například měření tělesné váhy bude mít:

1. Model a pravidla v `Domain/BodyMeasurements`.
2. Operace a rozhraní úložiště v `Application/BodyMeasurements`.
3. Stránky a ViewModely v `Features/BodyMeasurements`.
4. Implementaci ukládání a migraci v `Infrastructure/Persistence`.
5. Registraci závislostí a vstup do navigace.

Moduly nesahají do ViewModelů jiných modulů. Potřebná spolupráce vede přes aplikační operace a jasné datové kontrakty. Sdílený kód přesouváme až tehdy, když má více skutečných použití.

## Komentování a způsob práce

Při každé změně komentujeme podstatné řádky nebo bloky kódu česky. Komentář vysvětluje účel, důvod rozhodnutí nebo méně zřejmé chování, zejména:

- validaci a předčasné ukončení operace;
- změnu stavu obrazovky a ochranu proti opakovanému odeslání;
- předávání závislostí a důležité vazby mezi částmi;
- převod jednotek, mapování dat, transakce a migrace.

Zřejmé příkazy nepopisujeme mechanicky. Komentáře aktualizujeme spolu s kódem. Veřejná rozhraní dokumentujeme tam, kde je potřeba vysvětlit smlouvu, například význam `null` nebo chování při nenalezení záznamu.

Každý krok má malé změny, vysvětlení toku dat a praktické ověření. Automatické testy se soustředí na pravidla, datové převody, migrace a import; nebudujeme test pro každý triviální řádek UI.

## Dočasné sjednocení tlačítek

Vzhled zatím upravujeme jen pro základní použitelnost, při zachování pořadí funkcí v roadmapě. Sdílený styl `Button` v `Resources/Styles/Styles.xaml` používá odsazení `18,12`, minimální výšku 48, minimální šířku 140, maximální šířku 320 a zarovnání vlevo. Výška může růst s textem, například při větším systémovém písmu. Příkazy, bindingy a datová logika se tím nemění. Komplexní návrh UI zůstává odložený do dokončení základní funkčnosti.

## Report pro git commit

Po každém dokončeném celku úprav (kódu, konfigurace i dokumentace) připravíme v závěrečné odpovědi krátký český report vhodný ke zkopírování jako zpráva git commitu. Pravidlo platí pro všechny chaty projektu FitnessTracker.

Report má stručný název, několik bodů s provedenými změnami a řádek `Ověřeno:` se skutečně provedenými kontrolami. Neprovedené kontroly nepovažujeme za úspěšné; podstatné zbývající ověření uvedeme výslovně. Při čistém vysvětlování bez úpravy souborů report není potřeba. Příprava reportu sama o sobě není pokyn k provedení commitu nebo pushnutí změn.

## Přehled využití Codexu v projektových chatech

- Počítáme uživatelské prompty samostatně v každém chatu. Začátek každé odpovědi uvede počítadlo od posledního přehledu, například `[2/5]`.
- Po pátém promptu načteme aktuální account-wide limity Codexu a uvedeme zbývající procenta i textové ukazatele. Reportujeme přesné délky oken, které nástroj vrátí; pětihodinové okno nenazýváme denním limitem.
- Pokud denní údaj nebo procento není dostupné, napíšeme to výslovně. Neodhadujeme spotřebu tokenů jednotlivého chatu; account-wide limit není token usage tohoto vlákna.
- Po přehledu se počítadlo daného chatu vrací na nulu. Při ukončení práce navíc stručně zhodnotíme zahlcení vlákna, kvalitativně a bez vymyšleného přesného čísla tokenů.

## Dokumentace rozhodnutí

- [Datový model](data-model.md) určuje význam a ukládání dat.
- [Roadmapa](roadmap.md) určuje pořadí a podmínky dokončení jednotlivých kroků.
- Při změně návrhu aktualizujeme příslušný dokument ve stejném kroku jako kód.
- Více projektů, cloud a další obecné vrstvy přidáme až při konkrétní potřebě.

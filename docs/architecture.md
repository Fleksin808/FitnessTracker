# Architektura FitnessTracker

## Cíl a aktuální stav

FitnessTracker je .NET MAUI aplikace pro evidenci tréninků, vyvíjená po malých krocích s vysvětlením návrhu a kódu. První použitelná verze má fungovat lokálně bez připojení k internetu.

Tento dokument popisuje cílovou architekturu. Aktuálně je jednoduchý formulář v `Features/Workouts/WorkoutPage.xaml` a jeho chování ve `WorkoutViewModel.cs`. Stránka používá MVVM a závislosti sestavuje DI. V `Domain/Workouts/Workout.cs` je základ modelu tréninku, dosud bez propojení s formulářem. Úložiště, další modely a databáze přibudou podle [roadmapy](roadmap.md).

## Rozdělení projektu

Zpočátku používáme jeden MAUI projekt s oddělenými složkami. Složky a abstrakce přidáváme až při jejich skutečném použití.

```text
FitnessTracker/
├── Domain/
│   ├── Workouts/
│   └── Exercises/
├── Application/
│   ├── Workouts/
│   └── Exercises/
├── Features/
│   ├── Workouts/
│   └── Exercises/
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

Nejdřív použijeme úložiště v paměti. Přechod na SQLite má měnit implementaci úložiště a registraci závislostí, nikoliv způsob ukládání ve stránkách. Konkrétní knihovny zvolíme a zdokumentujeme při příslušném kroku.

## Aktuální implementace

### Rozhraní úložiště v kroku 2

`Application/Workouts/IWorkoutRepository.cs` definuje tři operace: `AddAsync`, `GetAllAsync` a `GetByIdAsync`. Implementace zatím neexistuje a rozhraní ještě není registrované v DI ani používané ViewModelem.

Návratové typy používají `Task`, aby databázová implementace mohla pracovat asynchronně. To samo o sobě nevytváří nové vlákno. Seznam je snímek pouze pro čtení, seřazený podle začátku sestupně a při shodě podle ID vzestupně. Neexistující ID vrací `null`; selhání úložiště je výjimka. Přidání duplicitního ID nesmí přepsat záznam a vyvolá `InvalidOperationException`. Názvy tréninků mohou být stejné.

Při načtení nesmí implementace vytvářet nová ID místo uložených. Způsob obnovení modelu s existujícím ID doplníme při databázové implementaci.

Nový jmenný prostor `FitnessTracker.Application` se jmenuje stejně jako základní třída MAUI `Application`. Proto má `App` výslovně uvedeného předka `Microsoft.Maui.Controls.Application`.

### MVVM formuláře

- `WorkoutViewModel` ručně implementuje `INotifyPropertyChanged`. Settery oznamují změny bindingu; `CallerMemberName` doplňuje název vlastnosti.
- `ConfirmWorkoutCommand` používá vestavěný MAUI `Command`, vystavený přes `ICommand`. Další NuGet balíček zatím nepotřebujeme; mechanismus je přímo vidět v kódu.
- `WorkoutPage` dostane ViewModel konstruktorem a nastaví `BindingContext`. `x:DataType` deklaruje typ pro kontrolu bindingů při sestavení, ale instanci ViewModelu nevytváří.
- `Entry.Text` má explicitní `TwoWay` binding; výsledný `Label.Text` má `OneWay` binding.
- `MauiProgram` registruje Shell, stránku a ViewModel jako transient. `App` vyžádá nový Shell při vytváření okna; `AppShell` dostane stránku konstruktorem. Rozhodnutí o získání instancí z DI zůstává v těchto vstupních bodech, nikoliv ve ViewModelu.
- ViewModel nyní pouze validuje vstup a nastavuje zprávu. Záznam tréninku ani ukládání zatím neexistují. Doménová pravidla přidáme v kroku 2.

Podklady: [compiled bindings](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/data-binding/compiled-bindings?view=net-maui-10.0) a [dependency injection](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/dependency-injection?view=net-maui-10.0).

## Přidání modulu

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

## Dokumentace rozhodnutí

- [Datový model](data-model.md) určuje význam a ukládání dat.
- [Roadmapa](roadmap.md) určuje pořadí a podmínky dokončení jednotlivých kroků.
- Při změně návrhu aktualizujeme příslušný dokument ve stejném kroku jako kód.
- Více projektů, cloud a další obecné vrstvy přidáme až při konkrétní potřebě.

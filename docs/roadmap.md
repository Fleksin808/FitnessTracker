# Roadmapa FitnessTracker

## Způsob vývoje

Cílem první verze je použitelný lokální deník silových tréninků. Postupujeme po dokončených funkčních celcích s vysvětlením klíčových rozhodnutí a toku dat. Od 12. 10. 2026 nepoužíváme jako výchozí režim úkoly po jednotlivých řádcích; větší části implementujeme podle roadmapy a potom je společně projdeme.

Každý krok zahrnuje vysvětlení návrhu, komentáře u podstatné logiky, praktické ověření a aktualizaci dokumentace. Neimplementujeme více etap najednou bez předchozí dohody.

Platí [architektura](architecture.md) a [datové dohody](data-model.md). Stav kroku měníme až podle skutečně dokončené práce.

## Výchozí stav

- [x] Existuje výchozí .NET MAUI projekt.
- [x] Jednoduchá stránka načte název tréninku a zobrazí ho; prázdný vstup odmítne.
- [x] Tato úprava prošla sestavením pro Windows.
- [x] Návrh architektury, dat a pořadí práce je uložen v dokumentaci.

Existuje model `Workout`, úložiště v paměti, formulář a seznam tréninků. Uživatel potvrdil fungování seznamu při spuštění. Detail včetně navigace je implementován a jeho logika automaticky ověřena; praktické ověření přechodů mezi stránkami zůstává otevřené. Trvalé ukládání a spuštění na telefonu zatím nemáme ověřené.

## 1. MVVM a rozdělení odpovědností

- [x] Převést současnou stránku na View a ViewModel v `Features/Workouts`.
- [x] Vysvětlit binding, oznamování změn vlastností a příkazy v komentářích a dokumentaci.
- [x] Přesunout zpracování vstupu z code-behind do ViewModelu.
- [x] Zavést předávání a registraci potřebných závislostí.
- [x] Zdokumentovat volbu vestavěného `Command` a ručního `INotifyPropertyChanged` bez další knihovny.
- [x] Prakticky ověřit spuštění stránky, binding a příkaz (potvrzeno uživatelem).

Hotovo, když zadání názvu a kontrola prázdného vstupu fungují přes binding a příkaz. Zatím bez databáze.

Kontrola obrazovky: po spuštění je vidět výchozí zpráva; prázdný vstup i samotné mezery vypíšou upozornění; `  Trénink nohou  ` zobrazí `Trénink přidán do paměti: Trénink nohou`; změna názvu a další potvrzení aktualizují výsledek. Ověřit také prázdný vstup po předchozím úspěšném potvrzení.

## 2. Tréninky v paměti

- [x] Zavést základ `Workout`: ID, název, začátek a kontrolu platnosti názvu.
- [x] Definovat `IWorkoutRepository` pro přidání, seznam a hledání podle ID.
- [x] Implementovat úložiště v paměti a registrovat je v DI jako singleton.
- [x] Propojit formulář s vytvořením a přidáním tréninku; ošetřit chyby a opakované odeslání.
- [x] Připravit `Workouts`, načítání při otevření stránky a obnovu po přidání.
- [x] Vysvětlit oznamování změn kolekce a sdílenou životnost úložiště.
- [x] Zobrazit kolekci tréninků v UI pomocí `CollectionView` (implementoval a ověřil uživatel).
- [x] Přidat detail a vysvětlit navigaci pomocí ID.
- [ ] Prakticky ověřit otevření detailu, návrat a opakované otevření stejné položky.

Hotovo, když lze vytvořit více tréninků a otevřít jejich detaily. Záznamy přežijí navigaci v rámci běhu aplikace; po restartu se ztratí.

Krok 2 dělíme na malé části: nejprve model, potom rozhraní úložiště, jeho implementace v paměti a nakonec propojení formuláře se seznamem a detailem. Nyní je připraven model, rozhraní i úložiště v paměti registrované v DI. Formulář vytváří trénink a přidává ho přes rozhraní úložiště. Automaticky bylo ověřeno přidání, aktuální čas, prázdný vstup, blokace opakovaného volání při čekání, synchronní i asynchronní chyba a úspěšné opakování po chybě. Fungování formuláře a seznamu při spuštění potvrdil uživatel. Konec tréninku a poznámky doplníme až s funkcemi, které je využijí.

Příprava seznamu v kroku 2: `WorkoutViewModel.Workouts` je stabilní instance `ObservableCollection<Workout>`. `LoadWorkoutsAsync` načte data a `WorkoutPage.OnAppearing` ji volá při zobrazení stránky. Po přidání se kolekce obnoví. `IsLoading`, `LoadError` a `LoadWorkoutsCommand` jsou připravené pro chat UI; seznam už XAML zobrazuje, zprávu LoadError zatím nikoliv. Ověřeno bylo řazení, obnova, oznámení kolekce, načtení sdílených dat, zachování položek při chybě, odlišení chyby čtení po úspěšném zápisu a blokace souběžných operací. Sestavení pro Windows prošlo bez chyb a upozornění; fungování seznamu následně při spuštění potvrdil uživatel.

Detail v kroku 2: `WorkoutDetailPage` přijme `workoutId` přes `IQueryAttributable` a v `OnAppearing` zavolá `WorkoutDetailViewModel.LoadAsync`. Seznam naviguje přes Shell route a po klepnutí zruší výběr. Detail zobrazuje název a místní čas, řeší neplatné a nenalezené ID, chybu úložiště a opakované načtení. Automatické ověření pokrylo načtení různých ID, oznámení vlastností, převod času, chyby a retry; Windows build prošel bez chyb a upozornění. Prakticky vyzkoušet: přidat dva tréninky, otevřít každý detail, vrátit se šipkou zpět a otevřít znovu stejnou položku. Do té doby není etapa 2 prakticky uzavřená.

## 3. Lokální SQLite

- [ ] Zvolit knihovnu pro přístup k SQLite a popsat důvody volby.
- [ ] Zavést databázi v datovém adresáři aplikace a první migraci.
- [ ] Implementovat SQLite úložiště tréninků a změnit registraci závislostí.
- [ ] Přidat stav ukládání, zobrazení chyb a ochranu před dvojím odesláním.
- [ ] Ověřit identifikátory, časy a načtení po restartu.
- [ ] Ověřit spuštění a ukládání na cílovém telefonu nebo emulátoru.

Hotovo, když tréninky přežijí restart na ověřených platformách a obrazovka neobsahuje databázový kód. Cílovou telefonní platformu a dostupné zařízení určíme před tímto ověřením.

## 4. Katalog cviků

- [ ] Přidat `Exercise`, aplikační operace a úložiště.
- [ ] Zavést migraci a obrazovky seznamu, vytvoření a úpravy.
- [ ] Umožnit archivaci a odlišit ji od smazání.
- [ ] Ověřit názvy a jejich změny.

Hotovo, když lze katalog spravovat a archivované cviky se běžně nenabízejí pro nový trénink.

## 5. Zapisování skutečného tréninku

- [ ] Přidat `WorkoutExercise` a `WorkoutSet` včetně migrace.
- [ ] Umožnit výběr cviku a zadání sérií, opakování a zátěže.
- [ ] Zavést kopii názvu cviku, převod kilogramů na gramy a validaci.
- [ ] Umožnit přeuspořádání cviků a sérií.
- [ ] Přidat ukončení tréninku.
- [ ] Ověřit transakce a návaznosti záznamů.

Hotovo, když lze zaznamenat celý trénink, znovu ho načíst a jeho údaje zůstávají správné i po přejmenování definice cviku.

## 6. Historie a úpravy

- [ ] Přidat přehled historie a filtrování podle data.
- [ ] Umožnit úpravu zaznamenaných údajů.
- [ ] Přidat smazání tréninku s potvrzením v aplikaci.
- [ ] Ověřit odstranění návazných dat a zachování katalogu cviků.

Hotovo, když lze starý trénink najít, upravit a odstranit bez nekonzistentních záznamů.

## 7. Záloha a obnova

- [ ] Dokončit verzovaný JSON kontrakt a pravidla obnovy.
- [ ] Implementovat export a obnovu ze zálohy.
- [ ] Před zápisem validovat soubor, vztahy a hodnoty.
- [ ] Zajistit atomickou obnovu a ověřit neplatnou i nepodporovanou zálohu.

Hotovo, když export a následná obnova zachovají údaje a neplatný import nezmění stávající data.

## 8. Statistiky a šablony

- [ ] Dohodnout definice statistik, například počet tréninků nebo objem zátěže.
- [ ] Přidávat statistiky jednotlivě a ověřovat výpočty na známých datech.
- [ ] Navrhnout šablony odděleně od zaznamenané historie.
- [ ] Umožnit vytvořit samostatný trénink ze šablony.

Hotovo, když mají statistiky vysvětlený význam a změna šablony nemění dříve vytvořený trénink.

## Pozdější samostatné moduly

- Měření tělesné váhy a dalších tělesných údajů.
- Cíle a připomínky.
- Další typy cviků a měřených výkonů.
- Účet, cloudová záloha a synchronizace po samostatném návrhu.

Jejich pořadí určíme po dokončení lokálního deníku podle skutečných potřeb.

## Kontrola dokončení každého kroku

1. Chování odpovídá popsanému výsledku kroku.
2. Podstatné řádky nebo bloky mají užitečné české komentáře.
3. Uživatel dostane vysvětlení toku dat a nových pojmů.
4. Projekt projde příslušným sestavením a změna je prakticky ověřena; neověřené platformy výslovně uvedeme.
5. Pravidla a rizikové operace mají přiměřené automatické ověření, pokud ho daný krok potřebuje.
6. Dokumentace odpovídá skutečnému stavu a změněným rozhodnutím.

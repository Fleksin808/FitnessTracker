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

Existuje model `Workout`, formulář, seznam a detail. Uživatel v chatu „Základní struktura“ potvrdil fungování detailu, návratu i opětovného otevření. Aplikace nyní používá SQLite; trvalost dat je automaticky ověřena mezi dvěma procesy. Zachování záznamů po restartu MAUI uživatel potvrdil. Telefon a kontrola detailů po restartu zůstávají otevřené. Funkčnost katalogu cviků uživatel potvrdil. Záznam cviků a sérií v tréninku je nyní implementovaný a automaticky ověřený; nová část detailu čeká na ruční kontrolu.

## 1. MVVM a rozdělení odpovědností

- [x] Převést současnou stránku na View a ViewModel v `Features/Workouts`.
- [x] Vysvětlit binding, oznamování změn vlastností a příkazy v komentářích a dokumentaci.
- [x] Přesunout zpracování vstupu z code-behind do ViewModelu.
- [x] Zavést předávání a registraci potřebných závislostí.
- [x] Zdokumentovat volbu vestavěného `Command` a ručního `INotifyPropertyChanged` bez další knihovny.
- [x] Prakticky ověřit spuštění stránky, binding a příkaz (potvrzeno uživatelem).

Hotovo, když zadání názvu a kontrola prázdného vstupu fungují přes binding a příkaz. Zatím bez databáze.

Kontrola obrazovky: po spuštění je vidět výchozí zpráva; prázdný vstup i samotné mezery vypíšou upozornění; `  Trénink nohou  ` zobrazí `Trénink uložen: Trénink nohou`; změna názvu a další potvrzení aktualizují výsledek. Ověřit také prázdný vstup po předchozím úspěšném potvrzení.

## 2. Tréninky v paměti

- [x] Zavést základ `Workout`: ID, název, začátek a kontrolu platnosti názvu.
- [x] Definovat `IWorkoutRepository` pro přidání, seznam a hledání podle ID.
- [x] Implementovat úložiště v paměti a registrovat je v DI jako singleton.
- [x] Propojit formulář s vytvořením a přidáním tréninku; ošetřit chyby a opakované odeslání.
- [x] Připravit `Workouts`, načítání při otevření stránky a obnovu po přidání.
- [x] Vysvětlit oznamování změn kolekce a sdílenou životnost úložiště.
- [x] Zobrazit kolekci tréninků v UI pomocí `CollectionView` (implementoval a ověřil uživatel).
- [x] Přidat detail a vysvětlit navigaci pomocí ID.
- [x] Prakticky ověřit otevření detailu, návrat a opakované otevření stejné položky (potvrzeno uživatelem v chatu „Základní struktura“).

Hotovo, když lze vytvořit více tréninků a otevřít jejich detaily. Záznamy přežijí navigaci v rámci běhu aplikace; po restartu se ztratí.

Krok 2 dělíme na malé části: nejprve model, potom rozhraní úložiště, jeho implementace v paměti a nakonec propojení formuláře se seznamem a detailem. Nyní je připraven model, rozhraní i úložiště v paměti registrované v DI. Formulář vytváří trénink a přidává ho přes rozhraní úložiště. Automaticky bylo ověřeno přidání, aktuální čas, prázdný vstup, blokace opakovaného volání při čekání, synchronní i asynchronní chyba a úspěšné opakování po chybě. Fungování formuláře a seznamu při spuštění potvrdil uživatel. Konec tréninku a poznámky doplníme až s funkcemi, které je využijí.

Příprava seznamu v kroku 2: `WorkoutViewModel.Workouts` je stabilní instance `ObservableCollection<Workout>`. `LoadWorkoutsAsync` načte data a `WorkoutPage.OnAppearing` ji volá při zobrazení stránky. Po přidání se kolekce obnoví. `IsLoading`, `LoadError` a `LoadWorkoutsCommand` jsou připravené pro chat UI; seznam už XAML zobrazuje, zprávu LoadError zatím nikoliv. Ověřeno bylo řazení, obnova, oznámení kolekce, načtení sdílených dat, zachování položek při chybě, odlišení chyby čtení po úspěšném zápisu a blokace souběžných operací. Sestavení pro Windows prošlo bez chyb a upozornění; fungování seznamu následně při spuštění potvrdil uživatel.

Detail v kroku 2: `WorkoutDetailPage` přijme `workoutId` přes `IQueryAttributable` a v `OnAppearing` zavolá `WorkoutDetailViewModel.LoadAsync`. Seznam naviguje přes Shell route a po klepnutí zruší výběr. Detail zobrazuje název a místní čas, řeší neplatné a nenalezené ID, chybu úložiště a opakované načtení. Automatické ověření pokrylo načtení různých ID, oznámení vlastností, převod času, chyby a retry; Windows build prošel bez chyb a upozornění. Prakticky vyzkoušet: přidat dva tréninky, otevřít každý detail, vrátit se šipkou zpět a otevřít znovu stejnou položku. Uživatel následně potvrdil fungování přechodů a etapa 2 je uzavřená.

## 3. Lokální SQLite

- [x] Zvolit `sqlite-net-pcl` 1.11.285 a popsat důvody volby.
- [x] Zavést databázi v datovém adresáři aplikace a migraci 0 → 1.
- [x] Implementovat SQLite úložiště tréninků a změnit registraci závislostí.
- [x] Zachovat stav ukládání a ochranu před dvojím odesláním; zobrazit chybu čtení a opakované načtení.
- [x] Automaticky ověřit ID, přesný UTC čas, migrace a načtení mezi dvěma samostatnými procesy.
- [x] Prakticky ověřit zachování tréninků po restartu MAUI (potvrzeno uživatelem).
- [ ] Prakticky ověřit detaily po restartu MAUI aplikace.
- [ ] Ověřit spuštění a ukládání na cílovém telefonu nebo emulátoru.

Hotovo, když tréninky přežijí restart na ověřených platformách a obrazovka neobsahuje databázový kód. Cílovou telefonní platformu a dostupné zařízení určíme před tímto ověřením.

Implementace kroku 3: `FitnessDatabase` otevírá databázi při prvním použití a provádí verzovanou migraci. `WorkoutRow` mapuje textové ID a UTC čas na `Workout.Restore`. `SqliteWorkoutRepository` používá stejné rozhraní jako paměťová varianta. Testy v `tests/FitnessTracker.PersistenceChecks` pokrývají trvalost mezi procesy, pořadí, duplicity, souběh, retry, neúspěšnou migraci, ochranu novější verze a poškozený záznam. Windows build, Android build (mimo omezené prostředí) a integrační ověření prošly. Zachování záznamů po restartu následně potvrdil uživatel. Běh na telefonu a kontrola detailů po restartu zůstávají otevřené.

Praktická kontrola: přidat dva tréninky, otevřít detail, zavřít aplikaci i ladicí běh, znovu ji spustit a ověřit oba tréninky i jejich detaily. Další přidání musí zachovat starší záznamy.

## 4. Katalog cviků

- [x] Přidat `Exercise`, rozhraní operací a SQLite úložiště.
- [x] Zavést migraci 1 → 2 a sekci Cviky se seznamem a editorem pro vytvoření i úpravy.
- [x] Umožnit vratnou archivaci a výchozí skrytí archivovaných cviků; fyzické smazání nenabízet.
- [x] Automaticky ověřit názvy, ID, přejmenování, archivaci, filtr, chybové stavy a migraci.
- [x] Prakticky ověřit funkčnost katalogu cviků (potvrzeno uživatelem).
- [ ] Samostatně ověřit zachování archivace po restartu a návrat do tréninků.

Hotovo, když lze katalog spravovat a archivované cviky se běžně nenabízejí pro nový trénink.

Implementace kroku 4: katalog má vlastní model, rozhraní, SQLite repository a ViewModel. V sekci Cviky lze přidat a upravit název, zrušit editaci, archivovat a znovu obnovit cvik. Výchozí seznam archiv skrývá, tlačítko umožní zobrazit ho. Migrace 1 → 2 zachová původní tréninky. Přejmenování a archivace nemění ID. Automaticky prošly databázové testy i ověření ViewModelu (validace, příkazy, souběh, chyby zápisu/čtení, rollback filtru a retry); Windows i Android build prošly bez chyb a upozornění. Funkčnost katalogu uživatel potvrdil; vzhled nyní záměrně neřešíme.

Ruční kontrola: otevřít Cviky, vytvořit dva cviky, jeden přejmenovat, zkusit prázdný název a zrušení úpravy, jeden archivovat, zobrazit archiv a obnovit. Po restartu ověřit názvy a archivaci a zkontrolovat i původní seznam a detail tréninku. Přidávání cviků do tréninku je až krok 5.

## 5. Zapisování skutečného tréninku

- [x] Přidat `WorkoutExercise`, `WorkoutSet`, snímek celého tréninku a migraci 2 → 3.
- [x] Umožnit výběr aktivního cviku a zápis sérií, opakování a zátěže v kg.
- [x] Zavést kopii názvu, přesný převod kg/gramy, null/0 a validaci podle kultury.
- [x] Umožnit atomické přesuny cviků a sérií o jednu pozici.
- [x] Přidat ukončení tréninku a ochranu před dalším zápisem.
- [x] Automaticky ověřit transakce, pořadí, návaznosti, migrace a záznam mezi procesy.
- [x] Prakticky ověřit nový detail s cviky, sériemi, přesuny, ukončením a restartem (potvrzeno uživatelem).

Hotovo, když lze zaznamenat celý trénink, znovu ho načíst a jeho údaje zůstávají správné i po přejmenování definice cviku.

Implementace kroku 5: detail používá `IWorkoutLogRepository`, načítá konzistentní snímek a aktivní katalog. SQLite drží kopii názvu a celé gramy. Přesuny používají dočasnou volnou pozici v transakci, po ukončení repository odmítá další změny. UI řídí stejné pravidlo a brání souběhu. Po úspěšném zápisu a chybě obnovy se další zápis blokuje do opětovného načtení. Windows a Android build i datové testy prošly, stejně jako ověření ViewModelu se skutečnou SQLite databází. Vzhled je záměrně bez redesignu.

Ruční kontrolu nového detailu, zápisu sérií, změn pořadí, restartu a ukončení uživatel potvrdil.

## 6. Historie a úpravy

- [x] Přidat přehled historie a filtrování podle data.
- [ ] Umožnit úpravu zaznamenaných údajů.
- [ ] Přidat smazání tréninku s potvrzením v aplikaci.
- [ ] Ověřit odstranění návazných dat a zachování katalogu cviků.

Hotovo, když lze starý trénink najít, upravit a odstranit bez nekonzistentních záznamů.

První část kroku 6: samostatná záložka Historie načte tréninky od repository a dovolí zapnout dolní a horní mez podle místního data začátku. Výběr řádku otevře existující detail podle ID. Filtr probíhá nad načteným snímkem v paměti; databázové schéma se nemění. Úpravy a mazání historie zůstávají dalšími částmi kroku.

Ruční kontrola historie čeká: otevřít záložku, ověřit seznam a detail, zapnout filtr od data i do data, zadat období bez výsledků a zkusit počáteční datum po koncovém. Potom zrušit filtr a ověřit návrat všech záznamů.

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

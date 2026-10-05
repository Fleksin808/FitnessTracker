# Roadmapa FitnessTracker

## Způsob vývoje

Cílem první verze je použitelný lokální deník silových tréninků. Postupujeme v malých dokončených krocích, aby byl kód srozumitelný a každá nová část měla jasný účel.

Každý krok zahrnuje vysvětlení návrhu, komentáře u podstatné logiky, praktické ověření a aktualizaci dokumentace. Neimplementujeme více etap najednou bez předchozí dohody.

Platí [architektura](architecture.md) a [datové dohody](data-model.md). Stav kroku měníme až podle skutečně dokončené práce.

## Výchozí stav

- [x] Existuje výchozí .NET MAUI projekt.
- [x] Jednoduchá stránka načte název tréninku a zobrazí ho; prázdný vstup odmítne.
- [x] Tato úprava prošla sestavením pro Windows.
- [x] Návrh architektury, dat a pořadí práce je uložen v dokumentaci.

Dosud nemáme seznam tréninků, doménové modely, MVVM ani ukládání. Spuštění na telefonu není dosud ověřené.

## 1. MVVM a rozdělení odpovědností

- [ ] Převést současnou stránku na View a ViewModel v `Features/Workouts`.
- [ ] Vysvětlit binding, oznamování změn vlastností a příkazy.
- [ ] Přesunout zpracování vstupu z code-behind do ViewModelu.
- [ ] Zavést předávání a registraci potřebných závislostí.
- [ ] Zdokumentovat případnou vybranou pomocnou knihovnu a její účel.

Hotovo, když zadání názvu a kontrola prázdného vstupu fungují přes binding a příkaz. Zatím bez databáze.

## 2. Tréninky v paměti

- [ ] Zavést `Workout` a jeho základní pravidla.
- [ ] Definovat `IWorkoutRepository` a implementaci v paměti.
- [ ] Přidat vytvoření tréninku, seznam a detail.
- [ ] Vysvětlit kolekce, navigaci pomocí ID a životnost úložiště.

Hotovo, když lze vytvořit více tréninků a otevřít jejich detaily. Záznamy přežijí navigaci v rámci běhu aplikace; po restartu se ztratí.

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

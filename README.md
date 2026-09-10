# RTS — Reparing's · Tuning's · Setting's

*(Angolul a README második felében olvasható!)*

> Javítások, Tuningok, BugFix-ek, Finomhangolások és Beállítások Windows-ra
> — a szerviz-tulajdonos szerszámos-ládája, egységes keretrendszerben.

![Verzió](https://img.shields.io/badge/verzió-0.2.0-brightgreen) ![Platform](https://img.shields.io/badge/platform-Windows-blue) ![Build](https://github.com/LordAthis/RTS/actions/workflows/build-exe.yml/badge.svg)

---

## Mi ez?

Az **RTS** az az ernyőprojekt, ami az évek (2000 óta) alatt összegyűjtött, önálló GitHub-repókban fejlesztett Windows-javító, -tuningoló és -beállító szkripteket, eszközöket egyetlen, letölthető, grafikus felületű alkalmazásba fogja össze — hogy szervizmunka közben ne kelljen tíz különböző repót egyesével keresgélni és futtatni.

Minden önálló repó (`IWS`, `DeepSysTools`, `Network-Tools`, `WinRegTools` és a többi) egyben **RTS-modul** is: az RTS letölti, katalogizálja, és egy közös felületről futtathatóvá teszi őket.

**Fő funkciók:**
- Grafikus (WPF) launcher, sötét/neon témával, OS-detektálással (XP / 7 / 10 / 11)
- Modul-kezelés: a `modules.json` alapján automatikusan letölti (`bootstrap.ps1`) és futtathatóvá teszi az összes engedélyezett modult
- Frissítés-figyelés (`update_check.ps1`): GitHub API-n keresztül ellenőrzi, van-e újabb commit egy-egy modulban
- **IWS modul ténylegesen bekötve**: biztonsági hardening szkriptek (Core Hardening, Quad9+DoH, USB-tiltás, böngésző-hardening, Autoruns-ellenőrzés, teljes telepítés) közvetlenül a felületről indíthatók
- Általános **"Modulok" nézet**: minden `modules.json`-beli modul egy listában, "Futtatás" gombbal — új modul RTS-be illesztéséhez elég a `modules.json`-t bővíteni, nem kell hozzá külön UI-kód
- **Prémium/zárt modulok** támogatása: néhány modul (pl. ügyfeleknek szánt extra) zárt GitHub-repóként marad, de licenc-token birtokában az RTS ugyanúgy letölti és futtatja
- Automatikus build: minden push-nál a GitHub Actions Windows runneren lefordítja az `RTS.exe`-t, tag push-nál (`v*.*.*`) pedig Release-t is készít belőle
- Admin-jogosultsággal indul (nincs szükség kézi "Futtatás rendszergazdaként"-re)

**Célközönség:** laikusok is tudják használni (részletes leírás a blogon: https://lordathis.blogspot.com), de szakembereknek is pontos és értékes eszköz.

---

## Használat

### 1. Első indítás (bootstrap)
```powershell
git clone https://github.com/LordAthis/RTS.git
cd RTS
.\bootstrap.ps1
```
Ez letölti a `modules.json`-ban engedélyezett összes modult az `Apps\` mappába. Csak egy-egy modult is letölthetsz:
```powershell
.\bootstrap.ps1 -ModuleFilter "IWS,Network-Tools"
```
Zárt (prémium) modulokhoz token szükséges:
```powershell
.\bootstrap.ps1 -LicenseToken "ghp_xxx..."
# vagy: $env:RTS_LICENSE_TOKEN = "ghp_xxx..."
```

### 2. Frissítések ellenőrzése
```powershell
.\update_check.ps1
```

### 3. Grafikus felület (RTS.exe)
- **Letöltve, kész exe-vel:** a [Releases](https://github.com/LordAthis/RTS/releases) oldalról letöltheted a legújabb `RTS.exe`-t (csak Windows 10/11-en tesztelt, önmagában futtatható, nem igényel .NET telepítést).
- **Forrásból** (fejlesztőknek): .NET 8 SDK szükséges hozzá.
  ```powershell
  dotnet publish src/RTS.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
  .\publish\RTS.exe
  ```
- Az `RTS.exe`-t az RTS gyökérmappájából (vagy oda másolva) érdemes futtatni, mert a `modules.json`-t és az `Apps\` mappát onnan keresi.

---

## Mappa struktúra

```
RTS/
├── bootstrap.ps1              ← Modulok letöltése (első indításkor / -Force-szal újra)
├── update_check.ps1           ← Frissítésfigyelő (kézi + automatikus)
├── modules.json                ← A modulok listája, GitHub URL-je, belépési pontja
├── .github/workflows/
│   └── build-exe.yml          ← CI: RTS.exe automatikus build + Release
├── src/                        ← RTS saját kódja (WPF GUI, .NET 8)
│   ├── RTS.csproj
│   ├── MainWindow.xaml(.cs)   ← Fő ablak, OS-választó, navigáció
│   ├── Services/
│   │   ├── ModuleRunner.cs    ← Egy modul entry_point-jának futtatása
│   │   └── ModuleCatalog.cs   ← modules.json beolvasása
│   ├── Models/
│   │   └── ModuleInfo.cs
│   └── Views/
│       ├── IWSView.xaml(.cs)      ← IWS modul saját felülete (valós szkriptekkel)
│       └── ModulesView.xaml(.cs)  ← Általános modul-lista minden más modulhoz
├── Apps/                        ← Letöltött modulok ide kerülnek (bootstrap.ps1 hozza létre)
│   ├── IWS/
│   ├── DeepSysTools/
│   └── ...
├── scripts/                     ← Önálló, RTS-be még be nem kötött segédszkriptek
└── _archive/                    ← Régi/retro anyagok
```

---

## Fejlesztés / Bővítés

**Új modul RTS-be illesztése:**
1. Vedd fel a `modules.json`-ba egy új bejegyzést (`name`, `repo`, `description`, `category`, `entry_point`, `visibility`, `enabled`).
2. Ha a repó `Launcher.ps1`-t (vagy más egységes belépési pontot) használ, a beépített "Modulok" nézet automatikusan felkínálja, külön kódolás nélkül.
3. Ha egyedi, saját felületet szeretnél a modulnak (mint az IWS-nek), készíts egy új `Views/<Modul>View.xaml(.cs)`-t az IWSView mintájára, és köss be rá egy gombot a `MainWindow`-ban.

**Zárt/prémium modul:** állítsd `"visibility": "private"`-ra és `"tier": "premium"`-ra a `modules.json`-ban — a `bootstrap.ps1` és az `RTS.exe` token birtokában ugyanúgy kezeli, mint egy publikus modult.

---

## Verzió / Version: 0.2.0

---
---

# RTS — Reparing's · Tuning's · Setting's (English)

> Repairs, tuning, bugfixes and fine-tuning for Windows — a service technician's toolbox, in one unified framework.

## What is this?

**RTS** is the umbrella project that brings together the Windows repair, tuning and configuration scripts and tools built up over the years (since 2000) across many separate GitHub repos, into a single downloadable app with a graphical interface — so that during service work you don't have to hunt down and run ten different repos one by one.

Every standalone repo (`IWS`, `DeepSysTools`, `Network-Tools`, `WinRegTools`, and others) is also an **RTS module**: RTS downloads, catalogs, and makes them runnable from one shared interface.

**Main features:**
- Graphical (WPF) launcher with a dark/neon theme and OS detection (XP / 7 / 10 / 11)
- Module management: based on `modules.json`, automatically downloads (`bootstrap.ps1`) and makes runnable every enabled module
- Update checking (`update_check.ps1`): checks the GitHub API for newer commits per module
- **IWS module actually wired in**: security hardening scripts (Core Hardening, Quad9+DoH, USB restriction, browser hardening, Autoruns check, full install) run directly from the UI
- Generic **"Modules" view**: every entry from `modules.json` listed with a "Run" button — adding a new module to RTS only requires extending `modules.json`, no custom UI code needed
- Support for **premium/private modules**: some modules (e.g. paid-customer extras) stay closed-source repos, but with a license token RTS downloads and runs them the same way
- Automatic build: every push is built into `RTS.exe` on a Windows runner via GitHub Actions; pushing a tag (`v*.*.*`) also creates a Release
- Launches with administrator privileges (no manual "Run as administrator" needed)

**Audience:** usable by laypeople too (detailed write-ups on the blog: https://lordathis.blogspot.com), while staying precise and valuable for professionals.

## Usage

### 1. First run (bootstrap)
```powershell
git clone https://github.com/LordAthis/RTS.git
cd RTS
.\bootstrap.ps1
```
This downloads every enabled module from `modules.json` into the `Apps\` folder. You can also download just specific modules:
```powershell
.\bootstrap.ps1 -ModuleFilter "IWS,Network-Tools"
```
Private (premium) modules need a token:
```powershell
.\bootstrap.ps1 -LicenseToken "ghp_xxx..."
# or: $env:RTS_LICENSE_TOKEN = "ghp_xxx..."
```

### 2. Checking for updates
```powershell
.\update_check.ps1
```

### 3. Graphical interface (RTS.exe)
- **Prebuilt:** download the latest `RTS.exe` from [Releases](https://github.com/LordAthis/RTS/releases) (tested on Windows 10/11, self-contained, no .NET install required).
- **From source** (for developers): requires the .NET 8 SDK.
  ```powershell
  dotnet publish src/RTS.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
  .\publish\RTS.exe
  ```
- Run `RTS.exe` from (or copied into) the RTS root folder, since it looks for `modules.json` and `Apps\` relative to itself.

## Folder structure

See the Hungarian section above — the tree is identical in both languages.

## Development / Extending

**Adding a new module to RTS:**
1. Add a new entry to `modules.json` (`name`, `repo`, `description`, `category`, `entry_point`, `visibility`, `enabled`).
2. If the repo uses `Launcher.ps1` (or another unified entry point), the built-in "Modules" view picks it up automatically, no extra code needed.
3. For a custom UI (like IWS has), create a new `Views/<Module>View.xaml(.cs)` modeled on `IWSView`, and wire a button to it in `MainWindow`.

**Private/premium module:** set `"visibility": "private"` and `"tier": "premium"` in `modules.json` — both `bootstrap.ps1` and `RTS.exe` handle it the same way as a public module, given a valid token.

## Version: 0.2.0

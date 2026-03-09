# HardwareChecker

HardwareChecker ist eine WPF-Desktopanwendung (.NET Framework 4.7.2), die lokale Hardwaredaten eines Windows-Rechners ausliest, in der Oberfläche anzeigt und per Klick in eine SQLite-Datenbank speichert.

## Was das Projekt macht

Die Anwendung sammelt Informationen über:

- CPU (Name, Hersteller, Kerne, logische Prozessoren, Takt)
- GPU (Name, Hersteller, Treiberversion, Video Processor, VRAM)
- RAM (Gesamtgröße und RAM-Takt)
- Laufwerke (Modell, Interface, Größe)
- Mainboard (Hersteller, Produkt, Seriennummer)

Die Daten werden beim Start über WMI abgefragt und im Hauptfenster angezeigt.

## Speichern in Datenbank

Über den Button **"Speichern"** werden die aktuell erkannten Hardwaredaten in eine SQLite-Datenbank geschrieben.

Speicherort der Datenbank:

- Immer im selben Ordner wie die laufende Anwendung: `app.db`

Die Anwendung stellt den Zielordner automatisch sicher (falls erforderlich).

## Datenmodell (SQLite)

Beim Speichern wird das Schema automatisch angelegt (falls noch nicht vorhanden):

- `cpu` (Primärschlüssel: `name`)
- `gpu` (Primärschlüssel: `name`)
- `motherboard` (Primärschlüssel: `name`)
- `computer` (Verknüpfung auf CPU/GPU/Mainboard per Foreign Keys)

Verhalten beim Schreiben:

- CPU, GPU und Mainboard werden per **Upsert** aktualisiert/eingefügt.
- Für `computer` wird pro Speichervorgang ein neuer Datensatz angelegt.
- Foreign Keys sind aktiviert (`PRAGMA foreign_keys = ON`).

## Projektstruktur

- `HardwareChecker.sln` - Visual-Studio-Solution
- `HardwareChecker/HardwareChecker.csproj` - WPF-Projektdatei
- `HardwareChecker/MainWindow.xaml` - UI (Anzeige + Speichern-Button)
- `HardwareChecker/MainWindow.xaml.cs` - Hardwareabfrage und DB-Logik
- `HardwareChecker/App.config` - Ziel-Framework-Konfiguration

## Voraussetzungen

- Windows (WMI-Klassen werden verwendet)
- .NET Framework 4.7.2
- Visual Studio 2022 (laut Solution-Metadaten)

## Build und Start

1. Solution in Visual Studio öffnen: `HardwareChecker.sln`
2. Build-Konfiguration wählen (`Debug` oder `Release`)
3. Projekt starten (F5)
4. Im Fenster Hardwaredaten prüfen und **"Speichern"** klicken

## Technische Hinweise

- Hardwaredaten werden über `System.Management` (WMI) ausgelesen.
- DB-Zugriff erfolgt über `Microsoft.Data.Sqlite`.
- Der Code nutzt teils englische, teils deutsche Bezeichner/Texte; funktional hat das keine Auswirkungen.

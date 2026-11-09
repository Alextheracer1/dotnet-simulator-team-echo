---
name: development-journal
description: >-
  Guides the creation, maintenance, and structured updating of student development journals (Entwicklungsjournal) in docs/development-journal/ following strict tag conventions (Done, KI, Artefact, Test, Comment).
---

# Development Journal Maintenance (`development-journal`)

Use this skill whenever an agent or developer documents development progress, commits, refactorings, or new features in the individual development journals.

## Location & Structure

Journals are located in `docs/development-journal/`:
* `docs/development-journal/Alexander-Graf.md`
* `docs/development-journal/Nikolas-Giesen.md`
* *(Weitere Teammitglieder analog: `<Vorname>-<Nachname>.md`)*

## Tag-Vorgaben & Format

Jeder Journaleintrag **muss** zwingend die drei Pflicht-Tags und bei Relevanz die optionalen Tags enthalten:

### Pflicht-Tags:
* `- **Done:**` Was wurde bearbeitet und welches konkrete Ergebnis liegt vor? Präzise Beschreibung der Module, Schnittstellen und Funktionalitäten.
* `- **KI:**` Verwendetes Werkzeug, Modell (z. B. Google Gemini 3.8 Flash (High), GitHub Copilot GPT-6 Luna), Einsatzform (Architektur-Entwurf, Code-Generierung, Debugging, Refactoring) und kritischer Umgang mit dem Ergebnis. Bei rein manueller Umsetzung explizit `keine` angeben.
* `- **Artefact:**` Betroffene Dateipfade (z. B. `TeamShared/TeamSharedModbus.cs`), Testdateien, generierte Logs/CSVs, Schemata oder Dokumente.

### Optionale Tags (bei Relevanz):
* `- **Test:**` Durchgeführte automatisierte (`dotnet test`, Test-Runner) oder manuelle Tests (Modbus-Diagnose-Client, Viewer, Broker-Interaktion) inklusive Ergebnis (`PASSED`, Beobachtungen).
* `- **Comment:**` Architekturentscheidungen, gelöste Probleme/Herausforderungen, Erkenntnisse oder nächste geplante Schritte.

---

## Standard-Vorlage (Markdown)

```markdown
## YYYY-MM-DD – <Prägnanter Titel des Arbeitspakets>

### <Sub-Thema / Komponente (falls mehrere Themen am selben Tag)>
- **Done:** <Beschreibung der Umsetzung und Resultate>
- **KI:** <Modell, Prompting-/Einsatzform, Überprüfung; oder 'keine'>
- **Artefact:** `<Pfad/Datei1.cs>`, `<Pfad/Datei2.cs>`
- **Test:** <Testausführung, Test-Runner oder manuelle Prüfung>
- **Comment:** <Entscheidungsgrundlage, Lessons Learned oder Hindernisse>
```

---

## Best-Practice Workflow für Agenten

1. **Kontext ermitteln:**
   * Git-Historie und Diff für das Arbeitspaket abfragen:
     ```bash
     git log -n 5 --stat
     git diff --stat HEAD~1 HEAD
     ```
   * Betroffene Komponenten identifizieren (`Dual-Port-Charger`, `TeamShared`, `TeamRabbitMQShared`, etc.).

2. **Qualitätsprüfung & Tests ausführen:**
   * Vor dem Dokumentieren sicherstellen, dass die Änderungen kompilieren und Tests bestehen:
     ```bash
     DOTNET_CLI_HOME=/tmp/dotnet_home /usr/local/share/dotnet/dotnet build dotNET_Programmierung_2026_Aufgabe_02.slnx -m:1
     ```

3. **Eintrag verfassen & anfügen:**
   * In `docs/development-journal/<Name>.md` chronologisch korrekt einpflegen.
   * Sicherstellen, dass keine Markdown-Syntaxfehler (wie unmaskierte Spectre-Markup-Tags `[[...]]`) entstehen.

# 📚 Dokumentation: Energy Twin 2026 (Team Echo)

Willkommen in der zentralen Dokumentation des verteilten Digital-Twin-Simulationssystems **Energy Twin 2026**.

---

## 📖 Schritt-für-Schritt Tutorials

Hier finden Sie praxisorientierte Anleitungen zur Einrichtung und Bedienung des Systems:

1. **[Tutorial: RabbitMQ mit Docker und .env-Datei einrichten](file:///Users/alex/Github/dotnet-simulator-team-echo/docs/tutorial-rabbitmq-docker.md)**
   * Konfiguration von RabbitMQ über Docker Compose und [`.env`](file:///Users/alex/Github/dotnet-simulator-team-echo/.env.example).
   * Verwalten des Containers (`docker compose up -d`, `ps`, `logs`, `down`).
   * Zugriff und Bedienung des RabbitMQ Management Web-UI (Port 15672).
   * Anbindung der .NET-Simulatoren und Troubleshooting.

2. **[Tutorial: Ausführen der .NET Applikationen](file:///Users/alex/Github/dotnet-simulator-team-echo/docs/tutorial-dotnet-applications.md)**
   * Architektur- und Komponentenübersicht (Master, Wetter, Feldgeräte, Gateway, Viewer).
   * Verbindliche 4-Phasen-Startreihenfolge.
   * Exakte Befehle und Tastatursteuerung für jedes Modul.
   * Automatisiertes Multi-Terminal-Startskript für Windows ([`scripts/start-all.bat`](file:///Users/alex/Github/dotnet-simulator-team-echo/scripts/start-all.bat), [`scripts/start-all.ps1`](file:///Users/alex/Github/dotnet-simulator-team-echo/scripts/start-all.ps1)) und macOS/Linux ([`scripts/start-all.sh`](file:///Users/alex/Github/dotnet-simulator-team-echo/scripts/start-all.sh)).
   * Port-, Register- und Troubleshooting-Matrix.

---

## 🏛️ Architektur & Entwickler-Richtlinien

* **[Agent Guidelines, Standards & Onboarding (AGENTS.md)](file:///Users/alex/Github/dotnet-simulator-team-echo/AGENTS.md):** Zentraler Leitfaden für Entwickler und autonome KI-Agenten mit Architekturüberblick, Modbus-Registerbelegung, RabbitMQ-Konventionen, Build-Vorschriften und Skill-Handbuch.
* **[Projekt-Kontext & Gesamtdokumentation](file:///Users/alex/Github/dotnet-simulator-team-echo/PROJECT_CONTEXT.md):** Umfassende Spezifikation der Netzwerkprotokolle (TCP-Master, Modbus LB02-Register, HTTP Weather-Push, RabbitMQ Pub/Sub).

---

## 📝 Entwicklungsjournale

Die individuellen Journale der Teammitglieder befinden sich im Unterordner [`docs/development-journal/`](file:///Users/alex/Github/dotnet-simulator-team-echo/docs/development-journal/):
* [`Alexander-Graf.md`](file:///Users/alex/Github/dotnet-simulator-team-echo/docs/development-journal/Alexander-Graf.md)
* [`Nikolas-Giesen.md`](file:///Users/alex/Github/dotnet-simulator-team-echo/docs/development-journal/Nikolas-Giesen.md)

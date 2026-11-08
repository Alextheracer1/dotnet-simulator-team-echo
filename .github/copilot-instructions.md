# Copilot Instructions

## General Guidelines
- Der Nutzer möchte, dass bei künftigen Aufgaben immer der GrillMe-Ansatz genutzt wird (gezielte Rückfragen vor Implementierung).
- Bei größeren, unklaren Implementierungen möchte der Nutzer vor Beginn den GrillMe-Ansatz: gezielte Rückfragen zu Anforderungen und Randbedingungen, bevor Code geändert wird.
- Zusätzlich soll der DevelopmentJournal-Skill verwendet werden.

## Projektrichtlinien
- Der Nutzer bevorzugt, dass bei unklaren Anforderungen zuerst der GrillMe-Ansatz genutzt wird (gezielte Rückfragen vor Implementierung).

## WeatherSimulator-HTTP-Dienst
- Der WeatherSimulator-HTTP-Dienst darf beim Start erst verfügbar werden, nachdem mindestens ein gültiger Zeit-/Environment-Stand vom Simulation Master empfangen wurde. Danach darf er bei späterem Master-Ausfall weiterlaufen und im Hintergrund erneut verbinden.
- Für die Wetter-Push-Receiver gelten dynamische freie HTTP-Ports: PV versucht zuerst Port 6000 und nimmt bei Belegung den nächsthöheren freien Port; Wind startet bei 6100 und sucht bei Belegung ebenfalls aufsteigend weiter. Der tatsächlich gebundene Port wird beim WeatherSimulator registriert.
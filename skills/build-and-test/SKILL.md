---
name: build-and-test
description: >-
  Builds and validates the entire Energy Twin .NET 10 solution, restores dependencies using local caches, and ensures all projects compile without errors.
---

# Build and Test Solution

Use this skill to compile all projects in the solution `dotNET_Programmierung_2026_Aufgabe_02.slnx`.

## Prerequisites
* .NET 10.0 SDK installed
* `DOTNET_CLI_HOME` set to an accessible directory (e.g., `/tmp/dotnet_home`)

## Build Procedure

1. **Restore all solution projects:**
   ```bash
   DOTNET_CLI_HOME=/tmp/dotnet_home dotnet restore dotNET_Programmierung_2026_Aufgabe_02.slnx -m:1
   ```

2. **Compile the complete solution:**
   ```bash
   DOTNET_CLI_HOME=/tmp/dotnet_home dotnet build dotNET_Programmierung_2026_Aufgabe_02.slnx -m:1 --no-restore
   ```

> [!NOTE]
> The `-m:1` flag disables MSBuild multi-node worker parallelism, preventing macOS sandbox IPC and socket errors.

3. **Verify single project compilation (e.g. Dual-Port-Charger):**
   ```bash
   DOTNET_CLI_HOME=/tmp/dotnet_home dotnet build Dual-Port-Charger/Dual-Port-Charger.csproj -m:1
   ```

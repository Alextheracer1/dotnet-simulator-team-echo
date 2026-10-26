#!/bin/bash
echo "Creating Solution file..."
dotnet new sln -n dotNET_Programmierung_2026_Aufgabe_02

echo "Finding and adding all .csproj files to the solution..."
#find . -name "*.csproj" -exec dotnet sln dotNET_Programmierung_2026_Aufgabe_02.sln add {} \;
find . -name "*.csproj" -exec dotnet sln dotNET_Programmierung_2026_Aufgabe_02.slnx add {} \;

echo "Solution generation complete."

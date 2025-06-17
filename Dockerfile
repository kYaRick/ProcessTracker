FROM mcr.microsoft.com/dotnet/sdk:8.0-nanoserver-ltsc2022 AS build

WORKDIR C:\ProcessTracker

COPY . .

RUN dotnet restore

RUN dotnet publish "./sources/ProcessTracker.Cli/ProcessTracker.Cli.csproj" -c Release -o "./bin/publish" --no-restore

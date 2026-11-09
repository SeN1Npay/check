#!/usr/bin/env bash
# Сборка из Linux/macOS/Windows (Git Bash): ./build.sh [build|publish|test]
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
mode="${1:-build}"
case "$mode" in
  build)
    dotnet build src/Metamorph.App/Metamorph.App.csproj -c Release ;;
  publish)
    rm -rf artifacts/publish
    dotnet publish src/Metamorph.App/Metamorph.App.csproj -c Release -r win-x64 --self-contained false \
      -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish
    cp -r themes README.md LICENSE THIRD-PARTY-NOTICES.txt artifacts/publish/
    rm -f artifacts/publish/*.pdb
    ver=$(grep -oP '(?<=<Version>)[^<]+' src/Metamorph.App/Metamorph.App.csproj)
    mkdir -p artifacts/dist
    (cd artifacts/publish && rm -f "../dist/Metamorph-$ver-win-x64.zip" && zip -qr "../dist/Metamorph-$ver-win-x64.zip" .)
    echo "artifacts/dist/Metamorph-$ver-win-x64.zip" ;;
  core)
    dotnet build src/Metamorph.Core/Metamorph.Core.csproj -c Release ;;
esac

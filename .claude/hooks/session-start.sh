#!/bin/bash
# Prepara sessoes do Claude Code na web: .NET 10 SDK, dotnet-ef e pacotes restaurados, para
# build, testes e migrations funcionarem desde o inicio. Idempotente: pula o que ja existe.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# O instalador oficial (builds.dotnet.microsoft.com) e bloqueado pelo proxy da sessao; o
# pacote do Ubuntu passa. apt-get update so avisa quando um PPA de terceiros falha.
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks | grep -q '^10\.'; then
  apt-get update -qq
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-10.0
fi

export PATH="$PATH:$HOME/.dotnet/tools"
if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> "$CLAUDE_ENV_FILE"
  echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1' >> "$CLAUDE_ENV_FILE"
fi

# Mesma versao do Microsoft.EntityFrameworkCore.Design do projeto. Se o comando nao roda mas
# o registro de ferramentas diz que esta instalado (shim apagado), reinstala do zero.
if ! dotnet-ef --version >/dev/null 2>&1; then
  dotnet tool uninstall --global dotnet-ef >/dev/null 2>&1 || true
  dotnet tool install --global dotnet-ef --version 10.0.5
fi

PROJETO="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}"
dotnet restore "$PROJETO/Tests/Tests.csproj"

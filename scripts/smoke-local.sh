#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

compose() {
  if docker compose version >/dev/null 2>&1; then
    docker compose "$@"
  else
    docker-compose "$@"
  fi
}

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is required for the local MySQL smoke test." >&2
  exit 1
fi

echo "Starting MySQL..."
compose up -d mysql

echo "Waiting for MySQL health..."
for _ in $(seq 1 60); do
  if compose exec -T mysql mysqladmin ping -h localhost -u root -prootpassword >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

if ! compose exec -T mysql mysqladmin ping -h localhost -u root -prootpassword >/dev/null 2>&1; then
  echo "MySQL did not become ready." >&2
  compose logs mysql
  exit 1
fi

echo "Restoring, building, and testing..."
dotnet restore hds.sln
dotnet build hds.sln --configuration Debug --no-restore --nologo
dotnet test hds.sln --configuration Debug --no-build --nologo

LOG_FILE="$(mktemp)"
SERVER_PID=""
cleanup() {
  if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" >/dev/null 2>&1; then
    kill "$SERVER_PID" >/dev/null 2>&1 || true
    wait "$SERVER_PID" >/dev/null 2>&1 || true
  fi
  rm -f "$LOG_FILE"
}
trap cleanup EXIT

echo "Launching server and waiting for readiness..."
dotnet run --project "hds/Hardline Dreams MxO server.csproj" --no-build >"$LOG_FILE" 2>&1 &
SERVER_PID="$!"

for _ in $(seq 1 90); do
  if grep -q "Im'running :D" "$LOG_FILE"; then
    # The ready line prints before listener threads finish starting; a listener crash
    # (e.g. a Windows-only socket call) kills the process right after it. Confirm the
    # server is still alive and the auth port accepts connections.
    sleep 3
    if ! kill -0 "$SERVER_PID" >/dev/null 2>&1; then
      echo "Server exited right after reporting ready." >&2
      cat "$LOG_FILE" >&2
      exit 1
    fi
    if ! nc -z 127.0.0.1 11000 >/dev/null 2>&1; then
      echo "Server reported ready but auth port 11000 is not accepting connections." >&2
      cat "$LOG_FILE" >&2
      exit 1
    fi
    echo "Smoke test passed: server reached ready state and auth port 11000 accepts connections."
    exit 0
  fi

  if ! kill -0 "$SERVER_PID" >/dev/null 2>&1; then
    echo "Server exited before readiness." >&2
    cat "$LOG_FILE" >&2
    exit 1
  fi

  sleep 1
done

echo "Timed out waiting for server readiness." >&2
cat "$LOG_FILE" >&2
exit 1

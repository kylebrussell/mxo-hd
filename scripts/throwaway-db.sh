#!/usr/bin/env bash
# Creates or drops a throwaway database for one test run, from SQL/reality_hd.sql, in the mxo-hd-mysql container.
#
#   scripts/throwaway-db.sh create mxotest_mine   then run a server with MXO_DB_NAME=mxotest_mine
#   scripts/throwaway-db.sh drop mxotest_mine
#   scripts/throwaway-db.sh list
#
# Names must start with mxotest_, which is what keeps this away from the shared reality_hd database. The app user has
# no rights outside reality_hd, so creating and dropping uses the container's root account, and each create makes sure
# the app user has rights on mxotest_* (and nothing new beyond that). Passwords stay inside the container.
set -euo pipefail

CONTAINER="${MXO_DB_CONTAINER:-mxo-hd-mysql}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCHEMA="$ROOT_DIR/SQL/reality_hd.sql"

usage() {
  echo "Usage: $0 create|drop <mxotest_name> | list" >&2
  exit 2
}

check_name() {
  if [[ ! "$1" =~ ^mxotest_[A-Za-z0-9_]{1,50}$ ]]; then
    echo "Refusing database name '$1': it must match mxotest_[A-Za-z0-9_]+" >&2
    exit 2
  fi
}

# Runs a script in the container with the name as $1. The script expands its own variables there, so no password is
# ever on this machine's command line.
in_container() {
  local script="$1"
  shift
  MSYS_NO_PATHCONV=1 docker exec -i "$CONTAINER" sh -c "$script" sh "$@"
}

command="${1:-}"
case "$command" in
  create)
    [ $# -eq 2 ] || usage
    check_name "$2"
    in_container 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" exec mysql -uroot -e "CREATE DATABASE \`$1\` CHARACTER SET latin1; GRANT ALL PRIVILEGES ON \`mxotest\\_%\`.* TO \`$MYSQL_USER\`@\`%\`;"' "$2"
    in_container 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" exec mysql -uroot "$1"' "$2" < "$SCHEMA"
    echo "Created $2"
    ;;
  drop)
    [ $# -eq 2 ] || usage
    check_name "$2"
    in_container 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" exec mysql -uroot -e "DROP DATABASE IF EXISTS \`$1\`;"' "$2"
    echo "Dropped $2"
    ;;
  list)
    [ $# -eq 1 ] || usage
    in_container 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" exec mysql -uroot -N -e "SHOW DATABASES LIKE \"mxotest\\_%\";"'
    ;;
  *)
    usage
    ;;
esac

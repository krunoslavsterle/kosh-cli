#!/bin/bash

set -e

IP="127.0.0.1"

# Detect OS and set HOSTS_FILE path
case "$(uname -s)" in
  Linux*)     HOSTS_FILE="/etc/hosts"; SUDO="sudo";;
  Darwin*)    HOSTS_FILE="/etc/hosts"; SUDO="sudo";;
  MINGW*|MSYS*) HOSTS_FILE="/c/Windows/System32/drivers/etc/hosts"; SUDO="";;
  *)          echo "❌ Unsupported OS: $(uname -s)"; exit 1;;
esac

# Function to patch one domain
patch_domain() {
  local DOMAIN="$1"
  if grep -qE "^$IP[[:space:]]+$DOMAIN$" "$HOSTS_FILE"; then
    echo "ℹ️ Exists: $DOMAIN"
  else
    echo "$IP $DOMAIN" | $SUDO tee -a "$HOSTS_FILE" > /dev/null
    echo "✅ Added: $DOMAIN"
  fi
}

# Loop through all arguments
for DOMAIN in "$@"; do
  patch_domain "$DOMAIN"
done

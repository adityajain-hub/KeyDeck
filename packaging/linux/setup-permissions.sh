#!/usr/bin/env bash
set -e

echo "=========================================================="
echo " KeyDeck Linux Input & Virtual Keyboard Setup"
echo "=========================================================="

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UDEV_RULE_FILE="$SCRIPT_DIR/99-keydeck.rules"

if [ ! -f "$UDEV_RULE_FILE" ]; then
    echo "Error: 99-keydeck.rules not found in $SCRIPT_DIR"
    exit 1
fi

echo "[1/3] Installing udev rules to /etc/udev/rules.d/..."
sudo cp "$UDEV_RULE_FILE" /etc/udev/rules.d/99-keydeck.rules

echo "[2/3] Reloading udev rules..."
sudo udevadm control --reload-rules
sudo udevadm trigger

echo "[3/3] Adding $USER to 'input' group..."
sudo usermod -aG input "$USER"

echo ""
echo "✅ Configuration complete!"
echo "NOTE: Please log out and log back in (or reboot) for your user group changes to take effect."
echo "=========================================================="

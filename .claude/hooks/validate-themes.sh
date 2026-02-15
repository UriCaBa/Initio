#!/usr/bin/env bash
# PostToolUse hook: Validate theme ResourceKey consistency across all 5 themes
# Only runs when a Themes/*.xaml file is edited or written

FILE_PATH=$(echo "$TOOL_INPUT" | sed -n 's/.*"file_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
FILE_PATH="${FILE_PATH//\\//}"

# Only run for theme files
case "$FILE_PATH" in
  */Themes/Theme.*.xaml) ;;
  *) exit 0 ;;
esac

# Derive project root from the file path
PROJECT_ROOT="${FILE_PATH%/Themes/*}"
THEMES_DIR="$PROJECT_ROOT/Themes"

if [ ! -d "$THEMES_DIR" ]; then
  exit 0
fi

REFERENCE=""
REFERENCE_NAME=""
HAS_ERRORS=0

for f in "$THEMES_DIR"/Theme.*.xaml; do
  [ -f "$f" ] || continue
  name=$(basename "$f")

  # Extract all x:Key values, one per line, sorted
  keys=$(grep 'x:Key="' "$f" | sed 's/.*x:Key="\([^"]*\)".*/\1/' | sort)

  if [ -z "$REFERENCE" ]; then
    REFERENCE="$keys"
    REFERENCE_NAME="$name"
    continue
  fi

  missing=$(comm -23 <(echo "$REFERENCE") <(echo "$keys"))
  extra=$(comm -13 <(echo "$REFERENCE") <(echo "$keys"))

  if [ -n "$missing" ]; then
    echo "MISSING in $name (present in $REFERENCE_NAME):"
    echo "$missing" | sed 's/^/  /'
    HAS_ERRORS=1
  fi
  if [ -n "$extra" ]; then
    echo "EXTRA in $name (not in $REFERENCE_NAME):"
    echo "$extra" | sed 's/^/  /'
    HAS_ERRORS=1
  fi
done

if [ "$HAS_ERRORS" -eq 1 ]; then
  echo ""
  echo "Theme consistency FAILED. All 5 themes must define the same ResourceKeys."
  exit 1
fi

echo "Theme consistency OK - all themes have matching ResourceKeys."
exit 0

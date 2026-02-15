#!/usr/bin/env bash
# PostToolUse hook: Validate catalog.json structure after edits
# Only runs when catalog.json is edited or written

FILE_PATH=$(echo "$TOOL_INPUT" | sed -n 's/.*"file_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
FILE_PATH="${FILE_PATH//\\//}"

# Only run for catalog.json at project root
case "$FILE_PATH" in
  */catalog.json) ;;
  *) exit 0 ;;
esac

# Validate JSON syntax and structure with python3
RESULT=$(python3 -c "
import json, sys

try:
    with open(sys.argv[1], encoding='utf-8') as f:
        data = json.load(f)
except json.JSONDecodeError as e:
    print(f'Invalid JSON: {e}')
    sys.exit(1)

if not isinstance(data, dict):
    print('catalog.json must be a JSON object with categories')
    sys.exit(1)

cats = data.get('categories')
if not isinstance(cats, list):
    print('catalog.json missing \"categories\" array')
    sys.exit(1)

errors = []
total_apps = 0
for ci, cat in enumerate(cats):
    if 'name' not in cat:
        errors.append(f'  Category {ci}: missing \"name\"')
    apps = cat.get('apps', [])
    if not isinstance(apps, list):
        errors.append(f'  {cat.get(\"name\", ci)}: \"apps\" is not an array')
        continue
    for ai, app in enumerate(apps):
        if 'name' not in app:
            errors.append(f'  Category \"{cat.get(\"name\")}\", app {ai}: missing \"name\"')
        if 'wingetId' not in app:
            errors.append(f'  {app.get(\"name\", f\"app {ai}\")}: missing \"wingetId\"')
    total_apps += len(apps)

if errors:
    print('catalog.json structure errors:')
    for e in errors:
        print(e)
    sys.exit(1)

print(f'catalog.json valid: {total_apps} apps in {len(cats)} categories')
" "$FILE_PATH" 2>&1)

EXIT_CODE=$?
echo "$RESULT"
exit $EXIT_CODE

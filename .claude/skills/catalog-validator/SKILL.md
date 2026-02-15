---
name: catalog-validator
description: Validate catalog.json structure, required fields, and detect drift between embedded and remote versions. Use when editing the app catalog, adding new apps, or checking catalog integrity.
---

# Catalog Validator

Validates catalog.json for Initio's 3-tier catalog system (remote -> cache -> embedded).

## Catalog Structure

```json
{
  "version": 1,
  "updatedAt": "2026-02-12",
  "categories": [
    {
      "name": "Productivity",
      "apps": [
        { "name": "7-Zip", "wingetId": "7zip.7zip" }
      ]
    }
  ]
}
```

## Validation Steps

1. **JSON syntax**: Parse catalog.json and report any syntax errors
2. **Top-level structure**: Must be object with `categories` array
3. **Category objects**: Each must have `name` and `apps` array
4. **App required fields**: Every app must have `name` and `wingetId`
5. **Duplicate detection**: Flag duplicate `wingetId` values
6. **Winget ID format**: Verify `wingetId` values match winget format (Publisher.AppName)

## Known Categories

The valid categories are defined in the catalog and used for tab filtering:
- Browsers
- Development
- Communication
- Media
- Utilities
- Gaming
- Productivity

## Execution

```bash
# Validate JSON syntax
python3 -c "import json; json.load(open('catalog.json'))"

# Full validation
python3 -c "
import json, sys, re
with open('catalog.json') as f:
    data = json.load(f)

required = {'name', 'id', 'category'}
known_cats = {'Browsers','Development','Communication','Media','Utilities','Gaming','Productivity'}
winget_re = re.compile(r'^[A-Za-z0-9]+\.[A-Za-z0-9.+-]+$')
ids = set()
errors = []

for i, app in enumerate(data):
    missing = required - set(app.keys())
    if missing:
        errors.append(f'{app.get(\"name\", f\"index {i}\")}: missing {missing}')
    aid = app.get('id', '')
    if aid in ids:
        errors.append(f'Duplicate id: {aid}')
    ids.add(aid)
    if aid and not winget_re.match(aid):
        errors.append(f'{aid}: invalid winget ID format')
    cat = app.get('category', '')
    if cat and cat not in known_cats:
        errors.append(f'{app.get(\"name\")}: unknown category \"{cat}\"')

if errors:
    print('Errors found:')
    for e in errors: print(f'  {e}')
    sys.exit(1)
print(f'Valid: {len(data)} apps, {len(known_cats & {a.get(\"category\") for a in data})} categories')
"
```

## When to Run

- After adding or removing apps from catalog.json
- After editing app fields (name, id, category changes)
- Before committing catalog changes
- When CatalogServiceTests fail unexpectedly

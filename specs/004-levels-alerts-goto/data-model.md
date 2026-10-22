# Data Model: Custom levels, custom alerts and go to time

## SessionConfig (existing, extended)

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `ErrorPattern` | string | `""` | regex, case-insensitive; empty = not used |
| `WarnPattern` | string | `""` | |
| `InfoPattern` | string | `""` | |
| `DebugPattern` | string | `""` | |
| `AlertPattern` | string | `""` | regex; new matching lines raise an alert |

- Validation: each non-empty field must be a valid regex (`SessionConfig.Validate`, localized message naming the field).
- JSON: camelCase (`errorPattern`, …); missing in older files → `""`. Included in export/import/duplicate (they go through `Clone` + JSON).
- Local files (`SessionConfig.Local`): all empty.

## LogLine (existing, extended)

- `IsAlert` (bool): line matched the session's alert pattern and is not history. Set at construction.

## LineRules (new, runtime only)

- Built from a `SessionConfig`; holds compiled regexes; not persisted.

## Go to time result

- `(int Index, GoToStatus Status)`, `GoToStatus` = `Found | BeforeFirst | NotFound | NoTimestamps`.

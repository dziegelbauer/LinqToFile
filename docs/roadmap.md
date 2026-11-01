# Roadmap

These features are not implemented.
This list records future work, not API availability or a delivery schedule.
Priority and design can change.

## File formats and headers

- Read additional tabular file formats.
- Read headerless CSV files.
- Extend workbook support to legacy `.xls`, binary `.xlsb`, and encrypted workbooks.

## Conversion and error handling

- Add custom converters and per-column converter annotations.
- Add exact date/time format annotations.
- Add configurable boolean tokens and explicit null tokens.
- Support platform-sized `nint` and `nuint` properties.
- Configure missing-column handling.
- Configure per-column error handling through annotations.
- Add opt-in raw field values in errors through a sensitive-data diagnostic option.

## Model mapping

- Support parameterized constructor mapping.
- Extend mapping to additional model shapes, including non-public and generic classes.

## Features to evaluate

These items need a concrete use case and a separate design decision:

- A public format-provider API.
- A custom `IQueryable` provider.

Use the [user guide](README.md) for implemented behavior.

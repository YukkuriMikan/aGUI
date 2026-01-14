# Coding Style and Conventions

## Comments
- Classes, Properties, Methods: Use XML comments (`/// <summary> ... </summary>`).
- If the comment fits on one line, keep the whole XML comment on a single line.
- Fields and Enum members: Use trailing single-line comments (`// ...`).
- Provide supplementary comments for complex logic to ensure readability.

## Attributes
- Fields with `[SerializeField]` must have a `[Tooltip]` attribute with a detailed description.
- Place the `[Tooltip]` attribute on a separate line from `[SerializeField]`.

## Layout
- Use `#region` to group related members (e.g., `#region Enums`, `#region SerializeField`, `#region Methods`).
- Do not insert empty lines between fields.

## Naming
- Maintain existing naming conventions (e.g., `a` prefix for class names).

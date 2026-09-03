# Localization (i18n)

Machine translations ship in `Resources/Strings.*.resx` for:

- `zh-CN` (default / authoritative source)
- `en`
- `ru`
- `ja`
- `de`

## For human translators

1. Edit or review `source-zh-CN.tsv` (key + Chinese text).
2. Provide translations for other languages against the same keys.
3. Maintainers merge into the matching `Strings.<culture>.resx` files.

Do not change keys without a code update. New UI strings must be added to:

- `source-zh-CN.tsv`
- every `Strings*.resx`
- `UiStrings` properties (if bound in XAML)

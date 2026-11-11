Strings are standard .NET `.resx` resources embedded by `Perenos.Presentation`.
`Resources.resx` is English; `Resources.ru.resx` is Russian. `Loc.ApplyLanguage`
updates the current and default thread cultures and notifies Avalonia bindings.
The tests check key parity and numeric placeholders between translations.

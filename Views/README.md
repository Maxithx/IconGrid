# Views layout notes

## TemplatePage (Views/TemplatePage.xaml)
- Provides the shared hero + card column that sits in the content host next to the sidebar.
- Hero content is injected via the `HeroContent` dependency property and rendered inside a `StartsideSectionCardStyle` border.
- Additional cards stack under the hero by setting the `CardsContent` property. Each card should also use `StartsideSectionCardStyle` so the built-in margin (`0,0,0,5`) keeps the spacing identical to the Startside shell.
- The `TemplateContentWidthConverter` (declared inside `TemplatePage` resources) keeps the card column centered, limits its width to ~1280px, reserves horizontal gutters, and automatically shrinks when the window isn’t maximized so every hero/card layout stays aligned with the Startside design.
- Placeholders, loaders, or future sections can host their own `StackPanel` or `ItemsControl` inside `CardsContent` without redefining the shell styling.

## Startside styles & spacing
- `Views/StartsideStyles.xaml` defines the key brushes and styles: `StartsideCardFill`, `StartsideLightCardFill`, `StartsideSectionCardStyle`, `ToggleSwitchStyle`, `ModernSliderStyle`, etc.
- `StartsideSectionCardStyle` sets the visual spacing between cards. Always reuse this style rather than applying new margins so every page lines up with the Startside look.
- Accent colors and the slider/toggle templates live alongside these styles so every page has a single source of truth for the palette.

## Sidebar navigation (SettingsWindow.xaml)
- The sidebar lives in `SettingsWindow`, uses `NavButtonStyle`, and controls selection by tagging the currently active button with `Tag="Selected"`.
- New pages simply call `ShowPage(new YourPage(), yourNavButton)` so the `SettingsContentHost` swaps in the template-powered card column.
- Keep the sidebar buttons in a single column and avoid duplicating the navigation UI inside each page: the TemplatePage handles the card panel only.

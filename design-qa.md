# Native DC WALLET design QA

final result: passed

Scope: visual adaptation of the supplied MinterWallet screenshots to the existing native Avalonia wallet. This is a C# application, not a browser prototype. Source-selected structure, light-theme spacing and cards are retained with the supplied DC WALLET brand assets. The requested dark theme uses the same components and a separate semantic palette.

## Evidence

- Reference: user-supplied MinterWallet screenshots, especially welcome `codex-clipboard-f5a0ec67-c200-4187-95af-ca325ceb0f42.png`, send `codex-clipboard-4903da09-d113-4c37-9586-9c5c5806466d.png`, password `codex-clipboard-4d96d83c-d03f-4aea-ace7-54a42246d6d0.png`, overview `codex-clipboard-97d49b79-498e-4716-b393-532a8c288b69.png`.
- Rendered implementation: `artifacts/screenshots/light-*` and `dark-*`, captured from the real Avalonia tree at 1100 x 720 logical pixels, headless scale 1.
- Combined comparison inputs: `artifacts/design-qa/welcome-comparison.png`, `send-comparison.png`, `password-comparison.png`, `overview-comparison.png`. References have the 28 px native title bar removed; images are otherwise not rescaled in the saved comparisons. Source widths differ by 0-7 px from the implementation. No source account phrases are copied into the implementation.
- Interaction evidence: 6 UI scenarios pass, including both themes, theme preference reload, watch-only rejection, creation, QR, validation errors, quote/confirmation separation and no automatic retry. RPC writes in tests go only to a local fixture server.

## Iterations

1. Initial implementation had a dark technical-console layout, oversized navigation buttons and exposed SDK parameters. This was a P1 mismatch with the supplied product direction. Replaced with centered onboarding, compact sidebar, dedicated wallet screens, native cards and typography; SDK tools are a separate section.
2. First visual pass found P2 navigation clipping and a separate row for the send-all action. Reduced sidebar spacing and moved the action beside the amount field. Recaptured both themes: all primary navigation entries and lock control are visible at the target viewport.
3. Theme pass found P2 contrast risk from white text over a light accent in dark mode. Primary actions now retain an accessible solid blue, while text, borders, cards, selected navigation and error colors have separate theme values. Rechecked dark send, overview, settings, errors and confirmation.
4. Added password visibility controls, proper field grouping and a card-based fee/recipient confirmation with technical details collapsed. Re-ran the UI scenarios and captured revised states.

## Required fidelity surfaces

- Fonts/typography: Segoe UI with platform fallback; 24 px section titles, 14 px body, 12-13 px secondary text. Headers are semibold, body text normal. No clipped labels in the checked states.
- Spacing/layout: 236 px sidebar, centered 640 px transfer form, 600 px wizard, 800 px overview; consistent 34 px inputs and small outlined buttons. Card radii 10 px, borders 1 px. Long settings and tool forms scroll without moving the sidebar footer.
- Colors/tokens: light canvas #F4F6FA, white cards, muted text #63728B; dark canvas #101722, cards #1B2636, muted text #A0B0C9. Blue primary actions, theme-specific selection/background/error values. Native focus and disabled states are retained.
- Images/icons: original DC WALLET PNG and existing icon geometry. No generated replacements, rasterized UI, invented balances or source Minter logos.
- Copy/content: Russian customer-facing copy and Decimal terminology. Minter-only analytics, bridge rules, gas-token settings and transaction assumptions are not copied. Unsupported private-key import is not presented as a working action. Network operations that remain SDK alpha are documented separately.

## Alpha.2 regression correction

The user's live screenshots exposed an untested light/dark -> system transition: the palette used the previous app theme while native controls used the OS theme. System mode now reads PlatformSettings directly and applies the same effective variant to the window, application and palette. Checkbox text explicitly uses the semantic foreground. Native OS color notifications keep system mode synchronized.

Settings also lacked an exit before unlock. A return button now sits outside the scrolling content and returns to unlock or overview as appropriate; Escape performs the same action. Regression screenshots `system-light-to-Dark.png` and `system-dark-to-Light.png` were inspected: both checkbox captions remain readable and the return control is visible at the bottom. Tests verify text/background contrast >= 4.5, both OS theme notifications, navigation while scrolled and return from unlocked settings. Total checks: 13 core + 7 UI.

## Cryptopunk theme

The user requested a third theme based on the existing MintCandy console. Its source tokens were read from `console/src/styles.css` rules for `body[data-theme="neon"]`: #070711 canvas, #0B0B1E panels, #19CCEF actions, #06101C action text, #7136D8 selection and #29DCFF accent. The existing DC WALLET brand/logo and native structure are retained. A low-opacity cyan card shadow supplies the reference's neon accent without decorating input text.

Rendered `neon-overview.png`, `neon-Отправить.png`, `neon-password.png`, `neon-preview.png` and `neon-persisted-settings.png` were inspected alongside the other themes. Tests cover all three theme palettes and explicit Neon persistence across a new window. System mode remains a fourth selection mode, not a fourth skin. Total checks: 13 core + 8 UI.

## Intentional differences and remaining gaps

- Minter addresses and BIP labels are replaced with Decimal EVM addresses and DEL. The source connection outage is not fabricated: the wallet shows an untested connection until a real check succeeds.
- The source analytics paragraph is omitted because this app does not collect those analytics. Its private-key tile is replaced by the implemented encrypted-backup restore action.
- Wizard steps share a stable top alignment rather than varying vertical centering as the source content changes. Password visibility uses a labeled native action.
- The dark theme has no supplied dark screenshot to clone; it is checked against the same component structure and contrast requirements.
- P3: native expander animation can be captured mid-transition by headless screenshots; final interaction is functional. Platform font rendering will vary on Linux/macOS.
- This visual pass does not certify complete network transaction coverage, signing-key memory erasure, production security, native tray behavior or execution on Linux/macOS/ARM64. Those retain the limitations in docs/CAPABILITIES.md.

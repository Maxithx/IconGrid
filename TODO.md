# IconGrid TODO

## Gaming overlay icons (pending)

- [ ] Create icons for collapsible section headers on Gaming Overlay settings page:
      `Gaming overlay`, `Automatisk under spil` (Automatic while in game),
      `Overlay størrelse` (Overlay scale), `FPS setup status`, `Spilopløsning` (Game resolution)
- [ ] Add icon files to `figma-icons/` and register in `IconGrid.csproj`
- [ ] Wire icons into expander headers in `GamingOverlayPage.xaml`

## System guide (planned)

- [ ] Create a comprehensive system guide documenting the full IconGrid architecture:
      launcher, gaming overlay, FPS/ETW pipeline, game resolution switching,
      hardware monitor, settings persistence, auto-behavior during gameplay

## Local voice AI — IconGrid Voice Agent (planned, 3-4 sessions)

### Arkitektur
- Whisper.cpp native agent (`Native/VoiceAgent/`) spejler `FpsAgent` mønsteret
- WASAPI mic capture + push-to-talk (hotkey)
- stdout IPC: `TRANSCRIPT:tekst` linjer → C# `VoiceController.cs` læser async
- `VoiceCommandParser.cs` — fuzzy-match mod `MainViewModel.Items` + external games
- Ingen internet, ingen cloud, 100% offline

### Sessioner
- [ ] **Fase 1** — Native VoiceAgent projekt (.vcxproj) + Whisper.cpp/ggml build + WASAPI capture + stdout transskription + model download workflow (~150 MB ggml-tiny.bin)
- [ ] **Fase 2** — C# `Helpers/Voice/VoiceController.cs` (spejler `HardwareMonitorAgent.cs` mønster) + `VoiceCommandParser.cs` (fuzzy-match) + system commands (`shutdown`, `sleep`, `restart`)
- [ ] **Fase 3** — Launcher integration: start/luk spil via stemme, push-to-talk hotkey, mikrofon-knap i top-bar, visuel feedback (optager=rød, transskriberer=gul, klar=grøn)
- [ ] **Fase 4** — Settings-side (Voice model download status, language da/en, hotkey), README opdatering, polish

### Stemme kommandoer (planned)
| Kommando | Action |
|----------|--------|
| "åbn {spil}" / "start {spil}" / "launch {spil}" | Fuzzy-match mod alle genveje + external games → launch |
| "luk {spil}" / "afslut {spil}" / "stop {spil}" | Luk spilproces |
| "luk computeren" / "shut down" | `shutdown /s /t 0` |
| "slumre" / "sleep" / "dvale" | `SetSuspendState()` |
| "genstart" / "restart" | `shutdown /r /t 0` |
| "åbn overlay" / "vis overlay" / "skjul overlay" | Gaming overlay toggle |
| "indstillinger" / "settings" | Åbn settings window |
| "skjul launcher" / "vis launcher" | Launcher hide/show |

## Local voice AI — Cline Voice Integration (planned, separate feature)

### Koncept
To-vejs stemme-samtale via to open source engine:
- **Whisper.cpp** (MIT) → Speech-to-Text: tal → tekst i Cline prompt felt
- **Piper TTS** (MIT) → Text-to-Speech: Cline svar → stemme

### Arkitektur
```
Dig (taler) → Whisper.cpp → tekst → Cline prompt felt → Enter
                                            │
                                    Cline svarer (normal chat)
                                            │
                                    VoiceResponseParser:
                                    1. Fjern kode-blokke, filnavne, stier
                                    2. Behold kun naturligt sprog
                                    3. Langt svar? → opsummér first 2-3 sætninger
                                            │
                                    Piper TTS → lyd → Du hører svaret
```

### Open source komponenter
| Komponent | Licens | Størrelse |
|-----------|--------|-----------|
| Whisper.cpp (STT) | MIT | ~150 MB (tiny model) |
| Piper TTS (text-to-speech) | MIT | ~50 MB per stemme (da_DK + en_US) |
| ggml (inference engine) | MIT | header-only |

### Sessioner
- [ ] **Fase C1** — Cline MCP tool: `voice_to_prompt` — push-to-talk → Whisper.cpp transskriber → append tekst til Cline prompt (IKKE auto-send)
- [ ] **Fase C2** — Global hotkey (system-tray agent) der virker uanset hvilket vindue der er aktivt
- [ ] **Fase C3** — TTS svar: Piper TTS integration + `VoiceResponseParser` (strip kode/filstier → kun naturligt sprog til tale, fuld detaljer i chatten)
- [ ] **Fase C4** — Stemme-redigering: "slet ord", "slet linje", "ryd alt" (kræver VSCode cursor tracking — kompleks, senere fase)

# Guide til IconGrid's Agent-hukommelsessystem

> Skrevet til nye samarbejdspartnere (menneskelige og AI'er) der skal arbejde på IconGrid-projektet med VS Code + Cline.

---

## 1. Oversigt over systemet

IconGrid bruger et flerlaget hukommelsessystem så AI-agenten (Cline) kan huske hvad der er sket mellem sessioner:

```
Indgang →  AGENT.md             "Læs dette først — her er reglerne"
           ↓
Session →  CHAT_STATE.md        "Hvad skete der sidst, hvad skal ske nu?"
           ↓
Teknisk →  .local-state/*.md    "Detaljerede tekniske noter (gitignored)"
           ↓
Værktøj →  MCP Notes Server     "Læs/skriv/søg i noter uden at røre filerne manuelt"
           ↓
Grænser → ARCHITECTURE_RULES.md "Hvor store må filer/metoder være?"
```

**Hvorfor dette findes:** Cline's indbyggede hukommelse er begrænset til én chat-session. Når du åbner en ny chat, starter den fra nul. Dette system giver agenten "persistent memory" ved at gemme vigtig information i filer.

---

## 2. AGENT.md — Indgangsfilen

`AGENT.md` er den FØRSTE fil en AI-agent skal læse når den starter i repository'et.

**Hvad den gør:**
- Fortæller agenten i hvilken rækkefølge den skal læse filer (CHAT_STATE → README → ARCHITECTURE_RULES)
- Beskriver commit-regler (aldrig commit uden brugerens godkendelse)
- Beskriver deploy-proceduren (build → kopiér HELE build-output → verificér timestamps)
- Fortæller om MCP Notes Server og hvordan man bruger den
- Indeholder session-hukommelses-ritualet (hvad man gør når en session slutter)

**Du skal IKKE ændre AGENT.md** uden at spørge først — den er stabil og sjældent opdateret.

---

## 3. CHAT_STATE.md — Sessionens hukommelse

Dette er den vigtigste fil for dagligt arbejde. Den lever i repository-roden og er versioneret med git.

### Struktur

```markdown
## Current focus
[1-2 linjer om hvad vi arbejder på lige nu]

## Session findings (YYYY-MM-DD)
[Hvad vi fandt ud af, rettelser, åbne problemer]

### Sub-overskrift for specifikt emne
- Punkt 1
- Punkt 2

## Good next steps
[Hvad skal gøres i næste session]
```

### Sådan bruger du den

1. **I starten af hver session:** Læs `CHAT_STATE.md` for at se hvad der er sket, og hvad der skal gøres.
2. **Under sessionen:** Brug `append_to_note` MCP-værktøjet til at tilføje findings — skriv IKKE direkte i filen.
3. **I slutningen af sessionen:** Opdatér `Good next steps` og evt. `Current focus`.

### Eksempel — tilføj en finding

```
MCP: append_to_note
  note: CHAT_STATE
  heading: Session findings (2026-01-01)
  content: - Fixed overlay position bug. Root cause was...
```

---

## 4. .local-state/ — Tekniske referencer

Mappen `.local-state/` indeholder tekniske noter der **ikke** er versioneret med git (de er i `.gitignore`).

### Hvornår opretter man en .local-state note?

Når du har detaljeret teknisk information der er for lang/kompleks til CHAT_STATE, f.eks.:
- Performance benchmarks
- Log-uddrag
- Detaljerede fejlanalyser
- Test-procedurer

### Navngivning

Brug korte, sigende navne:
- `gaming-overlay.md` — alt om gaming overlay
- `fps-etw.md` — FPS-målinger via ETW
- `scale-slider-fix.md` — specifik fix-dokumentation

### Eksempel — opret en note

```
MCP: append_to_note
  note: fps-etw
  heading: Measurements 2026-01-01
  content: | 
    Resolution: 1920x1080
    Avg FPS: 144.3
    Min FPS: 89.1
```

---

## 5. MCP Notes Server — Værktøjet

MCP (Model Context Protocol) serveren giver agenten værktøjer til at læse, skrive, søge og validere noter.

### Opsætning (første gang)

```bash
cd tools/mcp-notes-server
npm install
```

Derefter registrér serveren i Cline's MCP configuration:
```json
"icongrid-notes": {
  "command": "node",
  "args": ["E:/IconGrid-GitHub/tools/mcp-notes-server/src/index.js"],
  "env": { "ICONGRID_WORKSPACE": "E:\\IconGrid-GitHub" },
  "disabled": false,
  "autoApprove": []
}
```

### Vigtigste værktøjer

| Værktøj | Brug |
|---------|------|
| `read_note` | Læs en note (CHAT_STATE eller .local-state) |
| `append_to_note` | Tilføj indhold under en overskrift |
| `replace_in_note` | Find og erstat præcis tekst |
| `search_notes` | Søg i alle noter efter et mønster |
| `check_architecture_rules` | Tjek at filer overholder ARCHITECTURE_RULES |
| `check_version_consistency` | Tjek at version er synkroniseret mellem AssemblyInfo.cs og README.md |
| `list_notes` | List alle tilgængelige noter |

### Eksempel — søg i noter

```
MCP: search_notes
  pattern: overlay position
```

Returnerer alle linjer i alle noter der indeholder "overlay position".

### Eksempel — erstat tekst

```
MCP: replace_in_note
  note: CHAT_STATE
  find: Good next steps
  content: Good next steps
  - Fix transparency bug
  - Test in fullscreen games
```

---

## 6. ARCHITECTURE_RULES.md — Kode-grænser

Definerer maksimale filstørrelser og metodegrænser for at holde kodebasen modulær.

### Brug

```bash
# Kør før store ændringer og ved session-slut:
MCP: check_architecture_rules
```

Hvis den rapporterer `VIOLATION`, skal det fixes — det er ikke valgfrit.

### Typiske grænser

- Filstørrelse: maks 500-800 linjer per `.cs` fil
- Metode-længde: maks 30-50 linjer
- Kompleksitet: klasser med for mange metoder skal splittes op

---

## 7. Hverdagsworkflow — en typisk session

```
1. Åbn VS Code + Cline
2. Agenten læser automatisk AGENT.md (via .clinerules)
3. Agenten læser CHAT_STATE.md → ser "Good next steps"
4. Du beskriver opgaven
5. Agenten arbejder (build → deploy → du tester)
6. Agenten opdaterer CHAT_STATE.md løbende med findings
7. Ved session-slut:
   a. Kør check_architecture_rules
   b. Opdatér CHAT_STATE.md med session-fundings
   c. Opdatér "Good next steps"
   d. Spørg om commit (KUN hvis du godkender det)
```

---

## 8. Konkrete eksempler

### Eksempel A: Start en ny session om gaming overlay

```
1. Agent læser CHAT_STATE.md
2. Ser: "Good next steps: Fix overlay position at 150% scale"
3. Agent læser .local-state/gaming-overlay.md for tekniske detaljer
4. Går i gang med at debugge
```

### Eksempel B: Gem en vigtig teknisk observation

```
MCP: append_to_note
  note: fps-etw
  heading: Root cause found
  content: |
    The FPS counter dropped because ETW session was not properly disposed.
    Fix: added using() block around TraceEventSession in FpsMonitor.cs line 42.
```

### Eksempel C: Session-slut ritual

```
1. MCP: check_architecture_rules → alle filer OK
2. MCP: append_to_note CHAT_STATE → "Session findings (dagens dato)"
   - Fixed overlay position at 150% scale
   - Deployed to C:\IconGrid, user confirmed fix
3. MCP: replace_in_note CHAT_STATE → opdatér "Good next steps"
   - Investigate transparency bug on desktop
4. Du: "commit CHAT_STATE"
5. Agent: `git commit -m "session: fix overlay position + update notes"`
```

---

## Hurtig reference — MCP kommandoer

| Handling | MCP kald |
|----------|----------|
| Læs hvad der skete sidst | `read_note CHAT_STATE` |
| Tilføj en ny finding | `append_to_note CHAT_STATE "heading" "content"` |
| Find noget i noter | `search_notes "søgeord"` |
| Læs teknisk note | `read_note gaming-overlay` (uden .md) |
| Opdatér en note | `replace_in_note CHAT_STATE "find" "ny tekst"` |
| Tjek kode-regler | `check_architecture_rules` |
| Tjek version | `check_version_consistency` |
| List alle noter | `list_notes` |

---

*Sidst opdateret: 2026-08-06*
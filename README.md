# Garage Tycoon

A mobile idle/tycoon game built in Unity. You run a car garage: vehicles roll in needing repairs,
and instead of tapping the same button over and over, **every repair job is a different interactive
mini-game**. Serve customers before they lose patience, spend the takings on upgrades, hire mechanics
who keep working while the app is closed, and eventually sell the whole garage for permanent bonuses.

---

## Getting it running

1. Install **Unity 2022.3 LTS** (this project is pinned to `2022.3.62f1` in
   `ProjectSettings/ProjectVersion.txt` — any 2022.3.x will open it fine).
2. Open this folder as a Unity project.
3. Open `Assets/Scenes/Garage.unity` and press **Play**.

That's it. There is nothing to drag into the scene and no assets to import: the scene contains a
camera and a single `GameBootstrap` component, which builds the whole game at runtime.

**If the scene ever fails to open or looks empty**, use the menu **Garage Tycoon → Create Garage
Scene**. It rebuilds a working scene from scratch.

### Building for Android

Run **Garage Tycoon → Configure Android Player Settings** once. It sets portrait orientation, the
bundle id, IL2CPP with ARM64 (required by Google Play) and a minimum SDK of 23. Then use
**File → Build Settings → Android**.

---

## How the game plays

| | |
|---|---|
| **Cars arrive** | 10 car types across 5 rarity tiers, from a Rusty Ute to a Concours Prototype. Each arrives with 2–4 repair jobs and a patience timer. |
| **Each job is a mini-game** | Which one depends on the job — wheel work leans towards torque, electrics towards memory — with enough randomness that back-to-back jobs feel different. |
| **Finish before they leave** | Run out of patience and the customer drives off. Jobs already finished are still paid; the rest of the money is gone. |
| **Spend the takings** | 11 upgrades across 4 branches. |
| **Sell up** | Once you bank $150,000 you can sell the garage for Reputation Tokens, which permanently boost every future payout. |

### The four mini-games

1. **Timing bar** — a marker sweeps across a bar; tap inside the green zone. The gold core in the
   middle is a PERFECT and pays a cash bonus. Miss entirely and you lose a chunk of the customer's patience.
2. **Tool matching** — a job is announced ("Torque the head bolts") and the tools flash up briefly.
   Once the labels hide you have to remember which button held the right one. Grabbing the wrong
   tool *damages* the part: you lose progress as well as time.
3. **Hold and release** — hold to wind torque into the gauge and let go inside the green band.
   Let go early and the bolt is loose; hold past the redline and you strip the thread.
4. **Rapid sequence** — a short pattern of directions flashes up, then hides. Repeat it from memory.
   One wrong input ends the round.

Every mini-game gets harder on rarer cars (smaller windows, faster markers, longer patterns) and
easier as you buy Precision upgrades.

### The upgrade branches

| Branch | What it does |
|---|---|
| **Precision** | Makes the mini-games more forgiving: bigger windows, longer previews, slower gauges. |
| **Automation** | Hires mechanics who play the mini-games themselves — including while the app is closed. |
| **Reputation** | Attracts rarer, richer cars, more often, with more patient owners. |
| **Workshop** | Grows the business: more repair bays, higher labour rates. |

Hired mechanics are deliberately capped **below** a good human player (0.72 skill at 0.91× pace).
Idle income sits at roughly 78% of playing by hand — worth having, never better than actually playing.

---

## How the project is organised

```
Assets/
  Scenes/Garage.unity        The only scene. A camera plus one GameBootstrap object.
  Scripts/
    Core/                    THE GAME. Pure C#, zero UnityEngine references.
      Balance/               Every tunable number, in one file.
      Cars/                  Car blueprints, repair jobs, the spawner.
      Minigames/             The four mini-games + the auto-player that can play them.
      Economy/               Wallet, upgrades, prestige.
      Events/                Random events (VIP Weekend, Parts Shortage...).
      Simulation/            The game loop that ties it all together.
      Save/                  A dependency-free JSON writer/parser and save serialisation.
      Util/                  Seeded random number generation and small maths helpers.
    Unity/                   THE PRESENTATION. MonoBehaviours and UI, built at runtime.
      UI/                    Theme, generated sprites, widgets, screens.
      Minigames/             The on-screen half of each mini-game.
      Platform/              Save file I/O and GameBootstrap.
    Editor/                  Editor-only menu tools.
  Tests/EditMode/            NUnit tests for the Unity Test Runner.
  Prefabs/, UI/              See the notes in those folders - the UI is generated in code.
Tools/
  HeadlessTests/             The full automated test suite. Runs without Unity.
  CompileCheck/              Compiles the Unity scripts against API stubs, without Unity.
```

### The one idea worth understanding

**All of the game's rules live in `Assets/Scripts/Core`, and that folder cannot use Unity at all.**
Its assembly definition sets `"noEngineReferences": true`, so the compiler physically refuses to let
`UnityEngine` in.

That sounds like a restriction; it is actually the most useful thing in the project. Because the
rules are plain C#:

- The whole game can be played **thousands of times over, in seconds, with no editor open**. That is
  how the balance in this repo was set — measured, not guessed.
- A mini-game is a small state machine you can read top to bottom. `TimingBarMinigame` is about 100
  lines and contains every rule about timing bars. The Unity class that draws it contains none.
- Nothing about the game can break because of a missing prefab reference or a component that got
  unhooked in the inspector.

The Unity layer's only jobs are: draw what Core says, and forward touches back into it.

---

## Running the tests

**The full suite (88 tests, about seven seconds, no Unity needed):**

```bash
dotnet run --project Tools/HeadlessTests
```

It covers the mini-game rules, car spawning, the economy, save/load round-trips, a pile of edge
cases (a car timing out mid-repair, a broke player, corrupt save files, ten-second frame spikes)
and a measured balance pass. A non-zero exit code means something regressed.

Filter to one area by name:

```bash
dotnet run --project Tools/HeadlessTests -- balance
```

**The balance probe** — measurements rather than pass/fail, for when you change a number and want to
see what it did:

```bash
dotnet run --project Tools/HeadlessTests -- probe
```

**Compile-check the Unity scripts without opening Unity:**

```bash
dotnet build Tools/CompileCheck
```

This builds `Assets/Scripts/Unity` and `Assets/Scripts/Editor` against small hand-written UnityEngine
stubs. It catches typos, wrong signatures and missing `using` statements. It is not a substitute for
the editor — the stubs only model the API this game actually uses — but it turns a five-minute
"open Unity and wait for a domain reload" loop into a two-second one.

**Inside Unity:** Window → General → Test Runner → EditMode → Run All.

---

## Tuning the game

Start in **`Assets/Scripts/Core/Balance/GameBalance.cs`**. Everything about pacing lives there:
payouts, patience, progress per round, penalties, the prestige cap, offline limits.

Upgrade prices and effects are in **`Assets/Scripts/Core/Economy/UpgradeCatalog.cs`**, and car
values in **`Assets/Scripts/Core/Cars/CarCatalog.cs`**. Adding a new car is one entry in that list —
nothing else needs to change.

After any change, run the probe to see the effect, then the test suite to check nothing broke.

Current measured pacing (from the probe, at 80–85% skill):

| | |
|---|---|
| Opening income | ~$550–770 per minute |
| First upgrade | about 17 seconds in |
| Half an hour | ~68 cars, ~28 upgrade levels, 2 bays |
| Shop maxed out | around 2 hours |
| First prestige | around 3 hours, awarding 4 tokens (+48% permanently) |
| Idle vs playing | idle earns ~78% of hands-on play |

---

## Notes and known limitations

- **The UI is built entirely in code**, with sprites generated at runtime (`UISprites.cs`). There are
  no art assets to import and no prefabs to wire up. If you would rather build the UI by hand in the
  editor later, the screens in `Assets/Scripts/Unity/UI` are the place to start replacing.
- **Text uses Unity's built-in font** (`LegacyRuntime.ttf`) and the legacy `UnityEngine.UI.Text`
  rather than TextMeshPro, deliberately: TMP needs an "import essentials" step before it will render
  anything, and this project is meant to run the moment you press Play. Moving to TMP later is a
  worthwhile polish step.
- **Input uses the legacy Input Manager** (`StandaloneInputModule`), so there is no dependency on the
  new Input System package. Touch works on device; mouse works in the editor.
- **No audio yet.** There is no sound or music, which is the most obvious thing missing for a
  shippable mobile game.
- See `CHANGELOG.md` for what was built when, and for the open questions worth a decision.

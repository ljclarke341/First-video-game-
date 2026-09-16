# Changelog

Progress log for Garage Tycoon, newest first. Anything that needs **your** decision is flagged
under "Open questions for you" at the bottom — nothing there is blocking, they are judgement calls
I made a reasonable choice on and would rather you confirmed.

---

## Stage 6 — Playtest fixes (the first round of real human feedback)

A person played it and reported three things no automated test could ever have caught:
*"you have to like guess"*, *"it moves a bit too fast I can't even read the question"*, and that
holding the torque button highlighted the word on it. All three were real, and chasing them down
turned up four further balance faults underneath.

**Readability — the actual complaint**

- Tool matching previews were `1.5 / difficulty` seconds, so a rare car gave about a second to read
  a job prompt *and* scan up to five tool names. That is not a memory test, it is a coin flip. Now
  `2.6 / sqrt(difficulty)` with a 1.5s floor — roughly double, and it no longer collapses on hard cars.
- **The job prompt now stays on screen for the whole round.** Only the tool labels hide. You are
  being tested on which tool you grabbed, not on whether you read the question before it vanished.
- Tool buttons are numbered, so you can remember "it was 3" instead of holding a position in mind.
- Sequence steps flashed for as little as a sixth of a second. Now around half a second each, with a
  beat at the end before the pattern hides.
- **The sequence pattern is drawn as arrows rather than the words UP/RIGHT/DOWN/LEFT.** A glance
  reads an arrow far faster than a word, which is the entire point of something shown for a moment.
- Added a **Relaxed pace** setting (in Stats): more reading time, slower markers, identical payouts.
  Reading speed is personal, and a mini-game nobody can read is not a difficulty setting.
- Web build: holding any button no longer selects its text — the bug reported on the torque game,
  where holding is mandatory, so it fired on every single round.

**Four balance faults found while fixing the above**

Making rounds readable made them longer, and that broke things tuned against the old fast rounds:

- **Customers timed out mid-repair**, quietly turning the whole Reputation branch into a trap.
  Patience raised 30% (`GameBalance.PatienceScale`).
- **The patience clock ran while you were reading.** Every second spent reading cost the customer's
  goodwill, which made "Labelled Tool Wall" — an upgrade whose entire purpose is buying reading
  time — lose 39% of income. The clock now eases off during a preview phase.
- **Extra Bay made you poorer.** A car in a bay lost patience faster than one in the queue, so a new
  bay pulled cars out of the forgiving queue into a harsher one where, with a single pair of hands,
  they sat and rotted. One rate now covers every car nobody is touching, wherever it is parked.
- **The finishing tip was half the economy.** A flat $1.50 per second remaining was worth more than
  the entire repair on a cheap ute and pocket change on a supercar — and because it only rewarded
  reaching a car instantly, it punished owning bays. It is now 25% of the car's payout, scaled by
  how much patience survived **from when work began**, so it rewards a fast repair rather than a
  lucky arrival.

Every upgrade is now measurably worth buying in isolation, where two were previously negative.

**Two faults in the tests themselves**

- The virtual player's accuracy did not depend on preview length at all, so longer previews were
  pure cost in simulation — true of a robot, false of a person. It now models reading time, which is
  what exposed the patience-during-preview fault above.
- The virtual player picked the most *urgent* car. Cheap cars have the shortest patience, so that
  quietly prioritised rusty utes over supercars and made extra bays measure as a 30% income loss.
  It now picks by money at risk. A second test was comparing a single four-minute run and calling
  the noise a result; it averages seeds now, the same lesson already learned once in Stage 2.

Measured after all of it: ~$430/min opening income, first upgrade at 19s, first prestige at ~195
minutes for 4 tokens, idle at 72% of hands-on play. All 88 tests pass.

---

## Stage 5 — Polish and a second bug pass

**Polish**

- Progress bars now slide to their target instead of snapping. The patience timer, repair progress
  and the prestige bar all animate; the torque gauge deliberately does not, because on that one the
  exact needle position *is* the game.
- Finishing a job flashes the bay card it came from, so your eye is drawn to the right car even when
  you are looking down at the workbench. Completed cars flash gold or green, lost customers red.
- Popups fade and scale in over 150ms rather than appearing instantly.

**The garage pauses while a full-screen panel is open.** Previously a customer could run out of
patience while you were reading the upgrade list. Losing a car to a menu you cannot see past teaches
players not to open the menu, which defeats the point of having one. Nothing is exploitable by it:
this is a single player game, and idle income is paid from elapsed time rather than frames.

**Three bugs found while reviewing the Unity layer**

- **Destroyed sprites after a second Play session.** The generated-sprite cache is static, so it can
  outlive the sprites it holds (leaving play mode, or a domain reload with Fast Enter Play Mode on).
  A destroyed Unity object is not the same as a missing dictionary entry, so the cache would happily
  hand out already-destroyed sprites on the next run. Lookups now check validity and rebuild.
- **Offline income could be paid into a brand new game.** With "load save on start" turned off but a
  save still on disk, the offline calculation read that stale timestamp and credited the fresh garage
  with hours of the old garage's idle income. Offline progress now only applies when a save was
  actually restored.
- **Every floating message would have appeared off-screen.** A freshly created `RectTransform`
  anchors to its parent's bottom-left corner, not its centre — so the toast positions, which are all
  written as offsets from the middle of the screen, would have placed them just off the bottom-left.
  The UI factory now sets explicit centre anchors, so nothing it builds depends on that default.
- Also fixed while wiring the card flashes: cars are removed from their bay *before* the
  completed/left-angry events fire, so a live bay lookup would have found nothing at exactly the two
  moments most worth flashing. The lookup now falls back to the car's last bay index.

---

## Stage 4 — Documentation and project wiring

- `README.md` covering how to open and play the project, how everything is organised, how to run the
  three kinds of test, and the measured balance figures.
- Assembly definitions: `GarageTycoon.Core` (with `noEngineReferences: true`, which makes the
  "Core cannot use Unity" rule a compiler error rather than a good intention), `GarageTycoon.Unity`,
  `GarageTycoon.Editor`, `GarageTycoon.Tests`.
- `Assets/Scenes/Garage.unity` — camera plus a single `GameBootstrap` object, registered in Build Settings.
- Editor menu (**Garage Tycoon** at the top of the Unity window): rebuild the scene, delete the save,
  show where the save lives, configure Android player settings, and print a balance report.
- EditMode tests for the Unity Test Runner, so the editor's own test list is not empty.
- `Packages/manifest.json` pinned to uGUI and the Test Framework — no other package dependencies.

## Stage 3 — The Unity layer

- **Everything is built in code.** No prefabs, no imported art, no fonts to install. Press Play and
  the game exists.
- `UISprites.cs` generates every sprite at runtime: 9-sliced rounded rectangles, circles, rings, the
  garage backdrop (wall/floor gradient with a painted bay stripe) and the car silhouette.
- `Theme.cs` holds every colour, size and spacing value in one place.
- Screens: the garage (money HUD, event banner, bay cards, the queue outside, the workbench),
  the upgrade shop (branch tabs, scrollable list, live affordability), and reusable popups for the
  stats screen, the prestige confirmation and the welcome-back report.
- Each mini-game has its own view that draws the Core state and forwards touches back. The hold-and-
  release game uses a custom `PointerButton`, because Unity's standard Button cannot report the
  exact moment a finger lifts — and dragging off the button counts as a release, so a stray finger
  can never leave the gauge stuck at the redline.
- Pooled floating text for feedback ("PERFECT!", "+$120", "Customer left!") — pooled rather than
  allocated per tap, because per-tap allocation is exactly what makes a phone stutter.
- Saving: written to a temp file and moved into place, so being killed mid-write keeps the old save
  intact. Saves on pause, on focus loss, on quit and every 20 seconds. Offline progress is worked
  out from a UTC timestamp, so changing time zone cannot rewind or fast-forward your garage.

### Compile-checking without Unity

`Tools/CompileCheck` compiles the Unity-facing scripts against small hand-written UnityEngine stubs.
It catches typos, wrong signatures and missing usings in seconds instead of waiting on a domain
reload — and it means the Unity scripts are verified even on a machine with no Unity installed.
The stubs live outside `Assets/`, so Unity never sees them, and the real editor remains the source
of truth for anything they do not model.

## Stage 2 — Automated testing and the balance pass

88 automated tests in `Tools/HeadlessTests`, covering mini-game rules, spawning, the economy,
save/load round-trips, edge cases and balance. They run in about seven seconds without Unity.

The virtual player that drives the tests is the **same** `MinigameAutoPlayer` the hired mechanics
use in-game, so the idle and hands-on paths can never quietly drift apart in balance.

**Problems the tests found, and what was done about them:**

- **Fully trained mechanics out-earned playing by hand.** The optimal strategy was to put the phone
  down, which would have killed the game. Mechanic skill and speed are now capped below a good
  player (0.72 skill at 0.91× pace); idle income now sits at ~78% of hands-on play.
- **Upgrade costs grew too slowly.** Half an hour of play bought most of the shop. Cost growth is now
  1.7×–2.4× per level, and half an hour buys about 28 of 67 levels.
- **A finished car could occupy a bay forever.** If a car's jobs were completed outside the normal
  round-resolution path — a restored save, or any future instant-finish effect — nothing retired it
  and the bay was blocked permanently. There is now a retirement pass every tick.
- **The waiting queue was a meat grinder.** Cars outside lost patience fast enough that buying
  "more cars arrive" upgrades was a trap. Queued cars now lose patience at 22% of the normal rate:
  the queue is a buffer, and the clock only gets real once the car is on the ramp.
- **The prestige cap was set by guesswork and was wrong twice.** See below.

**The prestige cap, measured rather than guessed.** Tracing a full session showed the shop is
effectively bought out around the two hour mark, after which there is nothing to spend on. The cap
now sits at $150,000, which lands the first prestige at about three hours and awards four tokens
(+48% on every future payout). An earlier value of $1,000,000 left roughly three hours of dead time
with an empty shop; the original $250,000 let a player prestige repeatedly inside two hours.

**Measured pacing now** (80–85% skill, from `-- probe`):

| | |
|---|---|
| Opening income | ~$550–770 per minute |
| First upgrade | ~17 seconds in |
| Half an hour | ~68 cars, ~28 upgrade levels, 2 bays |
| Shop maxed out | ~2 hours |
| First prestige | ~3 hours, 4 tokens |
| Idle vs playing | ~78% |

## Stage 1 — The core game

All of the game's rules, as plain C# with no Unity references.

- **Cars:** 10 blueprints across 5 rarity tiers (Rusty Ute → Concours Prototype), 9 job types,
  weighted spawning that responds to your Reputation upgrades.
- **Mini-games:** timing bar, tool matching, hold-and-release, rapid sequence. Each is a state
  machine advanced by `Tick(deltaTime)` and driven by three generic inputs, which is what makes them
  testable and identical for the player and for hired mechanics.
- **Mini-game assignment** is weighted by job type (wheel work leans towards torque, electrics
  towards memory) and actively avoids giving one car the same game twice in a row — measured at
  under 15% back-to-back repeats.
- **Economy:** wallet, 11 upgrades across 4 branches with geometric costs, prestige tokens.
- **Random events:** VIP Weekend, Rush Hour, Parts Shortage, Tool Sale, Coffee Run, Apprentice Day,
  Quiet Afternoon — applied as modifiers so no event needs special-case code elsewhere.
- **Simulation:** bays, the waiting queue, patience, payouts, speed tips, flawless bonuses, hired
  mechanics, offline progress (capped at 8 hours, at 50% efficiency).
- **Saving:** a dependency-free JSON writer and parser (Unity's `JsonUtility` lives in UnityEngine,
  which Core is not allowed to touch). Saves cash, upgrades, prestige, statistics, the random seed
  *and* the cars currently on the forecourt with their half-finished jobs. Loading is deliberately
  forgiving: a missing field falls back to a default and an unknown car type is dropped, so an old
  save still loads rather than crashing.

---

## Open questions for you

None of these block anything — I made a call on each and the game plays fine. They are the decisions
I would want a second opinion on.

1. **No audio at all.** This is the most obvious gap for a shippable mobile game. It needs actual
   sound files, which is a content decision rather than a code one — worth saying what style you
   want before anyone writes an audio system.
2. **Legacy `Text` instead of TextMeshPro.** TMP looks considerably better, but it needs an
   "import essentials" step before it renders anything, which would mean the project does not just
   run on first open. Happy to switch once you have opened it once.
3. **First prestige at ~3 hours.** That is on the patient side for a mobile idle game — many aim for
   90 minutes. Lowering `GameBalance.PrestigeCashCap` moves it directly, and the balance probe will
   tell you exactly where it lands.
7. **Is Relaxed pace on or off by default?** It is off, so the default is the tuned experience. If
   the normal pace still rushes you, say so and I will make relaxed the default — it costs the
   player nothing, since payouts are identical either way.
8. **There is a web build of this game** (published as an Artifact) used for playtesting, because
   Unity cannot run in a browser. Every fix in Stage 6 was applied to both. It is not in this repo
   yet — keeping two implementations in step is a real cost, so that is your call.
4. **The UI is built in code, not prefabs.** Deliberate, and explained in `Assets/Prefabs/README.md`.
   If you would rather learn Unity's editor-driven workflow, converting one screen to prefabs is a
   good exercise — but it is a real fork in the road, so it is your call.
5. **Bays cap at 4.** Enough for a phone screen without scrolling. More would mean a scrolling bay
   list, which is a bigger UI change than it sounds.
6. **The scene file is hand-written.** It has been validated structurally, but I could not open Unity
   to confirm it. If it ever misbehaves, **Garage Tycoon → Create Garage Scene** rebuilds a known-good
   one, and that path does not depend on the committed file at all.

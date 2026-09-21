# Changelog

Progress log for Garage Tycoon, newest first. Anything that needs **your** decision is flagged
under "Open questions for you" at the bottom — nothing there is blocking, they are judgement calls
I made a reasonable choice on and would rather you confirmed.

---

## Stage 10 — You can now see the repair happening

**What you asked for:** an animation showing the car, the wheels and everything getting tightened
as you complete the mini-games.

### The problem with a progress bar

Up to now, finishing a round moved a bar. A bar tells you a number, but it never shows you the
work — you could play a whole car without ever looking at it. The job was invisible.

### What is there now

A car sits on the ramp above the mini-game, in the car's own colour, and the job happens **on it**.

- **Fasteners are the through-line.** Every job type has a cluster of bolts somewhere sensible on
  the car — wheel nuts on the front wheel for a tyre change, caliper bolts on the rear for brakes,
  bonnet bolts for an engine rebuild, a cluster up in the cabin for diagnostics. As a job
  progresses, they go tight one at a time, newest one popping and turning as it lands. So "I
  finished a round" always reads as "another bolt just went tight", whichever mini-game it was.
- **The wheels actually turn.** Wheel work spins the wheel it belongs to by 120 degrees, eased
  rather than snapped, so it looks like a wheel being spun back on.
- **A ring marks where you are working**, breathing slowly so your eye finds it without it being
  noisy.
- **A spark where the spanner lands** — gold for a perfect round, green for a good one.
- **A miss knocks the car** instead of turning a bolt, because a miss moves nothing.
- **The finished car swells once**, as a small "done, and looking good".
- Underneath, a plain count (`7 / 17 fasteners torqued`) for anyone who wants the number too.

### The rule that makes it honest, and the bug the tests caught

The number of turned bolts is read directly from the job's progress every frame — never a counter
that ticks up on its own, because that is exactly how an animation drifts out of step with the
game underneath it.

The first version rounded: `round(progress × bolts)`. Writing the test for that found a real bug.
A five-bolt job at 90% progress rounds to **five out of five** — the car claims to be finished
while the progress bar plainly is not. The other end is just as bad: 1% of real work shows nothing
at all, so the round you just won looks like it did nothing. Both are now clamped, and both are
pinned by tests:

- `An unfinished job never shows a finished car`
- `Any progress at all shows at least one fastener turned`
- `Fasteners only ever go one way as a job progresses` (200 steps, each job type)
- `A played car's fasteners track its jobs all the way through` — the virtual player works real
  cars and every single round is checked against the car's own numbers

The same bug was in the web build. Fixed there too, and verified in a browser: a five-bolt tyre
job now reads 0.2→1, 0.5→3, 0.8→4, 0.9→4, 0.999→4, 1.0→5.

### Where the layout lives

The map of "which job happens where on the car, and how many bolts it gets" is in
`Assets/Scripts/Core/Cars/RepairLayout.cs` — in **Core**, not in the Unity view. Two reasons: the
tests can check it without Unity being involved, and if a second front end ever draws the same car
they read the same table instead of drifting apart. The tests check every job type has a spot,
that no spot sits off the car, and that no two clusters would draw on top of each other.

### Also in this stage

- `GameBalance.MaxJobsPerCar` now exists as a real constant (4). The view sizes its bolt pool from
  it, and a test checks no car in the catalogue asks for more than the UI can draw.
- The mini-game area lost 156px to make room for the car, so all four games were re-fitted to the
  shorter panel. Every touch target is still comfortably above the 108px minimum — the hold button
  is 130px.
- Four gaps in the Unity API stubs turned up (`Quaternion`, `Transform.localRotation`,
  `Mathf.Cos`, `Time.unscaledTime`) and were added to match the real API.

**Tests: 135 passing** (up from 125). Compile check clean.

---

## Stage 9 — Fixing what Stage 8 got wrong, and giving the game somewhere to go

Three problems, all found by tracing a full three-hour run rather than by guessing.

### 1. The streak was not actually a mechanic

The trace was blunt about it: best streak **63 by minute 10, 135 by minute 50, and then flat
for the next two hours**. The cap was 12, so a competent player hit the ceiling in about thirty
seconds and held it forever. It was a permanent +48%, not a risk — the opposite of the chain
bonus it was modelled on.

- **Cap raised 12 → 50, step lowered 0.04 → 0.012.** Same ceiling value, but it now takes a
  couple of minutes of clean work to reach rather than half a minute.
- **It decays.** Stop working for seven seconds and it starts draining a step at a time. It
  previously could not be lost by inaction at all — you could put the phone down mid-chain and
  come back to the same multiplier. A streak you cannot lose is a discount.
- **It shows what it is worth.** The meter now reads "+$340" against the job in hand, so
  dropping it has a visible price instead of being an abstract multiplier.

After the change the streak fluctuates constantly through a run (24, 5, 0, 16, 0, 17, 0, 18…)
instead of sitting pinned at maximum. That is the mechanic doing its job.

### 2. Hour three played exactly like minute three

Difficulty scaling made the four mini-games faster and tighter, but never *different*.

**Garage rank** is earned by all-time earnings, so it survives selling up, and each rank unlocks
a **twist** that then turns up at random (30% of rounds) on one mini-game:

| Rank | Twist | What changes |
|---|---|---|
| Local Workshop | **Twin zones** | One wide target becomes two narrow ones |
| Certified Service | **Pre-loaded** | The torque gauge starts part-wound — far less time |
| Performance Shop | **Shuffle** | The tools swap places after they hide |
| Concours Specialist | **Backwards** | Repeat the pattern in reverse |

Landing a twisted round is worth **35% extra progress**, so a twist is something to want rather
than a tax for having played a while. Thresholds were tuned against the trace so the first one
lands around **minute 20** — an earlier set put it at minute 40, which is far too long to wait
for the game to change.

One design catch worth recording: **Shuffle was unplayable as first built.** Every button reads
"?" after the preview, so tools swapping behind them is invisible and the round becomes a coin
flip. It now flashes the new arrangement for a beat — you get a fraction of the usual time to
re-find your tool, which is a test of something.

### 3. The economy stopped giving

Income grew only ~6x across three hours — nearly linear, where the genre lives on feeling
progressively more powerful — and the shop was **bought out at minute 130 with prestige still
forty minutes away**.

**Master Tooling** is one upgrade with no ceiling whose effect *compounds* rather than adds
(x1.05 per level, cost x1.26 per level). It is the only multiplicative upgrade in the game.

- Income growth across a run: **6x → 10x**, and still accelerating at the end rather than
  plateauing.
- The shop is never empty, at any point, however long someone plays.
- It also creates the late-game decision the game did not have: keep compounding, or stop
  spending and bank the cash for a sell-up. The prestige cap moved to $120,000 to keep a first
  reset near three hours now that the sink competes for the same money.

**18 new tests** (125 total, all passing) covering decay behaviour and its edges, rank
progression surviving prestige, each twist individually, twists never attaching to the wrong
mini-game, twisted rounds always terminating, and the endless upgrade compounding without ever
reporting itself as maxed.

---

## Stage 8 — Depth pass: streaks, customers and a real prestige

Researched what comparable games do and built against two specific findings.

**From the time-management genre (Diner Dash's chain bonus)**

- **The work streak.** Land rounds back to back and a meter under your cash climbs; every job you
  finish while it runs pays more, up to 1.5x. A miss or a breakage drops it, and so does letting
  your own customer walk. A scrappy round holds it but does not build it, so a cautious player
  cannot sit on a maximum streak by never taking a risk.
  This is the single biggest change to how the game feels: before it, a perfect round and a scrappy
  one were worth nearly the same, so no individual round mattered. Measured effect: the gap between
  expert and novice play went from 3.7x to **7.3x**.
  Hired mechanics deliberately do not build it — a mechanic silently holding a 12x chain in a bay
  you are not looking at would make the whole mechanic meaningless.
- **Customer temperaments.** Every car now arrives with someone attached: *In a hurry* (impatient,
  tips well), *Big tipper*, *VIP* (pays 60% over the odds), *No rush*. Two identical hatchbacks are
  no longer the same job, and a busy forecourt is now a real decision.
- **Buy them a coffee.** A free button on each car that buys back patience it has lost, on a
  cooldown — so it is a decision about *which* car to save. Taken from the genre's mood-recovery
  mechanic, which is what stops a timer running out from feeling like something that merely
  happened to you.

**From the idle genre**

The literature is consistent that a single flat multiplier is what separates a shallow prestige
from a deep one, and that is exactly what this game had: "+12% per token", applied automatically.

- **Tokens are now a currency, not a score.** Seven permanent perks to spend them on — higher
  rates, an opening float, an inherited bay, a pre-trained crew, more patient customers, a longer
  streak cap, better night shifts. They cost more per level and survive every future sell-up.
  A sell-up now asks the player a question instead of just handing them a bigger number.
- A tampered or out-of-date save can never conjure tokens: what has been spent is re-derived from
  the perks owned rather than trusted from the file.

**Sound, synthesised**

Every sound effect is generated at runtime with the Web Audio API — taps, the cash chime, the
streak note that climbs with the chain, the engine burst when a car arrives. No audio files, so the
game stays one self-contained page, matching how the sprites are drawn rather than imported. The
audio context is created lazily on first tap (browsers require a gesture) and every call is wrapped,
so a browser that blocks audio cannot break the game. This closes the "no audio at all" gap flagged
back in Stage 4 — for the web build. **Unity still has no audio.**

**A third modelling fault in the tests**

`precision_speed` measured at **-29% income**. The virtual player's aiming error was stored in
*position* units, so a slower marker gave it no benefit at all — it only made rounds longer. A
person's error is a *timing* error in seconds, which only becomes a positional error once you
multiply by how fast the thing is moving. Modelling it properly turned that upgrade from -29% to
**+30%**. Every upgrade now measures positive in isolation (+2% to +67%).

That is the third time the test model, not the game, was the thing that was wrong. Worth
remembering when reading any of these numbers.

**19 new tests** (107 total, all passing) covering streak behaviour, mood effects, the calm
cooldown and its edge cases, perk costs, perks surviving prestige, and that every perk changes
something measurable.

---

## Stage 7 — How to play

The game explained nothing. It now opens with a **How to play** screen the first time anyone plays,
and a **?** button on the bottom bar brings it back any time.

It covers the loop in three numbered steps, then each of the four mini-games in its own card —
the web build shows a working miniature of each one, because a picture of the actual bar, gauge and
arrows teaches them faster than a paragraph does. Then how you get paid (perfect bonus, speed tip,
what damage costs), what each upgrade branch is for, and what selling the garage does.

It closes by pointing at **Relaxed pace** for anyone the normal speed still rushes, and notes the
keyboard controls for desktop play.

Built for both the Unity project (`HelpScreen.cs`) and the web build. The garage pauses while it is
open, like the other full-screen panels.

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
7. **Is the how-to-play screen too long?** It is one scroll on a phone. I would rather it be
   complete than short, but if people bounce off it, the honest fix is to cut it to the loop plus
   the four games and let the rest be discovered.
8. **Is Relaxed pace on or off by default?** It is off, so the default is the tuned experience. If
   the normal pace still rushes you, say so and I will make relaxed the default — it costs the
   player nothing, since payouts are identical either way.
9. **Unity has no sound.** The web build synthesises all of its audio; the same trick works in
   Unity (generating AudioClips procedurally) but is not done yet. Worth doing before anyone else
   plays the Unity build, because the difference in feel is large.
10. **Unity still has no sound**, and now also has no visible-shuffle animation (the web build
   flashes the tools; Unity shows the same twist but the reveal is plainer). Both are worth
   closing before anyone plays the Unity build seriously.
13. **The Unity repair car is simpler than the web one.** Both show the bolts going tight, the
   wheels turning, the active-job ring and the spark. The web build additionally draws the
   *damage* — a dent, exhaust smoke, a paint patch, an electrics spark — that fades as each job is
   finished. Unity does not, because those are drawn shapes per job type and it is a chunk of
   work for something the bolts already communicate. Say the word if you want parity.
14. **The mini-games are 156px shorter now.** I think the trade is clearly worth it — the car is
   the best feedback in the game — but if any of the four now feels cramped on your actual phone,
   tell me which and I will give it the space back by shrinking the car band instead.
11. **Is the streak decay too harsh?** Seven seconds of grace, then a step every 1.1s. A player
   who stops to read the shop will lose a long chain. I think that is correct — it is what makes
   the streak a thing you protect — but it is the single most likely thing to annoy someone, and
   `comboGrace` moves it.
12. **There is a web build of this game** (published as an Artifact) used for playtesting, because
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

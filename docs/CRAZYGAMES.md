# Publishing to CrazyGames

The fastest route to real players. No developer fee, no 12-tester wait, no
review queue measured in weeks.

> **Verify the SDK details before you submit.** The sandbox this was built in
> could not reach `docs.crazygames.com`, so the integration was written
> against secondary sources and validated with a mock SDK, not the real one.
> The call sequence is almost certainly right, but check the current method
> names at https://docs.crazygames.com/sdk/intro/ and test on their preview
> tool before publishing.

## Building the submission

```bash
npm run build:crazygames
```

Produces `dist/chroma-rush-crazygames.zip` (~82 KB) with `index.html` at the
archive root, which is what their uploader expects.

The only difference from the Play build is the SDK script tag injected into
`index.html`. Keeping it out of `www/` means the Capacitor build never depends
on an external script it cannot reach.

## What's wired up

`www/js/crazygames.js` implements the same surface as the AdMob backend, and
`www/js/platform.js` picks between them at runtime by checking for
`window.CrazyGames`. One codebase, two distributions.

| Moment | SDK call |
|---|---|
| Page ready | `init()` then `game.loadingStop()` |
| Run starts / resumes / revives | `game.gameplayStart()` |
| Pause, death, back to menu | `game.gameplayStop()` |
| New personal best | `game.happytime()` |
| "Watch ad to continue" / "2x coins" | `ad.requestAd('rewarded', …)` |
| Every third game over | `ad.requestAd('midgame', …)` |

Audio ducks on `adStarted` and restores on `adFinished` **or** `adError`, and
every ad request has a 45-second timeout, so a callback that never fires can't
leave the player staring at a blocked screen.

An unfilled or ad-blocked request arrives on `adError`. That's normal, not a
bug — the game treats it as "no reward" and carries on.

Midgame ads are throttled to one per three minutes on their side, so
requesting at every natural break is fine.

## Things the portal checks

Chroma Rush already satisfies these, but if you change the game, keep them true:

- **Self-contained.** No external requests except their SDK. No web fonts, no
  CDN scripts, no analytics. Ours loads nothing.
- **Mobile and desktop.** Touch (tap left/right) and keyboard (arrows / A-D)
  both work, and the canvas is responsive down to ~400px.
- **No other ad networks.** The AdMob path is never initialized when the
  CrazyGames SDK is present.
- **No external links** out of the game.
- **Fast first load.** 82 KB total is well inside anything they ask for.

## Store assets

In `store/crazygames/`:

- `cover-1600x900.png` — 16:9 cover
- `screenshot-1-menu.png`, `-2-early.png`, `-3-fast.png`, `-4-missions.png`

Listing copy in `store/listing.md` works as-is; drop the Google-Play-specific
contact fields.

## The leaderboard

Hidden automatically on this build — the live board runs on the claude.ai
artifact runtime, which doesn't exist here. See `docs/LEADERBOARD.md` for
putting it on Firestore if you want it on the portal.

## Submitting

1. Developer account: https://developer.crazygames.com
2. Upload the ZIP, add the cover and screenshots, title, description, tags
   (arcade, casual, reflex, one-button).
3. Use their QA/preview tool to confirm ads and gameplay events fire against
   the real SDK — this is the step that catches integration mistakes.
4. Submit for review.

Ads earn a revenue share; check their current developer terms for the split
and payout threshold, as those change.

## Why do this before Google Play

Same game, no $25 fee, no closed-testing requirement, and real players in days
rather than a month. You find out whether the game actually holds attention
*before* spending three weeks on Play compliance — and if retention is poor,
that's far cheaper to learn here.

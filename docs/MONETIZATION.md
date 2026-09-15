# Monetization

## What's already built

| Placement | Where | Status |
|---|---|---|
| **Rewarded — continue run** | Game over, once per run | Wired, `js/ads.js` |
| **Rewarded — 2× coins** | Game over, when coins were earned | Wired, `js/ads.js` |
| **Interstitial** | Every 3rd game over, never the first two | Wired, `js/ads.js` |
| **Remove ads (IAP)** | `profile.adFree` flag | Flag exists, purchase flow not wired |

In a browser the rewarded ads show a placeholder so you can test the flow.
On a device with the AdMob plugin installed, real ads are served.

The placements are deliberately restrained: no ad on the first two deaths, no
ad before a run, and never two prompts on one screen. Ad-stuffing a casual game
raises revenue-per-session and destroys retention, which is the thing that
actually compounds.

## Wiring up real ads

```bash
npm install @capacitor-community/admob
npx cap sync
```

1. Create an AdMob account: https://admob.google.com — add your app, then
   create one **Rewarded** and one **Interstitial** ad unit.
2. Put the AdMob **App ID** in `android/app/src/main/AndroidManifest.xml`,
   inside `<application>`:

   ```xml
   <meta-data
     android:name="com.google.android.gms.ads.APPLICATION_ID"
     android:value="ca-app-pub-XXXXXXXXXXXXXXXX~YYYYYYYYYY"/>
   ```

3. Replace the two IDs in `www/js/ads.js` → `AD_UNITS` with your real unit IDs.

> The IDs currently in `ads.js` are **Google's official test units**. Keep them
> during development. Never click your own live ads or test with real units on
> your own device — AdMob treats it as invalid traffic and suspends accounts
> for it, permanently and without much appeal.

## Adding "remove ads" as a purchase

`profile.adFree` already gates interstitials. To make it sellable, add a
billing plugin (e.g. `@capacitor-community/in-app-purchases`), create a
one-time **managed product** in Play Console → Monetize → In-app products,
and on a successful purchase set `profile.adFree = true; save();`.

Price it at $1.99–2.99. Restore-purchases must work, or you'll get refund
requests and one-star reviews.

## What this will actually earn

Being straight with you, because the plan matters more than the code:

- Rewarded video eCPM runs roughly **$5–15 per 1,000 views** in the US/UK/AU,
  and **$1–3** across much of the rest of the world. Interstitials are lower.
- A casual player who doesn't stick around generates maybe **2–5 ad
  impressions, total**.
- That's on the order of **$0.01–0.05 per install**, and organic installs for
  an unmarketed first game are typically in the **tens to low hundreds**.

So: a few dollars, realistically. **AdMob doesn't pay out until you reach
$100.** Plenty of first games never cross that line.

This isn't a reason not to ship. It's a reason to be clear about what shipping
buys you: the whole pipeline — signing, the Play Console forms, the testing
rule, store assets, ad mediation, crash triage — learned once, on a game that's
finished. The second game starts from here instead of from an empty folder,
and games two through five are where people's numbers usually start moving.

The three things that actually change the outcome, in order:

1. **Retention.** If players don't come back on day 1, nothing else matters.
   Watch D1 retention in Play Console; under ~20% means fix the game before
   you touch anything else.
2. **Organic reach.** Short vertical gameplay clips — the "shape-match at
   speed" moments read well in 8 seconds. This is free and it's how most
   breakout casual games actually got their first 10k players.
3. **Iteration.** Ship updates. Add levels, a daily challenge, leaderboards.
   Dead listings sink; updated ones get re-surfaced.

Paid user acquisition only makes sense once you know your revenue per install
exceeds your cost per install. Until then it's just buying losses.

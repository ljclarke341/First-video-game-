# Getting Chroma Rush onto Google Play

Rules verified September 2026. Google changes these — check the linked official
pages before you submit.

## 1. Developer account — $25, once

Sign up at https://play.google.com/console. One-time $25 fee, non-refundable.

Choose the account type carefully:

- **Personal account** — free to create, but see the testing rule below.
- **Organization account** — requires a D-U-N-S number (free, ~30 days to get).
  **Exempt from the 12-tester rule.**

You will also verify your identity (government ID) and, for personal accounts,
a phone number. Budget a few days.

## 2. The 12-tester rule — the part that surprises people

If your **personal** developer account was created after **13 November 2023**,
you cannot publish to production until:

- at least **12 testers** are opted in to a **closed test** of your app, and
- they have been **continuously opted in for 14 days**, and
- only then can you apply for production access (which is itself reviewed).

Testers who opt out and back in reset their clock — the 14 days must be
consecutive. Organization accounts and personal accounts created before
13 Nov 2023 skip this entirely.

**Plan for this.** Realistically it adds 3–4 weeks before your first release.
Line up 12 people with Google accounts *before* you finish the game — friends,
family, a Discord, a subreddit. You need their Gmail addresses to add them to
the closed test.

Official: https://support.google.com/googleplay/android-developer/answer/14151465

## 3. Target API level

As of **31 August 2026**, new apps and updates must target **Android 16
(API 36)** or higher. Extensions to 1 November 2026 could be requested.
Set `targetSdkVersion = 36` (see ANDROID_SETUP.md).

Official: https://support.google.com/googleplay/android-developer/answer/11926878

## 4. Store listing assets

All generated for you in `store/`:

| Asset | Requirement | File |
|---|---|---|
| App icon | 512×512 PNG | `icon-512.png` |
| Feature graphic | 1024×500 PNG | `feature-graphic-1024x500.png` |
| Phone screenshots | 2–8, min 320px | `screenshot-1..4-*.png` |

Listing copy is in `store/listing.md` — paste it straight in.

## 5. The forms

Play won't let you publish until every one of these is green. None are
optional, and the Data Safety one is where most first-timers get it wrong.

- **App content → Privacy policy.** A public URL is mandatory. Use
  `docs/PRIVACY_POLICY.md` — publish it as a GitHub Pages page or a free
  Carrd/Notion page and paste the link.
- **App content → Ads.** Answer **yes, my app contains ads** (it does, via
  AdMob). Lying here gets apps removed.
- **App content → Data safety.** If you ship AdMob you *are* collecting data.
  Declare: **Device or other IDs** (collected, shared with third parties, for
  Advertising/Marketing, not user-deletable). AdMob also auto-adds the
  `com.google.android.gms.permission.AD_ID` permission.
- **Content rating** — an IARC questionnaire. Chroma Rush has no violence,
  no user content, no gambling; it'll come back Everyone/PEGI 3.
- **Target audience.** If you select an age group under 13 you enter the
  **Families** programme, which restricts ad formats and SDKs. For a first
  game, target **13+** to avoid that entire compliance surface.
- **App access** — "all functionality is available without special access".
- **Government apps / News / Financial features** — all no.

## 6. Release

**Testing → Closed testing** first (mandatory for new personal accounts,
and a good idea regardless). Upload `app-release.aab`, add your 12 testers by
email, share the opt-in link.

After 14 continuous days: **apply for production access**, then
**Production → Create new release**.

First review typically takes a few days and can take longer for a new account.

## 7. After you ship

- **Play Console → Statistics** for installs and retention.
- **Android vitals** for crashes/ANRs — fix these first, they suppress ranking.
- Ratings matter more than anything else you control. Prompt for a review only
  after a player beats their best score, never on a crash or a loss.
- Update regularly. A dead listing sinks fast.

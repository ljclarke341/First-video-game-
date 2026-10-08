# The daily leaderboard

## How it works now

`www/js/leaderboard.js` stores one document per player:

```
daily/<viewerId>  ->  { scores: { "2026-10-08": 1240, ... }, updated: "..." }
```

The board subscribes to the whole `daily` collection, pulls out today's date
key from each row, sorts, and re-renders whenever anyone's score lands. That
subscription is what makes it *live* - no polling, no refresh button.

Only the most recent 14 days are kept per row, so documents stay small.

### Access rules

Declared at publish time:

```js
capabilities: {
  db: { rules: [
    { path: "daily",        read: "view", write: "owner" },
    { path: "daily/{self}", write: "interact" }
  ]},
  user: { scopes: ["profile"] }
}
```

Everyone who can open the page reads the whole board; each player can only
write their own row. Nobody can overwrite somebody else's score.

Names are resolved per render through `user.profiles()` and never stored -
only opaque viewer ids go into the database.

## The catch for Google Play

**This runs on claude.ai, not in the packaged Android app.** The shared store
is part of the artifact runtime. In the Capacitor build `window.claude` does
not exist, `initLeaderboard()` returns false, and the board shows an
unavailable notice. Everything else in the game is unaffected.

## Wiring a real backend for the Play build

`leaderboard.js` is deliberately a three-function surface. Reimplement these
and the rest of the game needs no changes:

```js
initLeaderboard()               // -> Promise<boolean>
submitDaily(dateKey, score)     // -> Promise<boolean>
subscribeDaily(dateKey, onRows) // -> unsubscribe fn; onRows(rows, total)
```

`rows` is `[{ id, name, score, rank, isMe }]`, already sorted.

**Firebase Firestore** is the cheapest route - the free tier covers a small
game comfortably, and Anonymous Auth means no sign-up screen:

1. Create a Firebase project, enable **Firestore** and **Anonymous Auth**.
2. `npm install firebase`.
3. Collection `daily/<uid>` with the same document shape as above.
4. Security rules - the equivalent of the artifact rules:

```
match /daily/{uid} {
  allow read: if request.auth != null;
  allow write: if request.auth != null && request.auth.uid == uid;
}
```

5. Use `onSnapshot` for the live subscription, exactly as here.

Anonymous accounts have no display name, so add a one-time "choose a name"
prompt and store it on the row. Treat it as user-generated content: Play
requires you to be able to moderate it, so filter it or keep the board to
initials.

> A leaderboard with real names is user-generated content. If you ship one,
> your Play Data Safety form has to declare it, and you need a way to remove
> abusive entries.

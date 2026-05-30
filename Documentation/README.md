# Pixygon Passport

Login + account system for Pixygon-connected games. Handles registration, verification, login (token-based remember-me), password recovery, profile updates, owned-games entitlements, and a small library of cross-game social posts (death messages, small wins).

Targets the Pixygon backend at `https://api.pixygon.com/v1/`.

## Contents

- [What this package gives you](#what-this-package-gives-you)
- [Install](#install)
- [Quick start](#quick-start)
- [Architecture](#architecture)
- [Auth flow](#auth-flow)
- [Public API — `PixygonApi`](#public-api--pixygonapi)
- [Persistence + remember-me](#persistence--remember-me)
- [Owned games & entitlements](#owned-games--entitlements)
- [Event posts (death messages, small wins)](#event-posts-death-messages-small-wins)
- [In-package UI prefabs](#in-package-ui-prefabs)
- [Friendly error messages](#friendly-error-messages)
- [Server endpoints reference](#server-endpoints-reference)
- [Integration recipes](#integration-recipes)
- [Dependencies](#dependencies)

---

## What this package gives you

- A drop-in `AccountUI` MonoBehaviour that handles every screen the player sees: login, signup, email-verification, password recovery, logout, delete-account.
- A persistent `PixygonApi` singleton that survives scene transitions and exposes a small async API surface for talking to the Pixygon backend.
- Token-based "remember me" — passwords are never persisted to PlayerPrefs. Access + refresh tokens are; a 7-day access token refreshes automatically via a 30-day refresh token.
- Live `OnLoginStateChanged` event so the rest of your game can react when the player signs in / out / refreshes mid-session, without polling.
- Owned-games entitlement check (`OwnsGame(slug)`) for paid feature gating.
- Friendly error humanizer that converts raw HTTP / JSON failures into prose the player can act on.
- Authenticated POST helpers for game-side social posts: `PostDeathMessage` (Discord red embed) and `PostSmallWin` (Discord green embed).

## Install

Add the package to your project's `manifest.json` under `Packages/`, or include it as a local file path during development:

```jsonc
{
  "dependencies": {
    "com.pixygon.passport": "0.5.0",
    "com.pixygon.saving":   "0.5.0"  // hard dependency
  }
}
```

Saving (`com.pixygon.saving`) is required — Passport writes the logged-in user to `SaveManager.SettingsSave._user` so other systems can read it without going through this package directly.

## Quick start

1. **Drop the AccountUI prefab into your boot scene.** The prefab contains the canvas + every account screen (login, register, verification, recovery, logout, error panel). The AccountUI MonoBehaviour on its root is what your code talks to.
2. **Add a `PixygonApi` MonoBehaviour to the same scene** (or any persistent boot-scene object). It self-singletons in `Awake` and `DontDestroyOnLoad`s — one instance per game session, persists across every scene load.
3. **Subscribe to login events** wherever your UI needs to react:

```csharp
using Pixygon.Passport;

void OnEnable() {
    if (PixygonApi.Instance != null)
        PixygonApi.Instance.OnLoginStateChanged += RefreshUI;
}
void OnDisable() {
    if (PixygonApi.Instance != null)
        PixygonApi.Instance.OnLoginStateChanged -= RefreshUI;
}
void RefreshUI() {
    if (PixygonApi.Instance.IsLoggedIn) {
        nameText.text = PixygonApi.Instance.AccountData.user.userName;
    } else {
        nameText.text = "Sign in";
    }
}
```

4. **Trigger sign-in from a button** by activating the AccountUI panel and calling `AccountUI.StartLogin()`, or just let the prefab's UI buttons drive it via UnityEvents — it's already wired internally.

That's the entire integration. Persistence, token refresh, the verification flow, password recovery — they all work without further code.

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│  PixygonApi (MonoBehaviour, singleton, DontDestroyOnLoad)│
│  ───────────────────────────────────────────────────────│
│  - AccountData     (current LoginToken: user + tokens)   │
│  - IsLoggedIn, IsLoggingIn  (state flags)                │
│  - OwnedGameIds    (string[] of slugs)                   │
│  - OnLoginStateChanged  (event)                          │
│                                                          │
│  StartLogin / StartSignup / VerifyUser                   │
│  ForgotPassword / ForgotPasswordRecovery                 │
│  FetchOwnedGames / OwnsGame                              │
│  PostDeathMessage / PostSmallWin                         │
│  PatchSave / PatchProfile / SetLatestActivity / ...      │
│  StartLogout / DeleteUser                                │
└──────────────────────────────────────────────────────────┘
                          ▲
                          │ called from
                          │
┌──────────────────────────────────────────────────────────┐
│  AccountUI (MonoBehaviour on the canvas prefab root)     │
│  - Orchestrates every screen in the flow                 │
│  - Subscribes to PixygonApi callbacks                    │
│  - Renders errors via ErrorPanel + LoginErrorHumanizer   │
│  - Fires OnLoginAction / OnLogoutAction for hosts        │
└──────────────────────────────────────────────────────────┘
                          ▲
                          │ owns
                          │
        ┌─────────────────┴─────────────────┐
        │                                   │
LoginPanel    RegisterPanel    VerificationPanel
  ForgotPasswordRequestPanel    ResetPasswordPanel
        ErrorPanel        (delete-account modal)
```

Two pieces matter to you:

- **`PixygonApi.Instance`** — the C# API. Everything you'd call from game code lives here.
- **`AccountUI`** — the prefab + MonoBehaviour you drop in scene. Talks to `PixygonApi` and exposes a handful of methods for non-UI hosts (e.g. a hub menu's "Account" button calls `AccountUI.StartLogin()` or `StartLogout()`).

## Auth flow

```
Player opens game
       │
       ▼
PixygonApi.Awake
       │
       ├─ no remember-me cached  ───► UI shows AccountUI on demand
       │
       └─ remember-me cached
              │
              ▼
      GET /v1/auth/refresh/{userId}  (sends REFRESH token)
              │
              ├─ 200 → fresh {token, refreshToken, expiresIn, user}
              │       ► IsLoggedIn = true
              │       ► PersistRememberMe (new tokens)
              │       ► FetchOwnedGames (background)
              │       ► OnLoginStateChanged fires
              │
              └─ 401/expired → ClearRememberMe → user signs in interactively
```

Interactive login:

```
LoginPanel.Login(user, pass, rememberMe)
       │
       ▼
AccountUI.Login → PixygonApi.StartLogin
       │
       ▼
POST /v1/auth/login { userName, password }
       │
       ├─ 200 → AccountData populated
       │       ► PersistRememberMe (if rememberMe was true)
       │       ► OnLoginStateChanged fires
       │       ► FetchOwnedGames fires in background
       │
       └─ 4xx → ErrorPanel.ShowError(LoginContext, …)
                ► humanized title + body via LoginErrorHumanizer
                ► on close, LoginPanel reopens with username preserved
                  and password focus restored
```

## Public API — `PixygonApi`

### State

| Member | Purpose |
|---|---|
| `Instance` | Static singleton. Survives scene loads. |
| `IsLoggedIn` | True if there's a valid session in memory. |
| `IsLoggingIn` | True between submit and response, including remember-me boot refresh. |
| `AccountData` | `LoginToken { user, token, refreshToken, expiresIn }` for the current session. Null when signed out. |
| `OwnedGameIds` | `string[]` of game slugs the account owns. Populated asynchronously after login. Empty until then. |
| `OnLoginStateChanged` | C# event. Fires after login, refresh, logout, and after `FetchOwnedGames` completes. |

### Methods

```csharp
// Authentication
void StartLogin(string user, string pass, bool rememberMe = false,
                Action onLogin = null, Action<ErrorResponse> onFail = null);
void StartSignup(string user, string email, string pass, bool rememberMe = false,
                 Action onSignup = null, Action<ErrorResponse> onFail = null);
static void VerifyUser(string user, int code,
                       Action onVerify = null, Action<ErrorResponse> onFail = null);
static void ForgotPassword(string email,
                           Action onVerify = null, Action<ErrorResponse> onFail = null);
static void ForgotPasswordRecovery(string email, string hash, string newPass,
                                   Action onVerify = null, Action<ErrorResponse> onFail = null);
void StartLogout();
void DeleteUser(Action onVerify = null, Action<ErrorResponse> onFail = null);

// Ownership
Task FetchOwnedGames();           // refresh OwnedGameIds from the server
bool OwnsGame(string slug);       // synchronous lookup against OwnedGameIds

// Profile + activity
void SetProfile(string bio, string displayName, string[] links,
                Action<ErrorResponse> onFail = null);
void SetLatestActivity(string activity, string subactivity, string gameId);
void PatchDreadwagerSkin(int i);
void PatchGameXp(int i);
void PatchStreamerXp(int i);
void PatchViewerXp(int i, string id);

// Save data
Task<Savedata> GetSave(string gameId, int slot);
Task<Savedata> PostSave(string gameId, int slot, string savedata);
void PatchSave(Savedata savedata);

// Lookups + social graph
static Task<AccountData> GetUser(string userId);
Task<AccountData> GetUserFromTwitch(string twitchId);
Task<string> FollowUser(string followId);
Task<string> GetFollowing(string userId);
Task<string> GetFollowers(string userId);
Task<string> UserSearch(string searchString);

// Game-side social posts (Discord embeds)
void PostDeathMessage(string gameId, string text,
                      Action onPosted = null, Action<ErrorResponse> onFail = null);
void PostSmallWin(string gameId, string text,
                  Action onPosted = null, Action<ErrorResponse> onFail = null);

// Highscores + feedback
static Task<string> GetHighScores(string gameId, string scoretype, string version,
                                  string fromDate, string toDate);
static void PostHighScore(string score);
void PostFeedback(Feedback feedback);

// Wallet patches (legacy NFT path, kept for back-compat)
void PatchWaxWallet(string wallet);
void PatchEthWallet(string wallet);
// (eth / tez / mat / imx / twitch variants too)

// Item box (legacy NFT path)
Task<string> MintAsset(ItemBoxSlots items);
Task<string> DepositItems(ItemBoxSlots items);
Task<string> WithdrawItems(ItemBoxSlots items);
Task<ItemBoxSlot[]> GetItems();
```

All `Action`-callback methods fire on the Unity main thread once the underlying `UnityWebRequest` completes.

## Persistence + remember-me

Three `PlayerPrefs` keys when the user ticks "Remember me":

| Key | Purpose |
|---|---|
| `Pixygon.RememberMe` | int sentinel (1 = remember-me active) |
| `Pixygon.UserId` | Mongo ObjectId of the user |
| `Pixygon.Token` | 7-day access JWT |
| `Pixygon.RefreshToken` | 30-day refresh JWT |

On boot, if the sentinel is set, `PixygonApi.Awake` calls `GET /v1/auth/refresh/{userId}` with the **refresh token** in the Authorization header. A 200 yields a fresh `{ token, refreshToken, expiresIn, user }`, which is persisted in place of the old pair. A 401/expired refresh clears the prefs silently and the user lands on the login UI as if they'd never signed in.

The user's password is never stored on disk after the first login. Older clients (pre-0.5) wrote `RememberMe` / `Username` / `Password` keys; the migration logic in `Awake` clears those on first run.

`LoginToken.expiresIn` (seconds until the access token expires) is included so a future change can proactively refresh before expiry. Today the refresh only fires at boot — runtime calls fail with 401 if the access token expires mid-session. For 7-day sessions this is rarely seen in practice.

## Owned games & entitlements

After login, `FetchOwnedGames` hits `GET /v1/users/{userId}/ownedGames` and stores the returned `string[]` of slugs in `OwnedGameIds`. The fetch is fire-and-forget — login UI doesn't block on it. When it completes, `OnLoginStateChanged` fires a second time so entitlement-aware UIs can re-evaluate.

```csharp
if (PixygonApi.Instance.OwnsGame("dreadwager")) {
    // Player owns Dreadwager — unlock Cursemark features etc.
}
```

`FetchOwnedGames` is also exposed publicly so a store flow can invalidate the cache after a purchase completes without forcing the player to log out and back in.

### WebGL host shortcut

When the WebGL build is hosted on the Pixygon site, the host page exposes a JS global `window.pixygonGameOwnership.ownedGames` **before** the Unity build boots. Reading from that global is a faster ownership check than waiting on the token refresh + ownedGames fetch round-trip. Consumers who care about that fast path should use a thin wrapper — see the Dreadwager project's `PixygonOwnership.cs` and `PixygonOwnership.jslib` for a reference implementation that prefers the global and falls back to `PixygonApi.OwnsGame(slug)` on other hosts and other platforms.

## Event posts (death messages, small wins)

Two authenticated endpoints for in-game social posts that show up as Discord embeds:

```csharp
PixygonApi.Instance.PostDeathMessage(
    gameId: "64e4df726a2585b4af8bce10",   // Mongo ObjectId, not slug
    text: "Died to slime on floor 3",
    onPosted: () => Debug.Log("Posted to Discord"),
    onFail: err => Debug.LogWarning(err._msg));

PixygonApi.Instance.PostSmallWin(
    gameId: "64e4df726a2585b4af8bce10",
    text: "Cleared the Tutorial with no deaths!");
```

The bearer token (from `AccountData.token`) attributes the post to the signed-in user. If the user isn't signed in, the request still fires anonymously — the server will accept it but won't attribute it to a player. The `gameId` parameter takes the backend's MongoDB ObjectId, **not the slug** — the slug is for ownership lookups.

Server formats deathMessage as a red Discord embed; smallWin as green.

## In-package UI prefabs

The package ships an `AccountUI` prefab containing every screen in the auth flow. Each screen is a separate panel under the canvas:

| Panel | Purpose |
|---|---|
| `LoginPanel` | Username + password + remember-me. Auto-focuses the right input field on open. |
| `RegisterPanel` | Username + email + password for signup. |
| `VerificationPanel` | 6-digit code from the welcome email. |
| `ForgotPasswordRequestPanel` | Email-only form that triggers a recovery link. |
| `ResetPasswordPanel` | Hash + new password (filled in from the recovery link). |
| `ErrorPanel` | Modal that surfaces humanized errors. |
| `DeleteAccountPanel` | Confirmation modal for `DeleteUser`. |
| Logout modal | Confirmation for `StartLogout`. |
| Login loading screen | Shown between submit and response. |

To customise:

- **Restyle** by editing the prefab's children directly. The MonoBehaviour scripts only care about the SerializeField references, not the visual layout.
- **Change the username placeholder to "Username or email"** by editing the Placeholder TMP_Text child of `LoginPanel`'s `_userInput` — the backend already accepts either.
- **Add a forgot-password button** anywhere; wire its OnClick to `AccountUI.OpenPasswordReset()`.

### AccountUI public surface

```csharp
public LoginState LoginState;        // current flow state
public Action OnLoginAction;         // fires after a successful login
public Action OnLogoutAction;        // fires after StartLogout / OnLogout
public bool ForceLogin;              // when true, the back button is hidden — login required

public void StartLogin();            // show LoginPanel
public void StartRegister();         // switch from login → register
public void CancelSignup();          // back to login from register
public void StartLogout();           // show logout confirmation
public void OnLogout();              // execute logout
public void OpenAccountScreen();     // generic open
public void CloseAccountScreen();    // dismiss
public void OpenPasswordReset();     // switch into recovery flow
public void StartDelete();           // show delete-account confirm
public void OnDeleteAccount();       // execute delete

// Called by individual panels:
public void Login(string user, string pass, bool rememberMe);
public void Signup(string user, string email, string pass, bool rememberMe);
public void OnVerify(string code);
public void OnResetPassword(string email);
public void OnSendResetPassword(string hash, string newPass);
```

`ForceLogin = true` hides the back button on LoginPanel — useful for games that won't run without an account.

## Friendly error messages

Raw server JSON like `{"msg":"Invalid Credentials"}` is humanized via `LoginErrorHumanizer` before reaching the UI. The humanizer takes a context enum and an `ErrorResponse`, returns a `HumanizedError { Title, Body }`:

```csharp
public enum LoginErrorContext {
    Login,
    Signup,
    Verification,
    PasswordRecovery,
}

LoginErrorHumanizer.Humanize(LoginErrorContext.Login, errorResponse);
// → HumanizedError { Title = "Couldn't sign in",
//                    Body  = "Wrong username or password. Try again." }
```

Strategy is four-step, applied in order:

1. **Network heuristic** — UWR strings like "Cannot connect to destination host" become "Couldn't reach Pixygon. Check your internet connection and try again."
2. **Body phrase match** — case-insensitive substring match for known server replies ("invalid credentials", "user not found", "already taken", "rate limit", etc.).
3. **HTTP status inference** — parsed out of UWR's "HTTP/1.1 401 Unauthorized" format. 401/403 → wrong credentials, 404 → no such account, 409 → already taken, 422 → bad input, 429 → rate-limited, 5xx → server hiccup.
4. **Context-specific generic** — last resort, never leaks the raw body.

Add new phrase matches in `LoginErrorHumanizer.Humanize` as the backend evolves. The humanizer always returns *something* — never null, never empty.

### Wiring custom screens through the humanizer

Use `ErrorPanel.ShowError(context, errorResponse, onClose)` from any panel:

```csharp
PixygonApi.SomeApiCall(args,
    onSuccess: HandleOk,
    onFail: err => _accountErrors.ShowError(
        LoginErrorContext.Login,   // or whichever context
        err,
        () => StartLogin()));      // continuation when user dismisses
```

The legacy `SetErrorMessage(title, ErrorResponse, onFail)` overload still works — it now humanizes the body but keeps your hand-written title.

## Server endpoints reference

Base URL: `https://api.pixygon.com/v1/`

| Method | Path | Auth | Purpose |
|---|---|---|---|
| POST | `auth/login` | none | Body: `{ userName, password }`. Returns `LoginToken`. |
| POST | `auth/register` | none | Body: `{ userName, email, password }`. Returns `LoginToken` (unverified). |
| POST | `auth/verify` | none | Body: `{ userName, code }`. Returns 200 on success. |
| POST | `auth/forgotPassword` | none | Body: `{ email }`. Triggers recovery email. |
| POST | `auth/forgotPasswordRecovery` | none | Body: `{ email, hash, newPassword }`. |
| GET | `auth/refresh/{userId}` | **Refresh token** | Returns a fresh `LoginToken`. |
| GET | `users/view/{userId}` | none | Public user profile. |
| GET | `users/{userId}/ownedGames` | access token | Returns `string[]` of game slugs. |
| GET | `users/twitch/{twitchId}` | none | Find a user by Twitch username. |
| POST | `users/setProfile` | access token | Body: `{ bio, displayName, links }`. |
| POST | `users/activity` | access token | Body: `{ activity, subactivity, gameId }`. |
| POST | `users/follow/{followId}` | access token | — |
| POST | `users/setPfp` | access token | Body: `{ chain, hash }`. |
| GET | `users/delete` | access token | Delete the calling user. |
| POST | `{gameId}/deathMessage` | access token | Body: `{ text }`. Posts Discord red embed. |
| POST | `{gameId}/smallWin` | access token | Body: `{ text }`. Posts Discord green embed. |
| GET | `savedata/{gameId}/{userId}/{slot}` | access token | Load save. |
| POST | `savedata/{gameId}/{userId}/{slot}` | access token | Create save. |
| PATCH | `savedata/{savedataId}` | access token | Update save. |
| GET | `highscores/{gameId}/{scoretype}/{version}/{from}/{to}` | none | Highscore range. |
| POST | `highscores` | access token | Submit a score. |

### Token semantics

| Token | Lifetime | Use |
|---|---|---|
| Access token (`AccountData.token`) | ~7 days | Bearer auth on every authenticated request. |
| Refresh token (`AccountData.refreshToken`) | ~30 days | Bearer auth on `auth/refresh/{userId}` ONLY. |

Sending the access token to `auth/refresh` will not work — the server validates the refresh-token signature specifically.

## Integration recipes

### Reactively painting a "Sign in" label

```csharp
[SerializeField] TMP_Text _label;

void OnEnable() {
    PixygonApi.Instance.OnLoginStateChanged += Repaint;
    Repaint();
}
void OnDisable() {
    if (PixygonApi.Instance != null)
        PixygonApi.Instance.OnLoginStateChanged -= Repaint;
}
void Repaint() {
    var online = Application.internetReachability != NetworkReachability.NotReachable;
    var api    = PixygonApi.Instance;
    var u      = api?.AccountData?.user;
    _label.text = !online            ? "Offline"
                : api.IsLoggedIn && u != null
                                     ? (string.IsNullOrEmpty(u.displayName) ? u.userName : u.displayName)
                                     : "Sign in";
}
```

### Gating a feature on game ownership

```csharp
[SerializeField] GameObject _premiumPanel;
const string Slug = "dreadwager";

void OnEnable() {
    PixygonApi.Instance.OnLoginStateChanged += Apply;
    Apply();
}
void OnDisable() {
    if (PixygonApi.Instance != null)
        PixygonApi.Instance.OnLoginStateChanged -= Apply;
}
void Apply() {
    _premiumPanel.SetActive(PixygonApi.Instance.OwnsGame(Slug));
}
```

### Posting a death message from gameplay

```csharp
// Called from your Death handler
PixygonApi.Instance.PostDeathMessage(
    gameId: "64e4df726a2585b4af8bce10",
    text:   $"Pixiel {name} fell to {killer.Name} after {seconds:F0}s");
```

### Force a re-fetch after a store purchase

```csharp
// After your store webhook tells you the user just bought a game
await PixygonApi.Instance.FetchOwnedGames();
// OnLoginStateChanged fires automatically once the response lands —
// every entitlement-gated UI repaints on its own.
```

## Dependencies

| Package | Hard / Soft | Why |
|---|---|---|
| `com.pixygon.saving` | Hard | `SaveManager.SettingsSave._user` is the cross-package handoff for the signed-in account. |
| `com.pixygon.nft` | Hard (legacy) | Wallet-patch endpoints + ItemBox helpers still live here. Will be split out in a future release. |
| `UnityEngine.Networking` | Built-in | All HTTP via `UnityWebRequest`. WebSockets not used. |
| TextMeshPro | Built-in | Account screen text. |

## Building for WebGL

`UnityWebRequest` works in WebGL with no extra setup. The only WebGL-specific footnote is that the host page can optionally expose `window.pixygonGameOwnership` as a fast-path for ownership checks; consumers like Dreadwager wrap that in a `.jslib` and prefer it over the REST round-trip when available. The package itself doesn't ship that jslib — it's a per-game integration concern.

## License

See `Third Party Notices.md` for third-party licenses used by the package.

The package source is distributed under the terms listed at <https://www.pixygon.io/dev/passport/license>.

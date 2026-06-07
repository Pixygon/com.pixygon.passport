# Pixygon — Passport

The **account spine**: login/registration, tokens, owned games (entitlements), the
account-owned **ItemBox**, cloud saves, and social. Every game authenticates and
checks ownership through here.

## Overview

`PixygonApi` (a `DontDestroyOnLoad` singleton) talks to `https://api.pixygon.io/v1/`.
It owns the login lifecycle, persists a remember-me token, and exposes the account's
owned games, item box, saves, and social graph. `AccountUI` + its panels are the
drop-in login/registration/verification/password-reset UI.

## Key types

| Type | What it is |
|---|---|
| **`PixygonApi`** | Singleton API: `StartLogin/StartSignup/StartLogout`, token refresh, `OwnedGameIds` + `OwnsGame(id)`, **ItemBox** (`DepositItems`/`WithdrawItems`/`GetItems`/`MintAsset`), cloud saves (`GetSave/PostSave`), `PostDeathMessage`/`PostSmallWin`, follow/search, `OnLoginStateChanged`. |
| **`AccountUI`** | Login flow controller + `LoginState`. |
| **`LoginPanel` / `RegisterPanel` / `VerificationPanel` / `ForgotPasswordRequestPanel` / `ResetPasswordPanel` / `ErrorPanel`** | The login UI screens. |
| **`LoginToken` / `ErrorResponse`** | Auth response + error payloads. |
| **`PassportBadge` / `PassportCard`** | Account display widgets. |
| **`LoginData` / `SignupData` / `VerifyData` / `RecoveryData` / …** | Request bodies. |

## Dependencies

`com.pixygon.saving` (stores the `AccountData` snapshot via `SaveManager`).

## Usage

```csharp
PixygonApi.Instance.StartLogin(user, pass, rememberMe: true, onLogin, onFail);
if (PixygonApi.Instance.OwnsGame(gameId)) { /* unlock paid content */ }
await PixygonApi.Instance.DepositItems(items);   // account-owned ItemBox
```

## Status

`0.5.0`. The platform's identity + entitlement backbone. **Platform note:** the
**ItemBox** (`Deposit/Withdraw/Mint`) is how account-owned **Animas** travel between
games (`com.pixygon.anima` — "your animas are yours"); `AccountData` is the seed for a
cross-game `profile` package. Subscribe to `OnLoginStateChanged` in `OnEnable` /
unsubscribe in `OnDisable` — don't poll.

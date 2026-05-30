using System;
using UnityEngine;

namespace Pixygon.Passport {
    /// <summary>
    /// The flow the user was in when the error happened. Different
    /// contexts produce different humanized messages — a 404 during
    /// login means "we couldn't find that account", while a 404 during
    /// verification means "the verification link expired".
    /// </summary>
    public enum LoginErrorContext {
        Login,
        Signup,
        Verification,
        PasswordRecovery,
    }

    /// <summary>
    /// Result of humanizing a server error — what to show in the error
    /// dialog's title and body. <see cref="Body"/> is always plain prose;
    /// raw server JSON / HTTP errors are stripped before reaching the UI.
    /// </summary>
    public readonly struct HumanizedError {
        public readonly string Title;
        public readonly string Body;

        public HumanizedError(string title, string body) {
            Title = title;
            Body = body;
        }
    }

    /// <summary>
    /// Turns <see cref="ErrorResponse"/> instances into friendly,
    /// context-aware messages. Replaces raw server JSON like
    /// <c>{ "msg": "Invalid Credentials" }</c> with prose like
    /// <c>"Wrong username or password — try again."</c>
    ///
    /// <para>Strategy: pattern-match the response body for known phrases,
    /// then fall back to UWR-style status code inference, then to a
    /// generic context-appropriate message. Always returns SOMETHING —
    /// never null, never empty.</para>
    /// </summary>
    public static class LoginErrorHumanizer {
        /// <summary>
        /// Friendly title + body for the given error in the given context.
        /// Safe to call with a null error (returns the generic "something
        /// went wrong" form).
        /// </summary>
        public static HumanizedError Humanize(LoginErrorContext context, ErrorResponse error) {
            var title = DefaultTitle(context);
            var rawCode = error?._code ?? string.Empty;
            var rawBody = error?._msg ?? string.Empty;

            // 1) No internet — UnityWebRequest reports this as "Cannot
            //    connect to destination host" or similar. Catch it before
            //    we try to read a body that doesn't exist.
            if (LooksLikeNetwork(rawCode, rawBody)) {
                return new HumanizedError(title, "Couldn't reach Pixygon. Check your internet connection and try again.");
            }

            // 2) Body pattern matches — the most precise signal we have.
            //    These cover the common server replies. Case-insensitive.
            var lowerBody = rawBody.ToLowerInvariant();
            if (lowerBody.Contains("invalid credentials") ||
                lowerBody.Contains("incorrect password") ||
                lowerBody.Contains("wrong password")) {
                return new HumanizedError(title, "Wrong username or password. Try again.");
            }
            if (lowerBody.Contains("user not found") ||
                lowerBody.Contains("no such user") ||
                lowerBody.Contains("account not found")) {
                return new HumanizedError(title,
                    context == LoginErrorContext.Login
                        ? "We couldn't find an account with that name. Check spelling or sign up instead."
                        : "We couldn't find that account.");
            }
            if (lowerBody.Contains("user exists") ||
                lowerBody.Contains("already taken") ||
                lowerBody.Contains("already registered") ||
                lowerBody.Contains("duplicate")) {
                return new HumanizedError(title, "That username or email is already taken. Try signing in instead.");
            }
            if (lowerBody.Contains("not verified") ||
                lowerBody.Contains("verify your email") ||
                lowerBody.Contains("email not verified")) {
                return new HumanizedError(title, "Your account isn't verified yet — check your email for the verification code.");
            }
            if (lowerBody.Contains("verification") &&
                (lowerBody.Contains("invalid") || lowerBody.Contains("expired") || lowerBody.Contains("incorrect"))) {
                return new HumanizedError(title, "That verification code didn't match. Try again, or request a new one.");
            }
            if (lowerBody.Contains("recovery") && lowerBody.Contains("expired")) {
                return new HumanizedError(title, "The recovery link has expired. Request a new one and try again.");
            }
            if (lowerBody.Contains("password") && lowerBody.Contains("weak")) {
                return new HumanizedError(title, "That password is too easy to guess. Try a longer one with mixed characters.");
            }
            if (lowerBody.Contains("rate limit") || lowerBody.Contains("too many")) {
                return new HumanizedError(title, "Too many attempts in a short time. Wait a minute and try again.");
            }

            // 3) Status-code inference from UnityWebRequest.error string.
            //    UWR formats it as "HTTP/1.1 401 Unauthorized" etc.
            var status = ExtractStatusCode(rawCode);
            switch (status) {
                case 400:
                    return new HumanizedError(title, ContextDefault400(context));
                case 401:
                case 403:
                    return new HumanizedError(title, "Wrong username or password. Try again.");
                case 404:
                    return new HumanizedError(title,
                        context == LoginErrorContext.Login
                            ? "We couldn't find an account with that name."
                            : "Account not found.");
                case 409:
                    return new HumanizedError(title, "That username or email is already taken.");
                case 422:
                    return new HumanizedError(title, "Some of the details you entered don't look right. Double-check and try again.");
                case 429:
                    return new HumanizedError(title, "Too many attempts. Wait a minute and try again.");
                case 500:
                case 502:
                case 503:
                case 504:
                    return new HumanizedError(title, "Pixygon's server is having a moment. Try again in a few seconds.");
            }

            // 4) Last resort — context-appropriate generic message. Never
            //    leak the raw response body to the user.
            return new HumanizedError(title, GenericFor(context));
        }

        private static string DefaultTitle(LoginErrorContext context) {
            switch (context) {
                case LoginErrorContext.Login: return "Couldn't sign in";
                case LoginErrorContext.Signup: return "Couldn't create account";
                case LoginErrorContext.Verification: return "Couldn't verify";
                case LoginErrorContext.PasswordRecovery: return "Couldn't reset password";
                default: return "Something went wrong";
            }
        }

        private static string GenericFor(LoginErrorContext context) {
            switch (context) {
                case LoginErrorContext.Login: return "Something went wrong signing you in. Try again in a moment.";
                case LoginErrorContext.Signup: return "Something went wrong creating your account. Try again in a moment.";
                case LoginErrorContext.Verification: return "Something went wrong verifying your code. Try again, or request a new one.";
                case LoginErrorContext.PasswordRecovery: return "Something went wrong resetting your password. Try again, or contact support.";
                default: return "Something went wrong. Try again.";
            }
        }

        private static string ContextDefault400(LoginErrorContext context) {
            switch (context) {
                case LoginErrorContext.Signup: return "Some of the signup details don't look right. Check your username, email, and password.";
                case LoginErrorContext.Verification: return "That verification code didn't match. Try again.";
                case LoginErrorContext.PasswordRecovery: return "We couldn't process that recovery request. Try again.";
                default: return "We couldn't process that request. Try again.";
            }
        }

        private static bool LooksLikeNetwork(string code, string body) {
            if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(body)) return true;
            var lower = (code + " " + body).ToLowerInvariant();
            return lower.Contains("cannot connect")
                || lower.Contains("cannot reach")
                || lower.Contains("name resolution")
                || lower.Contains("dns")
                || lower.Contains("network is unreachable")
                || lower.Contains("ssl handshake")
                || lower.Contains("certificate")
                || lower.Contains("connection refused")
                || lower.Contains("connection timed out")
                || lower.Contains("request timed out");
        }

        /// <summary>
        /// Pull a numeric HTTP status out of UWR's error string, which is
        /// formatted like "HTTP/1.1 401 Unauthorized". Returns 0 when no
        /// status is present.
        /// </summary>
        private static int ExtractStatusCode(string s) {
            if (string.IsNullOrEmpty(s)) return 0;
            // Look for the first 3-digit number in the range 100..599.
            for (var i = 0; i < s.Length - 2; i++) {
                if (!char.IsDigit(s[i])) continue;
                if (!char.IsDigit(s[i + 1])) continue;
                if (!char.IsDigit(s[i + 2])) continue;
                var n = (s[i] - '0') * 100 + (s[i + 1] - '0') * 10 + (s[i + 2] - '0');
                if (n >= 100 && n <= 599) return n;
            }
            return 0;
        }
    }
}

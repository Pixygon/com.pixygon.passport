using System;
using UnityEngine;

namespace Pixygon.Micro {
    /// <summary>
    /// Deprecated no-op stub. Was the wallet-discovery bridge for the legacy
    /// Pixygon NFT integration (WAX / Ethereum / Tezos). NFT features have
    /// been removed from <c>Pixygon.Passport</c>; this component remains
    /// only so existing scenes that still have a "WalletFetcher" GameObject
    /// (and WebGL <c>main.jslib</c> calls to
    /// <c>SendMessage('WalletFetcher', 'GotXxxWallet', …)</c>) keep
    /// loading cleanly. All callbacks log a one-shot warning and do nothing.
    /// </summary>
    [Obsolete("Pixygon NFT wallet bridge removed. Drop the WalletFetcher GameObject + the matching .jslib SendMessage calls.")]
    public class WalletFetcher : MonoBehaviour {
        public Action<int, string> _onComplete; // legacy field, unused

        /// <summary>Legacy entry point — kept so consumers compile. No-op.</summary>
        public void GetWallet(int chain, int walletProvider, Action<int, string> onComplete) {
            WarnOnce(nameof(GetWallet));
        }

        // Methods called by WebGL jslib SendMessage. Must exist or Unity
        // logs "SendMessage GotWaxWallet has no receiver" on every login
        // attempt — keep them as silent no-ops.
        public void GotWaxWallet(string wallet) { WarnOnce(nameof(GotWaxWallet)); }
        public void GotEthWallet(string wallet) { WarnOnce(nameof(GotEthWallet)); }
        public void GotTezWallet(string wallet) { WarnOnce(nameof(GotTezWallet)); }

        // One warning per process per method, not per call.
        private static readonly System.Collections.Generic.HashSet<string> s_warned
            = new System.Collections.Generic.HashSet<string>();
        private static void WarnOnce(string name) {
            if (s_warned.Add(name))
                Debug.LogWarning($"[WalletFetcher] {name} fired but Pixygon NFT integration was removed — no-op.");
        }
    }
}

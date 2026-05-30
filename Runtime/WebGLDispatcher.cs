using System;

namespace Pixygon.Micro {
    /// <summary>
    /// Deprecated no-op stub. Was the WebGL DllImport bridge for the legacy
    /// Pixygon NFT wallet login flows (WAX / Anchor / Ethereum / Tezos).
    /// NFT features have been removed from <c>Pixygon.Passport</c>; the
    /// class remains so any consumer that still references it compiles,
    /// but every method is a no-op and the underlying .jslib hooks are
    /// no longer required.
    ///
    /// <para>Safe to delete entirely once the consumer code is cleaned up.</para>
    /// </summary>
    [Obsolete("Pixygon NFT WebGL wallet bridge removed.")]
    public static class WebGLDispatcher {
        public static void Wax_Login() { }
        public static void Anchor_Login() { }
        public static void Eth_Login() { }
        public static void Tez_Login() { }
    }
}

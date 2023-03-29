using System;
using Pixygon.NFT;
using UnityEngine;

namespace Pixygon.Micro {
    public class WalletFetcher : MonoBehaviour {
        public Action<Chain, string> _onComplete;
        public void GetWallet(NFT.Chain chain, int walletProvider, Action<Chain, string> onComplete) {
            #if UNITY_WEBGL
            _onComplete = onComplete;
            switch (chain) {
                case Chain.Wax:
                    if(walletProvider == 0)
                        WebGLDispatcher.Wax_Login();
                    if(walletProvider == 1)
                        WebGLDispatcher.Anchor_Login();
                    break;
                case Chain.EOS:
                    break;
                
                case Chain.Ethereum:
                    WebGLDispatcher.Eth_Login();
                    break;
                case Chain.Tezos:
                    WebGLDispatcher.Tez_Login();
                    break;
                case Chain.Polygon:
                    break;
                case Chain.Solana:
                    break;
                case Chain.Flow:
                    break;
            }
            #endif
        }
        public void GotWaxWallet(string wallet) {
            _onComplete.Invoke(Chain.Wax, wallet);
            //MicroController._instance.SetWallet(Chain.Wax, wallet);
        }
        public void GotEthWallet(string wallet) {
            _onComplete.Invoke(Chain.Ethereum, wallet);
            //MicroController._instance.SetWallet(Chain.Ethereum, wallet);
        }
        public void GotTezWallet(string wallet) {
            _onComplete.Invoke(Chain.Tezos, wallet);
            //MicroController._instance.SetWallet(Chain.Tezos, wallet);
        }
    }
}
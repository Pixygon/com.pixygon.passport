using System.Runtime.InteropServices;

namespace  Pixygon.Micro {
    public static class WebGLDispatcher {
#if UNITY_WEBGL
    [DllImport("__Internal")] public static extern void Wax_Login();
    [DllImport("__Internal")] public static extern void Anchor_Login();
    [DllImport("__Internal")] public static extern void Eth_Login();
    [DllImport("__Internal")] public static extern void Tez_Login();
        
    //[DllImport("__Internal")] private static extern string Eth_WalletAddress();

    //[DllImport("__Internal")] public static extern void Tezos_DAppClient();
    //[DllImport("__Internal")] public static extern void Tezos_RequestPermission();
    //[DllImport("__Internal")] public static extern void Tezos_SendTez();
    //[DllImport("__Internal")] public static extern void Tezos_APICall();
    //[DllImport("__Internal")] public static extern string Tezos_TokenAddressString();
    //[DllImport("__Internal")] public static extern string Tezos_MyAddressString();
#endif
    }
}
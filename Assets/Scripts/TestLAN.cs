using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine.Networking;

public class TestLAN : NetworkDiscovery
{
    public event System.Action<string, string> OnReceive = delegate { };

    public override void OnReceivedBroadcast(string fromAddress, string data)
    {
        //NetworkManager.singleton.networkAddress = fromAddress;
        //NetworkManager.singleton.StartClient();

        OnReceive(fromAddress, data);
    }
}

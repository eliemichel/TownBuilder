using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MasterController : MonoBehaviour
{
    public GameObject splashScreen;
    public MarchingModuleManager moduleManager;

    private void Update()
    {
        if (splashScreen.activeInHierarchy)
        {
            if (moduleManager.AreAllRenderersReady())
            {
                splashScreen.SetActive(false);
            }
        }
    }
}

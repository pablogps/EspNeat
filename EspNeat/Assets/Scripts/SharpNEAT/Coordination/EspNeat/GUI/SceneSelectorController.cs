using UnityEngine;
using System.Collections;

public class SceneSelectorController : MonoBehaviour {

	private int sceneSelector;

    public void ChangeScene(int newSceneSelector)
    {
        sceneSelector = newSceneSelector;

        switch (sceneSelector)
        {
        case 0:
            Application.LoadLevel("XOR");
            break;
        case 1:
            Application.LoadLevel("CheapLabourSimple");
            break;
        case 2:
            Application.LoadLevel("CarScene");
            break;
        }
    }
}

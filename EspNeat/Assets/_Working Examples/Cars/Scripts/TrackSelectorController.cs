using UnityEngine;
using System.Collections;

public class TrackSelectorController : MonoBehaviour {

	private int sceneSelector;

    public void ChangeScene(int newSceneSelector)
    {
        sceneSelector = newSceneSelector;

        switch (sceneSelector)
        {
        case 0:
            Application.LoadLevel("CarScene");
            break;
        case 1:
            Application.LoadLevel("CarSceneLights");
            break;
        case 2:
            Application.LoadLevel("CarSceneJunctionsEasy");
            break;
        case 3:
            Application.LoadLevel("CarSceneComplete");
            break;
        }
    }
}

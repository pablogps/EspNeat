using UnityEngine;
using System.Collections;
using SharpNeat.Coordination;

public class AutoEvolutionSecretController : MonoBehaviour {
    
    private UImanager uiManager;

    // Use this for initialization
    void Awake()
    {
        uiManager = GameObject.Find("Evaluator").GetComponent<UImanager>();
    }

    public void StartAuto()
    {
        uiManager.LaunchAutoSecretFunction();
    }

    public void StopEvolution()
    {
        uiManager.StopAutoSecretFunction();
    }

    public void UseEvolutionCamera()
    {
		uiManager.UseEvolutionCameraSecretFunction();
    }

    public void UseEditingCamera()
	{
		uiManager.UseEditingCameraSecretFunction();
    }
}

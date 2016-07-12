using UnityEngine;
using System.Collections;
using SharpNeat.Coordination;

public class ApplyChangesButtonController : MonoBehaviour {

	private UImanager uiManager;

	// Use this for initialization
	void Awake()
    {
		uiManager = GameObject.Find("Evaluator").GetComponent<UImanager>();
	}
	
    /// <summary>
    /// We could avoid any code at all and simply call the RunBest method from
    /// the button. However, this offers more control (for example, to allow the
    /// button to either start a simulation or stop it, depending on the current
    /// status).
    /// </summary>
    public void ApplyChanges()
    {
		uiManager.ApplyChanges();
    }
}

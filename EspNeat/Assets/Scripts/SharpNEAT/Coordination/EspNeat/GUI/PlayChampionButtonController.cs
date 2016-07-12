using UnityEngine;
using System.Collections;

public class PlayChampionButtonController : MonoBehaviour {

    private EspNeatOptimizer optimizer;

	// Use this for initialization
	void Awake()
    {
        optimizer = GameObject.Find("Evaluator").GetComponent<EspNeatOptimizer>();
	}
	
    /// <summary>
    /// We could avoid any code at all and simply call the RunBest method from
    /// the button. However, this offers more control (for example, to allow the
    /// button to either start a simulation or stop it, depending on the current
    /// status).
    /// </summary>
    public void PlayBest()
    {
        optimizer.RunBest();
    }

    public void DeleteBest()
    {
        optimizer.DestroyBest();
    }
}

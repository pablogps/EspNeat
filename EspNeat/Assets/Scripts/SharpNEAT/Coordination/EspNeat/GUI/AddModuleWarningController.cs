using UnityEngine;
using System.Collections;
using SharpNeat.Coordination;

public class AddModuleWarningController : MonoBehaviour {

	private RegModuleController regModuleController = null;

    public RegModuleController RegModuleController
	{
        set { regModuleController = value; }
	}

    /// <summary>
    /// Proceeds with the operation (adds the module to the regulation module).
    /// </summary>
	public void AddModule()
	{
        regModuleController.AddModule();
	}

    /// <summary>
    /// The operation is cancelled: the module that had been moved over a
    /// regulation module is moved away.
    /// </summary>
	public void MoveAwayModule()
	{
        regModuleController.MoveModuleAway();
	}
}

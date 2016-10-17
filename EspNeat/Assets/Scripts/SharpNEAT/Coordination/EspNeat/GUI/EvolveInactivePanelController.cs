using UnityEngine;
using System.Collections;
using SharpNeat.Coordination;

public class EvolveInactivePanelController : MonoBehaviour {

    private ModuleController myModuleController;

    public ModuleController MyModuleController
    {
        set { myModuleController = value; }
    }

    /// <summary>
    /// Accepts starting a new evolutionary process.
    /// </summary>
    public void ProceedEvolve()
    {
        myModuleController.ProceedEvolve();
        // Destroy(this.gameObject);
    }

    /// <summary>
    /// Destroys the warning panel from the options panel (and resets the
    /// reference to null). If we do not care about the reference (it is not
    /// really needed) the warning panel could be directly eliminated from
    /// here with Destroy(this.gameObject)
    /// </summary>
    public void SelfDestroy()
    {
        // Destroy(this.gameObject);
        myModuleController.DestroyEvolutionWarning();
    }
}

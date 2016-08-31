using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using SharpNeat.Coordination;

public class OptionsPanelController : MonoBehaviour {

	private ModuleController moduleController;
    private GameObject warningDeletePanel;
    private GameObject warningEvolvePanel;

	#region PublicMethods

    /// <summary>
    /// Gets a copy of the module controller
    /// </summary>
    public void SetModuleController(ModuleController newModuleController)
    {
        moduleController = newModuleController;
    }

	/// <summary>
	/// Hides this panel (as opposed to creating and destroying it).
	/// </summary>
	public void HidePanel()
	{
		moduleController.HideOptions();
	}

    /// <summary>
    /// Delete option requires confirmation from the user, so a warning
    /// panel is instantiated.
    /// </summary>
	public void InstantiateDeleteWarning()
    {
        warningDeletePanel = InstantiateWarning("Prefabs/DeleteWarningPanel");
        warningDeletePanel.GetComponent<DeleteWarningPanelController>().OptionsPanel = this;
	}

    /// <summary>
    /// Instantiates a panel warning this is not the active module (so starting
    /// evolution here will lose population diversity for the current active
    /// module!)
    /// </summary>
    public void InstantiateEvolveWarning()
    {
        warningEvolvePanel = InstantiateWarning("Prefabs/EvolveInactiveWarning");
        warningEvolvePanel.GetComponent<EvolveInactivePanelController>().OptionsPanel = this;
    }

    /// <summary>
    /// Accepts delete and asks the module controller to move the order
    /// forward in the command chain.
    /// </summary>
    public void ProceedDelete()
    {
        moduleController.CallDelete();
        DestroyDeleteWarning();
	}

    /// <summary>
    /// Ask moduleController to deliver the order to clone this module.
    /// </summary>
    public void AskClone()
    {
        string moduleId = moduleController.ModuleId.ToString();
        moduleController.UiManager.WriteToRecord("Clone " + moduleId);

        moduleController.AskClone();
        HidePanel();
    }

    /// <summary>
    /// Tries to start an evolutionary process. If this is the active module
    /// simply asks to continue the process. Otherwise a panel warns that
    /// population diversity will be lost. If accepted, a new process is
    /// created and the previously active module simply takes the champion
    /// genome for all individuals in the population.
    /// </summary>
    public void TryEvolve()
    {
        if (moduleController.IsActive)
        {
            HidePanel();
            moduleController.Evolve();
        }
        else
        {
            HidePanel();
            InstantiateEvolveWarning();
        }        
    }

    /// <summary>
    /// Proceeds with the creation of a new evolutionary process (for an old
    /// module)
    /// </summary>
    public void ProceedEvolve()
    {
        moduleController.Evolve();
        DestroyEvolutionWarning();
    }

    /// <summary>
    /// Destroys the evolution warning panel
    /// </summary>
    public void DestroyEvolutionWarning()
    {
        Destroy(warningEvolvePanel);
        // I believe this is not necessary in Unity/C#, but is it a good
        // practice as it was in C++?
        // Note that otherwise the object could be directly deleted from its
        // own script with Destroy(this.gameObject)
        warningEvolvePanel = null;
    }

    /// <summary>
    /// Destroys the warning panel (another possibility is to activate and
    /// deactivate it at convenience).
    /// </summary>
	public void DestroyDeleteWarning()
	{
        Destroy(warningDeletePanel);
        // I believe this is not necessary in Unity/C#, but is it a good
        // practice as it was in C++?
        // Note that otherwise the object could be directly deleted from its
        // own script with Destroy(this.gameObject)
        warningDeletePanel = null;
	}

    #endregion

    /// <summary>
    /// D
    /// </summary>
    GameObject InstantiateWarning(string prefabAddress)
    {
        GameObject myPrefab = (GameObject)Resources.Load(prefabAddress);
        GameObject panel = (GameObject)Instantiate(myPrefab);

        // Used to ensure correct orientation
        moduleController.UiManager.SetUpPanel(panel, myPrefab);
        // But SetUpPanel leaves the gameObject inactive!
        panel.SetActive(true);

        return panel;
    }
}

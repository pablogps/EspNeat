using UnityEngine;
using System.Collections;
using SharpNeat.Coordination;

public class DeleteWarningPanelController : MonoBehaviour {

	private OptionsPanelController optionsPanel;

	public OptionsPanelController OptionsPanel
	{
		set { optionsPanel = value; }
	}

    /// <summary>
    /// Accepts delete and asks the module controller to move the order
    /// forward in the command chain.
    /// </summary>
	public void ProceedDelete()
	{
        optionsPanel.ProceedDelete();
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
        optionsPanel.DestroyDeleteWarning();
	}
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using SharpNeat.Coordination;

public class RegulationPanelController : MonoBehaviour {

	private ModuleController moduleController;
    private Dropdown inputSelector;
    private Text activationText;
    private int pandemoniumGroup;
    private Text pandemLabel;
    private bool activeWhenInputActive;

    void Awake()
    {
        inputSelector = transform.Find("InputSelectorDropdown").GetComponent<Dropdown>();
		activationText = transform.Find("ButtonInputOnOff").
                         Find("ButtonInputOnOffText").GetComponent<Text>();
        pandemLabel = transform.Find("PandemoniumGroup").Find("PandemLabel").
                      GetComponent<Text>();
    }

    #region Properties

	public ModuleController ModuleController
	{
		set { moduleController = value; }
	}

    #endregion

    #region PublicMethods

    /// <summary>
    /// Hides this panel (as opposed to creating and destroying it).
    /// </summary>
	public void HidePanel()
	{
		moduleController.HideRegulation();
	}

    /// <summary>
    /// Gets the list with the labels for all the inputs in the system, so that
    /// they can be offered in the dropdown menu.
    /// </summary>
    public void GetInputList(List<string> localIn)
    {
        inputSelector.AddOptions(localIn);
    }

    /// <summary>
    /// Use this to manually update the dropdown value (input used for regulation
    /// in basic regulation mode), used to set the initial value.
    /// </summary>
    public void SetDropdownValue(int newValue)
    {
        inputSelector.value = newValue;
    }
        
    /// <summary>
    /// This module will be active when the selected input is active.
    /// </summary>
    public void SetActiveWhenActive()
    {
        activeWhenInputActive = true;
        activationText.text = "active";
    }

    /// <summary>
    /// This module will be active when the selected input is inactive.
    /// </summary>
    public void SetActiveWhenInactive()
    {
        activeWhenInputActive = false;
        activationText.text = "inactive";
    }

    /// <summary>
    /// Toggles between "active when input is active" and "active when input
    /// is inactive" modes.
    /// </summary>
    public void ToggleActivationRegime()
    {
        if (activeWhenInputActive)
        {
            SetActiveWhenInactive();
        }
        else
        {
            SetActiveWhenActive();
        }
        UpdateBasicRegulation();
    }

    /// <summary>
    /// Creates the desired regulation scheme
    /// </summary>
    private void UpdateBasicRegulation()
    {
        // First resets the regulation list
        moduleController.ResetRegulation();

        // Is it "Active when X is active" or when "X is inactive"?
        if (activeWhenInputActive)
        {
            // "Active when X is active" is the easiest case:
            moduleController.AddInputToReg(inputSelector.value, 1.0);
        }
        else
        {
            // First checks the special case for "active when bias is inactive"
            // In this case we do not want 2 different connections from bias
            // which would be problematic, because they would share Id!
            if (inputSelector.value == 0)
            {
                // This should never be active, so it uses weight 0
                moduleController.AddInputToReg(inputSelector.value, 0.0);
            }
            else
            {
                // This is the normal case. +1 regulation with bias and -1
                // from the selected input (so when it is active the result
                // is +1 -1 = 0 --> no activation)

                // First adds the auxiliary connection from bias
                moduleController.AddInputToReg(0, 1.0);
                // Then the connection from the selected input, with weight -1
                moduleController.AddInputToReg(inputSelector.value, -1.0);
            }
        }

        // Asks module controller to pass the new regulation scheme to
        // UImanager.
        moduleController.PassRegulation();
    }

    /// <summary>
    /// Receives the pandemonium group 
    /// </summary>
    public void SetPandemoniumValue(int newPandem)
    {
        pandemoniumGroup = newPandem;
        string pandemString = "";
        if (pandemoniumGroup == 0)
        {
            pandemString = "N";
        }
        else
        {
            pandemString = pandemoniumGroup.ToString();
        }
        UpdatePandemoniumLabel(pandemString);
    }

    public void PandemoniumUp()
    {
        ++pandemoniumGroup;
        SetPandemoniumValue(pandemoniumGroup);
        moduleController.PassPandemonium(pandemoniumGroup);
    }

    public void PandemoniumDown()
    {
        --pandemoniumGroup;
        if (pandemoniumGroup < 0)
        {
            pandemoniumGroup = 0;
        }
        SetPandemoniumValue(pandemoniumGroup);
        moduleController.PassPandemonium(pandemoniumGroup);
    }

    public void UseAdvancedRegulation()
    {
        moduleController.BasicRegulation = false;
        HidePanel();
    }

    #endregion

    /// <summary>
    /// Allows to load or change the pandemonium label
    /// </summary>
    void UpdatePandemoniumLabel(string newLabel)
    {
        pandemLabel.text = "Pandemonium:      " + newLabel;
    }
}

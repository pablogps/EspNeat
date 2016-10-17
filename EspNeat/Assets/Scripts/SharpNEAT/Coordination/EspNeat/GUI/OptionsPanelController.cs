using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using SharpNeat.Coordination;

public class OptionsPanelController : MonoBehaviour {

	private ModuleController moduleController;
    private GameObject warningDeletePanel;

    // The slider allows to change the weights with which modules are combined.
    private Slider slider = null;

    private GameObject regulationElements = null;
    private GameObject menuBackground = null;
    private Toggle alwaysActiveToggle = null;
    private Text alwaysActiveToggleText = null;

    // Children objects of regulationElements:
    private Dropdown inputSelector;
    private Text activationText;
    private int pandemoniumGroup;
    private Text pandemLabel;
    private bool activeWhenInputActive;

    // Only used for regulation modules:
    private Toggle onlyOneToggle = null;
    private Toggle combineToggle = null;
    private bool isCombine = false;

    // All methods (public or not) regarding regulation are in Regulation region
	#region PublicMethods

    void Awake()
    {
        // Reference to the slider
        if (transform.Find("Slider"))
        {
            slider = transform.Find("Slider").GetComponent<Slider>();            
        }

        regulationElements = transform.Find("RegulationElements").gameObject;
        menuBackground = transform.Find("MenuBackGround").gameObject;

        alwaysActiveToggle = transform.Find("AlwaysActiveToggle").GetComponent<Toggle>();
        alwaysActiveToggleText = transform.Find("AlwaysActiveToggle").
                Find("Label").GetComponent<Text>();

        // Here we access some elements within regulationElements:
        inputSelector = regulationElements.transform.Find("InputSelectorDropdown").
                GetComponent<Dropdown>();
        activationText = regulationElements.transform.Find("ButtonInputOnOff").
                Find("ButtonInputOnOffText").GetComponent<Text>();
        pandemLabel = regulationElements.transform.Find("PandemoniumGroup").
                Find("PandemLabel").GetComponent<Text>();    
    }

    /// <summary>
    /// Gets a copy of the module controller
    /// </summary>
    public void SetModuleController(ModuleController newModuleController)
    {
        moduleController = newModuleController;

        // In regulation modules we need references to two toggles:
        if (moduleController.IsRegModule)
        {  
            onlyOneToggle = transform.Find("OnlyOneToggle").GetComponent<Toggle>();
            combineToggle = transform.Find("CombineToggle").GetComponent<Toggle>();
        }
    }

    /// <summary>
    /// Used to (safely) set the value of the slider.
    /// </summary>
    public void SetSliderValue(float newValue)
    {
        if (slider != null)
        { 
            slider.value = (float)newValue;           
        }
    }

    /// <summary>
    /// Using the slider, gets a new weight for all output connections. 
    /// </summary>
    public void UpdateWeights()
    {        
        moduleController.newOutputWeightBasic((double)slider.value);
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
        warningDeletePanel = moduleController.InstantiateWarning("Prefabs/DeleteWarningPanel");
        warningDeletePanel.GetComponent<DeleteWarningPanelController>().OptionsPanel = this;
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

    #region Regulation

    public void ActivationLabelAsChild()
    {
        alwaysActiveToggle.interactable = false;
        alwaysActiveToggleText.text = "Activation controlled by\nparent";
        menuBackground.GetComponent<RectTransform>().sizeDelta = new Vector2(192f, 125f);
    }

    /// <summary>
    /// Toggles regulation optioins (otherwise shows "always active").
    /// Changes the background size to make space for the new elements.
    /// </summary>
    public void ToggleRegulationButton(bool alwaysActive)
    {
        if (alwaysActive)
        {
            regulationElements.SetActive(false);
            menuBackground.GetComponent<RectTransform>().sizeDelta =
                new Vector2(192f, 109f);
            SetActiveWhenActive();
            SetDropdownValue(0);
            UpdateBasicRegulation();
        }
        else
        {
            regulationElements.SetActive(true);
            menuBackground.GetComponent<RectTransform>().sizeDelta =
                new Vector2(192f, 204f);
        }
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
    public void UpdateBasicRegulation()
    {
        // First resets the regulation list
        moduleController.ResetRegulation();

        // Is it "Active when X is active" or when "X is inactive"?
        if (activeWhenInputActive)
        {
            // "Active when X is active" is the easiest case:
            moduleController.AddInputToReg(inputSelector.value, 1.0);

            // We record this action:
            moduleController.UiManager.WriteToRecord(
                "New regulation for module " + moduleController.ModuleId +
                ": when " + inputSelector.value.ToString() + " active");
        }
        else
        {
            // We record this action:
            moduleController.UiManager.WriteToRecord(
                "New regulation for module " + moduleController.ModuleId +
                ": when " + inputSelector.value.ToString() + " inactive");

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

        // We record this action:
        moduleController.UiManager.WriteToRecord(
            "New pandemonium for" + moduleController.ModuleId +
            " now: " + pandemoniumGroup);
    }

    public void PandemoniumDown()
    {
        // We record this action:
        moduleController.UiManager.WriteToRecord("New pandemonium " +
            moduleController.ModuleId);

        --pandemoniumGroup;
        if (pandemoniumGroup < 0)
        {
            pandemoniumGroup = 0;
        }
        SetPandemoniumValue(pandemoniumGroup);
        moduleController.PassPandemonium(pandemoniumGroup);
    }

    /// <summary>
    /// Currently not in use
    /// </summary>
    public void UseAdvancedRegulation()
    {
        moduleController.BasicRegulation = false;
    }

    /// <summary>
    /// Allows to load or change the pandemonium label
    /// </summary>
    void UpdatePandemoniumLabel(string newLabel)
    {
        pandemLabel.text = "Pandemonium:      " + newLabel;
    }

    #endregion

    #region RegulationModules

    /// <summary>
    /// Only one option can be selected, either "only one module" (creates
    /// a pandemonium) or "combine" (regulation is evolved)
    /// </summary>
    public void ToggleOnlyOne(bool value)
    {
        combineToggle.isOn = !value;

        // Note ToggleOnlyOne and ToggleCombine will always be called together
        // (combineToggle.isOn = !value; triggers a call to ToggleCombine()
        // so this is not needed twice!
/*      isCombine = !value;
        ChangeRegulationType();*/
    }
    public void ToggleCombine(bool value)
    {
        onlyOneToggle.isOn = !value;
        isCombine = value;
        ChangeRegulationType();
    }

    /// <summary>
    /// Moves the children of the regulation module into or outside of the
    /// pandemonium group. If they are in the group then only one will be active
    /// at a given time, otherwise their outputs are added.
    /// </summary>
    private void ChangeRegulationType()
    {
        if (isCombine)
        {
            moduleController.ChildrenToNoPandem();
        }
        else
        {
            moduleController.ChildrenToPandem();
        }
    }

    #endregion

}

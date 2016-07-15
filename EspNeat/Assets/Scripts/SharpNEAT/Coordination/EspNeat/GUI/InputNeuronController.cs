using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using SharpNeat.Coordination;

public class InputNeuronController : MonoBehaviour {

    private UImanager uiManager;
    private GameObject inputField;
    private Text label;
    private Text idLabel;
	private int inputId;

	// Use this for initialization
	void Awake()
    {
        inputField = transform.Find("ShowInputFieldButton").transform.
                     Find("InputField").gameObject;
        label = transform.Find("ShowInputFieldButton").transform.
                Find("Label").GetComponent<Text>();
        idLabel = transform.Find("IdLabel").GetComponent<Text>();
		inputId = 0;
    }

    #region Properties

    public UImanager UiManager
    {
        set { uiManager = value; }
    }

    #endregion

    #region PublicMethods

    public void SetInputId(int iD)
    {
        inputId = iD;
        idLabel.text = "I" + iD.ToString();
    }

    /// <summary>
    /// Allows to load or change the module label
    /// </summary>
    public void SetLabel(string newLabel)
    {
        label.text = newLabel;
    }

    /// <summary>
    /// Activates the input field so the label can be updated
    /// </summary>
    public void ShowInputField()
    {
        inputField.SetActive(true);
    }

    /// <summary>
    /// Takes the new label, deactivates the input field and informs UImanager
    /// </summary>
    public void UpdateLabel(string newLabel)
    {
        label.text = newLabel;

        // Sends label back to UImanager
        uiManager.GetNewInputLabel(inputId, newLabel);

        inputField.SetActive(false);
    }

    #endregion
}

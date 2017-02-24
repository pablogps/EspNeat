using UnityEngine;
using System.Collections;
using SharpNeat.Coordination;

/// <summary>
/// This class is also used for the list of outputs, not only for inputs.
/// </summary>
public class InputToggleController : MonoBehaviour {

    private SelectInputPanelController selectInputController;
    private int indexRef;

    public SelectInputPanelController SelectInputController
    {
        set { selectInputController = value; }
    }

    public int IndexRef
    {
        set { indexRef = value; }
    }

    public void ToggleValue()
    {
        selectInputController.ToggleElement(indexRef);
    }
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using SharpNeat.Coordination;

public class SelectInputPanelController : MonoBehaviour {

    UImanager uiManager;

    private bool isRegulationModule;
    List<newLink> inputList;
    List<string> inputLabelsLocal;
    List<string> userGivenLabels;

    List<bool> chosenInputsList;

    #region Properties

    public UImanager UiManager
    {
        set { uiManager = value; }
    }

    public bool IsRegulationModule
    {
        set { isRegulationModule = value; }
    }

    public List<newLink> InputList
    {
        set { inputList = value; }
    }

    public List<string> InputLabelsLocal
    {
        set { inputLabelsLocal = value; }
    }

    #endregion

    #region PublicMethods

    /// <summary>
    /// Receives the complete input lists and creates a local list of bool
    /// elements, with the same size, to easily determine which will be used
    /// and not in the end.
    /// 
    /// userGivenLabels is only used to display complete labels in the toggle
    /// elements. inputLabelsLocal will be passed further, and those simple
    /// labels only identify each input, for instance the third input will
    /// always be "I2" ("I3" if you don't count bias), but may have any
    /// given label such as "bananaCounter".
    /// </summary>
    public void GetCompleteInputList(List<newLink> givenInputList,
                                     List<string> givenInputLabelsLocal,
                                     List<string> givenUserGivenLabels)
    {
        inputList = givenInputList;
        inputLabelsLocal = givenInputLabelsLocal;
        userGivenLabels = givenUserGivenLabels;

        float yOffset = 0f;

        chosenInputsList = new List<bool>();
        for (int i = 0; i < inputList.Count; ++i)
        {
            // All elements start as true!
            chosenInputsList.Add(true);

            // Instantiates the required element for the interface!
            InstantiateToggle(i, yOffset);
            yOffset += 25;
        }
    }

    /// <summary>
    /// Toggles the selection status of an element 
    /// </summary>
    public void ToggleElement(int index)
    {
        if (index < chosenInputsList.Count)
        {
            if (chosenInputsList[index] == true)
            {
                chosenInputsList[index] = false;
            }
            else
            {
                chosenInputsList[index] = true;
            }
        }
        else
        {
            // This should never happen, really
            Debug.Log("Asked to toggle an element not found in chosenInputsList");
        }
    }

    /// <summary>
    /// Makes the final input lists and proceeds with the creation of the module
    /// (which continues in UImanager!)
    /// </summary>
    public void ProceedCreateModule()
    {
        MakeFinalLists();
        uiManager.AddBasicModulePart2(isRegulationModule, inputList, inputLabelsLocal);
        Destroy(this.gameObject);
    }

    #endregion

    #region PrivateMethods

    /// <summary>
    /// Goes through elements in chosenInputsList and removes from the input
    /// lists those that are marked as false. To avoid problems with the
    /// indices we do this backwards (so removing elements from the list
    /// will not affect the position of those not visited yet)
    /// </summary>
    void MakeFinalLists()
    {
        for (int i = chosenInputsList.Count - 1; i >= 0; --i)
        {
            if (chosenInputsList[i] == false)
            {
                inputList.RemoveAt(i);
                inputLabelsLocal.RemoveAt(i);
            }
        }
    }

    void InstantiateToggle(int index, float yOffset)
    {
        // Instantiates the element
        GameObject myPrefab = (GameObject)Resources.Load("Prefabs/InputToggle");
        GameObject inputToggle = (GameObject)Instantiate(myPrefab);

        // Passes a reference to this script an to the index of the input in
        // the lists
        InputToggleController elementController =
            inputToggle.GetComponent<InputToggleController>();
        elementController.IndexRef = index;
        elementController.SelectInputController = this;

        // Sets the correct label
        inputToggle.GetComponentInChildren<Text>().text = userGivenLabels[index];

        // Sets the element as a child of this GameObject
        inputToggle.transform.SetParent(this.transform);
        // Makes sure rotation is correct
        //inputToggle.transform.rotation = myPrefab.transform.rotation;
        inputToggle.transform.localRotation = myPrefab.transform.rotation;
        // Makes sure the position is correct
        Vector3 positionVector = new Vector3(5f, 100f - yOffset, 0f);
        inputToggle.transform.localPosition = positionVector;
        // Makes sure the size is correct!
        inputToggle.transform.localScale = new Vector3(1f, 1f, 1f);
    }

    #endregion
}

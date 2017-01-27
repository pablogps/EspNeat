﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using SharpNeat.Coordination;

/// <summary>
/// This class controls the object for selecting the inputs (and then outputs)
/// of a new module. By default all inputs/outputs are selected.
/// </summary>
public class SelectInputPanelController : MonoBehaviour {

    UImanager uiManager;

    private bool isRegulationModule;
    List<newLink> inputList;
    List<string> inputLabelsLocal;
    List<string> userGivenLabelsIn;

	List<newLink> outputList;
	List<string> outputLabelsLocal;
	List<string> userGivenLabelsOut;

	List<bool> chosenElementsList;

    List<GameObject> toggleElements;

    GameObject nextButton;
    GameObject acceptButton;
    GameObject inputText;
    GameObject outputText;

    int totalSelected;

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

	public List<newLink> OutputList
	{
		set { outputList = value; }
	}

    public List<string> InputLabelsLocal
    {
        set { inputLabelsLocal = value; }
	}

	public List<string> OutputLabelsLocal
	{
		set { outputLabelsLocal = value; }
	}

    #endregion

    void Awake() {
        nextButton = transform.Find("NextButton").gameObject;
        acceptButton = transform.Find("AcceptButton").gameObject;
        inputText = transform.Find("TextInput").gameObject;
        outputText = transform.Find("TextOutput").gameObject;

        acceptButton.gameObject.SetActive(false);
        outputText.gameObject.SetActive(false);
    }

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
                                     List<string> givenUserGivenLabelsIn)
    {
        inputList = givenInputList;
        inputLabelsLocal = givenInputLabelsLocal;
        userGivenLabelsIn = givenUserGivenLabelsIn;

        CreateToggles(inputList, userGivenLabelsIn);
    }

	/// <summary>
	/// Also gets the lists for outputs (which will be displayed AFTER the
	/// inputs)
	/// </summary>
	public void GetCompleteOutputList(List<newLink> givenOutputList,
		                              List<string> givenOutputLabelsLocal,
		                              List<string> givenUserGivenLabelsOut)
	{
		outputList = givenOutputList;
		outputLabelsLocal = givenOutputLabelsLocal;
		userGivenLabelsOut = givenUserGivenLabelsOut;
	}

    /// <summary>
    /// Toggles the selection status of an element 
    /// </summary>
    public void ToggleElement(int index)
    {
		if (index < chosenElementsList.Count)
        {
			if (chosenElementsList[index] == true)
            {
				chosenElementsList[index] = false;
                --totalSelected;
            }
            else
            {
				chosenElementsList[index] = true;
                ++totalSelected;
            }
        }
        else
        {
            // This should never happen, really
            Debug.Log("Asked to toggle an element not found in chosenInputsList");
        }
    }

    /// <summary>
    /// Inputs have been selected. Now we need to choose outputs. Things to do:
    /// ·Get final input list (1)
    /// ·Switch button and text (2)
    /// ·Destroy input toggle elements (3)
    /// ·Create output toggle elements (4)
    /// ·Update chosenElementsList (4)
    /// </summary>
    public void InputDoneNowOutput()
    {
        // At least one input!
        if (totalSelected > 0)
        {
            MakeFinalLists(inputList, inputLabelsLocal);

            // If this is a regulatory module output selection is NOT needed. That
            // will be done automatically (and by the user when a module is dropped
            // within the regulatory module)
            if (isRegulationModule)
            {
                ProceedCreateModule();
            }
            else
            {
                SwitchButtonAndLabel();
                DeleteInputToggles();
                CreateToggles(outputList, userGivenLabelsOut); 
            }            
        }
    }

    /// <summary>
    /// Makes the final input lists and proceeds with the creation of the module
    /// (which continues in UImanager!)
    /// </summary>
    public void ProceedCreateModule()
    {
        // At least one output!
        if (totalSelected > 0)
        {
            // If this is a regulatory module, the output list has been automatically
            // processed (if we attempt to call MakeFinalLists the list for labels
            // will have more entries than the outputList, which is problematic!)
            if (!isRegulationModule)
            {
                MakeFinalLists(outputList, outputLabelsLocal);
            }

            uiManager.AddBasicModulePart2(isRegulationModule, inputList, inputLabelsLocal,
                outputList, outputLabelsLocal);
            Destroy(this.gameObject);            
        }
    }

    /// <summary>
    /// Aborts the process!
    /// </summary>
    /// <returns><c>true</c> if this instance cancel operation; otherwise, <c>false</c>.</returns>
    public void CancelOperation()
    {
        uiManager.AbortNewModule();
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
    void MakeFinalLists(List<newLink> myList1, List<string> myList2)
    {
		for (int i = chosenElementsList.Count - 1; i >= 0; --i)
        {
			if (chosenElementsList[i] == false)
            {
                myList1.RemoveAt(i);
                myList2.RemoveAt(i); 
            }
        }
    }

    void InstantiateToggle(int index, float yOffset, List<string> userLabels)
    {
        // Instantiates the element
        GameObject myPrefab = (GameObject)Resources.Load("Prefabs/InputToggle");
        GameObject inputToggle = (GameObject)Instantiate(myPrefab);

        toggleElements.Add(inputToggle);

        // Passes a reference to this script an to the index of the input in
        // the lists
        InputToggleController elementController =
            inputToggle.GetComponent<InputToggleController>();
        elementController.IndexRef = index;
        elementController.SelectInputController = this;

        // Sets the correct label
        inputToggle.GetComponentInChildren<Text>().text = userLabels[index];

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

    void SwitchButtonAndLabel() {
        acceptButton.gameObject.SetActive(true);
        outputText.gameObject.SetActive(true);   
        nextButton.gameObject.SetActive(false);
        inputText.gameObject.SetActive(false);       
    }

    /// <summary>
    /// Creates the toggles for inputs and outputs.
    /// </summary>
    void CreateToggles(List<newLink> toggleList, List<string> userLabels) {
        toggleElements = new List<GameObject>();
        totalSelected = 0;
        float yOffset = 0f;
        chosenElementsList = new List<bool>();
        for (int i = 0; i < toggleList.Count; ++i)
        {
            // All elements start as true!
            chosenElementsList.Add(true);
            ++totalSelected;

            // Instantiates the required element for the interface!
            InstantiateToggle(i, yOffset, userLabels);
            yOffset += 25;
        }
    }

    void DeleteInputToggles() {
        for (int i = 0; i < toggleElements.Count; ++i)
        {
            Destroy(toggleElements[i]);
        }
    }

    #endregion
}

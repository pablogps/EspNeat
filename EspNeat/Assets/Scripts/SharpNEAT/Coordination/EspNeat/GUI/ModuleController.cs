﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using SharpNeat.Coordination;

public class ModuleController : MonoBehaviour {

    protected UImanager uiManager;

    protected int moduleId = 0;
    protected bool isActive = false;

    protected bool isRegModule = false;

    // We sometimes need to place a module where it does not collide with any
    // others. We use these:
    protected bool colliderAsProbe = false;
    protected int collisions = 0;

    // We need the canvas GameObject so that we can add UI elements with this 
    // canvas as parent. We also need to create other elements using a screen
    // canvas.
    // backgroundCamera is the camera associated with the screen canvas.
    protected GameObject objectCanvas;
    protected GameObject screenCanvas;
    protected Camera backgroundCamera;
    protected int totalInNeurons;
    protected int totalOutNeurons;

    // This is the regulation menu. It will be activated or deactivated (as
    // opposed to instantiated and destroyed).
    protected GameObject regulatoryMenu;
    protected GameObject optionsMenu;

    // Stores a reference for all elements instantiated to represent
    // local inputs and outputs
    protected List<GameObject> myConnections;
    protected List<GameObject> myLocalIOlabels;

    // This is a list with information regarding the regulation of this module
    protected List<newLink> regulatoryInputList;
    // The id of the regulatory neuron is used as reference for the innovation
    // index of other connections. This is also useful to handle regulation
    // modules!
    protected uint regulatoryId;

    // This is a list with output connections from this module. A copy here is
    // useful in case we want to display and modify their weights (for example).
    protected List<newLink> localOutputList;

    private Slider slider = null;

    // Determines whether a basic regulation scheme is being used
    protected bool basicRegulation = true;

    // These are references to the labels, so they can be updated.
    protected Text moduleIdLabel;
    protected Text moduleLabelButtonText;
    protected GameObject moduleLabelInputField;
    protected Text regIdLabel;

    // Even if this is not used here, other objects may need it!
    // (See ReModuleController)
    protected GameObject dragButton;

    // Determines how fast the module will be dragged
    protected float dragFactor = 0.04f;
    // These two are references for child objects
    protected const float length = 250f;
    protected const float leftSide = -160.6f;
    protected Vector3 shortLength = new Vector3(0.4f, 1f, 2.77f);
    protected Vector3 longLength = new Vector3(0.4f, 1f, 6.94f);
    protected Vector3 ioLabelLength = new Vector3(0.43f, 0.43f, 1f);

    #region Start/Update

    /// <summary>
    /// Use this for initialization. Start is executed before the first frame
    /// (which is AFTER other methods are called!)
    /// Note that derived classes (RegModuleController) may want to call
    /// this Awake method and their own! This one should be virtual (and may
    /// be accessed via base.Awake())
    /// </summary>
    protected virtual void Awake () {
        // We need a reference to the screen canvas and the background camera
        // to instantiate and move the regulation panel.
        screenCanvas = GameObject.Find("ScreenSpaceCanvas");
        backgroundCamera = GameObject.Find("BackgroundCamera").GetComponent<Camera>();

        // References to labels and the world-reference canvas 
        objectCanvas = transform.Find("ModuleCanvas").gameObject;
        moduleIdLabel = transform.Find("ModuleCanvas").Find("ModuleIdLabel").
                        GetComponent<Text>();

        moduleLabelButtonText = transform.Find("ModuleCanvas").
                                Find("ModuleLabelButton").Find("ModuleLabel").GetComponent<Text>();

        moduleLabelInputField = transform.Find("ModuleCanvas").
                                Find("ModuleLabelButton").Find("InputField").gameObject;

        regIdLabel = transform.Find("ModuleCanvas").Find("RegulationElements").
                               Find("RegIdLabel").GetComponent<Text>(); 
        
        dragButton = transform.Find("ModuleCanvas").
                               Find("DragModuleButton").gameObject;

        // Regulation modules will not have sliders (or will they?)
        if (transform.Find("ModuleCanvas").Find("Slider") != null)
        {
            // Reference to the slider
            slider = transform.Find("ModuleCanvas").Find("Slider").GetComponent<Slider>();           
        }

        // Connections and labels will be created as child objects (for the 
        // local inputs and local outputs, which cannot be made in the prefab)
        myConnections = new List<GameObject>();
        myLocalIOlabels = new List<GameObject>();
        // Instantiates the regulation menu panel associated with this module.
		InstantiateRegulationPanel();

        // Instantiates the options menu panel associated with this module.
		InstantiateOptionsPanel();
	}
	
	// Update is called once per frame
    protected virtual void Update() {
        // Sets the position of the regulation panel so it follows the module.
        // Note that the module is in the world frame of reference, while
        // the regulation menu is in the screen canvas.
        FollowModule();

        // If the module is set as probe, move it to the right (when no collisions
        // are detected probe status will be removed)
        if (colliderAsProbe)
        {
            if (collisions < 1)
            {
                colliderAsProbe = false;
            }
            else
            {
                MoveAway();
            }
        }
	}
    
    #endregion
    
    #region Properties

    /// <summary>
    /// Use SetActive to set.
    /// </summary>
    public bool IsActive
    {
        get { return isActive; }
    }

    public int ModuleId
    {
        get { return moduleId; }
    }

    public UImanager SetUiManager
    {
        set { uiManager = value; }
    }

    public int TotalInNeurons
    {
        set { totalInNeurons = value; }
    }

    public int TotalOutNeurons
    {
        set { totalOutNeurons = value; }
    }

    public bool BasicRegulation
    {
        set { basicRegulation = value; }
    }

    public uint RegulatoryId
    {
        get { return regulatoryId; }
    }

    public List<newLink> RegulatoryInputList
    {
        get { return regulatoryInputList; }
        set { regulatoryInputList = value; }
    }

    public GameObject ObjectCanvas
    {
        get { return objectCanvas; }
        set { objectCanvas = value; }
    }

    public bool IsRegModule
    {
        get { return isRegModule; }
        set { isRegModule = value; }
    }

    public bool ColliderAsProbe
    {
        get { return colliderAsProbe; }
        set { colliderAsProbe = value; }
    }

    public GameObject DragButton
    {
        get { return dragButton; }
        set { dragButton = value; }
    }

    #endregion

    #region PublicSetupMethods

    /// <summary>
    /// Also creates the connection for bias (which is often created and
    /// destroyed when using basic regulation!)
    /// </summary>
    public void SetRegulatoryId(uint newId)
    {
        regulatoryId = newId;
    }

    /// <summary>
    /// Sets the value for the module Id and also updates the module id label
    /// (which is why it is kept as a public method and not in the properties section.
    /// </summary>
    public void SetModuleId(int newId)
    {
        moduleId = newId;
        moduleIdLabel.text = "M" + newId.ToString();
        regIdLabel.text = "R" + newId.ToString();
    }

    /// <summary>
    /// Allows to load or change the module label
    /// </summary>
    public void SetModuleLabel(string newLabel)
    {
        moduleLabelButtonText.text = newLabel;
    }

    /// <summary>
    /// Receives the pandemonium group (it is passed to the regulation panel) 
    /// </summary>
    public void SetPandemoniumValue(int newPandem)
    {
        regulatoryMenu.GetComponent<RegulationPanelController>().
                       SetPandemoniumValue(newPandem);
    }

    /// <summary>
    /// This method adds the graphical representation (connections and labels)
    /// for local inputs and local outputs. A special tag is used, so that
    /// these elements are not hard to remove in case this is needed.
    /// </summary>
    public void AddLocalIO(List<string> localList, bool isLocalIn)
    {
        float interval;
        float xPosition;
        float yPosition;
        float yPositionLabel;

        const float yPosShortUp = 90.7f;
        const float yPosLongUp = 110.9f;
        const float yPosLabShortUp = 121.9f;
        const float yPosLabLongUp = 162.4f;

        const float yPosShortDown = -55.6f;
        const float yPosLongDown = -77.2f;
        const float yPosLabShortDown = -94.3f;
        const float yPosLabLongDown = -136.2f;

        if (isLocalIn)
        {
            yPosition = yPosShortUp;
            yPositionLabel =yPosLabShortUp;
        }
        else
        {
            yPosition = yPosShortDown;
            yPositionLabel = yPosLabShortDown;
        }

        interval = length / (localList.Count + 1f);

        // Checks if all inputs are being used
        if (isLocalIn && localList.Count == totalInNeurons)
        {
            // TODO: Avoid this label when using special inputs, like local output
            // from a different module: those should always be represented!

            // Displays a special "all inputs" label.
            interval = length / 3f;
            xPosition = leftSide + interval;
            myConnections.Add(InstantiateConnector(xPosition, yPosition, true));

            // This longer text is slightly offset
            myLocalIOlabels.Add(InstantiateLabel(xPosition - 13f, yPositionLabel,
                                                 "All inputs", isLocalIn));
        }
        else
        {
            // Either we have local outputs or a custom selection of local inputs.
            if (localList.Count > 3)
            {

                // Uses an alternating length pattern for more compression
                // Short connections (one in two)
                for (int i = 0; i < localList.Count; i += 2)
                {
                    xPosition = leftSide + ((float)i + 1f) * interval;

                    myConnections.Add(InstantiateConnector(xPosition, yPosition, true));

                    myLocalIOlabels.Add(InstantiateLabel(xPosition, yPositionLabel,
                                        localList[i], isLocalIn));
                } 

                // Long connections (one in two, starting with the second)
                if (isLocalIn)
                {
                    yPosition = yPosLongUp;
                    yPositionLabel = yPosLabLongUp;
                }
                else
                {
                    yPosition = yPosLongDown;
                    yPositionLabel = yPosLabLongDown;
                } 
                for (int i = 1; i < localList.Count; i += 2)
                {
                    xPosition = leftSide + ((float)i + 1f) * interval;

                    myConnections.Add(InstantiateConnector(xPosition, yPosition,
                                                           false));

                    myLocalIOlabels.Add(InstantiateLabel(xPosition, yPositionLabel,
                                                         localList[i], isLocalIn));
                } 
            }
            else
            {
                // All connectors are short
                for (int i = 0; i < localList.Count; ++i)
                {
                    xPosition = leftSide + ((float)i + 1f) * interval;

                    myConnections.Add(InstantiateConnector(xPosition, yPosition,
                                                           true));

                    myLocalIOlabels.Add(InstantiateLabel(xPosition, yPositionLabel,
                                                         localList[i], isLocalIn));
                }  
            }
        }        
    }

    // Passes the input list to the regulation menu.
    // As soon as the module is instantiated the regulation panel is as well,
    // so the GameObject will exist.
    public void AddInToRegulationMenu(List<string> inputList)
    {
        regulatoryMenu.GetComponent<RegulationPanelController>().GetInputList(inputList);
    }

    /// <summary>
    /// Receives a list with output connections (connection Id, target and weight)
    /// </summary>
    public void LoadOutputList(List<newLink> newlocalOutputList)
    {
        localOutputList = newlocalOutputList;
		double firstValue = localOutputList[0].weight;
        double delta = 0.01;
		for (int i = 1; i < localOutputList.Count; ++i)
		{
            // Always avoid comparing non-integers!
            // In this case, we want "if (a != b)"
            if (System.Math.Abs(localOutputList[i].weight - firstValue) > delta)
			{
				// LOCAL OUTPUT WEIGHTS HAVE DIFFERENT VALUES!
                return;
			}
		}

        // If we are here, all local outputs have the same weights.
        // Slider will be null for regulation modules!
        if (slider != null)
        {
            slider.value = (float)firstValue;            
        }
    }

    /// <summary>
    /// Receibes and updates the regulation for the module. Checks if the given
    /// regulation scheme is compatible with basic regulation or not, and
    /// sets the variables if it is so.
    /// </summary>
    public void LoadRegulation(List<newLink> regulationList)
    {
        double delta = 0.01;

        regulatoryInputList = regulationList;
        basicRegulation = true;
        // In basic regulation: the module will be active when the chosen input
        // is active or inactive?
        bool activeWhenInputActive = true;

        // Basic regulation may only contain up to two entries
        if (regulationList.Count > 2)
        {
            basicRegulation = false;
            return;
        }
        // If there is only one connection:
        if (regulationList.Count == 1)
        {
            if (regulationList[0].otherNeuron > (uint)totalInNeurons)
            {
                // There is a connection from a neuron that is not in the
                // basic input list
                basicRegulation = false;
                return; 
            }

            // Special case: active when bias inactive! (never active, achived
            // using a connection from bias with weight 0):
            if (regulationList[0].otherNeuron == 0 &&
                System.Math.Abs(regulationList[0].weight - 0) < delta)
            {
                regulatoryMenu.GetComponent<RegulationPanelController>().
                SetDropdownValue((int)regulationList[0].otherNeuron);
                regulatoryMenu.GetComponent<RegulationPanelController>().
                SetActiveWhenInactive();
                return;
            }

            // We still need to check that the connection weight is +1
            // In particular we check if the weight is NOT! 1 and proceed 
            // otherwise.
            if (System.Math.Abs(regulationList[0].weight - 1) > delta)
            {
                // The connection weight is not compatible with basic regulation
                basicRegulation = false;
                return;                 
            }

            // The module will be active when regulationList[0].otherNeuron
            // is active:
            regulatoryMenu.GetComponent<RegulationPanelController>().
                    SetDropdownValue((int)regulationList[0].otherNeuron);
            regulatoryMenu.GetComponent<RegulationPanelController>().
                    SetActiveWhenActive();
        }
        else if (regulationList.Count == 2) 
        {
            // This may be a case of basic regulation (active when x is inactive)
            // or it may be a case of advanced regulation.
            // Basic regulation case: one connection from bias with weight 1
            // and one from an input with weight -1

            // First checks if the first (it should be the first) connection
            // is from bias and with weight +1
            if (regulationList[0].otherNeuron == 0 &&
                System.Math.Abs(regulationList[0].weight - 1.0) < delta)
            {
                // Now checks if the second connection is from a global input
                // neuron and with weight -1
                if (System.Math.Abs(regulationList[1].weight + 1.0) < delta &&
                    regulationList[1].otherNeuron  <= (uint)totalInNeurons)
                {
                    // This is a basic regulation case!
                    regulatoryMenu.GetComponent<RegulationPanelController>().
                            SetDropdownValue((int)regulationList[1].otherNeuron);
                    regulatoryMenu.GetComponent<RegulationPanelController>().
                            SetActiveWhenInactive();
                    return;                
                }
            }
            // Criteria for basic regulation with two inputs not met:
            basicRegulation = false;
            return;
        }
        else
        {
            // RegulationList must be empty: create default activation:
            // active when bias is active (always)
            // May this situation happen at all?   
        }
    }

    #endregion

    #region OtherPublicMethods

    /// <summary>
    /// Removes the GameObject to which this script belongs (the module UI
    /// elements). Also takes care of its associated menu screens.
    /// </summary>
    public void RemoveModule()
    {
        // Do not forget to destroy the regulation and options menues!
        Destroy(regulatoryMenu);
        Destroy(optionsMenu);
        // Commits suicide!
        Destroy(this.gameObject);
    }

    /// <summary>
    /// Resets or deletes this module. This is called from the options menu, 
    /// which promts a confirmation panel, since this actions may lose progress.
    /// </summary>
    public void CallResetOrDelete()
    {
        if (isActive)
        {
            // Active module: reset

            // It is best if uiManager takes care of all the interaction
            // with Optimizer (as opposed to getting a reference to
            // Optimizer here and calling reset directly).
            uiManager.AskResetActiveModule();
        }
        else
        {
            // Not active module: delete
            uiManager.AskDeleteModule(moduleId);
        }
    }

    /// <summary>
    /// Not in properties so we take the chance to call 
    /// another method.
    /// </summary>
    public void SetActive(bool newState)
    {
        isActive = newState;
        optionsMenu.GetComponent<OptionsPanelController>().
                    ToggleResetDeleteText();
    }

    /// <summary>
    /// Checks if this is the active module. If it is, launches evolution.
    /// Otherwise sets off a warning.
    /// </summary>
    public void Evolve()
    {
        if (isActive)
        {
            // This is the continuation of the current evolutionary process
            uiManager.LaunchEvolution();
        }
        else
        {
            // This will be a new evolutionary process
            uiManager.SetActiveAndEvolve(moduleId);
        }
    }

    /// <summary>
    /// Activates the input field for a new label.
    /// </summary>
    public void ActivateLabelField()
    {
        moduleLabelInputField.SetActive(true);
    }

    /// <summary>
    /// Gets a new label from the user
    /// </summary>
    public void UpdateLabelAndDeactivate(string newLabel)
    {
        SetModuleLabel(newLabel);

        // Sends label back to UImanager
        uiManager.GetNewModuleLabel(moduleId, newLabel);

        // Deactivates the input field
        moduleLabelInputField.SetActive(false);
    }

    /// <summary>
    /// Passes the regulation scheme back to UImanager (where all of them
    /// are centralized in a list).
    /// </summary>
    public void PassRegulation()
    {
        uiManager.GetNewRegulationScheme(moduleId, regulatoryInputList);
    }

    /// <summary>
    /// Resets the module's regulation
    /// </summary>
    public void ResetRegulation()
    {
        regulatoryInputList = new List<newLink>();
    }

    /// <summary>
    /// Adds a new connection to the regulation list (from a given input, with
    /// a desired weight). As of this version this method does not work
    /// with other inputs (only global input neurons not, for example,
    /// local output neurons as regulation sources).
    /// </summary>
    public void AddInputToReg(int newInput, double newWeight)
    {
        newLink auxLink = new newLink();
        // Bias (0) will take id = reg + 1, input 1 will take reg + 2 and so on.
        auxLink.id = regulatoryId + (uint)newInput + 1;
        // For basic regulation, source neuron Id = source neuron Index
        // (if this changes, we will need to be sure to find the Id first!)
        auxLink.otherNeuron = (uint)newInput;
        auxLink.weight = newWeight;

        regulatoryInputList.Add(auxLink);
    }
   
    /// <summary>
    /// Shows the regulation menu if it is inactive.
    /// Note activeSelf tells if the element is set as active, but if a parent
    /// is inactive it may still not show in the scene. activeInHierarchy may
    /// be used to know that.
    /// </summary>
    public void ShowRegulation()
    {
        if (basicRegulation && !regulatoryMenu.activeSelf)
        {
            regulatoryMenu.SetActive(true);
        }
    }

	/// <summary>
	/// Shows the options menu if it is inactive.
	/// </summary>
	public void ShowOptions()
	{
		if (!optionsMenu.activeSelf)
		{
			optionsMenu.SetActive(true);
		}
	}

    /// <summary>
    /// Hides the regulation menu.
    /// </summary>
    public void HideRegulation()
    {
        regulatoryMenu.SetActive(false);
	}

	/// <summary>
	/// Hides the options menu.
	/// </summary>
	public void HideOptions()
	{
		optionsMenu.SetActive(false);
	}

    /// <summary>
    /// Using the slider, gets a new weight for all output connections. 
    /// </summary>
    public void newOutputWeightBasic()
    {
        double newWeight = (double)slider.value;
        for (int i = 0; i < localOutputList.Count; ++i)
        {
            newLink auxLink = localOutputList[i];
            auxLink.weight = newWeight;
            localOutputList[i] = auxLink;
        }
        // Returns the new values back to UImanager
        uiManager.GetNewOutputConnectionList(moduleId, localOutputList);
    }

    /// <summary>
    /// Allows to dragg the module around the screen.
    /// </summary>
    public virtual void moveModule2(UnityEngine.EventSystems.BaseEventData eventData)
    {
        var pointerData = eventData as UnityEngine.EventSystems.PointerEventData;
        if (pointerData == null)
        {
            return;
        }

        Vector3 transformCopy = transform.position;
        transformCopy.x -= dragFactor*pointerData.delta.x;
        transformCopy.z -= dragFactor*pointerData.delta.y;
        this.transform.position = transformCopy;    
    }
    /*  // This method does not work, probably because of the coordinates system
    // being used. Left as reference.
    public void moveModule()
    {
        transform.position = Input.mousePosition;   
    }*/

	/// <summary>
	/// Makes sure the panels are instantiated correctly. This is used for regulation
	/// and the options panel.
	/// 
	/// It has been made public so that child objects can use it.
	/// </summary>
	public void SetUpPanel(GameObject panel, GameObject myPrefab)
	{
		// Makes sure rotation is as expected. For some reason the canvas can
		// take weird rotation values, although everything seemed correct and
		// only affected modules added after loading. In any case this works
		// as it should.
		screenCanvas.transform.rotation = myPrefab.transform.rotation;
		// Sets the screen canvas as parent
		panel.transform.SetParent(screenCanvas.transform);
		// regulatoryMenu.transform.rotation = myPrefab.transform.rotation;

		// Makes sure the scale is normal
		panel.transform.localScale = new Vector3(1f, 1f, 1f);

		// At creation, the element is NOT active
		panel.SetActive(false);		
	}

    /// <summary>
    /// Looks through all children of the object canvas for elements with
    /// tag "IOelement" and sets them active or inactive.
    /// </summary>
    public void SetActiveIOelements(bool isActive)
    {
        SetActiveChildWithTag(objectCanvas.transform, "IOelement", isActive);
    }

    /// <summary>
    /// This is only so we can easily call SetAllRegIddle on mouse button down
    /// </summary>
    public void SetAllRegAsIddle()
    {
        uiManager.SetAllRegIddle();
    }

    /// <summary>
    /// Moves the module to the right.
    /// </summary>
    public void MoveAway()
    {
        // New position for the module:
        transform.position = transform.position + new Vector3(-1f, 0f, 0f); 
    }

    #endregion

    #region ProtectedMethods

    /// <summary>
    /// Sets the position of the regulation info panel. This is tricky, because 
    /// we need coordinates on canvas. WorldToScreenPoint returns points that
    /// are heavily shifted. 
    /// </summary>
    protected void FollowModule()
    {
        // Apparently 0,0 is at the centre for canvas, but the lower left corner
        // for the viewport.
        RectTransform canvasRect = screenCanvas.GetComponent<RectTransform>();

        Vector2 viewportPosition = backgroundCamera.WorldToViewportPoint(transform.position);
        Vector2 canvasPosition = new Vector2 (
            viewportPosition.x * canvasRect.sizeDelta.x - canvasRect.sizeDelta.x * 0.5f,
            viewportPosition.y * canvasRect.sizeDelta.y - canvasRect.sizeDelta.y * 0.5f);

        // Some offset to avoid hidding the module (rethink this part, add
        // intelligent offset to avoid going outside of the screen)
        canvasPosition.x += 180;
        canvasPosition.y += 40f;

        regulatoryMenu.transform.localPosition = canvasPosition;     
        optionsMenu.transform.localPosition = canvasPosition;
    }

    #endregion

    #region PrivateMethods

	/// <summary>
	/// Instantiates the options menu panel. This will be activated or
	/// deactivated as needed.
	/// </summary>
	void InstantiateOptionsPanel()
	{
		// Instantiates the object
		GameObject myPrefab = (GameObject)Resources.Load("Prefabs/ModuleOptionsMenu");
		optionsMenu = (GameObject)Instantiate(myPrefab);

		SetUpPanel(optionsMenu, myPrefab);

		// We need to pass a reference to this script, so that the regulation panel
		// can communicate with this module in particular
        optionsMenu.GetComponent<OptionsPanelController>().SetModuleController(this);
	}

    /// <summary>
    /// Instantiates the regulation menu panel. This will be activated or
    /// deactivated as needed.
    /// </summary>
    void InstantiateRegulationPanel()
    {
        // Instantiates the object
        GameObject myPrefab = (GameObject)Resources.Load("Prefabs/RegulationMenuBasic");
        regulatoryMenu = (GameObject)Instantiate(myPrefab);

		SetUpPanel(regulatoryMenu, myPrefab);

		// We need to pass a reference to this script, so that the regulation panel
		// can communicate with this module in particular
		regulatoryMenu.GetComponent<RegulationPanelController>().ModuleController = this;
    }

    /// <summary>
    /// Instantiates a connector. It can be either at the top of the module
    /// or below it, short or long.
    /// </summary>
    GameObject InstantiateConnector(float xPosition, float yPosition, bool isShort)
    {
        Vector3 helperTransf;
        GameObject newConnector;
        // Instantiates the object
        newConnector = (GameObject)Instantiate(Resources.Load("Prefabs/ModuleIOconnector"));
        // Sets the canvas as parent
		newConnector.transform.parent = objectCanvas.transform;
        // Gives the desired rotation
        newConnector.transform.Rotate(90f,180f,0f);
        // Sets the size (depends on isShort)
        if (isShort)
        {
            newConnector.transform.localScale = shortLength;
        }
        else
        {
            newConnector.transform.localScale = longLength;
        }
        // Sets the position
        helperTransf = new Vector3(xPosition, yPosition, 0f);  
        newConnector.transform.localPosition = helperTransf;
        return newConnector;
    }

    /// <summary>
    /// Instantiates a label. It can be either at the top of the module
    /// or below it.
    /// </summary>
    GameObject InstantiateLabel(float xPosition, float yPosition, string ioText,
                                bool isLocalIn)
    {
        float outLabelOffset = 12.6f;
        float inLabelOffset = 20f;
        float labelOffset = outLabelOffset;
        if (isLocalIn)
        {
            labelOffset = inLabelOffset;
        }

        Vector3 helperTransf;
        GameObject newLabel;
        // Instantiates the object
        newLabel = (GameObject)Instantiate(Resources.Load("Prefabs/IOlabel"));
        // Sets the canvas as parent
		newLabel.transform.SetParent(objectCanvas.transform);
        // Gives the desired rotation
        // Yes, a clever person would only use one rotation.
        newLabel.transform.Rotate(-90f,0f,0f);
        newLabel.transform.Rotate(0f,180f,0f);
        newLabel.transform.localScale = ioLabelLength;

        // Writes the correct text
        newLabel.GetComponent<Text>().text = ioText;

        // Sets the position ("Bias" is a longer label, so it takes no offset)
        if (ioText != "Bias")
        {
            xPosition += labelOffset;
        }
        helperTransf = new Vector3(xPosition, yPosition, 0f);  
        newLabel.transform.localPosition = helperTransf;
        return newLabel;
    }

    /// <summary>
    /// Searchs for the desired tag among the children of a parent unit and
    /// sets all of them as active or inactive (according to isActive)
    /// </summary>
    void SetActiveChildWithTag(Transform parent_transform, string tag,
                                     bool isActive)
    {
        foreach (Transform child_transform in parent_transform)
        {
            if (child_transform.tag == tag)
            {
                child_transform.gameObject.SetActive(isActive);
            }
        }
    }

    /// <summary>
    /// When the collider finds or exits another collider, the counter for 
    /// the number of collisions is updated.
    /// </summary>
    protected virtual void OnTriggerEnter(Collider other)
    {
        ++collisions;
    }
    protected virtual void OnTriggerExit(Collider other)
    {
        --collisions;
    }

    #endregion
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using SharpNeat.Coordination;

public class ModuleController : MonoBehaviour {

    // This has been made public so it can be determined in the prefab.
    // Note that the get/set functions have been left as before.
    public bool isRegModule = false;

    protected UImanager uiManager;

    protected int moduleId = 0;
    protected bool isActive = false;

    // We sometimes need to place a module where it does not collide with any
    // others. We use these:
    protected bool colliderAsProbe = false;
    protected int collisions = 0;

    protected GameObject warningEvolvePanel;

    // We need the canvas GameObject so that we can add UI elements with this 
    // canvas as parent. We also need to create other elements using a screen
    // canvas.
    // backgroundCamera is the camera associated with the screen canvas.
    protected GameObject objectCanvas;
    protected GameObject screenCanvas;
    protected Camera backgroundCamera;
    protected int totalInNeurons;
    protected int totalOutNeurons;

    // This is the options menu. It will be activated or deactivated (as
    // opposed to instantiated and destroyed).
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

    // Determines whether a basic regulation scheme is being used
    protected bool basicRegulation = true;

    // These are references to the labels, so they can be updated.
    protected Text moduleIdLabel;
    protected Text moduleLabelButtonText;
    protected GameObject moduleLabelInputField;
	// This label is currently hidden (so all related text could be commented)
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
    protected virtual void Awake() {
        // Gets uiManager this way (not with a set property function) because
        // it needs to be referenced already during Awake!
        uiManager = GameObject.Find("Evaluator").GetComponent<UImanager>();

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

        // Connections and labels will be created as child objects (for the 
        // local inputs and local outputs, which cannot be made in the prefab)
        myConnections = new List<GameObject>();
        myLocalIOlabels = new List<GameObject>();

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

    public UImanager UiManager
    {
        get { return uiManager; }
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
        optionsMenu.GetComponent<OptionsPanelController>().SetPandemoniumValue(newPandem);
    }

    /// <summary>
    /// This method adds the graphical representation (connections and labels)
    /// for local inputs and local outputs. A special tag is used, so that
    /// these elements are not hard to remove in case this is needed.
    /// 
    /// Currently local outputs are NOT shown, which could simplify this method.
    /// However, the gain is probably not worth the loss of flexibility in the future.
    /// </summary>
    public void AddLocalIO(List<string> localList, bool isLocalIn)
    {
        float interval;
        float xPosition;
        float yPosition;
        float yPositionLabel;

		// Offset for input/output elements (lines and texts)
        const float yPosShortUp = 87.7f;
        const float yPosLongUp = 107.9f;
        const float yPosLabShortUp = 118.9f;
        const float yPosLabLongUp = 159.4f;

        const float yPosShortDown = -108.6f;
        const float yPosLongDown = -130.2f;
        const float yPosLabShortDown = -147.3f;
        const float yPosLabLongDown = -189.2f;

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
        optionsMenu.GetComponent<OptionsPanelController>().GetInputList(inputList);
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
        optionsMenu.GetComponent<OptionsPanelController>().SetSliderValue((float)firstValue);
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
                optionsMenu.GetComponent<OptionsPanelController>().
                SetDropdownValue((int)regulationList[0].otherNeuron);
                optionsMenu.GetComponent<OptionsPanelController>().
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
            optionsMenu.GetComponent<OptionsPanelController>().
            SetActiveWhenActive();
            optionsMenu.GetComponent<OptionsPanelController>().
                    SetDropdownValue((int)regulationList[0].otherNeuron);
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
                    optionsMenu.GetComponent<OptionsPanelController>().
                    SetActiveWhenInactive();
                    optionsMenu.GetComponent<OptionsPanelController>().
                            SetDropdownValue((int)regulationList[1].otherNeuron);
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
    /// These two are only relevant in regulation modules. We include them
    /// here so they can be called from the common controller script
    /// OptionsPanelController
    /// </summary>
    public virtual void ChildrenToNoPandem() {}
    public virtual void ChildrenToPandem() {}

    public void ActivationLabelAsChild()
    {
        optionsMenu.GetComponent<OptionsPanelController>().ActivationLabelAsChild();
    }

    /// <summary>
    /// Removes the GameObject to which this script belongs (the module UI
    /// elements). Also takes care of its associated menu screens.
    /// </summary>
    public void RemoveModule()
    {
        // Do not forget to destroy the options menu!
        Destroy(optionsMenu);
        // Commits suicide!
        Destroy(this.gameObject);
    }

    /// <summary>
    /// When asked by OptionsPanelController, asks uiManager to ask optimizer
    /// to ask Factory to clone this module.
    /// Perhaps this should be simplified!!
    /// </summary>
    public virtual void AskClone()
    {
        uiManager.AskCloneModule(moduleId, isRegModule);
    }

    /// <summary>
    /// Deletes this module. This is called from the options menu, 
    /// which promts a confirmation panel, since this actions may lose progress.
    /// </summary>
    public virtual void CallDelete()
    {
        // We record this action:
        uiManager.WriteToRecord("Delete " + moduleId.ToString());

        if (isActive)
        {
            // Active module: set another as active and delete

            // Sets another module as active
            uiManager.SetAnotherActive(moduleId);

            // Now deletes this module
            uiManager.AskDeleteModule(moduleId);
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
        if (isActive)
        {
            Evolve();
        }
        else
        {
            InstantiateEvolveWarning();
        }        
    }

    /// <summary>
    /// Instantiates a panel warning this is not the active module (so starting
    /// evolution here will lose population diversity for the current active
    /// module!)
    /// </summary>
    public void InstantiateEvolveWarning()
    {
        warningEvolvePanel = InstantiateWarning("Prefabs/EvolveInactiveWarning");
        warningEvolvePanel.GetComponent<EvolveInactivePanelController>().MyModuleController = this;
    }

    /// <summary>
    /// This is used in here and in options panel controller.
    /// </summary>
    public GameObject InstantiateWarning(string prefabAddress)
    {
        GameObject myPrefab = (GameObject)Resources.Load(prefabAddress);
        GameObject panel = (GameObject)Instantiate(myPrefab);

        // Used to ensure correct orientation
        uiManager.SetUpPanel(panel, myPrefab);
        // But SetUpPanel leaves the gameObject inactive!
        panel.SetActive(true);

        return panel;
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
    /// Proceeds with the creation of a new evolutionary process (for an old
    /// module)
    /// </summary>
    public void ProceedEvolve()
    {
        Evolve();
        DestroyEvolutionWarning();
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
    /// Passes the pandemonium group back to UImanager (where all of them
    /// are centralized in a list).
    /// </summary>
    public void PassPandemonium(int newPandem)
    {
        uiManager.GetNewPandemonium(moduleId, newPandem);
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
	/// Hides the options menu.
	/// </summary>
	public void HideOptions()
	{
		optionsMenu.SetActive(false);
	}

    /// <summary>
    /// Sets a new weight for all output connections. 
    /// </summary>
    public void newOutputWeightBasic(double newWeight)
    {        
        for (int i = 0; i < localOutputList.Count; ++i)
        {
            newLink auxLink = localOutputList[i];
            auxLink.weight = newWeight;
            localOutputList[i] = auxLink;
        }
        // Returns the new values back to UImanager
        uiManager.GetNewOutputConnectionList(moduleId, localOutputList);

        // We record this action:
        uiManager.WriteToRecord("New weights for " + moduleId + " now: " + newWeight.ToString());
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
    /// Calls the second part, EvolveContinue. First creates a list with the
    /// children of this module. In this case this is a normal module, with no
    /// children!
    /// </summary>
    protected virtual void Evolve()
    {
        // This is a normal module, with no children
        List<int> noChildren = new List<int>();
        EvolveContinue(noChildren);
    }
    /// <summary>
    /// Make this the active module if it is not already.
    /// If the module has basic regulation it is also (provisionally) made
    /// the only active module (children left normal). This is so during evolution
    /// only the behaviour of this module is shown, with no interference.
    /// </summary>
    protected void EvolveContinue(List<int> childrenId)
    {
        if (!isActive)
        {
            // This will be a new evolutionary process, so we need to set this
            // module as the active module in the genome!
            uiManager.SetModuleActive(moduleId);
        }

        // If this module uses basic regulation, then we set it as the only
        // active module! (its children will also remain active!)
        //
        // USE WITH ADVANCED REGULATION IS NOT FULLY SUPPORTED YET: USE WITH
        // CAUTION!
        newLink newRegulation = new newLink();
        // Regulation from bias
        newRegulation.otherNeuron = 0;
        // With weight 1
        newRegulation.weight = 1.0;
        // Takes the ID following that of the regulatory neuron
        newRegulation.id = regulatoryId + 1;

        uiManager.MakeOnlyActiveMod(moduleId, newRegulation, childrenId);

        uiManager.LaunchEvolution();               
    }

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
        GameObject myPrefab = null;
        if (isRegModule)
        {
            myPrefab = (GameObject)Resources.Load("Prefabs/ModuleOptionsMenu-reg");  
        }
        else
        {
            myPrefab = (GameObject)Resources.Load("Prefabs/ModuleOptionsMenu"); 
        }
		optionsMenu = (GameObject)Instantiate(myPrefab);

        uiManager.SetUpPanel(optionsMenu, myPrefab);

		// We need to pass a reference to this script, so that the regulation panel
		// can communicate with this module in particular
        optionsMenu.GetComponent<OptionsPanelController>().SetModuleController(this);
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

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using SharpNeat.Coordination;

public class RegModuleController : ModuleController {

    private RegModuleSizes regModuleSizes;
    private int modulesContained;

    private bool beingDragged = false;
    private Collider otherModule;

    private GameObject mainTexture;
    private GameObject regulationElements;
    private GameObject addElements;
    private GameObject background;
    private GameObject plusSymbol;
    private GameObject plusSymbolHover;

    GameObject warningPanel = null;

    // These are references to the modules that have been included in this
    // regualtion module!
    private List<GameObject> containedModules;

    // Note that if the virtual method is protected, this should be 
    // protected as well.
    protected override void Awake()
    {
        // Do not forget about the parent-class' initializer!
        base.Awake();

        mainTexture = transform.Find("ModuleCanvas").Find("MainTexture").gameObject;
        regulationElements = transform.Find("ModuleCanvas").
                                       Find("RegulationElements").gameObject;
        background = transform.Find("ModuleCanvas").Find("Add-Elements").
                               Find("Background").gameObject;
        addElements = transform.Find("ModuleCanvas").Find("Add-Elements").gameObject;
        plusSymbol = transform.Find("ModuleCanvas").Find("Add-Elements").
                               Find("PlusSymbol").gameObject;
        plusSymbolHover = transform.Find("ModuleCanvas").Find("Add-Elements").
                                    Find("PlusSymbolHover").gameObject;
        plusSymbolHover.SetActive(false);

        regModuleSizes = new RegModuleSizes();
        modulesContained = 0;
    
        containedModules = new List<GameObject>();

        InstantiateWarning();
    }

    // Update is called once per frame
    protected override void Update() {
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
                // We also need to move all the modules that have been included!
                foreach (GameObject module in containedModules)
                {
                    module.GetComponent<ModuleController>().MoveAway();
                }
            }
        }
    }

    public bool BeingDragged
    {
        set { beingDragged = value; }
    }

    /// <summary>
    /// When the user clicks on a regulation module, it is set as "being dragged".
    /// This way it is possible to know which module was dropped into which
    /// if a user drops a regulation module inside of another (and we can also
    /// avoid trying to add both inside of the other!)
    /// Click up does not reset this, but the only possible conflic is if the
    /// user clicks on another module, and that will reset all.
    /// </summary>
    public void SetAsDragged()
    {
        SetAllRegAsIddle();
        beingDragged = true;
    }

    /// <summary>
    /// Allows to dragg the module around the screen.
    /// </summary>
    public override void moveModule2(UnityEngine.EventSystems.BaseEventData eventData)
    {
        base.moveModule2(eventData);

        // We also need to move all the modules that have been included!
        foreach (GameObject module in containedModules)
        {
            module.GetComponent<ModuleController>().moveModule2(eventData);
        }
    }

    /// <summary>
    /// Takes a module and places it inside this regulation module. For this,
    /// it changes the size and position of the other module. Also updates
    /// the size of this module, and updates the list of child modules.
    /// 
    /// We have kept the parameter so it is easy to call this from uiManager
    /// after loading modules. Another way is to remove the parameter and
    /// update the variable "otherModule" of the regulation module before calling
    /// MoveModuleInside from uiManager.
    /// </summary>
    public void MoveModuleInside(Collider other)
    {
        ++modulesContained;
        GetNewSizes(modulesContained);

        // Hides input/output in the module that will be included
        // (otherwise they take too much space!)
        other.GetComponent<ModuleController>().SetActiveIOelements(false);
        // Makes the new module smaller
        other.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
        // Fixes the position of the module!
        other.transform.position = this.transform.position +
                                   regModuleSizes.moduleOffset[modulesContained];
        // We do not want to interact with modules that are already
        // part of the group!
        other.GetComponent<BoxCollider>().enabled = false;
        other.GetComponent<ModuleController>().DragButton.SetActive(false);

        // If we are adding a regulation module we need a few extra steps
        if (other.GetComponent<ModuleController>().IsRegModule)
        {
           AddRegModule(other);
        }

        // After adding a module, we reset the plus icon to normal state
        plusSymbol.SetActive(true);
        plusSymbolHover.SetActive(false);

        containedModules.Add(other.gameObject); 
    }

    /// <summary>
    /// Adds the module (saved in the reference otherModule) to this regulation
    /// module
    /// </summary>
    public void AddModule()
    {
        // The regulation scheme of the other module is reset (cleaned)
        // This is done first so genome statistics are updated before trying
        // to add new elements.
        ResetRegulation();

        beingDragged = true;

        if (!isActive)
        {
            uiManager.SetModuleActive(moduleId); 
        }

        MoveModuleInside(otherModule);

        // The new module is added to the hierarchy dictionary in
        // UImanager
        uiManager.GetNewRegulationModuleContent(
            moduleId, otherModule.GetComponent<ModuleController>().ModuleId);

        UpdateLocalOutAndNotify();  

        warningPanel.SetActive(false);  
    }

    /// <summary>
    /// Sets the other module (the module that triggered a collision, saved in
    /// otherModule) as probe (so that it moves until it finds no collisions
    /// with other modules).
    /// </summary>
    public void MoveModuleAway()
    {
        otherModule.GetComponent<ModuleController>().ColliderAsProbe = true;
        warningPanel.SetActive(false);  
    }

    /// <summary>
    /// When the number of elements in the regulation module changes, so does
    /// its size on screen. Instead of making calculations, we simply load
    /// values from an specific class: RegModuleSizes.
    /// </summary>
    void GetNewSizes(int numberOfElements)
    {
        mainTexture.GetComponent<RectTransform>().sizeDelta =
            regModuleSizes.mainTextureSize[numberOfElements];

        dragButton.GetComponent<RectTransform>().sizeDelta =
            regModuleSizes.dragSize[numberOfElements];

        regulationElements.GetComponent<RectTransform>().anchoredPosition =
            regModuleSizes.regulationPosition[numberOfElements];

        background.GetComponent<RectTransform>().sizeDelta =
            regModuleSizes.addBackgroundSize[numberOfElements];            

        plusSymbol.GetComponent<RectTransform>().anchoredPosition =
            regModuleSizes.addPlusPosition[numberOfElements];
        plusSymbolHover.GetComponent<RectTransform>().anchoredPosition =
            regModuleSizes.addPlusPosition[numberOfElements]; 

        this.GetComponent<BoxCollider>().center = 
            regModuleSizes.colliderCenter[numberOfElements];
        this.GetComponent<BoxCollider>().size = 
            regModuleSizes.colliderSize[numberOfElements];
    }

    /// <summary>
    /// Trigger enter and exit will change plusSymbol texture
    /// </summary>
    protected override void OnTriggerEnter(Collider other)
    {
        plusSymbol.SetActive(false);
        plusSymbolHover.SetActive(true);
        base.OnTriggerEnter(other);
    }
    protected override void OnTriggerExit(Collider other)
    {
        plusSymbol.SetActive(true);
        plusSymbolHover.SetActive(false);
        base.OnTriggerExit(other);
    }

    /// <summary>
    /// This is called if another gameobject (with a collision trigger)
    /// is over this module (which happens if we want to add a module to
    /// this regulation module).
    /// </summary>
    void OnTriggerStay(Collider other)
    {
        // (0) checks if the left button is pressed
        // (1 for right button and 2 for middle button)
        if (!Input.GetMouseButton(0))
        {
            // We only proceed if this module was not being dragged (in which
            // case it is being added to another regulation module, not the
            // other way around! The parent module is always stationary)
            // Also: if the other module is set as probe (finding a position
            // without collisions) avoid starting the process.
            if (!beingDragged && !other.GetComponent<ModuleController>().ColliderAsProbe)
            {
                // We save a reference to the other module so it can be used by
                // other methods (specially so that they can be called from outside
                // of this script, from a button for instance)
                otherModule = other;

                // The mouse has been released while a module was over the
                // regulation module! So we attempt to add it to the regulation
                // module!
                if (modulesContained < 9)
                {
                    // Adding a module to a regulation module will set the
                    // regulation module as active. That will lose information
                    // in the current active module, so we check if this is 
                    // already the active module. If not, there is a confirmation
                    // panel.
                    if (!isActive)
                    {
                        warningPanel.SetActive(true);     
                    }
                    else
                    {
                        AddModule();
                    }
                }
                else
                {
                    // No more space: moves the other module away
                    MoveModuleAway();
                }                
            }

        }
    }

    void InstantiateWarning()
    {
        // Prompts a panel asking for confirmation!
        GameObject myPrefab = (GameObject)Resources.Load("Prefabs/AddModuleWarning");
        warningPanel = (GameObject)Instantiate(myPrefab);
        SetUpPanel(warningPanel, myPrefab);
        warningPanel.GetComponent<AddModuleWarningController>().
        RegModuleController = this;
    }

    void AddRegModule(Collider other)
    {
        RegModuleController otherController = other.GetComponent<RegModuleController>();

        // 1) Switches off children
        foreach (GameObject module in otherController.containedModules)
        {
            module.SetActive(false);
        }

        // 2) Sets the module to the size for 0 elements!
        otherController.GetNewSizes(0);

        // 3) Switches off the Add-Elements group
        otherController.addElements.SetActive(false);

        // 4) Main texture needs a custom size:
        otherController.mainTexture.GetComponent<RectTransform>().sizeDelta =
                new Vector2(255.3f, 122.3f);

        // 5) Position needs an extra touch:
        other.transform.position += new Vector3(0.51f, 0f, 0f);

        // 6) Canvas layer 0 (so it is surely on top!)
        otherController.ObjectCanvas.GetComponent<Canvas>().sortingLayerID = 0;
    }

    /// <summary>
    /// Resets the regulation scheme of the other module. It is also set as
    /// advanced regulation!
    /// </summary>
    void ResetRegulation()
    {
        ModuleController otherController = otherModule.GetComponent<ModuleController>();
        otherController.BasicRegulation = false;
        otherController.RegulatoryInputList = new List<newLink>();

        otherController.PassRegulation();
        uiManager.UpdateInToReg();
    }

    /// <summary>
    /// Creates the new element for the local output and adds it to the list.
    /// The new local output will link with the regulatory neuron of the other
    /// module. This is notified to UImanager, which invokes EspGenomeFactory
    /// so this connection is actually created in the genome.
    /// </summary>
    void UpdateLocalOutAndNotify()
    {
        newLink newLocalOut = new newLink();
        newLocalOut.otherNeuron = otherModule.GetComponent<ModuleController>().RegulatoryId;
        newLocalOut.weight = 1.0;
    
        uint lastLocalOutId;
        int lastLocalOutindex;
        if (uiManager.Genome.NeuronGeneList.FindLastLocalOut(moduleId,
                                                             out lastLocalOutindex))
        {
            lastLocalOutId = uiManager.Genome.NeuronGeneList[lastLocalOutindex].Id;

            if (modulesContained > 1)
            {
                // Our new local output neuron will take the index of the last
                // + 2, and + 1 for the protected connection, which comes first
                newLocalOut.id = lastLocalOutId + 1;
            }
            else
            {
                // If modulesContained == 1, this is the first module we are adding,
                // and the ID for the connection is the ID of the local output
                // neuron, - 1.
                newLocalOut.id = lastLocalOutId - 1;
            }

            // Call add  
            uiManager.AskAddModuleToRegModule(newLocalOut);

            // Before leaving we add the information of this connection to the
            // input-to-regulation list of the other module. Note that this
            // connection (coming from a local output instead of an input or
            // bias neuron) will be disregarded in factory, but it can be very
            // useful for the interface.
            // We need to change the "otherNeuron" field, because in the other
            // module we need the source of the connection, not the target!
            newLocalOut.otherNeuron = newLocalOut.id + 1;
            otherModule.GetComponent<ModuleController>().
                        RegulatoryInputList.Add(newLocalOut);
        }
        else
        {
            // TODO: Notify error!
            Debug.Log("Could not find local out neuron. RegModuleController.");
        }
    }
}

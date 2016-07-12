using UnityEngine;
using System.Collections.Generic;

namespace SharpNeat.Coordination
{
    /// <summary>
    /// ESP (module-based neural networks) requires a complex user interface,
    /// which makes it convenient to use dedicated classes (as opposed to having
    /// these methods in Optimizer).
    /// Visualizer class also takes much of the effort, specially for the
    /// schematic representation of the moduleswith their inputs, outputs,
	/// labels, and so on.
    /// 
    /// This is a very long class. Perhaps it would be reasonable to consider
    /// breaking it further into smaller classes.
    /// </summary>
    public class GuiManager : MonoBehaviour {

        // This is a long class, but it is organized in smaller regions. Even
        // if you are not using Microsoft Visual Studio (which allows to collapse
        // regions) they are a useful guide. These are the regions, exactly as
        // they appear (so you can copy-search-jump). Searching the keyword
        // "#endregion" will also be useful.

        // #region Properties
        // #region Public Methods
        // #region Menu Selection

        // #region Main menu

        // #region Methods common to several menus
        // #region Main Menu (old)
        // #region Edit modules menu
        // #region Add Module methods
        // #region Edit protected weights screen
        // #region Edit regulation menu
        // #region PlayGUI methods


        private EspNeatOptimizer optimizer;
		private Visualizer visualizer;

		// GuiVariables class encapsulates a number of variables for convenience.
        private GuiVariables guiVar;
        // Here we have all or most Rects used in this class (for icons mostly).
        private GuiButtonRects rects;
        // used to adjust the time scale with a slider in manual selection mode
        private float timeScale = 1.0f; 
        private string savePath;

        // XCoords classes have the position for several elements needed to draw
        // modules or neurons (like labels and so on).
        private List<ModuleCoords> moduleCoords;
        private List<NeuronCoords> inputNeuronCoords;
        private List<NeuronCoords> outputNeuronCoords;

        // Skins used for icons, labels, boxes...
        private GUISkin myLocalSkins;
        GUIStyle normalText = new GUIStyle();

        private Texture2D hourglass = (Texture2D)Resources.Load("Textures/StopWatch");

        // Fast reference for the number of modules. Originally it marked the index
        // for the new module in AddModule methods, later it also allowed to
        // use some methods also when no new module is being created (by using
        // this variable as a loop limit and setting it to either the number
        // of modules or the number of modules + 1 (for AddModule)).
        private int newModule;

        // Not super clever, but simple. Selects which type of connections to
        // show in the weight editing menu. The starting value (currently from
        // 0 to 1) is quite irrelevant.
        private int typeOfConnectionSelector = 1;
        // This selects the module for the weight-editing menu.
        private int displayModule = 1;
        // Some methods (EditInToRegGetInfo) may need to know which module
        // called them.
        private int moduleThatCalled = 0;
        // This is a local copy so we can revert changes to the inputs for
        // a given regulatory neuron.
        private List<newLink> inToRegCopy = new List<newLink>();
        // Some menus can be called from different screens, so with this we can
        // know where to return.
        private MenuScreens previousScreen = MenuScreens.AddModule;

        // Used in the protected weights editing menu.
        private int firstSliderId = 0;
        private int lastSliderId = 1;

		// Used to allow double click options and tooltip delay.
		private const float doubleClickThreshold = 0.3f;
        private float lastClick = 0f;
        private const float tooltipThreshold = 0.5f;
        private float lastMouse = 0f;

        // These are not used yet. The idea is to toggle this options for the
        // evolutionary algorithm (maybe these should be moved to GuiVariables).
        private bool evolveLocalInOut = false;
        private bool evolveProtected = false;

        // private GUIStyle slidersLocalIn;
        private GUIStyle slidersLocalOut;
        private GUIStyle slidersReg;
        // Allows to toggle between reward and punish button skins.
        private bool isEvil = false;

    	// ! We use Awake() instead of a constructor for MonoBehaviour classes.
        /// <summary>
        /// Constructor for MonoBehaviour classes.
        /// </summary>
        void Awake()
        {
            guiVar = new GuiVariables();
            rects = new GuiButtonRects();

            normalText.fontSize = 16;
            normalText.normal.textColor = Color.white;

            lastSliderId = rects.maxSliders;
        }

        #region Properties

        /// <summary>
        /// Sets the optimizer
        /// </summary>
    	public EspNeatOptimizer Optimizer
    	{
    		set { optimizer = value; }
    	}

        /// <summary>
        /// Sets the path to save files.
        /// </summary>
        public string SavePath
        {
            set { savePath = value; }
        }

        public MenuScreens CurrentMenu
        {
            get { return guiVar.CurrentMenu; }
            set { guiVar.CurrentMenu = value; }
        }

        /// <summary>
        /// Remember that in Visualizer the base (moduleId = 0) is not considered,
        /// so module = 0 means the first real module (moduleId = 1).
        /// This is confusing and should probably be changed!
        /// </summary>
        public int ModuleThatCalled
        {
            set { moduleThatCalled = value + 1; }
        }

        /// <summary>
        /// Used in visualizer
        /// </summary>
        public List<List<newLink>> LocalInputList
        {
            get { return guiVar.LocalInputList; }
            set { guiVar.LocalInputList = value; }
        }
        public List<List<newLink>> LocalOutputList
        {
            get { return guiVar.LocalOutputList; }
            set { guiVar.LocalOutputList = value; }
        }
        public List<List<newLink>> RegulatoryInputList
		{
			get { return guiVar.RegulatoryInputList; }
            set { guiVar.RegulatoryInputList = value; }
		}
        public List<newLink> InToRegCopy
        {
            get { return inToRegCopy; }
            set { inToRegCopy = value; }
        }

        /// <summary>
        /// We get the skin with all the styles from Optimizer, because this
        /// script is instantiated at runtime and the skin must be assigned
        /// through the game object's inspector menu in Unity.
        /// </summary>
        public void SetSkin(GUISkin mySkins)
        {
            myLocalSkins = mySkins;

            // slidersLocalIn = myLocalSkins.button;
            slidersLocalOut = myLocalSkins.button;
            slidersReg = myLocalSkins.button;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// We need a set accessor here because Visualizer is a MonoBehaviour class,
        /// so we instantiate it in EspNeatOptimizer via AddComponent (just as we
        /// did with this class!)
        /// </summary>
        public void Initialize(Visualizer visualizerRef)
        {
            visualizer = visualizerRef; 
            moduleCoords = visualizer.ModuleCoords;
            inputNeuronCoords = visualizer.InputNeuronCoords;
            outputNeuronCoords = visualizer.OutputNeuronCoords;
			newModule = moduleCoords.Count;
        }

        /// <summary>
        /// This is only here so we can call it at a different time than
        /// Initialize, which created a conflict of priorities in Visualizer
        /// (Initialize requires InitializeCoords, which requires
        /// UpdateLocalInOutInfo which requires Initialize). By doing this
        /// we can call Initialize without resetting guiVars (and so after
        /// UpdateLocalInOutInfo, which also updates guiVar lists).
        /// </summary>
        public void GuiVarReset()
        {
            guiVar.Reset();
        }

        /// <summary>
        /// Sets the current menu screen selector.
        /// </summary>
        public void SetMenuScreen(MenuScreens chosenScreen)
        {
            guiVar.CurrentMenu = chosenScreen;
        }

        /// <summary>
        /// Modifies the pandemonium group value for a module. Allows increasing
        /// or decraseing the value. Called from visualizer.
        /// totalModules takes into account the module that is being created.
        /// </summary>
        public void ModifyPandemonium(int totalModules, int currentValue,
                                      int module, bool isIncrease)
        {
            // Updates the value.
            if (isIncrease)
            {
                ++currentValue;
                if (currentValue > totalModules)
                {
                    currentValue = 0;
                }
            }
            else
            {
                --currentValue;
                if (currentValue < 0)
                {
                    currentValue = totalModules;
                }
            }
            // Saves the changes.
            guiVar.Pandemonium[module] = currentValue;
            visualizer.UpdatePandemonium(module, currentValue);
        }
        /// <summary>
        /// Used during initialization in visualizer.
        /// </summary>
        public void SetPandemonium(int module, int value)
        {
            guiVar.Pandemonium[module] = value;
        }

        /// <summary>
        /// Old local outputs can be used for new local inputs. However, these
        /// buttons are easier to make from Visualizer. So we need a public
        /// function to update this.
        /// </summary>
        public void CallAddRemoveLocalIn(uint sourceId)
        {
            AddRemoveToList(guiVar.LocalInputList[newModule], sourceId, InformLocalIn);
        }

        #endregion

        #region Menu Selection

        /// <summary>
        /// Here we get the correct screen, depending on the value of our screen 
        /// selector.
        /// </summary>
        void OnGUI()
        {
            GUI.skin = myLocalSkins;

            switch (guiVar.CurrentMenu)
            {
            case MenuScreens.Play:
                PlayGUI();
                break;
            case MenuScreens.Edit:
                visualizer.DrawScheme();
                DefaultMenu();
                break;



            // These are to be either upgraded or deleted!! (With a propper
            // cleaning of no longer used features).

            case MenuScreens.PlayEditWeights:
                // DrawScheme only adds a transparend layer: very optional.
                visualizer.DrawScheme();
                EditOnlyWeights();
                break;
            case MenuScreens.EditModules:
                EditModulesMenu();
                break;
            case MenuScreens.EditInToReg:
                visualizer.DrawScheme();
				EditInToReg();
                break;
            case MenuScreens.EditInToRegGetInfo:
                visualizer.DrawScheme();
                EditInToRegGetInfo();
                break;
            case MenuScreens.AddModule:
                AddModuleGUI();
                break;
            case MenuScreens.MainMenu:
                MainMenuGUI();
                break;
            case MenuScreens.MainMenuReset:
                ResetGui();
                break;
            case MenuScreens.MainMenuResetActive:
                ResetActiveModule();
                break;
            // Any case of AddModule goes to default.
            default:
                AddModuleGUI();
                break;
            }
        }

        /// <summary>
        /// Special screen that displays an schematic view of the network, and 
        /// allows to specify the number of new local output neurons and their
        /// targets.
        /// </summary>
        void AddModuleGUI()
        {
            // In AddModule we would like to see a scheme of our network.
            visualizer.DrawScheme();

            switch (guiVar.CurrentMenu)
            {
            // Default screen showing the scheme and (¿invisible?) option buttons.
            case MenuScreens.AddModule:
				AddModuleScreen();
                break;
            // Adds all input and outputs and then goes to add module screen.
            case MenuScreens.AddModuleInit:
				AddAllLocalIn();
				AddAllLocalOut();
                guiVar.CurrentMenu = MenuScreens.AddModule;
                break;
            // Edit local input for the new module
            case MenuScreens.AddModuleLocalIn:
                EditLocalInScreen();
                break;
            // Edit local output for the new module
            case MenuScreens.AddModuleLocalOut:
                EditLocalOutScreen();
                break;
            // Edit regulation input
            case MenuScreens.AddModuleRegulation:
                EditRegulatoryInput();
                break;
            // Edit label objects 
            case MenuScreens.AddModuleLabels:
                AcceptAndReturn();
                break;
            // Edit protected weights
            case MenuScreens.ProtectedWeights:
                EditWeightsScreen();
                break;
            default:
                break;
            }
        }

        #endregion

        #region Main Menu

        void DefaultMenu()
        {
            
        }

        #endregion

        #region Methods common to several menus

        /// <summary>
        /// A button that returns to the main menu. Used in play and in the
        /// module edition menu.
        /// </summary>
        void ReturnToMainButton()
        {
            if (GUI.Button(rects.returnToMain, "", "Back"))
            {
                ReturnToMainMenu();               
            } 
        }

        /// <summary>
        /// Returns to main menu.
        /// </summary>
        void ReturnToMainMenu()
        {
            visualizer.UpdateScreen(MenuScreens.MainMenu);
            guiVar.CurrentMenu = MenuScreens.MainMenu; 
        }

        /// <summary>
        /// Returns to play menu.
        /// </summary>
        void ReturnToPlay()
        {
            // Return to Play screen
            guiVar.CurrentMenu = MenuScreens.Play;
            visualizer.UpdateScreen(MenuScreens.Play);   
        }

        /// <summary>
        /// Highlights modules on mouse-over. Creates an invisible button that
        /// allows dragging modules. On double-click calls the menu for
        /// modifying protected connections.
        /// </summary>
        void MouseOver(int i)
        {
            // Is the mouse over the module?
            if (moduleCoords[i].dragButton.Contains(Event.current.mousePosition))
            {
                // Started pressing the mouse.
                if (Event.current.type == EventType.MouseDown)
                {
                    moduleCoords[i].dragPressed = true;
                }
                // Released mouse.
                if (Event.current.type == EventType.MouseUp)
                {
                    moduleCoords[i].dragPressed = false;

                    // Is this a double click?
                    if (Time.time - lastClick < doubleClickThreshold)
                    {
                        // For the time being, only allows calling this method
                        // from AddModule.
                        if (guiVar.CurrentMenu == MenuScreens.AddModule)
                        {
                            displayModule = i + 1;
                            ToProtectedWeights();
                        }
                    }
                    lastClick = Time.time;
                }
                // While mouse button is clicked, allows dragging.
                if (moduleCoords[i].dragPressed &&
                    Event.current.type == EventType.MouseDrag)
                {
                    moduleCoords[i].ResetTo(
                        (int)(moduleCoords[i].x + Event.current.delta.x),
                        (int)(moduleCoords[i].y + Event.current.delta.y));
                }

                // Simply highlights the module on mouse-over and action.
                GUI.Button(moduleCoords[i].highLightArea, "", "highlight");
            }            
        }

        /// <summary>
        /// To avoid a single repetition of this...
        /// Selects the menu for changing protected weights.
        /// </summary>
        void ToProtectedWeights()
        {
            // Selects the menu for editing protected weights.
            visualizer.PlusRegulation = false;
            guiVar.CurrentMenu = MenuScreens.ProtectedWeights;
            visualizer.UpdateScreen(MenuScreens.ProtectedWeights);  
        }

        /// <summary>
        /// Every time the user selects a source/target, this method checks if the 
        /// the element should be added (new) or removed (old) to or from the
        /// corresponding list. Then informs Visualizer of any changes.
        /// 
        /// Uses a delegate as input, since this function should be exactly the
        /// same for changes in local input, local output and input to the
        /// regulatory neuron. The only difference is which method should be
        /// called in Visualized to update changes in the screen!
        /// </summary>
        void AddRemoveToList(List<newLink> localList, uint targetId,
            visualizerInform updateMethod)
        {     
            // This expresion returns the index for the element with "otherNeuron"
            // value matching targetId. If none does, returns -1.
            int index = localList.FindIndex(x => x.otherNeuron == targetId);
            // If it was not found:
            if (index < 0)
            {
                // Adds the new element, with default weight:
                newLink newElement = createDefaultWithTarget(targetId);
                localList.Add(newElement);
                updateMethod(newElement.otherNeuron, true);
            }
            else
            {
                // Calls update before deleting the information.
                updateMethod(localList[index].otherNeuron, false);
                // Deletes the element!
                localList.RemoveAt(index);
            }
        }

        /// <summary>
        /// AddRemoveToList needs to inform Visualizer when a list changes.
        /// However, depending on the list we use, we need to call a different
        /// update method in Visualizer.
        /// </summary>
        delegate void visualizerInform(uint index, bool isAddition);
        /// <summary>
        /// First function to be used as visualizerInform. Updates local output
        /// information in Vizualizer.
        /// </summary>
        void InformLocalOut(uint index, bool isAddition)
        {
            visualizer.UpdateLocalOut(index, isAddition);
        }
        /// <summary>
        /// Second function to be used as visualizerInform. Updates local input
        /// information in Vizualizer.
        /// </summary>
        void InformLocalIn(uint index, bool isAddition)
        {
            visualizer.UpdateLocalIn(index, isAddition);
        }
        /// <summary>
        /// Third function to be used as visualizerInform. Does not need to do
        /// anything, since Visualizer accesses guiVar.RegulatoryInputList
        /// directly.
        /// </summary>
        void InformRegulatoryIn(uint index, bool isAddition)
        {
        }

        /// <summary>
        /// Avoids code repetition. Creates a new newLink element with default
        /// weight.
        /// </summary>
        newLink createDefaultWithTarget(uint target)
        {
            newLink newElement = new newLink();
            newElement.otherNeuron = target;
            newElement.weight = 1.0;
            newElement.id = 0;
            return newElement;
        }

        #endregion

        #region Main Menu (old)

        /// <summary>
        /// From the main menu the user may proceed to the evolution menu (PlayGUI)
        /// or to the screen with options for editing modules.
        /// </summary>
        void MainMenuGUI()
        {
            StartEvolutionButton();
            EditModulesButton();
            Exit();
            ResetButton();
        }

        /// <summary>
        /// Goes to play (evolution) screen. Note that if there is no population 
        /// file to load, the creation of a new population will prompt the 
        /// AddModule menu.
        /// </summary>
        void StartEvolutionButton()
        {
            if (GUI.Button(rects.goToEvolution, "", "StartEvolution"))
            {
                if (System.IO.File.Exists(savePath))
                {
                    guiVar.CurrentMenu = MenuScreens.Play; 
                }
                else
                {
                    optimizer.StartGenomePopulation();
                    // NeatGenomeFactory will set AddModule screen.
                }
            } 
        }

        /// <summary>
        /// Opens the edit modules menu.
        /// </summary>
        void EditModulesButton()
        {
            if (GUI.Button(rects.editModules, "", "EditModules"))
            {
                guiVar.CurrentMenu = MenuScreens.EditModules;
                visualizer.UpdateScreen(MenuScreens.EditModules);
            } 
        }

        /// <summary>
        /// This button, offered in MainMenuGUI allows to delete any saved progress.
        /// </summary>
        void ResetButton()
        {
            // Resets neuroevolution (you will lose your evolution time!)
            // Make reset text red as a warning!
            GUI.contentColor = Color.red;
            if (GUI.Button(rects.resetEvolution, "Reset evolution", "CustomButton"))
            {
                // optimizer.StopEA();
                optimizer.DestroyBest();
                guiVar.TryReset = true; 

                // This MenuScreen option in Visualizer will only draw a darker background.
                visualizer.UpdateScreen(MenuScreens.PlayEditWeights);
                guiVar.CurrentMenu = MenuScreens.MainMenuReset;
            }       
        }

        /// <summary>
        /// Deleting save files right away is too dangerous (the user may click on
        /// the button by accident!) Here we ask for confirmation for ResetButton.
        /// </summary>
        void ResetGui()
        {
            GUI.contentColor = Color.white;

            // Provides a clear background
            visualizer.DrawScheme();
            GUI.Label(rects.weightsBackground, "", "WeightsMenu");

            GUI.Label(new Rect(Screen.width / 2 - 300, Screen.height / 2 - 100, 1, 1), 
                guiVar.WarningMessage, guiVar.BigText);

            // Confirm and delete!
            GUI.contentColor = Color.red;
            if (GUI.Button(rects.confirmReset, "DELETE", "CustomButton"))
            {
                optimizer.DeleteSave();
                guiVar.TryReset = false;

                // We have no genomes, so we need to create them again.
                // We also need a new timeStamp in optimizer.
                optimizer.TimeStamp = System.DateTime.Now.ToString("HHmm");
                visualizer.Reset();
                visualizer.UpdateScreen(MenuScreens.AddModule);
                optimizer.StartGenomePopulation();
            } 

            // Return without deleting anything.
            GUI.contentColor = Color.white;
            if (GUI.Button(rects.safeReturn, "Safe return", "CustomButton"))
            {
                guiVar.TryReset = false;
                guiVar.CurrentMenu = MenuScreens.MainMenu;
            }  
        }

        /// <summary>
        /// Ends program.
        /// </summary>
        void Exit()
        {
            if (GUI.Button(rects.exit, "Exit Program", "CustomButton"))
            {
                Application.Quit();
            }
        }

        #endregion

        #region Edit modules menu

        /// <summary>
        /// This menu allows to choose from different options for editing
        /// modules, like adding a new module or resetting the active module.
        /// </summary>
        void EditModulesMenu()
        {
            AddModuleButton();
            ResetActiveModuleButton();
            // Return to main menu
            ReturnToMainButton(); 
        }

        /// <summary>
        /// Used to add a new module to the genome.
        /// </summary>
        void AddModuleButton()
        {
            if (GUI.Button(rects.addModule, "", "AddModule"))
            {
                // If the file does not exists (because it has been deleted at some
                // point) then we have to create a new base.
                if (!System.IO.File.Exists(savePath))
                {
                    optimizer.StartGenomePopulation();
                }
                guiVar.CurrentMenu = MenuScreens.AddModuleInit;
                visualizer.PlusRegulation = true;
                visualizer.UpdateScreen(MenuScreens.AddModule);
                // Do not forget to update the genome in visualizer (this avoids, for
                // example, some minor issues with the information for input to regulatory)
                visualizer.UpdateModelGenome(optimizer.EvolutionAlgorithm.GenomeList[0]);
            } 
        }

        /// <summary>
        /// Calls the reset active module methods.
        /// </summary>
        void ResetActiveModuleButton()
        {
            if (GUI.Button(rects.resetActive, "", "ResetModule"))
            {
                guiVar.CurrentMenu = MenuScreens.MainMenuResetActive;
                // We use PlayEditWeights, since it does the same basic thing
                // (adds a background).
                visualizer.UpdateScreen(MenuScreens.PlayEditWeights);
            }
        }

        /// <summary>
        /// Resets the last module in the genomes.
        /// </summary>
        void ResetActiveModule()
        {
            GUI.contentColor = Color.white;

            // Provides a clear background
            visualizer.DrawScheme();
            GUI.Label(rects.weightsBackground, "", "WeightsMenu");

            const string confirmRemove = "This will reset the active module in " + 
                "all genomes. Do you wish to continue?";

            GUI.Label(new Rect(Screen.width / 2 - 300, Screen.height / 2 - 100, 1, 1), 
                confirmRemove, guiVar.BigText);

            // Confirm and remove last module
            GUI.contentColor = Color.red;
            if (GUI.Button(rects.confirmReset, "RESET", "CustomButton"))
            {
                optimizer.AskResetActiveModule();
                // visualizer.UpdateModelGenome is already called from Factory --> optimizer
                ReturnToMainMenu();
            } 

            // Return without deleting anything.
            GUI.contentColor = Color.white;
            if (GUI.Button(rects.safeReturn, "Safe return", "CustomButton"))
            {
                ReturnToMainMenu();
            } 
        }

        #endregion

        #region Add Module methods
        
		/// <summary>
		/// Creates a few invisible buttons that allow to move modules with
        /// the mouse and also to go to the label-editing screen. 
		/// </summary>
		void AddModuleScreen()
        {
            // These are invisible buttons to allow moving around the modules.
            for (int i = 0; i < newModule; ++i)
            {
                // The button for label editing. Must be here for priority
                // reasons.
                if (GUI.Button(moduleCoords[i].moduleLabelButton, "", "highlight"))
                {
                    SelectLabelEditing();                      
                }

                // This method allows to move the modules. Also calls the menu
                // for modifying protected weights on double click.
                MouseOver(i);
            }

            // We also have two extra buttons to go to the label-editing screen.
            if (GUI.Button(rects.fromInputToLabelEdit, "", "highlight"))
            {
                SelectLabelEditing();
            }
            if (GUI.Button(rects.fromOutputToLabelEdit, "", "highlight"))
            {
                SelectLabelEditing();
            }

            if (GUI.Button(rects.weights, "", "Weights"))
            {
                ToProtectedWeights();  
            }

            // This button allows to accept the choices and create the
            // module.
            if (GUI.Button(rects.acceptAndCreate, "Accept and Create", "CustomButton"))
            {
				CallCreateAndReset();              
            }

            // Return to main menu option.
            if (GUI.Button(rects.backFromAdd, "", "BackFromAdd"))
            {
                if (newModule > 1)
                {
                    ReturnToMainMenu();  
                }
                else
                {
                    UnityEngine.Debug.Log("You need to create the first module!");
                }
            }  
        }  

        /// <summary>
        /// Creates buttons to select local output targets, which can be either
        /// output neurons or regulatory neurons.
        /// </summary>
		void EditLocalOutScreen()
        {
            visualizerInform updateLocalOut = InformLocalOut;

            int targetIndex;
            uint targetId;

            // Output neurons
            for (int i = 0; i < outputNeuronCoords.Count; ++i)
            {
                if (GUI.Button(outputNeuronCoords[i].neuronRect, "", "highlight"))
                {
                    if (optimizer.EvolutionAlgorithm.GenomeList[0].NeuronGeneList.FindTargetOut(
                        i + 1, out targetId))
                    {
                        AddRemoveToList(guiVar.LocalOutputList[newModule],
                                        targetId, updateLocalOut);
                    }                    
                }
            }

            // Regulatory neurons: this includes the regulatory neuron of the
            // same module, which creates a feedback-loop.
            for (int i = 0; i < newModule - 1; ++i)
            {
                if (GUI.Button(moduleCoords[i].regulatoryNeuron, "", "highlight"))
                {
                    if (optimizer.EvolutionAlgorithm.GenomeList[0].NeuronGeneList.FindRegulatory(
                        i + 1, out targetIndex))
                    {
                        targetId = optimizer.EvolutionAlgorithm.GenomeList[0].NeuronGeneList[targetIndex].Id;
                        AddRemoveToList(guiVar.LocalOutputList[newModule],
                                        targetId, updateLocalOut);
                    }
                }
			}
            // The last module is special, because the regulatory neuron does not
            // exist yet. But we know its Id, since it will be the first new
            // element in the module. It has conviniently been added to the
            // Id-to-string dictionary in Visualizer.
            if (GUI.Button(moduleCoords[newModule - 1].regulatoryNeuron, "", "highlight"))
            {
                targetId = optimizer.EvolutionAlgorithm.GenomeList[0].FindLastId() + 1;
                AddRemoveToList(guiVar.LocalOutputList[newModule], targetId,
                                updateLocalOut);
            }

            // Add all/remove all buttons
            if (GUI.Button(moduleCoords[newModule - 1].removeAllOut, "Remove All"))
            {
                RemoveAllLocalOut();
            }
            if (GUI.Button(moduleCoords[newModule - 1].addAllOut, "Add All"))
            {
                AddAllLocalOut();
            }

			AcceptAndReturn();
        }

        /// <summary>
        /// Creates buttons to select local input targets.
        /// </summary>
        void EditLocalInScreen()
        {
            visualizerInform updateLocalIn = InformLocalIn;

            // Input neurons
            for (int i = 0; i < inputNeuronCoords.Count; ++i)
            {
                if (GUI.Button(inputNeuronCoords[i].neuronRect, "", "highlight"))
                {
                    // Remember that for bias and input neurons the index is the
                    // same as their Id and is the first values starting from
                    // 0, so it is the same as our iterator variable.
                    AddRemoveToList(guiVar.LocalInputList[newModule], (uint)i, updateLocalIn);
                }
            }

            // Local output neurons are created in Visualizer.

			// Add all/remove all buttons
            if (GUI.Button(moduleCoords[newModule - 1].removeAllIn, "Remove All"))
			{
				RemoveAllLocalIn();
			}
            if (GUI.Button(moduleCoords[newModule - 1].addAllIn, "Add All"))
			{
                AddAllLocalIn();
			}

            AcceptAndReturn();
        }

        /// <summary>
        /// Modifies the list regulatoryInputList in GuiVariables.
        /// This is only for new modules being added (see EditInToRegGetInfo for
        /// other modules).
        /// </summary>
        void EditRegulatoryInput()
        {
            visualizerInform updateRegulatoryIn = InformRegulatoryIn;

            // Creates buttons at the input neurons.
            for (int i = 0; i < inputNeuronCoords.Count; ++i)
            {
                if (GUI.Button(inputNeuronCoords[i].neuronRect, "", "highlight"))
                {
                    // Remember that for bias and input neurons the index is the
                    // same as their Id and is the first values starting from
                    // 0, so it is the same as our iterator variable.
                    AddRemoveToList(guiVar.RegulatoryInputList[newModule],
                                    (uint)i, updateRegulatoryIn);
                }
            }
            AcceptAndReturn();
        }

        /// <summary>
        /// Creates the menu that allows to change the protected weights in the
        /// new module. Note this method has details that are specific for
        /// AddModule, which is why it is not in Edit protected weights region.
        /// </summary>
        void EditWeightsScreen()
        {
            EditWeightsCommon();

            evolveLocalInOut = GUI.Toggle(rects.allowNewLocal, evolveLocalInOut,
                                          "Allow new local input/output during evolution");

            evolveProtected = GUI.Toggle(rects.allowProtected, evolveProtected,
                                         "Allow evolution of protected weights");
            if (evolveProtected)
            {
            }

            AcceptAndReturn();
        }

        /// <summary>
        /// Returns to the main "AddModule" screen.
        /// </summary>
        void AcceptAndReturn()
        {
            // Creates an invisible button that covers all the screen.
            if (rects.allScreen.Contains(Event.current.mousePosition) &&
                Event.current.type == EventType.MouseUp)
            {
                ReturnToAddModule(); 
            }
        }
        /// <summary>
        /// Returns to AddModule. Also called from EditInToRegGetInfo and ResetGUI.
        /// </summary>
        void ReturnToAddModule()
        {
            // Return to AddModule screen (and enable input regulation button)
            visualizer.PlusRegulation = true;
            guiVar.CurrentMenu = MenuScreens.AddModule;
            visualizer.UpdateScreen(MenuScreens.AddModule);              
        }

        /// <summary>
        /// Simply changes the menu to Label Eiting
        /// </summary>
        void SelectLabelEditing()
        {
            // Select label-editing screen!
            // That screen does not require input-regulation buttons:
            visualizer.PlusRegulation = false;
            visualizer.UpdateScreen(MenuScreens.AddModuleLabels);
            guiVar.CurrentMenu = MenuScreens.AddModuleLabels;            
        }

        /// <summary>
        /// This method allows to remove all currently selected local input.
        /// </summary>
        void RemoveAllLocalIn()
        {
            // Resets the list
            guiVar.LocalInputList[newModule] = new List<newLink>();
            // Asks Visualizer to do the same
            visualizer.RemoveAllLocalIn();
        }

        /// <summary>
        /// This method allows to remove all currently selected local output.
        /// </summary>
        void RemoveAllLocalOut()
        {
            // Resets the list
            guiVar.LocalOutputList[newModule] = new List<newLink>();
            // Asks Visualizer to do the same
            visualizer.RemoveAllLocalOut();
        }

        /// <summary>
        /// Adds all inputs as local input. Note there can also be local output
        /// neurons as input for local input neurons.
        /// </summary>
        void AddAllLocalIn()
        {
            for (uint i = 0; i < (uint)inputNeuronCoords.Count; ++i)
            {
                // If it is not in the list yet, it is added.
                // This expresion returns the index for the element with "otherNeuron"
                // value matching i. If none does, returns -1.
                int index = guiVar.LocalInputList[newModule].FindIndex(x => x.otherNeuron == i);
                if (index < 0)
                {
                    guiVar.LocalInputList[newModule].Add(createDefaultWithTarget(i));
                }                
            }
            // Asks Visualizer to do the same
            visualizer.AddAllLocalIn();
        }

        /// <summary>
        /// Adds all outputs as local output. Note there can also be regulatory
        /// neurons or local input neurons as local output targets.
        /// </summary>
        void AddAllLocalOut()
        {
            for (uint i = (uint)inputNeuronCoords.Count;
                 i < (uint)inputNeuronCoords.Count + (uint)outputNeuronCoords.Count; ++i)
            {
                // If it is not in the list yet, it is added.
                // This expresion returns the index for the element with "otherNeuron"
                // value matching i. If none does, returns -1.
                int index = guiVar.LocalOutputList[newModule].FindIndex(x => x.otherNeuron == i);
                if (index < 0)
                {
                    guiVar.LocalOutputList[newModule].Add(createDefaultWithTarget(i));
                }                
            }
            // Asks Visualizer to do the same
            visualizer.AddAllLocalOut();
        }

        /// <summary>
        /// Simply calls the creation of the new module and resets variables.
        /// Here we accept the details for the new module, which is saved. Then
        /// we are directed to the Play menu to start the evolutionary process.
        /// </summary>
        void CallCreateAndReset()
		{
            if (guiVar.LocalInputList[newModule].Count > 0 &&
                guiVar.LocalOutputList[newModule].Count > 0)
            {
                // It seems clumsy to call this method from a particular member
                // of our genome list. It would be best to call it from here 
                // (but we need to pass a copy of the factory to this script)
                // or from the evolutionary algorithm, which has a copy of factory, 
                // but uses generic variables and the result would be very 
                // convoluted.

                // If the pandemonium value for the new module has not been modified,
                // we need to include it in the dictionary!
                int currentModule = optimizer.EvolutionAlgorithm.GenomeList[0].Regulatory;
                if (!guiVar.Pandemonium.ContainsKey(currentModule))
                {
                    // Default value: 0
                    guiVar.Pandemonium[currentModule] = 0;
                }
                ReturnToMainMenu();

                //optimizer.AskCreateModule(guiVar); 
                // We reset our variables for the next time.
                guiVar.Reset();  
            }
            else
            {
                UnityEngine.Debug.Log("A new module needs at least one local " +
                                      "input and one local output neurons.");
            }

        }

        #endregion

        #region Edit protected weights screen

        /// <summary>
        /// Common code for edit weights screens in add-module menu and from
        /// play menu.
        /// </summary>
        void EditWeightsCommon()
        {
            GUI.Label(rects.weightsBackground, "", "WeightsMenu");
            GUI.Label(rects.selectTypeText, "Show weights for: ", normalText);
            string str = "Current module:         M" + (displayModule).ToString() +
                "—" + visualizer.ModuleLabels[displayModule - 1];
            GUI.Label(rects.currentModule, str, normalText);

            // Buttons to change the module.
            // Left button to decrease the value
            if (GUI.Button(rects.leftButton, "", "leftButton"))
            {
                --displayModule;
                if (displayModule < 1)
                {
                    displayModule = newModule;
                }
            } 
            // Right button to increase the value
            if (GUI.Button(rects.rightButton, "", "rightButton"))
            {
                ++displayModule;
                if (displayModule > newModule)
                {
                    displayModule = 1;
                }
            } 

            switch (typeOfConnectionSelector)
            {
            // Bias + input to local input connections play no role at all!
            // Local output to local input do, but these can be accessed
            // through the local outputs case.
            /*            case 0:
                GUI.Label(rects.fromText, "Source", normalText);
                GUI.Label(rects.weightText, "Weight", normalText);
                Sliders(guiVar.LocalInputList[displayModule]);
                break;*/
            case 0:
                GUI.Label(rects.fromText, "Target", normalText);
                GUI.Label(rects.weightText, "Weight", normalText);
                Sliders(guiVar.LocalOutputList[displayModule]);
                break;
            case 1:
                GUI.Label(rects.fromText, "Source", normalText);
                GUI.Label(rects.weightText, "Weight", normalText);
                Sliders(guiVar.RegulatoryInputList[displayModule]);
                break;
            default:
                break;
            }

            // Bias + input to local input connections play no role at all!
            // Local output to local input do, but these can be accessed
            // through the local outputs case.
            /*          if (GUI.Button(rects.showLocalInput, "Local Input", slidersLocalIn))
            {
                typeOfConnectionSelector = 0;
                // Sets styles to highlight the selected mode.
                slidersLocalIn = myLocalSkins.FindStyle("CustomButton");
                slidersLocalOut = myLocalSkins.button;
                slidersReg = myLocalSkins.button;
            }*/
            if (GUI.Button(rects.showLocalOutput, "Local Output", slidersLocalOut))
            {
                typeOfConnectionSelector = 0;
                // Sets styles to highlight the selected mode.
                // slidersLocalIn = myLocalSkins.button;
                slidersLocalOut = myLocalSkins.FindStyle("CustomButton");
                slidersReg = myLocalSkins.button;
            }
            if (GUI.Button(rects.showRegInput, "Input for Regulatory", slidersReg))
            {
                typeOfConnectionSelector = 1;
                // Sets styles to highlight the selected mode.
                // slidersLocalIn = myLocalSkins.button;
                slidersLocalOut = myLocalSkins.button;
                slidersReg = myLocalSkins.FindStyle("CustomButton");
            }

            // This button automatically sets local outputs to 0.0
            if (GUI.Button(rects.zero, "Set outputs to 0"))
            {
                SetZeroOne(guiVar.LocalOutputList[displayModule], 0.0);
            }
            // This button automatically sets local outputs to 1.0
            if (GUI.Button(rects.one, "Set outputs to 1"))
            {
                SetZeroOne(guiVar.LocalOutputList[displayModule], 1.0);
            }
        }

		/// <summary>
		/// This method allows to change the protected weights in the network.
		/// It is the same as the one in AddModule (thus "EditWeightsCommon")
		/// but in this case only this changes will be saved (as opposed to a
		/// new module and modified weights).
		/// </summary>
		void EditOnlyWeights()
		{
			EditWeightsCommon();

			if (GUI.Button(rects.changeWeights, "Apply Changes"))
			{
				// Accept changes and call factory (through optimizer).
				// We delete the last entry from the lists, since it corresponds
				// to a new module we are not creating.
				guiVar.LocalOutputList.RemoveAt(guiVar.LocalOutputList.Count - 1);
				guiVar.RegulatoryInputList.RemoveAt(guiVar.RegulatoryInputList.Count - 1);

				//optimizer.AskChangeWeights(guiVar);
				ReturnToPlay();   
			}

			// Creates an invisible button that covers all the screen.
			if (rects.allScreen.Contains(Event.current.mousePosition) &&
				Event.current.type == EventType.MouseUp)
			{
				ReturnToPlay();      
			}
		}

        /// <summary>
        /// Creates a slider for each protected weight in the given list.
        /// </summary>
        void Sliders(List<newLink> localList)
        {
            int sliderWidth = 745;

            // If we are trying to show too many sliders at the same time...
            if (rects.maxSliders < localList.Count)
            {
                lastSliderId = firstSliderId + rects.maxSliders;
                // Allows to change the firstSliderId.
                ScrollSliders(localList.Count);
            }
            else
            {
                firstSliderId = 0;
                lastSliderId = localList.Count;  
            }

            int j = 0;
            for (int i = firstSliderId; i < lastSliderId; ++i)
            {
                // Structs are passed by value, so, we cannot directly modify
                // localList[i].weight
                newLink localCopy = localList[i];

                int sliderY = rects.y + 190 + 50 * j;
                ++j;

                localCopy.weight = (double)GUI.HorizontalSlider(new Rect(rects.x + 215, sliderY, sliderWidth, 30),
                    (float)localCopy.weight, -5f, 5f);
                localList[i] = localCopy;

                // From label
                GUI.Label(new Rect(rects.x + 60, sliderY, 100, 40),
                    visualizer.TargetSourceStringById[localCopy.otherNeuron],
                    normalText);
                // Weight Label
                GUI.Label(new Rect(rects.x + 135, sliderY, 100, 40),
                    localCopy.weight.ToString("F2"), normalText);
            }

            if (localList.Count == 0)
            {
                GUI.Label(rects.noConnectionsText, "There are no connections " +
                    "of this type yet", normalText);                
            }
        }

        /// <summary>
        /// In some cases the menu will not be tall enought to fit all the
        /// sliders required for a type of connections (say local input protected
        /// connections). In this case we restrict the nomber of sliders that
        /// are shown simultaneously, and here we allow to scroll through them.
        /// 
        /// For simplicity, first we implement buttons (and in the future
        /// perhaps proper scrolling with the mouse wheel).
        /// </summary>
        void ScrollSliders(int maxValue)
        {
            if (GUI.Button(rects.upButton, "", "UpButton"))
            {
                if (firstSliderId > 0)
                {
                    --firstSliderId;
                }
            }

            if (GUI.Button(rects.downButton, "", "DownButton"))
            {
                if (lastSliderId < maxValue)
                {
                    ++firstSliderId;
                }
            } 
        }

        /// <summary>
        /// Sets the weights of local_output protected-connections to either
        /// 1 or 0 (for the chosen module!).
        /// </summary>
        void SetZeroOne(List<newLink> localList, double zeroOne)
        {
            // Sets values to one or zero.
            for (int i = 0; i < localList.Count; ++i)
            {
                // Structs are passed by value, so, we cannot directly modify
                // localList[i].weight
                newLink localCopy = localList[i];
                localCopy.weight = zeroOne;
                localList[i] = localCopy;
            }            
        }

        #endregion

        #region Edit regulation menu

		/// <summary>
		/// Here the user can modify connections from bias/input to regulatory
        /// neurons in an existing population. During AddModule this can be done
        /// for the module that is being created.
		/// </summary>
		void EditInToReg()
		{
            // Allows dragging modules (also calls weight modification on
            // double click, currently disabled from here).
            for (int i = 0; i < newModule; ++i)
            {
                MouseOver(i);
            }

            // Creates an invisible button that covers all the screen.
            if (rects.allScreen.Contains(Event.current.mousePosition) &&
                Event.current.type == EventType.MouseUp)
            {
                ReturnToPlay();
            }

            if (GUI.Button(rects.inToRegApply, "Update regulation", "CustomButton"))
            {
                // Applies the changes using NeatGenomeFactory.
                //optimizer.AskUpdatePandem(guiVar);
                ReturnToPlay();
            }    
		}

        /// <summary>
        /// Here we can edit the list with the input for a given regulatory
        /// neuron (except for the new module in AddModule, which is done in
        /// EditRegulatoryInput()).
        /// We can apply or discard these changes.
        /// </summary>
        void EditInToRegGetInfo()
        {
            visualizerInform updateRegulatoryIn = InformRegulatoryIn;

            // Creates buttons at the input neurons.
            for (int i = 0; i < inputNeuronCoords.Count; ++i)
            {
                if (GUI.Button(inputNeuronCoords[i].neuronRect, "", "highlight"))
                {
                    // Remember that for bias and input neurons the index is the
                    // same as their Id and is the first values starting from
                    // 0, so it is the same as our iterator variable.
                    // In Visualizer module = 0 is the first module (not the
                    // base), which takes moduleId = 1. Perhaps this should change, 
                    // since it is clearly confusing. In any case, it has been
                    // taken into account in ModuleThatCalled.
                    AddRemoveToList(guiVar.RegulatoryInputList[moduleThatCalled],
                                    (uint)i, updateRegulatoryIn);
                }
            }

            if (GUI.Button(rects.inToRegApply, "Apply", "CustomButton"))
            {
                // Applies the changes using NeatGenomeFactory.
                //optimizer.AskUpdateInToReg(guiVar, moduleThatCalled);

                ReturnToPrevious();
            }
            if (GUI.Button(rects.inToRegDiscard, "Discard", "CustomButton"))
            {
                // Resets guiVar.RegulatoryInputList[moduleThatCalled]
                guiVar.RegulatoryInputList[moduleThatCalled] = inToRegCopy;

                ReturnToPrevious();
            }
        }

        /// <summary>
        /// Returns from the menu to edit the input for a regulatory neuron.
        /// This can either be called from AddModule or from Play screens.
        /// </summary>
        void ReturnToPrevious()
        {
            if (previousScreen == MenuScreens.AddModule)
            {
                ReturnToAddModule();
            }
            else
            {
                ReturnToPlay();
            }
            previousScreen = MenuScreens.AddModule;
        }

        #endregion

        #region PlayGUI methods

        /// <summary>
        /// Play menu. Start evolution, instantiate champion or edit current
        /// modules (protected weights and regulation).
        /// </summary>
        void PlayGUI()
        {
            TimeSlider();

            if (optimizer.GetEARUnning)
            {
                // We enter manual mode, which has some specific interface:
                if (optimizer.Manual == true)
                {  
                    // Manual evolution GUI.
                    ManualGUI();
                    StopInteractiveEvolButton();
                }
                else
                {
                    StopFitnessBasedEvolButton();           
                }

                // Information button: current generation and maximum fitness.
                GUI.Button(rects.info, string.Format("Generation: {0}\nFitness: {1:0.00}", 
                    optimizer.Generation, optimizer.Fitness));  
            }
            else
            {
                // No evolution running. Options for starting evolution, creating
                // instances of champions, and for editing the current modules.
                StartFitnessBasedEvolButton();
                StartInteractiveEvolButton();
                InstantiateChampionButton();
                KillChampionsButton();
                EditProtectedWeightsButton();
                EditRegulationButton();
                ReturnToMainButton();
                var hover = GUI.tooltip;
                DisplayTooltips(hover);
            }       
        }

        /// <summary>
        /// Specific GUI elements for the manual selection of units mode
        /// </summary>
        void ManualGUI()
        {
            // A button to procede to a new manual generation
            if (GUI.Button(rects.advanceGeneration, "Next Generation", "CustomButton"))
            {
                optimizer.EndManual();
                optimizer.EaIsNextManual(true);
            }

            // A button (with two skins!) to change between reward and punishment
            if (isEvil)
            {
                if (GUI.Button(rects.punishReward,
                    new GUIContent("", "Toggle reward/punishment"),
                    "EvilGood"))
                {
                    optimizer.TogglePunishReward();
                    ToggleEvilGood();
                }                
            }
            else
            {
                if (GUI.Button(rects.punishReward,
                    new GUIContent("", "Toggle reward/punishment"),
                    "GoodEvil"))
                {
                    optimizer.TogglePunishReward();
                    ToggleEvilGood();
                }                
            }

            var hover = GUI.tooltip;
            DisplayTooltips(hover);
        }

        /// <summary>
        /// Starts fitness-based evolution.
        /// </summary>
        void StartFitnessBasedEvolButton()
        {
            if (GUI.Button(rects.startEvolution, 
                new GUIContent("", "Fitness-based evolution"),
                "AutoEvolution"))
            {
                // Gets rid of units created with "run best" option.
                optimizer.DestroyBest();
                optimizer.StartEA();
            }
        }

        /// <summary>
        /// Stops fitness-based evolution.
        /// </summary>
        void StopFitnessBasedEvolButton()
        {
            // Button to stop neuroevolution.
            if (GUI.Button(rects.stopEvolution, "", "StopEvolution"))
            {
                optimizer.StopEA();
            }         
        }

        /// <summary>
        /// Starts interactive evolution.
        /// </summary>
        void StartInteractiveEvolButton()
        {
            if (GUI.Button(rects.manualSelection,
                new GUIContent("", "Interactive evolution"),
                "ManualSelect"))
            {
                // Gets rid of units created with "run best" option.
                optimizer.DestroyBest();
                optimizer.Manual = true;
                optimizer.StartEA();
            }
        }

        /// <summary>
        /// Stops interactive evolution.
        /// </summary>
        void StopInteractiveEvolButton()
        {
            if (GUI.Button(rects.stopEvolution, "", "StopEvolution"))
            {
                StopManual();
            }            
        }

        /// <summary>
        /// Instantiates a unit using the current champion genome.
        /// </summary>
        void InstantiateChampionButton()
        {
            if (GUI.Button(rects.runBest,
                new GUIContent("", "Create champion instance"),
                "PlayBest"))
            {
                optimizer.RunBest();
            }
        }

        /// <summary>
        /// Kills all units created with InstantiateChampionButton.
        /// </summary>
        void KillChampionsButton()
        {
            if (GUI.Button(rects.clearBest, 
                new GUIContent("", "Delete champion instances"),
                "DeleteBest"))
            {
                optimizer.DestroyBest();
            }            
        }

        /// <summary>
        /// Calls the menu for editing protected weights.
        /// </summary>
        void EditProtectedWeightsButton()
        {
            if (GUI.Button(rects.onlyWeights, 
                new GUIContent("", "Edit special weights"),
                "Weights"))
            {
                visualizer.UpdateScreen(MenuScreens.PlayEditWeights);
                guiVar.CurrentMenu = MenuScreens.PlayEditWeights;
                PrepareModuleEdition();
            }
        }

        /// <summary>
        /// Calls the menu for editing the regulation of modules. In particular
        /// pandemonium values and input used by regulatory neurons.
        /// </summary>
        void EditRegulationButton()
        {
            // Bias/in to regulatory connections modification menu
            if (GUI.Button(rects.inToReg, 
                new GUIContent("Regulation menu", "Edit input to regulatory"),
                "CustomButton"))
            {
                previousScreen = MenuScreens.Play;
                visualizer.UpdateScreen(MenuScreens.EditInToReg);
                guiVar.CurrentMenu = MenuScreens.EditInToReg;
                PrepareModuleEdition();
            }
        }

        /// <summary>
        /// Avoids repetition (this would be called, for instance, before
        /// going to the weight editing menu, or before going to the menu for
        /// editing in/bias to regulatory connections. Perhaps in more cases in
        /// the future).
        /// </summary>
        void PrepareModuleEdition()
        {
            // We analyse the genome again, just in case.
            visualizer.UpdateModelGenome(optimizer.EvolutionAlgorithm.GenomeList[0]);
            // We are not adding a new module this time, so we are not
            // interested in the new module.
            --newModule;
            displayModule = newModule;

        }

        /// <summary>
        /// Displays mouse tooltips (mouse-over texts).
        /// </summary>
        void DisplayTooltips(string hover)
        {
            if (hover != "")
            {               
                if (Time.time - lastMouse > tooltipThreshold)
                {
                    GUI.Label(new Rect(50 + Input.mousePosition.x,
                                       Screen.height - Input.mousePosition.y, 175, 30),
                              hover, "box");
                }

                if (HasMouseMoved())
                {
                    lastMouse = Time.time;
                }
            }
        }

        /// <summary>
        /// Determines whether the mouse has moved.
        /// Taken from http://goo.gl/VRyqwr by timuther, Jan 06 at 11:31.
        /// </summary>
        bool HasMouseMoved()
        {
            return (Input.GetAxis("Mouse X") != 0) || (Input.GetAxis("Mouse Y") != 0);
        }      

        /// <summary>
        /// Toggles the variable that determines the skin for the Reward/Punish
        /// button.
        /// </summary>
        void ToggleEvilGood()
        {
            if (isEvil)
            {
                isEvil = false;
            }
            else
            {
                isEvil = true;
            }
        }

        void StopManual()
        {
            // Abort manual selection: Here we mix instructions saying the next
            // round will be manual (so we avoid automatic trial of units)
            // with preparations to end manual mode and ready auto mode.
            // Thanks to this StopEA will be fast and Manual will work fine 
            // when selected again.

            // Note that if you select auto after this the first generation will
            // not have proper fitness values. But evaluation is the first step
            // when a evolutionary algorithm starts, so this is not a problem.

            // Old fitness values will be recovered and used to produce offspring.
            optimizer.SetAborted();
            optimizer.EndManual();        
            // Automatic testing of units disabled (we will abort, this is not needed).
            optimizer.EaIsNextManual(true);
            optimizer.GoThenAuto();
            optimizer.StopEA();            
        }

		/// <summary>
		/// A slider to select the speed of the simulation.
		/// </summary>
		void TimeSlider()
		{
			// Slider to adjust the time scale (which can be too fast for visual
			// selection of the units!
			// 0 and 30 are min and max values (use variables if this becomes relevant)
            timeScale = GUI.HorizontalSlider(rects.timeScale, timeScale, 0.0f, 30.0f); 
            Time.timeScale = timeScale;
			// Stopwatch icon to hint the meaning of the slider
			GUI.DrawTexture(rects.hourGlass, hourglass, ScaleMode.ScaleToFit, true, 0);			
		}

        #endregion
    }
}

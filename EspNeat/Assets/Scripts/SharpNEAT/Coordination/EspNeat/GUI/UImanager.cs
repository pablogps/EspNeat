using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SharpNeat.Genomes.Neat;
using SharpNeat.Network;

namespace SharpNeat.Coordination
{
    /// <summary>
    /// TO BE WRITEN
    /// </summary>
    public class UImanager : MonoBehaviour {

        // Modules always take the correct Id (where module 0 is the base of
        // the genome and has no local inputs or outputs). This includes lists
        // that are sorted by module: module index 0 will correspond to the base
        // even if this will be empty. This avoids some confusion.

		private EspNeatOptimizer optimizer;
        private NeatGenome genome;
		private string namesPath;
		private string hierarchyPath;
		private string eventsRecordPath;

        private Camera backgroundCamera;
        private Camera editingCamera;
        private Camera evolutionCamera;
        private GameObject editingCanvas;
        private GameObject evolutionCanvas;

        private List<GameObject> myModules;
        private List<GameObject> inputNeurons;
        private List<GameObject> outputNeurons;

        // We need to be able to access the time-scale sliders, so that we can
        // reset the right time-scales when starting and ending evolutionary
        // processes.
        private GameObject sliderEvolution;
        private GameObject sliderChampDisplay;

        // These are used to save the real network connectivity if provisional
        // changes are intended (to evolve only one module, for example)
        private bool isCopyInUse = false;
        private Dictionary<int, int> pandemoniumCopy;
        private Dictionary<int ,List<newLink>> localOutputListCopy;
        private Dictionary<int, List<newLink>> regulatoryInputListCopy;

        // GuiVariables class encapsulates a number of variables for convenience.
        private UIvariables uiVar;
        // Labels for the input neurons in the system
		private List<string> inputLabels;
        // Labels for the output neurons
		private List<string> outputLabels;
        // Human-given name for modules
        private Dictionary<int, string> moduleLabels;
        private Dictionary<int, string> pandemoniums;
		// private List<int> pandemoniumsInt;
		// List with the list of local in/out targets (as string) in each module.
		private Dictionary<int, List<string>> localInSources;
		private Dictionary<int, List<string>> localOutTargets;
		// This dictionary has the string with type and order value of input,
		// output and regulatory neurons. The first local output neuron will 
		// be "O1", the third regulatory will be "R3", and so on.
		private Dictionary<uint, string> neuronStringFromId;

        private int numberOfModules = 0;

        private bool isMoveRight = false;
        private bool isMoveLeft = false;
        private bool isMoveUp = false;
        private bool isMoveDown = false;

		private GameObject punishOrReward1;
		private GameObject punishOrReward2;

		#region Initialize

        /// <summary>
        /// Constructor for MonoBehaviour classes.
        /// </summary>
        void Awake()
        {
            optimizer = GetComponent<EspNeatOptimizer>();

            backgroundCamera = GameObject.Find("BackgroundCamera").GetComponent<Camera>();
            editingCamera = GameObject.Find("EditingCamera").GetComponent<Camera>();

            // Note .Find() only works on active GameObjects! This is why
            // EvolutionCamera starts active and is immediately set innactive.
            evolutionCamera = GameObject.Find("EvolutionCamera").GetComponent<Camera>();
            evolutionCamera.gameObject.SetActive(false);

            // We get this before ScreenSpaceCanvasEvolution, because Find only
            // works with active elements (and after the canvas is found it is 
            // set as inactive, which affects its children, whereas the children
            // may be set as inactive and the canvas can still be found!)
            punishOrReward1 = GameObject.Find("ToggleGoodOrEvil(1)");
            punishOrReward2 = GameObject.Find("ToggleGoodOrEvil(2)");
            punishOrReward2.SetActive(false);

            // Looks for the time-scale sliders. Do this before the evolution
            // canvas (with all its children) is turned off!
            sliderChampDisplay = GameObject.Find("TimeScaleSlider");
            sliderEvolution = GameObject.Find("TimeScaleSliderEvolution");

            editingCanvas = GameObject.Find("ScreenSpaceCanvas");
            evolutionCanvas = GameObject.Find("ScreenSpaceCanvasEvolution");
            evolutionCanvas.gameObject.SetActive(false);

            // Because magic
            // (Seriously: if this nonsensical piece of code is removed
            // world-reference canvas elements in module gameObjects will
            // not work. There must be a reason for this, beacuse it worked
            // fine before.)
            editingCamera.gameObject.SetActive(false);
            editingCamera.gameObject.SetActive(true);

            Reset();
        }

        /// <summary>
        /// Called each frame.
        /// </summary>
        void Update()
        {
            MoveModules();
        }

		/// <summary>
		/// This could go in Awake, but this way we may call this procedure at
        /// other times.
		/// </summary>
		public void Reset()
        {
            myModules = new List<GameObject>();
            inputNeurons = new List<GameObject>();
            outputNeurons = new List<GameObject>();
            uiVar = new UIvariables();
			inputLabels = new List<string>();
			outputLabels = new List<string>();
			moduleLabels = new Dictionary<int, string>();
            localInSources = new Dictionary<int, List<string>>();
            localOutTargets = new Dictionary<int, List<string>>();
		}

		/// <summary>
		/// Gets the genome to represent. Note that for this crude representation
		/// any genome in the population is the same.
		/// </summary>
		public void UpdateModelGenome(NeatGenome inGenome)
        {
			// We must wait until we get our genome to create default labels
			// (otherwise we do not know how many neurons to expect).
			genome = inGenome;
            uiXmlIO.Genome = genome;

            // No matter the order and IDs, there must be one and only one
            // regulatory neuron per module.
            numberOfModules = genome.Regulatory;

            // This one is a fairly complicated method (with submethods). 
            // But there is no easy way to avoid this, at least when loading
            // a new genome. Also, it is actually quite fast in real time
            // and will only be called from time to time, so it is not really
            // something to worry about.
            ResetUI();
            UpdateLocalInOutInfo();
            UpdatePandemoniums();
            UpdateLabels();
            UpdateHierarchy();
            InitializeRegulatoryInputList();
		}

		/// <summary>
		/// Instantiates the loaded elements: modules, input/output neurons
		/// </summary>
		public void InstantiateLoadedElements()
		{
            UpdateHierarchy();
  
            Coroutiner.StartCoroutine(InstantiateLoadedModules());
            InstantiateInputOutput();
		}

        #endregion

        #region Properties

        public NeatGenome Genome
        {
            get { return genome; }
        }

		#endregion

		#region PublicMethods

        /// <summary>
        /// This is used to write events to the events record file. For instance,
        /// "reset used".
        /// </summary>
		public void WriteToRecord(string info)
		{
			using (System.IO.StreamWriter file =
				    new System.IO.StreamWriter(eventsRecordPath, true))
			{
                file.WriteLine(info);
			}
		}

        /// <summary>
        /// Instead of passing the three individually, we pass the base address
        /// and make the rest here.
        /// </summary>
		public void SetPaths(string baseFilesPath)
		{
			namesPath = baseFilesPath + ".names.xml";
			uiXmlIO.NamesPath = namesPath;

			hierarchyPath = baseFilesPath + ".hierarchy.xml";
			uiXmlIO.HierarchyPath = hierarchyPath;

			eventsRecordPath = baseFilesPath + ".eventsRecord.dat";
		}

        /// <summary>
        /// Moves modules further or closer upon mouse scroll. Essentially
        /// a zoom option that only affects modules.
        /// </summary>
        public void Scroll()
        {
            for (int i = 0; i < myModules.Count; ++i)
            {
                Vector3 auxPosition = myModules[i].transform.position;
                auxPosition.y += 10f* Input.GetAxisRaw("Mouse ScrollWheel");
                myModules[i].transform.position = auxPosition;
            }
        }

        /// <summary>
        /// All of these switch on and off movement in any of the four axes.
        /// </summary>
        public void StartRight()
        {
            isMoveRight = true;
        }
        public void StopRight()
        {
            isMoveRight = false;
        }
        public void StartLeft()
        {
            isMoveLeft = true;
        }
        public void StopLeft()
        {
            isMoveLeft = false;
        }
        public void StartUp()
        {
            isMoveUp = true;
        }
        public void StopUp()
        {
            isMoveUp = false;
        }
        public void StartDown()
        {
            isMoveDown = true;
        }
        public void StopDown()
        {
            isMoveDown = false;
        }

        /// <summary>
        /// Goes through all regulation modules and sets the variable 
        /// beingDragged as false.
        /// </summary>
        public void SetAllRegIddle()
        {
            // TODO: Take this chance to also change the order in layer!
            foreach (int regModuleId in uiVar.hierarchy.Keys)
            {
                GameObject parentModule = FindModuleWithId(regModuleId);
                parentModule.GetComponent<RegModuleController>().BeingDragged = false;
            }
        }

        /// <summary>
        /// This method resets the active module (and creates a new random
        /// population for it!)
        /// 
        /// This method could be called directly from ModuleController, but
        /// it may be best if all interaction with Optimizer is kept within
        /// this class.
        /// </summary>
        public void AskResetActiveModule()
        {
            optimizer.AskResetActiveModule();
            RestartSimulation();
        }

        /// <summary>
        /// Adds a local output to the regulation module (the active module)
        /// and connects it to the regulatory neuron of the module that
        /// triggered the process.
        /// </summary>
        public void AskAddModuleToRegModule(newLink localOutInfo)
        {
            optimizer.AskAddModuleToRegModule(uiVar, localOutInfo);
        }

        /// <summary>
        /// Cloning regulation modules is more complicated, because we need
        /// to clone the children as well, then we need to move the new children
        /// inside the cloned parent and update their regulation.
        /// 
        /// Note this can be recursive if one child is a regulation module itself!
        /// </summary>
        public void AskCloneRegModule(int parentModule, bool isRegulationModule)
        {
            // After creating each module we need to wait a bit so the module
            // can find a place to stop. If two are created at the same time
            // at the same place they will try to move away from each other
            // together!
            Coroutiner.StartCoroutine(AskCloneRegModule2(parentModule, isRegulationModule));
        }

        /// <summary>
        /// This is used to clone a module. Note two things:
        /// 1) The cloned module will never be the active module! (It will be placed
        /// immediately before.)
        /// 2) Complex modules (such as regulation modules) will need some further
        /// work to make sure the connexions among modules are correct.
        /// </summary>
        public void AskCloneModule(int whichModule, bool isRegulationModule)
        {
            // To increase the Dictionaries with necessary module information
            // we first need to know the new module ID:
            int newId = genome.FindYoungestModule() + 1;

            // The new Id is added to the list of modules
            uiVar.moduleIdList.Add(newId);

            // Adds a basic-regulation connection to the list
            IncreaseRegulationListBasic(newId);

            // Cloned modules are outside of a pandemonium (except the children
            // of regulation modules)
            uiVar.pandemonium.Add(newId, 0);
            uiVar.localInputList.Add(newId, uiVar.localInputList[whichModule]);
            uiVar.localOutputList.Add(newId, uiVar.localOutputList[whichModule]);

			// Does the actual work of cloning the genome
            optimizer.AskCloneModule(uiVar, whichModule);

            if (isRegulationModule)
            {
                // If this is a regulation module, it is added to the dictionary,
                // but the list of contents is currently empty!
                uiVar.hierarchy.Add(newId, new List<int>());
            }

            RestartSimulation();

            // Finally the UI object is instantiated
            moduleLabels.Add(newId, "Module" + newId.ToString());

            // Saves the new labels
            SaveLabels();

            // Saves the hierarchy
            SaveHierarchy();

            GameObject newModule = null;
            if (isRegulationModule)
            {
                newModule = InstantiateOneModule(newId, true);
            }
            else
            {
                newModule = InstantiateOneModule(newId, false);
            }

            // Sets the new module as INactive
            newModule.GetComponent<ModuleController>().SetActive(false);

            // Stores the module in the list
            myModules.Add(newModule);
        }

        public void AskDeleteRegulation(int whichModule, List<GameObject> children)
        {
            // Gets parent game object
            int parentIndex = getModuleIndexById(whichModule);
            GameObject parent = myModules[parentIndex];

            // Removes the parent entry from the hierarchy dictionary
            uiVar.hierarchy.Remove(whichModule);
            SaveHierarchy();

            // Deletes the parent first (this way we do not have to rewire
            // its output so as to avoid trouble!
            parent.GetComponent<RegModuleController>().CallBaseDelete();

            // Deletes the children
            for (int i = children.Count - 1; i >= 0; --i)
            {
                ModuleController childController =
                        children[i].GetComponent<ModuleController>();

                int moduleId = childController.ModuleId;

                childController.CallDelete();
            }
        }

        /// <summary>
        /// This method deletes the chosen module.
        /// 
        /// This method could be called directly from ModuleController, but
        /// it may be best if all interaction with Optimizer is kept within
        /// this class.
        /// 
        /// THIS METHOD IS STILL PENDING IMPROVEMENTS. Specific debugging is
        /// required. Also: at the moment it does what it says, blindly.
        /// We need to address the case where connections from local output
        /// neurons go to other regulatory neurons or local input neurons
        /// (and also the case where this module takes local input from other
        /// module's local output!)
        /// </summary>
        public void AskDeleteModule(int whichModule)
        {
            // Removes the Module UI element
            // Do not forget about the options and regulation panel!
            int index = getModuleIndexById(whichModule);
            // Gets a reference to the element we desire to delete
            GameObject module = myModules[index];
            // The element is removed from the list
            myModules.RemoveAt(index);
            // And is actually destroyed (it exist even if it is not 
            // in the list!)
            module.GetComponent<ModuleController>().RemoveModule();

            // Remove from moduleLabels dictionary
            moduleLabels.Remove(whichModule);
            SaveLabels();

            // The lists with input, output, labels and so on (here and in
            // the uiVar instance of UIvariables will be updated at the end
            // of the process in the factory, which calls _optimizer.ResetGUI();
            optimizer.AskDeleteModule(whichModule);
        }

        /// <summary>
        /// Sets whichModule as the active module in the genome
        /// </summary>
        public void SetModuleActive(int whichModule)
        {
            optimizer.AskSetActive(uiVar, whichModule);

            // Update active module:
            // Old active module:
            DeactivateCurrentActive();

            // New active module:
            ActivateModule(whichModule);

            // This method first clones the champion for all individuals, and
            // then moves whichModule to the end of the genome. At this point
            // all of them are equal! So here we mutate them (not the first
            // one, so there is at least one copy of the champion).
            optimizer.AskMutateOnce();      
        }

        /// <summary>
        /// Gets a module ID different from that of the caller, and sets that
        /// module as active.
        /// </summary>
        public void SetAnotherActive(int whichModule)
        {
            if (uiVar.moduleIdList.Count > 1)
            {
                for (int i = 1; i < uiVar.moduleIdList.Count; ++i)
                {
                    int candidateId = uiVar.moduleIdList[i];
                    if (candidateId != whichModule)
                    {
                        SetModuleActive(candidateId);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Launchs the evolution.
        /// </summary>
        public void LaunchEvolution()
        {
            // Sets time-scale to the corresponding value
			sliderEvolution.GetComponent<TimeSliderController>().SetTimeScale();

            Coroutiner.StartCoroutine(LaunchEvolution2());
        }
        /// <summary>
        /// We want to make sure that all champion genomes have been completely
        /// removed before proceeding!
        /// No problems had been yet identified for this reason, but better safe
        /// than sorry?
        /// </summary>
        IEnumerator LaunchEvolution2()
        {
            // MakeOnlyActiveMod ends with ApplyChanges, which resets champions.
            // This takes some time, so we need to wait before trying to destroy
            // champions (otherwise we will attempt to remove them, but a champion
            // will be instantiated AFTER that!). A bit messy all this (my
            // inexperience with IEnumerators perhaps).
            int waitForFrames = 20;
            while (waitForFrames >= 0)
            {
                --waitForFrames;
                yield return null;
            }

            // Gets rid of units created with "run best" option.
            if (optimizer.ChampRunning)
            {
                optimizer.DestroyBest();
            }
            optimizer.Manual = true;

            // Wait before champion genomes are deleted...
            waitForFrames = 20;
            while (waitForFrames >= 0)
            {
                --waitForFrames;
                yield return null;
            }

            optimizer.StartEA();

            // Set evolution camera
            ActivateEvolutionCamera();
        }

		/// <summary>
		/// This is used to visualize champions in the bigger evolution screen.
		/// The goal is to have more resolution in screenshots for papers and the like.
		/// </summary>
		public void UseEvolutionCameraSecretFunction()
		{
			ActivateEvolutionCamera();
		}

		public void UseEditingCameraSecretFunction()
		{
			DeactivateEvolutionCamera();
		}

        /// <summary>
        /// EspNeat is intended to be used with interactive evolution. However, 
        /// for research porpuses it may be useful to compare with automatic
        /// evolution. Note this is not fully-tested.
        /// 
        /// This does not take care of champion-genome instances.
        /// </summary>
        public void LaunchAutoSecretFunction()
        {
            // Sets time-scale to the corresponding value
            sliderEvolution.GetComponent<TimeSliderController>().SetTimeScale();
            optimizer.StartEA();

            // Set evolution camera
            ActivateEvolutionCamera();
        }

        /// <summary>
        /// Complement to LaunchAutoSecretFunction.
        /// </summary>
        public void StopAutoSecretFunction()
        {
            optimizer.SimpleSavePopulation();
            optimizer.StopEA();

            // Sets editing camera
            DeactivateEvolutionCamera();

            // Sometimes evolution uses a version of the genome with some changes
            // (so, for example, the activity of other modules is hidden to see
            // clearly what the evolving module is doing). Those changes only
            // affect protected properties (regulation and output weights) that
            // may NOT change during evolution. Now they are loaded again.
            //ReloadSavedUiVar();

            // Resets time-scale to the desired value
            sliderChampDisplay.GetComponent<TimeSliderController>().SetTimeScale();
            // If the timeScale is 0 the program will not remove the old units
            // and problems may happen: set to small value.
            if (Time.timeScale < 0.05f)
            {
                Time.timeScale = 0.05f;
            }
        }

        /// <summary>
        /// When evolving a module, we only want to see the behaviour of the selected 
        /// module (not mixed with other modules) if possible!
        /// </summary>
        public void MakeOnlyActiveMod(int chosenModule, newLink newRegulation,
                                      List<int> childrenId)
        {
            // Makes copy of the weights and connections
            MakeUiVarCopy();

            // Goes through all modules, and decides what to do
            foreach (int moduleId in uiVar.moduleIdList)
            {
                if (!childrenId.Contains(moduleId))
                {
                    if (moduleId != chosenModule)
                    {
                        // This is a normal module (makes output 0!)
                        MakeOutputNull(moduleId);
                    }
                    else
                    {
                        // This is the module that called (make always active)
                        MakeAlwaysActive(chosenModule, newRegulation);
                    }
                }
                else
                {
                    // Does nothing with children (they should keep their outputs
                    // because through them we understand regulation modules, and
                    // the same applies to their regulation schemes, which will
                    // take no influence from other modules outside of this group
                    // since those modules will have null outputs)
                }
            }

            // Saves the population (simple save)
            ApplyChanges();
        }

        /// <summary>
        /// Proceeds to a new generation (interactive evolution)
        /// </summary>
        public void NextGeneration()
        {
            optimizer.EndManual();
            optimizer.EaIsNextManual(true);
        }

        /// <summary>
        /// Used to reward selected items in evolution, or to punish them
        /// </summary>
        public void TogglePunishReward()
        {
            optimizer.TogglePunishReward();
            if (punishOrReward1.activeSelf)
            {
                // We record this action:
                WriteToRecord("Set to punish mode");
                punishOrReward1.SetActive(false);
                punishOrReward2.SetActive(true);
            }
            else
            {
                // We record this action:
                WriteToRecord("Set to reward mode");
                punishOrReward1.SetActive(true);
                punishOrReward2.SetActive(false);
            }
        }

        public void StopInteractiveEvolution()
        {
            // Abort manual selection: Here we mix instructions saying the next
            // round will be manual (so we avoid automatic trial of units)
            // with preparations to end manual mode and ready auto mode.
            // Thanks to this StopEA will be fast and Manual will work fine 
            // when selected again.

            // Note that if you select auto after this the first generation will
            // not have proper fitness values. But evaluation is the first step
            // when a evolutionary algorithm starts, so this is not a problem.
            optimizer.StopInteractiveEvolution();

            // Sets editing camera
            DeactivateEvolutionCamera();

            // Sometimes evolution uses a version of the genome with some changes
            // (so, for example, the activity of other modules is hidden to see
            // clearly what the evolving module is doing). Those changes only
            // affect protected properties (regulation and output weights) that
            // may NOT change during evolution. Now they are loaded again.
            ReloadSavedUiVar();

            // Resets time-scale to the desired value
            sliderChampDisplay.GetComponent<TimeSliderController>().SetTimeScale();
            // If the timeScale is 0 the program will not remove the old units
            // and problems may happen: set to small value.
            if (Time.timeScale < 0.05f)
            {
                Time.timeScale = 0.05f;
            }
        }

		/// <summary>
		/// Saves changes and restarts simulation if needed
		/// </summary>
		public void ApplyChanges()
		{    
            optimizer.SimpleSavePopulation();
            RestartSimulation();
		}

        /// <summary>
        /// This is used by modules to update their regulation scheme if
        /// there have been any changes.
        /// </summary>
        public void GetNewRegulationScheme(int moduleId,
                                           List<newLink> regulatoryInputList)
        {
            if (uiVar.regulatoryInputList.ContainsKey(moduleId))
            {
                uiVar.regulatoryInputList[moduleId] = regulatoryInputList;
                optimizer.AskUpdateInToReg(uiVar);   
                ApplyChanges();
            }
            else
            {
                Debug.Log("Failed to update regulation: the module was not "
                          + "found in the dictionary.");
            }
        }

        /// <summary>
        /// This is used by modules to update their pandemonium group.
        /// </summary>
        public void GetNewPandemonium(int moduleId, int newPandem)
        {
            if (uiVar.pandemonium.ContainsKey(moduleId))
            {
                uiVar.pandemonium[moduleId] = newPandem;
                optimizer.AskUpdatePandem(uiVar);
                ApplyChanges();
            }
            else
            {
                Debug.Log("Failed to update pandemonium: the module was not "
                          + "found in the dictionary.");
            }
        }

        /// <summary>
        /// This is used by modules to update their output connections if
        /// there have been any changes.
        /// </summary>
        public void GetNewOutputConnectionList(int moduleId,
                                               List<newLink> newOutputList)
        {
            uiVar.localOutputList[moduleId] = newOutputList;
            optimizer.AskChangeWeights(uiVar);
            ApplyChanges();
        }

        /// <summary>
        /// This is used by regulation modules to update their contents!
        /// </summary>
        public void GetNewRegulationModuleContent(int moduleId, int newModule)
        {
			uiVar.hierarchy[moduleId].Add(newModule);
            SaveHierarchy();
        }

        /// <summary>
        /// This is used by modules to update their label if
        /// there have been any changes.
        /// </summary>
        public void GetNewModuleLabel(int moduleId, string newLabel)
        {
            moduleLabels[moduleId] = newLabel;
            // Saves the new labels
            SaveLabels();
        }
        /// <summary>
        /// This is used by input neurons to update their label if
        /// there have been any changes.
        /// </summary>
        public void GetNewInputLabel(int inputId, string newLabel)
        {
            inputLabels[inputId] = newLabel;
            // Saves the new labels
            SaveLabels();
        }
        /// <summary>
        /// This is used by output neurons to update their label if
        /// there have been any changes.
        /// </summary>
        public void GetNewOutputLabel(int outputId, string newLabel)
        {
            outputLabels[outputId] = newLabel;
            // Saves the new labels
            SaveLabels();
        }

        /// <summary>
        /// This method is used to create a new module.
        /// </summary>
        public void AddBasicModule(bool isRegulationModule)
        {
            if (isRegulationModule)
            {
                WriteToRecord("Add regulation module");
            }
            else
            {
                WriteToRecord("Add module");
            }

            // Before the module is created we need to update the information
            // required for its construction!

            // Sets the previously active module as inactive.
            DeactivateCurrentActive();

            ++numberOfModules;

            // To increase the Dictionarys with necessary module information
            // we first need to know the new module ID:
            int newId = genome.FindYoungestModule() + 1;

            // The new Id is added to the list of modules
            uiVar.moduleIdList.Add(newId);

            // Old way: adds all global inputs as inputs for the new module
			//IncreaseLocalInputListBasic(newId);
            // Lets the user select the inputs for the module. This is important, 
            // since in many cases inputs may complicate evolution (for instance
            // if an input not needed for the module's behaviour will be required
            // to always have some value when the module is active, because 
            // it may be difficult to ensure this condition during evolution).
            // Regulation modules will often use a subset of inputs as well (if
            // you want to develop regulation of the kind "be active when input
            // X is active but input Y is inactive, you will only need those!)
            GetInputList(isRegulationModule, newId);
        }

        /// <summary>
        /// Reverts some steps in AddBasicModule
        /// </summary>
        public void AbortNewModule()
        {
            --numberOfModules;
            int newId = uiVar.moduleIdList[uiVar.moduleIdList.Count - 1];
            uiVar.moduleIdList.Remove(newId);
        }

        /// <summary>
        /// Continues creating the module once the user has decided which
        /// inputs to use!
        /// </summary>
        public void AddBasicModulePart2(bool isRegulationModule,
                                        List<newLink> inputList,
                                        List<string> inputLabelsLocal)
        {
            // To increase the Dictionarys with necessary module information
            // we first need to know the new module ID:
            int newId = genome.FindYoungestModule() + 1;

            // The new input list is added to the general list.
            uiVar.localInputList.Add(newId, inputList);  
            localInSources.Add(newId, inputLabelsLocal); 

            // Adds outputs for the module (all for basic modules, one
            // place-holder connection for regulation modules)
            if (isRegulationModule)
            {
                // Regulation modules start with no local outputs!
                // To be safe (although this is believed NOT to be necessary)
                // we create a single local output to the first output, with
                // weight 0. This will be modified later.
                IncreaseLocalOutputListRegulation(newId);
            }
            else
            {
                // Local out to all output neurons!
                IncreaseLocalOutputListBasic(newId);
			}

            // Adds a basic-regulation connection to the list
            IncreaseRegulationListBasic(newId);

            // Pandeomonium set to 0
            uiVar.pandemonium.Add(newId, 0);

            // Then it is created. Extra local outputs are considered also for
            // regular modules, so regulation modules and normal modules can
            // share this method (remember we need to reserve some IDs for
            // new local output neurons that will be created when adding new
            // modules to a regulation module).
            optimizer.AskCreateModule(uiVar);

            if (isRegulationModule)
            {
                // If this is a regulation module, it is added to the dictionary,
                // but the list of contents is currently empty!
                uiVar.hierarchy.Add(newId, new List<int>());
            }

            RestartSimulation();

            // Finally the UI object is instantiated
            moduleLabels.Add(newId, "Module" + newId.ToString());

            // Saves the new labels
            SaveLabels();

            // Saves the hierarchy
            SaveHierarchy();

            GameObject newModule = null;

            if (isRegulationModule)
            {
                newModule = InstantiateOneModule(newId, true);
            }
            else
            {
                newModule = InstantiateOneModule(newId, false);
            }

            // Sets the new module as active
            newModule.GetComponent<ModuleController>().SetActive(true);

            // Stores the module in the list
            myModules.Add(newModule);                    
        }

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
            editingCanvas.transform.rotation = myPrefab.transform.rotation;
            // Sets the screen canvas as parent
            panel.transform.SetParent(editingCanvas.transform);
            // regulatoryMenu.transform.rotation = myPrefab.transform.rotation;

            // Makes sure the scale is normal
            panel.transform.localScale = new Vector3(1f, 1f, 1f);

            // At creation, the element is NOT active
            panel.SetActive(false);     
        }

        #endregion

        #region PrivateMethods

        /// <summary>
        /// Because cloning uses pauses, it will be called as an enumerator.
        /// 
        /// Cloning regulation modules is more complicated, because we need
        /// to clone the children as well, then we need to move the new children
        /// inside the cloned parent and update their regulation.
        /// 
        /// Note this can be recursive if one child is a regulation module itself!
        /// </summary>
        IEnumerator AskCloneRegModule2(int parentModule, bool isRegulationModule)
        {     
            int parentIndex = getModuleIndexById(parentModule);
            List<GameObject> childrenList = 
                myModules[parentIndex].GetComponent<RegModuleController>().ContainedModules;

            List<int> childrenClonesIds = new List<int>();

            // Instantiates all modules involved.

            // Instantiates the parent. This may result in some funny numbering
            // of the cloned modules, but it is useful to ensure that we know
            // the Id of the cloned child if the child is itself a regulation
            // module (otherwise it will be instantiated after all its children
            // and we need to count them, and they may contain more regulation...)
            int clonedParentId = genome.FindYoungestModule() + 1;
            AskCloneModule(parentModule, true);

            // Instantiates all the children:
            for (int i = 0; i < childrenList.Count; ++i) 
            {
                // Wait while the new module finds a resting place. This is 
                // BEFORE because this can be called recursively!
                int waitForFrames = 20;
                while (waitForFrames >= 0)
                {
                    --waitForFrames;
                    yield return null;
                }

                // Later we need to references to the CLONES, se here we save
                // the moduleId the clone is about to receive
                childrenClonesIds.Add(genome.FindYoungestModule() + 1);

                int moduleId = childrenList[i].GetComponent<ModuleController>().ModuleId;
                bool isChildRegModule = 
                    childrenList[i].GetComponent<ModuleController>().IsRegModule;

                // If the child is a regulation module it will call these methods
                // recursively.
                if (isChildRegModule)
                {
                    // This way the first regulation module will wait until
                    // the child regulation module has been created (we want
                    // to avoid instantiating elements in parallel)
                    yield return Coroutiner.StartCoroutine(
                        AskCloneRegModule2(moduleId, isChildRegModule));
                }
                else
                {
                    AskCloneModule(moduleId, isChildRegModule); 
                }

                int waitForFrames1 = 20;
                while (waitForFrames1 >= 0)
                {
                    --waitForFrames1;
                    yield return null;
                }
            }

            // Moves the children into the parent (using the clones, not the
            // originals!)
            int waitForFrames2 = 100;
            while (waitForFrames2 >= 0)
            {
                --waitForFrames2;
                yield return null;
            }
            AskCloneReg_MoveChildrenIntoParent(parentIndex, clonedParentId,
                childrenClonesIds);
        }

        /// <summary>
        /// All children and the parent have been instantiated. Now we need to
        /// move the children inside of the parent. We also need to rewire
        /// the local output of the parent (to the regulation neurons of the
        /// cloned children instead of the originals) and, finally, update the
        /// information of the inputs used by regulation neurons in the cloned
        /// children.
        /// </summary>
        void AskCloneReg_MoveChildrenIntoParent(int parentIndex, int clonedParentId,
            List<int> childrenClonesIds)
        {
            parentIndex = getModuleIndexById(clonedParentId);
            RegModuleController parentController =
                myModules[parentIndex].GetComponent<RegModuleController>();

            for (int i = 0; i < childrenClonesIds.Count; ++i) 
            {
                int childIndex = getModuleIndexById(childrenClonesIds[i]);
                Collider childCollider = myModules[childIndex].GetComponent<Collider>();
                parentController.AddModuleFromClone(childCollider);
            }

            // Updates info to rewire the cloned parent (its local output are
            // now pointing to the original children's regulatory neurons, but
            // we need to point to the cloned-children's regulatory neurons!)
            for (int i = 0; i < childrenClonesIds.Count; ++i)
            {
                uint childRegulatoryId = 0;
                childRegulatoryId = optimizer.RegIdFromModId(childrenClonesIds[i]);

                // Remember, this property cannot be directly modified, we first
                // need a copy!
                newLink localOutLink = uiVar.localOutputList[clonedParentId][i];
                localOutLink.otherNeuron = childRegulatoryId;
                uiVar.localOutputList[clonedParentId][i] = localOutLink;
            }

            // Rewires the parent's outputs to the cloned-children's regulatory
            // neurons
            optimizer.AskChangeTargets(uiVar, clonedParentId);
            ApplyChanges();

            // Finally, we update the list with inputs for the regulatory neurons
            // of the children!
            AskCloneReg_InToReg(clonedParentId, childrenClonesIds);            
        }

        /// <summary>
        /// Updates the list with the inputs used by the regulation neuron.
        /// This may be useful for the interface of advanced regulation schemes.
        /// </summary>
        void AskCloneReg_InToReg(int clonedParentId, List<int> childrenClonesIds)
        {
            for (int i = 0; i < childrenClonesIds.Count; ++i)
            {
                uint childRegulatoryId = 0;
                childRegulatoryId = optimizer.RegIdFromModId(childrenClonesIds[i]);

                uint connectionId;
                uint connectionSource;
                optimizer.ConnectionIdFromModAndTarget(clonedParentId, childRegulatoryId,
                    out connectionId, out connectionSource);

                newLink regulationLink = new newLink();
                regulationLink.id = connectionId;
                regulationLink.otherNeuron = connectionSource;
                regulationLink.weight = 1.0;

                int childIndex = getModuleIndexById(childrenClonesIds[i]);
                myModules[childIndex].GetComponent<ModuleController>().
                RegulatoryInputList.Add(regulationLink);
            }            
        }

        /// <summary>
        /// Used to find a module in the list "myModules" when its ID is known
        /// </summary>
        int getModuleIndexById(int id)
        {
            for (int i = myModules.Count - 1; i >= 0; --i)
            {
                if (myModules[i].GetComponent<ModuleController>().ModuleId == id)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Makes a copy of the variables in UiVar that will (sometimes) be
        /// overwritten before starting evolution (to make a module the only one
        /// active, so other modules do not interfere) 
        /// 
        /// Note that a direct copy like pandemoniumCopy = uiVar.pandemonium
        /// creates a reference to uiVar.pandemonium, so changes in uiVar
        /// also apply to the copy! (And this is certainly NOT what we want.)
        /// </summary>
        void MakeUiVarCopy()
        {
            isCopyInUse = true;
            pandemoniumCopy = new Dictionary<int, int>();
            foreach (KeyValuePair<int, int> entry in uiVar.pandemonium)
            {
                pandemoniumCopy.Add(entry.Key, entry.Value);
            }
            localOutputListCopy = new Dictionary<int, List<newLink>>();
            foreach (KeyValuePair<int, List<newLink>> entry in uiVar.localOutputList)
            {
                localOutputListCopy.Add(entry.Key, entry.Value);
            }
            regulatoryInputListCopy = new Dictionary<int, List<newLink>>();
            foreach (KeyValuePair<int, List<newLink>> entry in uiVar.regulatoryInputList)
            {
                regulatoryInputListCopy.Add(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Sometimes evolution uses a version of the genome with some changes
        /// (so, for example, the activity of other modules is hidden to see
        /// clearly what the evolving module is doing). Those changes only
        /// affect protected properties (regulation and output weights) that
        /// may NOT change during evolution. Now they are loaded again.
        /// </summary>
        void ReloadSavedUiVar()
        {
            isCopyInUse = false;
            uiVar.pandemonium = new Dictionary<int, int>();
            foreach (KeyValuePair<int, int> entry in pandemoniumCopy)
            {
                uiVar.pandemonium.Add(entry.Key, entry.Value);
            }
            uiVar.localOutputList = new Dictionary<int, List<newLink>>();
            foreach (KeyValuePair<int, List<newLink>> entry in localOutputListCopy)
            {
                uiVar.localOutputList.Add(entry.Key, entry.Value);
            }
            uiVar.regulatoryInputList = new Dictionary<int, List<newLink>>();
            foreach (KeyValuePair<int, List<newLink>> entry in regulatoryInputListCopy)
            {
                uiVar.regulatoryInputList.Add(entry.Key, entry.Value);
            } 

            // Saves to overwirte a perhaps incomplete saved version after
            // evolution
            optimizer.AskUpdateInToReg(uiVar);   
            optimizer.AskUpdatePandem(uiVar);
            optimizer.AskChangeWeights(uiVar); 
            ApplyChanges();
        }

        /// <summary>
        /// Nulls all the output connections of a module, by given them
        /// weight = 0
        /// </summary>
        void MakeOutputNull(int moduleId)
        {
            // It is otherwise painful to find bugs when keys are not found
            if (uiVar.localOutputList.ContainsKey(moduleId))
            {
                // It will not allow to directly modify the weights in the strcuts!
                // Creates a new lists where we will add copies of the connections, 
                // with weights set to 0
                List<newLink> localCopy = new List<newLink>();
                foreach (newLink link in uiVar.localOutputList[moduleId])
                {
                    newLink nulled = new newLink();
                    nulled.id = link.id;
                    nulled.otherNeuron = link.otherNeuron;
                    nulled.weight = 0.0;
                    localCopy.Add(nulled);
                }
                // The list is done, so it substitues the old one (of which
                // there is a copy in this script in localOutputListCopy)
                uiVar.localOutputList[moduleId] = localCopy;
                optimizer.AskChangeWeights(uiVar);
            }
            else
            {
                Debug.Log("Could not find the given module to make outputs null");
            }
        }

        /// <summary>
        /// When a module is evolved, we are interested in seeing the behaviour
        /// it produces, so we make it always active.
        /// </summary>
		void MakeAlwaysActive(int chosenModule, newLink newRegulation)
		{
            // It is otherwise painful to find bugs when keys are not found
            if (uiVar.pandemonium.ContainsKey(chosenModule))
            {
                // Takes the module outside of any pandemoniums (in case another
                // module in said pandemonium has higher activation)
                uiVar.pandemonium[chosenModule] = 0;    
                optimizer.AskUpdatePandem(uiVar);             
            }
            else
            {
                Debug.Log("Failed to make a module always active for evolution "
                          + "or display. It was not found in the pandemonium "
                          + "dictionary.");
            }

            // It is otherwise painful to find bugs when keys are not found
            if (uiVar.regulatoryInputList.ContainsKey(chosenModule))
            {
				List<newLink> localCopy = new List<newLink>();
				localCopy.Add(newRegulation);
                uiVar.regulatoryInputList[chosenModule] = localCopy;
                optimizer.AskUpdateInToReg(uiVar);                 
            }
            else
            {
                Debug.Log("Failed to make a module always active for evolution "
                    + "or display. It was not found in the regulation input "
                    + "dictionary.");    
            }

		}

        /// <summary>
        /// Finds the module that is active and sets it as inactive
        /// </summary>
        void DeactivateCurrentActive()
        {
            for (int i = 0; i < numberOfModules; ++i)
            {
                // Finds the active module and sets it inactive:
                if (myModules[i].GetComponent<ModuleController>().IsActive)
                {
                    myModules[i].GetComponent<ModuleController>().SetActive(false);
                    break;                        
                }
            }
        }

        /// <summary>
        /// Finds the desired module and activates it
        /// </summary>
        void ActivateModule(int whichModule)
        {
            for (int i = 0; i < numberOfModules; ++i)
            {
                // Finds the module and sets it active:
                if (myModules[i].GetComponent<ModuleController>().ModuleId == whichModule)
                {
                    myModules[i].GetComponent<ModuleController>().SetActive(true);
                    break;                        
                }
            }
        }

        /// <summary>
        /// Instantiates the UI elements for loaded modules.
        /// </summary>
        IEnumerator InstantiateLoadedModules()
        {
            GameObject newModule = null;

            // We loop using the correct module index
            for (int i = 0; i < numberOfModules; ++i)
            {
                // Instantiates the module
                // If the module Id is found in the dictionary, this should be
                // instantiated as a regulation module!
                int moduleId = uiVar.moduleIdList[i];
                if (uiVar.hierarchy.ContainsKey(moduleId))
                {
                    // Instantiate as regulation module
                    newModule = InstantiateOneModule(moduleId, true);
                }
                else
                {
                    // Instantiate as normal module
                    newModule = InstantiateOneModule(moduleId, false);
                }

                // A reference to the GameObject is stored in this script
                myModules.Add(newModule);

                // Modules are instantiated in the same place in probe mode, 
                // so that they search for the nearest empty place. We must not
                // instantiate them in the same frame, so they would move together
                // and never find an empty space!!
                int waitForFrames = 20;
                while (waitForFrames >= 0)
                {
                    --waitForFrames;
                    yield return null;
                }

                // After waiting (and updating the collision counter) we set the
                // new module as probe, so it can search for its empty space
                newModule.GetComponent<ModuleController>().ColliderAsProbe = true;
                // One more frame so it can reset is probe state if there are
                // no collisions (before maybe instantiating another!)
                waitForFrames = 5;
                while (waitForFrames >= 0)
                {
                    --waitForFrames;
                    yield return null;
                }
            }

            // Sets the last module as active
            if (myModules.Count > 0)
            {
                myModules[myModules.Count - 1].GetComponent<ModuleController>().
                SetActive(true);
            }

            //ApplyHierarchy();
            Coroutiner.StartCoroutine(ApplyHierarchy());
        }

        /// <summary>
        /// Reads the hierarchy and sets children modules where they belong!
        /// </summary>
        IEnumerator ApplyHierarchy()
        {
            // Wait before modules are settled...
            int waitForFrames = 100;
            while (waitForFrames >= 0)
            {
                --waitForFrames;
                yield return null;
            }

            // It is better to process the Hierarchy tree in an organized way.
            // First we will process the regulation modoules that contain no
            // regulation modules themselves, and so on moving up one level 
            // at a time.

            // With this dictionary we know if the module has already been 
            // processed!
            Dictionary<int, bool> isModuleDone = new Dictionary<int, bool>();
            foreach (int parentId in uiVar.hierarchy.Keys)
            {
                isModuleDone.Add(parentId, false);
            }

            // Loop through all the regulation modules and try to add their
            // children, but then we will check if any of them is also a
            // regulation module, which would be processed first. When we
            // finally add the children to a module we mark it as processed
            // in isModuleDone, so that we can then process the parent, and
            // so on until all the Hierarchy has been covered in good order!
            foreach (int parentId in uiVar.hierarchy.Keys)
            {
                if (isModuleDone[parentId] == false)
                {
                    MoveAllChildrenInside(parentId, isModuleDone);                    
                }
            }
        }

        /// <summary>
        /// Used in ApplyHierarchy so that this part can be called in a
        /// recursive way.
        /// </summary>
        void MoveAllChildrenInside(int parentId, Dictionary<int, bool> isModuleDone)
		{
            // Is any of the children a regulation module? In that case, 
            // do that first!
            foreach (int childId in uiVar.hierarchy[parentId])
            {
                if (uiVar.hierarchy.ContainsKey(childId) &&
                    isModuleDone[childId] == false)
                {
                    // One of the children of this module is a regulation
                    // module! So we do it first:
                    MoveAllChildrenInside(childId, isModuleDone);
                }
            }

            // No more children are unprocessed regulation modules, so we
            // continue and mark this module as processed.
            isModuleDone[parentId] = true;
			GameObject parentModule = FindModuleWithId(parentId);
			foreach (int childId in uiVar.hierarchy[parentId])
			{
				GameObject childModule = FindModuleWithId(childId);
				childModule.GetComponent<ModuleController>().BasicRegulation = false;
				Collider childCollider = childModule.GetComponent<Collider>();
				parentModule.GetComponent<RegModuleController>().
				MoveModuleInside(childCollider);
			}			
		}

        /// <summary>
        /// Returns from the list the module with the desired ID
        /// </summary>
        GameObject FindModuleWithId(int id)
        {
            GameObject returnModule = null;

            for (int i = 0; i < myModules.Count; ++i)
            {
                if (myModules[i].GetComponent<ModuleController>().ModuleId == id)
                {
                    return myModules[i];
                }
            }

            return returnModule;
        }

        /// <summary>
        /// Instantiates UI elements for input and output neurons.
        /// </summary>
        void InstantiateInputOutput()
        {
            GameObject newNeuron;
            int xOffset = 65;

            // We loop through input neurons. We use <= since this counter does
            // not include the bias neuron!
            for (int i = 0; i <= genome.Input; ++i)
            {
                // Instantiates the module
                newNeuron = (GameObject)Instantiate(Resources.Load("Prefabs/InputNeuron"));
                // Sets the correct canvas as parent
                newNeuron.transform.SetParent(editingCanvas.transform);
                // Makes sure the scale is normal
                newNeuron.transform.localScale = new Vector3(1f, 1f, 1f);

                // Position is set using a ScreenSpaceButtonStarter script
                newNeuron.GetComponent<ScreenSpaceButtonStarter>().xOffsetPixels = i * xOffset;

                InputNeuronController neuronController =
                    newNeuron.GetComponent<InputNeuronController>();
                // Gives the neuron a unique Id
                neuronController.SetInputId(i);
                // Passes the label
                neuronController.SetLabel(inputLabels[i]);

                // A reference to the GameObject is stored in this script
                inputNeurons.Add(newNeuron);
            } 
        }

        /// <summary>
        /// Sets the evolution camera as the only active one. Also deactivates
        /// the editing canvas.
        /// </summary>
        void ActivateEvolutionCamera()
        {
            backgroundCamera.gameObject.SetActive(false);
            editingCamera.gameObject.SetActive(false);
            editingCanvas.gameObject.SetActive(false);
            evolutionCamera.gameObject.SetActive(true); 
            evolutionCanvas.gameObject.SetActive(true);
        }

        /// <summary>
        /// Sets the editing camera as the only active one. Also deactivates
        /// the evolution canvas.
        /// </summary>
        void DeactivateEvolutionCamera()
        {
            backgroundCamera.gameObject.SetActive(true);
            editingCamera.gameObject.SetActive(true);
            editingCanvas.gameObject.SetActive(true);
            evolutionCamera.gameObject.SetActive(false);  
            evolutionCanvas.gameObject.SetActive(false);
        }

        /// <summary>
        /// Moves the modules only (not other elements on the screen!)
        /// </summary>
        void MoveModules()
        {
            for (int i = 0; i < myModules.Count; ++i)
            {
                Vector3 auxPosition = myModules[i].transform.position;
                // Ideally this should be a function of position.y
                // The closer it is, the smaller the increment.

                float increment = 0.5f;

                if (isMoveRight)
                {
                    auxPosition.x += 0.5f;
                }
                if (isMoveLeft)
                {
                    auxPosition.x -= 0.5f;
                }
                if (isMoveUp)
                {
                    auxPosition.z += 0.5f;
                }
                if (isMoveDown)
                {
                    auxPosition.z -= 0.5f;
                }

                myModules[i].transform.position = auxPosition;
            }
        }

        /// <summary>
        /// Writes the current labels to a file
        /// </summary>
        void SaveLabels()
        {
            uiXmlIO.WriteLabels(inputLabels, outputLabels, moduleLabels);
        }

		void SaveHierarchy()
		{
			uiXmlIO.WriteHierarchy(uiVar.hierarchy);
		}

        /// <summary>
        /// After the population has been changed, the preview simulation is
        /// reset so that it reflects these changes.
        /// </summary>
        void RestartSimulation()
        {
            // Stop and restart simulation if there is one
            if (optimizer.ChampRunning)
            {
                optimizer.DestroyBest();
                Coroutiner.StartCoroutine(Restart2());
            }
        }
        /// <summary>
        /// We want to make sure that all champion genomes have been completely
        /// removed before proceeding!
        /// </summary>
        IEnumerator Restart2()
        {
            // Wait before champion genomes are deleted... is this enough?
            int waitForFrames = 5;
            while (waitForFrames >= 0)
            {
                --waitForFrames;
                yield return null;
            }
            optimizer.RunBest();
        }

        /// <summary>
        /// In preparation for a new module, creates a local input list by
        /// default, consisting of all global input neurons.
        /// 
        /// It is most often a good idea to choose the inputs the module will
        /// use, for several reasons. This method is currently unused.
        /// </summary>
        /*void IncreaseLocalInputListBasic(int newId)
        {
            // Local input:
            List<newLink> inputList = new List<newLink>();
            List<string> inputLabels = new List<string>();

            for (int i = 0; i < genome.Input + 1; ++i)
            {
                // Ads connections from global inputs.
                // For the time being the connection Id is left as 0. This
                // will be updated in Factory.
                inputList.Add(CreateDefaultWithTarget((uint)i));
                inputLabels.Add(neuronStringFromId[(uint)i]);
            }
            // The new input list is added to the general list.
            uiVar.localInputList.Add(newId, inputList);  
            localInSources.Add(newId, inputLabels);
        }*/

        /// <summary>
        /// This method allows the user to choose the inputs the new module will use.
        /// </summary>
        void GetInputList(bool isRegulationModule, int newId)
        {
            // Local input:
            List<newLink> inputList = new List<newLink>();
            List<string> inputLabelsLocal = new List<string>();

            for (int i = 0; i < genome.Input + 1; ++i)
            {
                // Ads connections from global inputs.
                // For the time being the connection Id is left as 0. This
                // will be updated in Factory.
                inputList.Add(CreateDefaultWithTarget((uint)i));
                inputLabelsLocal.Add(neuronStringFromId[(uint)i]);
            }

            // Instantiates the input selection panel
			GameObject myPrefab = (GameObject)Resources.Load("Prefabs/SelectInputPanel");
			GameObject inputSelectPanel = (GameObject)Instantiate(myPrefab);
            SetUpPanel(inputSelectPanel, myPrefab);
            // But SetUpPanel leaves the gameObject inactive!
            inputSelectPanel.SetActive(true);

            SelectInputPanelController panelController =
                    inputSelectPanel.GetComponent<SelectInputPanelController>();

            // Passes a reference to this class
            panelController.UiManager = this;

            // Passes isRegulationModule to the panel
            panelController.IsRegulationModule = isRegulationModule;

            // Passes the complete input lists to the panel
            panelController.GetCompleteInputList(inputList, inputLabelsLocal, 
                                                 inputLabels);           
        }

        /// <summary>
        /// In preparation for a new module, creates a local output list by
        /// default, consisting of all global output neurons.
        /// </summary>
        void IncreaseLocalOutputListBasic(int newId)
        {
            // Local output:
            List<newLink> outputList = new List<newLink>();
            List<string> outputLabels = new List<string>();

            for (int i = genome.Input + 1;
                 i < genome.Input + 1 + genome.Output; ++i)
            {
                // Ads connections to global outputs.
                // For the time being the connection Id is left as 0. This
                // will be updated in Factory.
                outputList.Add(CreateDefaultWithTarget((uint)i));
                outputLabels.Add(neuronStringFromId[(uint)i]);
            }
            // The new output list is added to the general list.
            uiVar.localOutputList.Add(newId, outputList);    
            localOutTargets.Add(newId, outputLabels); 
        }

        /// <summary>
        /// Increases the local output list regulation.
        /// For a regulation module we will create a simple connection
        /// to the first output neuron (this will be modified as soon as the
        /// user adds the first module)
        /// </summary>
        void IncreaseLocalOutputListRegulation(int newId)
        {
            // Local output:
            List<newLink> outputList = new List<newLink>();
            List<string> outputLabels = new List<string>();

            // Ads a connection to global output #1.
            // For the time being the connection Id is left as 0. This
            // will be updated in Factory.
            int i = genome.Input + 1;
            newLink newElement = new newLink();
            newElement.otherNeuron = (uint)i;
            newElement.weight = 0.0;
            newElement.id = 0;
            outputList.Add(newElement);
            outputLabels.Add(neuronStringFromId[(uint)i]);

            // The new output list is added to the general list.
            uiVar.localOutputList.Add(newId, outputList);    
            localOutTargets.Add(newId, outputLabels);             
        }

        /// <summary>
        /// In preparation for a new module, creates a regulation scheme by
        /// default.
        /// </summary>
        void IncreaseRegulationListBasic(int newId)
        {
            List<newLink> regulationList = new List<newLink>();
            // Connection to bias (index = 0)
            regulationList.Add(CreateDefaultWithTarget(0));
            uiVar.regulatoryInputList.Add(newId, regulationList);    
        }

        /// <summary>
        /// Avoids code repetition. Creates a new newLink element with default
        /// weight.
        /// </summary>
        newLink CreateDefaultWithTarget(uint target)
        {
            newLink newElement = new newLink();
            newElement.otherNeuron = target;
            newElement.weight = 1.0;
            newElement.id = 0;
            return newElement;
        }

        /// <summary>
        /// Instantiates a new module, with the provided imoduleId. Relevant
        /// information must exist in input, labels, etc.
        /// </summary>
        GameObject InstantiateOneModule(int moduleId, bool isRegulationModule)
        {
            GameObject newModule;

            // Instantiates the module
            if (isRegulationModule) {
                newModule = (GameObject)Instantiate(Resources.Load("Prefabs/Module-regulatory"));   
            } else {
                newModule = (GameObject)Instantiate(Resources.Load("Prefabs/Module"));                
            }

            // Just so this is easier to read:
            ModuleController newModuleController =
                    newModule.GetComponent<ModuleController>();

            // Gives the module a unique Id
            newModuleController.SetModuleId(moduleId);
            // Passes the total number of input and output neurons in the system
            newModuleController.TotalInNeurons = genome.Input + 1;
            newModuleController.TotalOutNeurons = genome.Output;

            newModuleController.ColliderAsProbe = true;
            if (isRegulationModule)
            {
                newModule.GetComponent<RegModuleController>().IsRegModule = true;
                newModule.GetComponent<RegModuleController>().BeingDragged = true;
            }

            // If there are already other modules instantiated, copies the y
            // coordinate (so that if zoom is changed new modules appear in the
            // same plane!
            //float yPosition = -14.15f;
            float yPosition = -20f;
            if (myModules.Count > 0)
            {
                yPosition = myModules[0].transform.position.y;
            }
            newModule.transform.position = new Vector3(397f, yPosition, 43.4f);

            // Local input and outputs are added
            newModuleController.AddLocalIO(localInSources[moduleId], true);
            // Local output is not shown for regulation modules!
            if (!isRegulationModule)
            {
                newModuleController.AddLocalIO(localOutTargets[moduleId], false);                
            }

            // We need the complete input list in the regulation menu panel
            newModuleController.AddInToRegulationMenu(inputLabels);

            // We pass the regulation information for this module
            newModuleController.LoadRegulation(uiVar.regulatoryInputList[moduleId]);

			// Also passes the output connections (so we can easily modify their
            // weights)

            newModuleController.LoadOutputList(uiVar.localOutputList[moduleId]);

            // Passes the module label
            newModuleController.SetModuleLabel(moduleLabels[moduleId]);

            // Passes the pandemonium value
            newModuleController.SetPandemoniumValue(uiVar.pandemonium[moduleId]);

            // FindRegulatory: gets the index for the regulatory neuron
            // in the desired module.
            int regIndex;
            genome.NeuronGeneList.FindRegulatory(moduleId, out regIndex);
            newModuleController.SetRegulatoryId(genome.NeuronGeneList[regIndex].Id);

            return newModule;
        }

        #endregion

        #region AnalyseGenome

        /// <summary>
        /// Resets the user interface (for example, if a new genome is
        /// provided)
        /// </summary>
        void ResetUI()
        {
            localInSources = new Dictionary<int, List<string>>();
            localOutTargets = new Dictionary<int, List<string>>();
            neuronStringFromId = new Dictionary<uint, string>();

            uiVar.Reset();

            ResetModuleIdList();
            MakeTargetSourceStringById();            
        }

        /// <summary>
        /// Goes through the genome and lists the different modules, in the
        /// order they are found!
        /// </summary>
        void ResetModuleIdList()
        {
            // Resets the list
            uiVar.moduleIdList = new List<int>();

            int currentId = 0;

            NeuronGeneList neuronList = genome.NeuronGeneList;

            // Goes through all neurons in the genome
            for (int i = neuronList.LastBase + 1; i < neuronList.Count; ++i)
			{
                // If this is a new module...
                if (neuronList[i].ModuleId != currentId)
                {
                    // Updates the current module marker
                    currentId = neuronList[i].ModuleId;

                    // Adds the module to the list
                    uiVar.moduleIdList.Add(currentId);
                }
			}            
        }

        /// <summary>
        /// In order to represent our modules we need to know how many local in/out
        /// neurons each module has, and which targets they have. Obviously this 
        /// should NOT be computed each frame, so we save the information. 
        /// We only update this information if we get a new genome (with a 
        /// possibly new structure).
        /// </summary>
        void UpdateLocalInOutInfo()
        {
            // Goes through the different modules 
			for (int i = 0; i < numberOfModules; ++i)
            {
                // Adds an empty list for uiVar.localOutputList and
                // uiVar.localInputList. The contents will be written in
                // GetSourceTargetsInModule --> GetSourceTarget.
				uiVar.localOutputList.Add(uiVar.moduleIdList[i], new List<newLink>());
				uiVar.localInputList.Add(uiVar.moduleIdList[i], new List<newLink>());

                // Gets the sources for each local input.
				localInSources.Add(uiVar.moduleIdList[i],
					               GetSourceTargetsInModule(uiVar.moduleIdList[i],
						                                    NodeType.Local_Input));

                // Gets the targets for each local output.
				localOutTargets.Add(uiVar.moduleIdList[i],
					                GetSourceTargetsInModule(uiVar.moduleIdList[i],
						                                     NodeType.Local_Output));
            } 
        }

        /// <summary>
        /// Counts the order of input, output and regulatory neurons, and writes
        /// in a dictionary a string with its ID (for example, "R3" for the third 
        /// regulatory neuron in the list). The keys are innovation ID values for
        /// these neurons.
        /// Note input includes bias.
        /// </summary>
        void MakeTargetSourceStringById()
        {
            // IMPORTANT: For bias, input and output neurons the index in the
            // list and their ID are the same, but not necessarily for regulatory.

            // First, we add the bias neuron:
            neuronStringFromId.Add(0, "Bias");
            // Use <= because the bias neuron takes intex 0.
            for (int i = 1; i <= genome.Input; ++i)
            {
				neuronStringFromId.Add((uint)i, "I" + i.ToString());                
            }

            // Output neurons.
            // Again, <= accounts for the bias neuron.
            int count = 1;
            for (int i = genome.Input + 1; i <= genome.Input + genome.Output; ++i)
            {
				neuronStringFromId.Add((uint)i, "O" + count.ToString());
                ++count;
            }

            NeuronGeneList neuronList = genome.NeuronGeneList;

            // Regulatory neurons and local input/output only come if there are
            // modules at all! (If we do not check this we will create some
            // out-of-range errors.)
            if (genome.Regulatory > 0)
            {
                // Regulatory neurons.
                count = 1;
                for (int i = genome.Input + genome.Output + 1;
                    i <= neuronList.LastBase; ++i)
                {
                    // We do not write the variable "i" as the ID because, unlike
                    // output neurons, regulatory neurons may take non-consecutive
                    // values.
                    neuronStringFromId.Add(neuronList[i].Id, "R" + count.ToString());
                    ++count;
                }

                // Includes local input/output neurons in the dictionary in case there is
                // a connection from local output to local in.
                // Also includes local output neurons in case there is a connection
                // from a local output to a regulatory neuron.
                count = 1;
                uint firstRegIndx = (uint)genome.Input + (uint)genome.Output;
                NodeType currentType = NodeType.Local_Input;
                for (int i = neuronList.LastBase + 1; i < neuronList.Count; ++i)
                {
                    // Resets the count for new neuron types.
                    if (neuronList[i].NodeType != currentType)
                    {
                        currentType = neuronList[i].NodeType;
                        count = 1;
                    }

                    // We are only interested in local_in with local_out sources.
                    // These have an Id that is always > input + 1.
                    // These connections (at least in this version) have only one
                    // source, but there is no easy way to go through hashSets.
                    if (neuronList[i].NodeType == NodeType.Local_Input)
                    {
                        if (HashSetContainsBiggerThan(neuronList[i].SourceNeurons,
                            (uint)(genome.Input + 1)))
                        {
                            neuronStringFromId.Add(neuronList[i].Id, "M" +
                                neuronList[i].ModuleId.ToString() +
                                "i" + count.ToString());
                        }
                    }

                    // We are only interested in local_out with local_in as targets.
                    // These will always have an Id > LastBase.
                    if (neuronList[i].NodeType == NodeType.Local_Output)
                    {
                        if (HashSetContainsBiggerThan(neuronList[i].TargetNeurons,
                            firstRegIndx))
                        {
                            neuronStringFromId.Add(neuronList[i].Id, "M" + 
                                neuronList[i].ModuleId.ToString() +
                                "o" + count.ToString());
                        }
                    }
                    ++count;
                }                
            }
        }

        /// <summary>
        /// Determines whether the provided hashSet contains any elements bigger
        /// than a given value.
        /// There is a copy in EspCyclicNetworkFactory.
        /// </summary>
        bool HashSetContainsBiggerThan(HashSet<uint> hashSet, uint value)
        {
            foreach (uint element in hashSet)
            {
                if (element > value)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the source or target for each local input or output neuron
        /// in a module.
        /// </summary>
        List<string> GetSourceTargetsInModule(int module, NodeType inOrOut)
        {
            List<string> innerList = new List<string>();
            bool started = false;
            // Starts after bias + input + output + regulatory.
            for (int i = genome.NeuronGeneList.LastBase + 1; 
                 i < genome.NeuronGeneList.Count; ++i)
            {
                // Starts after the first local input/output of the module.
                if (!started && genome.NeuronGeneList[i].ModuleId == module &&
                    genome.NeuronGeneList[i].NodeType == inOrOut)
                {
                    started = true;
                }
                if (started)
                {
                    // If we find a new module or a different type, we stop counting.
                    if (genome.NeuronGeneList[i].ModuleId != module ||
                        genome.NeuronGeneList[i].NodeType != inOrOut)
                    {
                        return innerList;
                    }
                    // Otherwise we add this neuron
                    innerList.Add(GetSourceTarget(module, i, inOrOut));
                }
            }
            // If there are no hidden neurons (should NOT be the case) we exit here:
            return innerList;
        }

        /// <summary>
        /// Given an index (and type), searchs for its source (local in) or 
        /// target (local out) and returns a string with this information.
        /// Module is given so guiManager.LocalOutputList and guiManager.LocalInputList
        /// can be updated here.
        /// </summary>
        string GetSourceTarget(int module, int index, NodeType inOrOut)
        {
            // This is the protected connection we will add to LocalOutputList
            // and LocalInputList.
            newLink protectedConnect = new newLink();

            // With the given index, we know the neuron ID.
            uint ID = genome.NeuronGeneList[index].Id;

            // Looks for the source or target:
            if (inOrOut == NodeType.Local_Input)
            {
                // Looks for a connection with this ID as target:
                foreach (ConnectionGene connection in genome.ConnectionGeneList)
                {
                    if (connection.TargetNodeId == ID)
                    {
                        protectedConnect.otherNeuron = connection.SourceNodeId;
                        protectedConnect.weight = connection.Weight;
                        protectedConnect.id = connection.InnovationId;
                        uiVar.localInputList[module].Add(protectedConnect);
                        break;
                    }
                }                
            }
            else
            {
                // Looks for a connection with this ID as source:
                foreach (ConnectionGene connection in genome.ConnectionGeneList)
                {
                    if (connection.SourceNodeId == ID)
                    {
                        protectedConnect.otherNeuron = connection.TargetNodeId;
                        protectedConnect.weight = connection.Weight;
                        protectedConnect.id = connection.InnovationId;
                        uiVar.localOutputList[module].Add(protectedConnect);
                        break;
                    }
                }                
            }

            // We have our target ID.
            // We can now return our string with the target ID. This information
            // is stored in a list.
			return neuronStringFromId[protectedConnect.otherNeuron];
        }

        /// <summary>
        /// In this list we keep track of the pandemonium state in each module.
        /// </summary>
        void UpdatePandemoniums()
        {
			pandemoniums = new Dictionary<int, string>();

            for (int i = 0; i <= genome.NeuronGeneList.LastBase; ++i)
            {
                if (genome.NeuronGeneList[i].NodeType == NodeType.Regulatory)
                {
                    int pandValue = genome.NeuronGeneList[i].Pandemonium;
					int moduleId = genome.NeuronGeneList[i].ModuleId;
                    string valueToString;
                    if (pandValue == 0)
                    {
                        valueToString = "N";
                    }
                    else
                    {
                        valueToString = pandValue.ToString();
                    }
					pandemoniums.Add(moduleId, valueToString);
					uiVar.pandemonium[moduleId] = pandValue;
                }
            }
        }

        void UpdateLabels()
        {
            // Checks if the list for names has already been created (so we avoid
            // overwirting).
            if (inputLabels.Count == 0)
            {
                // If there is a file with names, we load them
                if (System.IO.File.Exists(namesPath))
                {
                    uiXmlIO.ReadLabels(out inputLabels, out outputLabels, out moduleLabels);
                }
                else
                    // Otherwise we create some default labels
                {
                    DefaultLabels();
                }
            }
        }

        /// <summary>
        /// Reads the genome's hierarchy from a file. This is easier than trying
        /// to reconstruct it from the genome itself. This has problems such as:
        /// how to know if a freshly-created regulation module, with no children
        /// modules yet, is a regulation module or a normal module with only a
        /// connection to bias and weight 0? It would not be impossible, but
        /// this is also acceptable.
        /// </summary>
		void UpdateHierarchy()
		{
			// There MUST exist a file!
			if (System.IO.File.Exists(hierarchyPath))
			{
				uiXmlIO.ReadHierarchy(out uiVar.hierarchy);
			}
			else
			{
				// If there were no modules yet, this is not a problem
				if (numberOfModules > 1)
				{
					// THIS is a problem!!
					// TODO: Perhaps do something about it?
					Debug.Log("Failed to find hierarchy file!");
				}
			}
		}

        /// <summary>
        /// Input, output and regulatory neurons, as well as modules, may have
        /// custom labels (as well as their ID values). Here we get default
        /// labels (like "Input1" and so on).
        /// </summary>
        void DefaultLabels()
        {
            inputLabels.Add("Bias"); 
            for (int i = 1; i <= genome.Input; ++i)
            {
                inputLabels.Add("Input" + (i).ToString()); 
            }

			// Cheap labour and cars have pre-defined labels to help in user-tests
			if (optimizer.ExperimentName == "_ESP_CheapLabour")
			{
				CheapLabourDefaultInput();
			}
			else if (optimizer.ExperimentName == "_ESP_Cars")
			{
				CarsDefaultInput();
			}

            for (int i = 0; i < genome.Output; ++i)
            {
                outputLabels.Add("Output" + (i + 1).ToString()); 
            } 

			moduleLabels = new Dictionary<int, string>();
			for (int i = 0; i < numberOfModules; ++i)
            {
                moduleLabels.Add(uiVar.moduleIdList[i],
                                 "Module" + uiVar.moduleIdList[i].ToString());
            }  
        }

		/// <summary>
		/// These two will give custom default labels for two of the provided
        /// examples, Cheap Labour and Cars. Of course these may be removed
        /// (or adapted) for a general use of this code.
		/// </summary>
		void CheapLabourDefaultInput()
		{
			inputLabels[1] = "Front";
			inputLabels[2] = "Front-L";
			inputLabels[3] = "Front-R";
			inputLabels[4] = "Blue";
			inputLabels[5] = "Red";
			inputLabels[6] = "Pink";
			inputLabels[7] = "Cargo";
			inputLabels[8] = "Clock";
		}
		void CarsDefaultInput()
		{
			inputLabels[1] = "Front";
			inputLabels[2] = "Front-L";
			inputLabels[3] = "Left";
			inputLabels[4] = "Front-R";
			inputLabels[5] = "Right";
			inputLabels[6] = "TrLights";
			inputLabels[7] = "Junctions";
		}

        /// <summary>
        /// Initializes the list with the inputs for regulatory neurons.
        /// </summary>
        void InitializeRegulatoryInputList()
        {
			uiVar.regulatoryInputList = new Dictionary<int, List<newLink>>();

			for (int i = 0; i < numberOfModules; ++i)
            {
                uiVar.regulatoryInputList.Add(uiVar.moduleIdList[i],
                                              GetInputToReg(uiVar.moduleIdList[i]));
            }
        }

        /// <summary>
        /// Creates a list with all the neurons that target the regulatory
        /// neuron in a module (also gets the connection weights).
        /// Remember the first module takes index 1 in this script.
        /// </summary>
        List<newLink> GetInputToReg(int module)
        {
            List<newLink> returnList = new List<newLink>();

            int regIndex;
            // Gets the index for the regulatory neuron in the module.
            if (genome.NeuronGeneList.FindRegulatory(module, out regIndex))
            {
                // With the index we get the Id.
                uint regId = genome.NeuronGeneList[regIndex].Id;
                // Looks for connections with regId as target
                foreach (ConnectionGene connection in genome.ConnectionGeneList)
                {
                    if (connection.TargetNodeId == regId)
                    {
                        newLink toBeAdded = new newLink();
                        toBeAdded.otherNeuron = connection.SourceNodeId;
                        toBeAdded.weight = connection.Weight;
                        toBeAdded.id = connection.InnovationId;
                        returnList.Add(toBeAdded);
                    }
                }
            }
            return returnList;
        }

        #endregion
	}
}

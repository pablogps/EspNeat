using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.Genomes.Neat;
using System;
using System.Xml;
using SharpNeat.Coordination;

/// <summary>
/// This class coordinates the neuroevolution process, where neural networks 
/// are developed to control given agents.
/// 
/// This class will use NEAT evolution, with either manual or automatic 
/// fitness evaluation.
/// 
/// Most objects are initiated in SimpleExperiment, and some parameters are loaded
/// from the file experiment.config.
/// Evolution can be automatic, where units play around and then are given a 
/// fitness value depending on their performance, or manual, where the user
/// decides which behaviours should be rewarded. 
/// </summary>
public class EspNeatOptimizer : Optimizer
{
    // Number of input/output neurons in the neural networks. These are constant 
    // values through neuroevolution.
    public int num_inputs = 7;
    public int num_outputs = 2;
    public string experiment_name = "experiment";	
    // This are the objects that will use the neural controllers.
    public GameObject Unit;

    // Controls whether the population will be saved for each generation.
    public bool writeAllGenerations = false;

    // This is the fitness value that units will receive if they are selected
    // during a manual selection round. Right now it is only relevant that this
    // is a value greater than 0.
    public double fitness_in_manual = 10.0;
    // In case we want to change the size of the highlight bubble for manual mode.
    public float selection_sphere_radius = 1f;

    // Used in Update to calculate frames per second.
    private DateTime startTime;
    int frames = 0;
    float timeLeft = 0.0f;
    float accum = 0.0f;

	private UImanager uiManager;

    private uint generation;
    private double fitness;
    private bool manual = false; // fitness selection or manual selection?
    // Use if you need your rays in manual selection to interact only with
    // a given set of layers
    private LayerMask for_mouse_input;
    private string unit_tag = "Unit";
    private bool isReward = true;
    
    // Is the evolutionary algorithm active?
    private bool EARunning;
    // Is a champion instane running?
    private bool champRunning = false;
    // Here the genomes are saved in an external file. If another evolutionary
    // process is started it will load genome populations from here. If this is 
    // not wanted, change the name of the experiment (in the GameObject "Evaluator")
    // or delete these files.
    private string popFileSavePath, champFileSavePath, baseFilesPath;
    // These two are used to save ALL the genomes in each generation! This is
    // for research purposes and can be controlled by the variable writeAllGenerations.
    private string timeStamp = "";

    // SimpleExperiment initiates most objects needed for the evolutionary
    // process.
    SimpleExperiment experiment;
    // Units are controlled by a script, which inherits from UnitController.
    // Each of these controllers uses a neural network of type IBlackBox.
	Dictionary<SharpNeat.Phenomes.IBlackBox, UnitController> ControllerMap = 
			new Dictionary<SharpNeat.Phenomes.IBlackBox, UnitController>();
    // Neural evolution algorithm using NeatGenome.
    static NeatEvolutionAlgorithm<NeatGenome> _ea;
    // Manual evolution extension.
    static NeatManualEvolution<NeatGenome> manual_ea;

    // This is used when the user wants to choose the champion genome (but
    // not to proceed with evolution!)
    private bool isSetChampion = false;

    #region Properties

    /// <summary>
    /// Gets the evolution algorithm.
    /// </summary>
    public NeatEvolutionAlgorithm<NeatGenome> EvolutionAlgorithm
    {
        get { return _ea; }
    }

    public uint Generation
    {
        get { return generation; }
    }

    public double Fitness
    {
        get { return fitness; }
    }

    public bool GetEARUnning
    {
        get { return EARunning; }
    }

    public bool ChampRunning
    {
        get { return champRunning; }
	}

	public string ExperimentName
	{
		get { return experiment_name; }
	}

    public string TimeStamp
    {
        set { timeStamp = value; }
    }

    public bool Manual
    {
        get { return manual; }
        set { manual = value; }
    }

    public bool IsSetChampion
    {
        set { isSetChampion = value; }
    }

    #endregion

    #region Public Methods

	public void StartEvolutionAlgorithm()
    {         
        Utility.DebugLog = true;
        Utility.Log("Starting neuroevolution");

        StartGenomePopulation();

        // We do it here, so we do not try to access this before the algorithm is set up.
        if (manual == true)
        {
            _ea.Manual = true;
        }

        startTime = DateTime.Now;

        // Subscribes the function _ea.UpdateEvent and .PausedEvent to the event UpdateEvent
        // which is handled by the evolution algorithm (which is our object _ea, 
        // of type NeatEvolutionAlgorithm : AbstracGenerationalAlgorithm
        // The same for PausedEvent.
        // When _ea notifies there is an update or a pause these events will notify
        // optimizer, which will then call the functions we are subscribing.
        _ea.UpdateEvent += new EventHandler(ea_UpdateEvent);
        _ea.PausedEvent += new EventHandler(ea_PauseEvent);
        // The above can be simplified in Unity: _ea.UpdateEvent += ea_UpdateEvent is valid

        // TODO: There should be an unsubscribe line somewhere, but right now 
        // the program ends with Optimizer, so it is not needed. 

        // Starts the algorithm running. The algorithm will switch to the Running 
        // state from either the Ready or Paused states.
		_ea.StartContinue();
        EARunning = true;
    }

    /// <summary>
    ///  Simple saving: only affects the current population and will be overwritten.
    ///  To really save use SavePopulation(directoryPath, populationPath, championPath)
    /// </summary>
    public void SimpleSavePopulation()
    {
        SavePopulation(baseFilesPath); 
    }
    void SavePopulation(string directoryPath)
    {
        SavePopulation(directoryPath, popFileSavePath, champFileSavePath);
        SaveResearch(directoryPath);
    }
    /// <summary>
    /// Allows to specify other save paths (e.g.: used from NeatGenomeFactory
    /// before adding a new module so the genome diversity is not lost if we
    /// want to reset the addition of the new module, which overwrites every
    /// genome with the champion!)
    /// </summary>
    public void SavePopulation(string directoryPath, string popPath, string champPath)
    {
        XmlWriterSettings _xwSettings = new XmlWriterSettings();
        _xwSettings.Indent = true;

        CreateDirectoryIfNew(directoryPath);

        // Save genomes to xml file.  
        using (XmlWriter xw = XmlWriter.Create(popPath, _xwSettings))
        {
            experiment.SavePopulation(xw, _ea.GenomeList);
        }
        // Also save the best genome
        using (XmlWriter xw = XmlWriter.Create(champPath, _xwSettings))
        {
            experiment.SavePopulation(xw, new NeatGenome[] { _ea.CurrentChampGenome });
        }
    }

    /// <summary>
    /// This is used in research experiments, so all generations are saved.
    /// timeStamp allows to classify different experiments.
    /// Note that in any case we have the normal files, because this may be
    /// needed to load a population.
    /// </summary>
    public void SaveResearch(string directoryPath)
    {
        string fitnessDirectory = directoryPath + "/" + experiment_name + ".fitnessEvol.dat";
        using (System.IO.StreamWriter file = new System.IO.StreamWriter(fitnessDirectory, true))
        {
            string line = "Generation: " + generation.ToString() +
                          ", max fitness: " + fitness.ToString();
            file.WriteLine(line);
        }

        if (writeAllGenerations)
        {
            string newDirectory = directoryPath + "/" + timeStamp;
            string popPath = CreateSavePath(newDirectory, ".pop.xml");
            string champPath = CreateSavePath(newDirectory, ".champ.xml");
            SavePopulation(newDirectory, popPath, champPath);            
        }        
    }

    string CreateSavePath(string basePath, string pathSuffix)
    {
        return basePath + "/" + generation.ToString() + experiment_name + pathSuffix; 
    }

    /// <summary>
    /// Resets GUI elements and analyses a new genome instance (in case its
    /// structure (the common structure of all instances) has changed).
    /// </summary>
    public void ResetGUI()
    {
        // For the schematic view all genomes are the same.
        uiManager.UpdateModelGenome(_ea.GenomeList[0]);
    }

    /// <summary>
    /// Stops the evolutionary process (progress is saved)
    /// </summary>
    public void StopEA()
    {
        if (_ea != null && _ea.RunState == SharpNeat.Core.RunState.Running)
        {
            // Requests that the algorithm pauses (will notify via an UpdateEvent)
            _ea.Stop();
        }
    }

    public void StopInteractiveEvolution()
    {
        SetAborted();
        EndManual(); 
        // EaIsNextManual(true) prevents fitness-based evaluation of the genome, 
        // which we don't need when aborting interactive evolution.
        EaIsNextManual(true);
        GoThenAuto();
        StopEA(); 
    }

    /// <summary>
    /// Called by SimpleEvaluator and NeatManualEvaluator.
    /// Instantiates and activates a unit. Adds its controller (box parameter)
    /// to the dictionary ControllerMap.
    /// </summary>
	public override void InstantiateCandidate(SharpNeat.Phenomes.IBlackBox box)
    {
        GameObject obj = Instantiate(Unit, Unit.transform.position, 
                                     Unit.transform.rotation) as GameObject;
        UnitController controller = obj.GetComponent<UnitController>();
        ControllerMap.Add(box, controller);
        controller.Activate(box);
    }

    /// <summary>
    /// Destroys the unit that uses a given controller (parameter box)
    /// </summary>
    public override void DestroyCandidate(SharpNeat.Phenomes.IBlackBox box)
    {
        UnitController ct = ControllerMap[box];
        Destroy(ct.gameObject);
    }

    /// <summary>
    ///  Creates an instance of the current champion
    /// </summary>
    public void RunBest()
    {
        champRunning = true;
        NeatGenome genome = LoadChampion();

        // Get a genome decoder that can convert genomes to phenomes.
        var genomeDecoder = experiment.CreateGenomeDecoder();
        // Decode the genome into a phenome (neural network).
        var phenome = genomeDecoder.Decode(genome);
        InstantiateCandidate(phenome);
        GameObject bestInstance = ControllerMap[phenome].gameObject;
            
        // Special tag so we can selectively actuate on these units.
        bestInstance.tag = "BestUnit";
        // Also removes the tag from child components (but no granchildren. This
        // is currently ok since these elements have no coliders and champions
        // are destroyed before entering evolution).
        // These cannot use "BestUnit" or we will be in trouble when we destroy
        // the unit! 
        foreach (Transform child in bestInstance.transform)
        {
            child.gameObject.tag = "Untagged";
        }

        // This is called if we want the champions to be evaluated, mostly for
        // research reasons.
        if (false)
        {
            Coroutiner.StartCoroutine(EvaluateChamp(phenome));
        }
	}

    /// <summary>
    /// Waits for the trial duration and then asks for a fitness evaluation.
    /// Used mainly for research.
    /// </summary>
	public IEnumerator EvaluateChamp(SharpNeat.Phenomes.IBlackBox brain) {
        while (champRunning)
        {
            yield return new WaitForSeconds(base.TrialDuration);
            float fit = GetFitness(brain); 

            Debug.Log("Fitness evaluation returns: " + fit + "\n");
        }  
	}

    /// <summary>
    /// Gets the fitness corresponding to the unit which uses the controller "box"
    /// </summary>
	public override float GetFitness(SharpNeat.Phenomes.IBlackBox box)
    {
        if (ControllerMap.ContainsKey(box))
        {
            // This is the function written by the user in the derived class from the
            // abstract UnitController script!
            return ControllerMap[box].GetFitness();
        }
        return 0;
    }

    /// <summary>
    /// Destroys the units created by "Run Best"
    /// </summary>
    public void DestroyBest()
    {
        champRunning = false;

        GameObject[] bests;
        bests = GameObject.FindGameObjectsWithTag("BestUnit");
        foreach (GameObject best in bests)
        {
			SharpNeat.Phenomes.IBlackBox box = best.GetComponent<UnitController>().GetBox();
            if (ControllerMap.ContainsKey(box))
            {        
                ControllerMap.Remove(box);
            }
            // We destroy the unit
            Destroy(best);
        }
    }

    /// <summary>
    /// Sets "whichModule" as active: clones the current champion and moves
    /// "whichModule" to the end of the genome, then (from UImanager) produces mutations.
    /// </summary>
    public void AskSetActive(UIvariables uiVar, int whichModule)
    {
        _ea.GenomeList[0].GenomeFactory.SetModuleActive(
                _ea.GenomeList, baseFilesPath, experiment_name, uiVar, whichModule);

        // Sets whichModule as the current module in the factory!
        _ea.GenomeList[0].GenomeFactory.CurrentModule = whichModule;
    }

    /// <summary>
    /// Used from UImanager to finally proceed with the creation of the
    /// new module (once we have all the details!)
    /// </summary>
    public void AskCreateModule(UIvariables uiVar)
    {
        _ea.GenomeList[0].GenomeFactory.AddNewModule(
                _ea.GenomeList, baseFilesPath, experiment_name, uiVar);

		UpdateChampion();
        SimpleSavePopulation();    

        // TODO: IMPORTANT NOTICE!
        // There seems to be a bug here: when a population is loaded the ID
        // generator is reset to the last value used + 1. Then, only the FIRST
        // time AskCreateModule is called, even if at the very end of AskCreateModule
        // the ID generator is more advanced (because elements have been created)
        // the ID at the exit of AddNewModule is again the original value used
        // after loading the population. Why? Problems with references?
        // This is not usually relevant (apparently AddNewModule will find the
        // correct value again if called a second time, and for some reason in new
        // calls the problem does not happen again). But AskMutateOnce will
        // not correct the problem, and it may try to add elements with repeated
        // IDs, which creates genomes that fail the integrity check.

        // Easy patch: update the ID generator here.
		_ea.GenomeList[0].GenomeFactory.InitializeGeneratorAfterLoad(_ea.GenomeList); 
    }

    /// <summary>
    /// This is used to clone a module. Note two things:
    /// 1) The cloned module will never be the active module! (It will be placed
    /// immediately before.)
    /// TODO: 2) Complex modules (such as regulation modules) will need some further
    /// work to make sure the connexions among modules are correct.
    /// </summary>
    public void AskCloneModule(UIvariables uiVar, int whichModule)
    {
        _ea.GenomeList[0].GenomeFactory.CloneModule(
                _ea.GenomeList, baseFilesPath, experiment_name, uiVar, whichModule);   
		UpdateChampion();
        SimpleSavePopulation();   
    }

    /// <summary>
    /// The "base" is the same for all genomes, so we use the first one.
    /// First finds the index for the regulatory neuron, then returns
    /// the ID of this neuron.
    /// </summary>
    public uint RegIdFromModId(int moduleId)
    {
        int regIndex = 0;
        _ea.GenomeList[0].NeuronGeneList.FindRegulatory(moduleId, out regIndex);
        return _ea.GenomeList[0].NeuronGeneList[regIndex].Id;
    }

    /// <summary>
    /// Used from regulation modules to add a new local output neuron connected
    /// to the regulatory neuron of the module we are adding. Also creates 
    /// connections from local inputs to the new local output.
    /// </summary>
    public void AskAddModuleToRegModule(UIvariables uiVar, newLink localOutInfo)
    {
        _ea.GenomeList[0].GenomeFactory.AddModuleToRegModule(
                _ea.GenomeList, baseFilesPath, experiment_name, uiVar, localOutInfo);
        UpdateChampion();
        SimpleSavePopulation();  
        
    }

    /// <summary>
    /// Used from UImanager to change only the protected weights in the
    /// genome population.
    /// </summary>
    public void AskChangeWeights(UIvariables uiVar)
    {
        _ea.GenomeList[0].GenomeFactory.ChangeWeights(_ea.GenomeList, uiVar);
        UpdateChampion();
    }

    /// <summary>
    /// Used from UImanager to change only the local output targets of a 
    /// module in the genome population.
    /// </summary>
    public void AskChangeTargets(UIvariables uiVar, int whichModule)
    {
        _ea.GenomeList[0].GenomeFactory.ChangeTargets(_ea.GenomeList, uiVar, whichModule);
        UpdateChampion();
    }

    /// <summary>
    /// Given a module and target, returns the protected connection with such target.
    /// </summary>
    public ConnectionGene ConnectionFromModAndTarget(int moduleId, uint targetId)
    {
		ConnectionGene returnConnection = new ConnectionGene(0, 0, targetId, 0, moduleId, true);
        foreach (ConnectionGene connection in _ea.GenomeList[0].ConnectionGeneList)
        {
            if (connection.ModuleId == moduleId &&
                connection.TargetNodeId == targetId)
            {
                returnConnection = connection;
            }
        }
        return returnConnection;
    }

    /// <summary>
    /// Used from UImanager to change the input that goes to a regulatory
    /// neuron (but not for the new module in AddModule, which is done normally
    /// with AskCreateModule).
    /// </summary>
    public void AskUpdateInToReg(UIvariables uiVar)
    {
        _ea.GenomeList[0].GenomeFactory.UpdateInToReg(_ea.GenomeList, uiVar);
        _ea.GenomeList[0].GenomeFactory.UpdateStatistics(_ea.GenomeList[0]);
        UpdateChampion(); 
    }

    /// <summary>
    /// Used from UImanager to update the pandemonium state of modules
    /// without having to add a new module.
    /// </summary>
    public void AskUpdatePandem(UIvariables uiVar)
    {
        // This method does not require to pass the genome list (it will use
        // the variable for Optimizer from Factory, as we could do for the
        // rest of these AskSomething methods).
        _ea.GenomeList[0].GenomeFactory.UpdatePandem(uiVar); 
        UpdateChampion();  
    }

    /// <summary>
    /// Used from UImanager to ask for the resetting of the last module in
    /// all genomes.
    /// This counts as a new generation!
    /// </summary>
    public void AskResetActiveModule()
    {
        uiManager.WriteToRecord("Reset");
        // Note this will not return the cameras to the editing menu.
        StopInteractiveEvolution();
        // Restart and reset (after allowing time to stop and remove old individuals!)
        Coroutiner.StartCoroutine(WaitResetStart());
    }
    /// <summary>
    /// Second part of the method AskResetActiveModule. The first part of the
    /// process is stopping the simulation, which needs time to remove old
    /// individuals. Because coroutines are used, it may (will) be problematic
    /// to continue without allowing time for this to finish.
    /// </summary>
    IEnumerator WaitResetStart()
    {
        // Some time is needed to finish removing old individuals
        int waitForFrames = 20;
        while (waitForFrames >= 0)
        {
            --waitForFrames;
            yield return null;
        }

        // Resets the active module (resets the evolution)
        ++generation;
        _ea.GenomeList[0].GenomeFactory.ResetActiveModule(_ea.GenomeList, generation);
        UpdateChampion(); 
        SimpleSavePopulation();

        // Starts a new evolutionary process
        Manual = true;
        StartEvolutionAlgorithm();        
    }

    /// <summary>
    /// Asks the genome factory to remove a given module from all genomes. Note
    /// that this option may only be called from a module that is not currently
    /// beeing evolved! In that case the option "reset" is offered instead.
    /// </summary>
	public void AskDeleteModule(int whichModule)
	{
        // TODO: Ensure this is NOT the active module. The active module is the
        // one being evolved, and is the only module that is not exactly the
        // same for all individuals in the population.

        ++generation;

		_ea.GenomeList[0].GenomeFactory.DeleteModule(_ea.GenomeList,
                                                          generation, whichModule);

        UpdateChampion(); 
        SimpleSavePopulation(); 		
	}

    public void AskMutateOnce()
    {
        ++generation;

        foreach (NeatGenome genome in _ea.GenomeList)
        {
            genome.SimpleMutation();
        }

        UpdateChampion();
        SimpleSavePopulation();     
    }

    /// <summary>
    /// Champion stores a copy of a genome, but apparently does not copy changes
    /// that are made in that genome. Here we make sure that this is updated
    /// after genomes are changed.
    /// </summary>
    public void UpdateChampion()
    {
        uint champId = _ea.CurrentChampGenome.Id;
        foreach (NeatGenome genome in _ea.GenomeList)
        {
            if (genome.Id == champId)
            {
                _ea.CurrentChampGenome = genome;
                return;
            }
        }
        // If the genome is not found, leaves things as they are. Which may be
        // problematic, so at least warns the developer:
        Debug.Log("Champion not found in the current population!");
    }
    public void UpdateChampion(uint champId)
    {
        foreach (NeatGenome genome in _ea.GenomeList)
        {
            if (genome.Id == champId)
            {
				_ea.CurrentChampGenome = genome;
                SimpleSavePopulation();    
                return;
            }
        }
        // If the genome is not found, leaves things as they are. Which may be
        // problematic, so at least warns the developer:
        Debug.Log("Champion not found in the current population!");
    }

    /// <summary>
    /// Used from GuiManager to delete save files!
    /// </summary>
    public void DeleteSave()
    {
        System.IO.File.Delete(champFileSavePath);
        System.IO.File.Delete(popFileSavePath);        
    }

    /// <summary>
    /// Used from GuiManager to determine whether the next generation 
    /// will be manual or automatic (only used while in manual mode).
    /// </summary>
    public void EaIsNextManual(bool nexManual)
    {
        _ea.IsNextManual = nexManual;
    }

    /// <summary>
    /// Manual selection is complete. We can now exit the NeatManualEvolution
    /// extension and return to NeatEvolutionAlgorithm.
    /// </summary>
    public void EndManual()
    {
        // This tells the manual algorithm to finish and return to the auto section.
        manual_ea.NextGeneration = true;
        // This stops the waiting in the auto section.
        _ea.Proceed = true;        
    }

    /// <summary>
    /// Sets (as true) the label for an aborted exit from manual mode. 
    /// </summary>
    public void SetAborted()
    {
        manual_ea.IsAborted = true;
    }

    /// <summary>
    /// When the manual selection is complete, proceed to create offspring and
    /// then continue with automatic generations.
    /// </summary>
    public void GoThenAuto()
    {
        _ea.Manual = false;
        // No more need for the manual GUI.
        manual = false;    
    }

    /// <summary>
    /// Initializes our evolutionary algorithms (For instance, here the
    /// manual selection extension gets access to the genome-to-phenome
    /// decoder.
    /// Loads a genome population from a save file or calls the factory to 
    /// create the first generation.
    /// </summary>
    public void StartGenomePopulation()
    {
        _ea = experiment.CreateEvolutionAlgorithm(popFileSavePath);
        experiment.SetDecoderInManualEA(manual_ea);
        // We also pass the instance of NeatManualEvolution to 
        // NeatEvolutionaryAlgorithm and vice versa
        _ea.Manual_ea = manual_ea;  
        // Resets the current generation in case we are loading files.
        generation = FindOldGeneration();
        _ea.CurrentGeneration = generation;

        // We pass a genome to the uiManager. For the schematic view all 
		// genomes are exactly the same, so we can pass any we like.
        uiManager.UpdateModelGenome(_ea.GenomeList[0]);
    }

    /// <summary>
    /// Toggles the manual selection from punishment to reward.
    /// </summary>
    public void TogglePunishReward()
    {
        if (isReward)
        {
            isReward = false;
            manual_ea.IsReward = false;
        }
        else
        {
            isReward = true;
            manual_ea.IsReward = true;
        }
    }

    /// <summary>
    /// In manual selection we sometimes want to select an interesting individual
    /// as champion, but NOT to continue evolution. This method is to do that.
    /// </summary>
    public void ManualEvolutionSetChampion()
    {
        isSetChampion = true;
        manual_ea.ActivateSetChampion();
    }

    #endregion

    #region Private Methods

	/// <summary>
	/// Debugging porpuses
	/// </summary>
	void DisplayUnit(NeatGenome genome)
	{
		Debug.Log("Nodes");
		foreach (NeuronGene neuron in genome.NeuronGeneList)
		{
			Debug.Log("id " + neuron.Id + " module " + neuron.ModuleId + " pandem " + neuron.Pandemonium);
		}

		Debug.Log("Connections");
		foreach (ConnectionGene link in genome.ConnectionGeneList)
		{
			Debug.Log("id " + link.InnovationId + " source " + link.SourceNodeId + " target " + link.TargetNodeId + " weight " + link.Weight);
		}
	}

    /// <summary>
    /// Reads the configuration document and passes some object instances
    /// around so different objects can refer to each other. 
    /// </summary>
    void Start()
    {	
        // If writeAllGenerations == true then all generations will be saved.
        // The time stamp will allow to distinguish different experiments.
        timeStamp = DateTime.Now.ToString("HHmm");
        InitSavePaths();
        InitGUI();
        Utility.DebugLog = true;
        InitExperiment();
        manual_ea = new NeatManualEvolution<NeatGenome>(this, fitness_in_manual);
        SetRaycastLayers();
        CheckUnitTag();
        StartGenomePopulation();

        // We need to load the champion now (in case we want to add a new module
        // without starting an evolutionary process first, so the program builds
        // the base on the correct genome, instead of a random genome if we skip
        // this step).
        if (LoadChampion() != null)
        {
            _ea.CurrentChampGenome = LoadChampion();            
        }
        uiManager.InstantiateLoadedElements();
    }

    void InitExperiment()
    {
        // We get rid of spaces in the experiment name to avoid trouble with paths
        experiment_name = experiment_name.Replace(' ', '_');
        experiment = new SimpleExperiment(this); 

        // Loads experiment parameters into XML document.
        XmlDocument xmlConfig = new XmlDocument();
        TextAsset textAsset = (TextAsset)Resources.Load("experiment.config");
        xmlConfig.LoadXml(textAsset.text);

        // Loads the experiment parameters from the xml file "experiment.config" 
        experiment.Initialize(experiment_name, xmlConfig.DocumentElement, 
                              num_inputs, num_outputs);
    }

    void InitSavePaths()
    {
        // Fast, hidden address:
        // C:\Users\userName\AppData\LocalLow\CompanyNameInProject\ProjectName
        // Slow, normal address:
        // baseFilesPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        // baseFilesPath += "/" + Application.productName;
        baseFilesPath = Application.persistentDataPath;
        champFileSavePath = baseFilesPath + "/" + experiment_name + ".champ.xml";
        popFileSavePath   = baseFilesPath + "/" + experiment_name + ".pop.xml";
        Debug.Log("Local files' path: " + baseFilesPath);    
    }

    void SetRaycastLayers()
    {
        // Use if you need your rays in manual selection to interact only with
        // a given set of layers. 
        // In this exammple we want to exclude layer 9 "SeenByWorker":
        for_mouse_input = (1 << LayerMask.NameToLayer("SeenByWorker"));
        // We can exclude multiple layers:
        // for_mouse_input |= (1 << 13);
        // If we omit this line raycast will only interact with the previous layers!
        for_mouse_input = ~for_mouse_input;
    }

    void CheckUnitTag()
    {
        if (Unit.transform.tag != "Unit")
        {
            unit_tag = Unit.transform.tag;
            Debug.Log("WARNING: Unit tag is: " + unit_tag);
            Debug.Log("Please set tag as 'Unit'.");
            // TODO: Enforce this? End program otherwise?
            if (Unit.transform.tag == "Untagged")
            {
                unit_tag = "Unit"; 
                Debug.Log("Units were not correctly tagged. Please choose a " +
                    "new tag if you want to use manual selection.");
            }
        }        
    }

    /// <summary>
    /// Instantiates and initializes elements needed for the program GUI.
    /// </summary>
    void InitGUI()
    {
		uiManager = GetComponent<UImanager>();
        uiManager.SetPaths(baseFilesPath + "/" + experiment_name);
        // Needed if sub-folders are used (in uiManager, for instance)
        //CreateDirectoryIfNew(baseFilesPath + "/" + experiment_name);
	}

    void Update()
    {
        if (manual)
        {
            ManualSelection(); 
        }
    }

    /// <summary>
    /// Returns the latest generation in a loaded (or freshly created) population.
    /// </summary>
    uint FindOldGeneration()
    {
        uint max = 0;
        foreach (NeatGenome genome in _ea.GenomeList)
        {
            if (genome.BirthGeneration > max)
            {
                max = genome.BirthGeneration;
            }
        }
        return max;
    }

    NeatGenome LoadChampion()
    {
        NeatGenome genome = null;
        // Try to load the genome from the XML document.
        try
        {
            // It would make more sense to use LoadGenome instead of ReadCompleteGenomeList
            using (XmlReader xr = XmlReader.Create(champFileSavePath))
                genome = NeatGenomeXmlIO.ReadCompleteGenomeList(
                        xr, false, (NeatGenomeFactory)experiment.CreateGenomeFactory())[0];
        } 
        catch (Exception e1) 
        {
        }
        return genome;
    }

    /// <summary>
    /// Updates some information for the user. This is subscribed to the UpdateEvent
    /// used in the evolution algorithm.
    /// </summary>
    void ea_UpdateEvent(object sender, EventArgs e)
    {
        Utility.Log(string.Format("gen={0:N0} bestFitness={1:N6}",
                                  _ea.CurrentGeneration, 
                                  _ea.Statistics._maxFitness));
        fitness = _ea.Statistics._maxFitness;
        generation = _ea.CurrentGeneration;
        SaveResearch(baseFilesPath);
    }

    /// <summary>
    /// Pause the evolutionary process and save the current genomes. This is 
    /// subscribed to the PauseEvent event used in the evolutionary algorithm.
    /// </summary>
    void ea_PauseEvent(object sender, EventArgs e)
    {
        //Time.timeScale = 1;      
        SimpleSavePopulation();
        DateTime endTime = DateTime.Now;
        Utility.Log("Total time elapsed: " + (endTime - startTime));
        EARunning = false;   
    }

    /// <summary>
    /// Checks if the directory path provided exists, and creates it if it does not.
    /// </summary>
    void CreateDirectoryIfNew(string directoryPath)
    {
        System.IO.DirectoryInfo dirInf = new System.IO.DirectoryInfo(directoryPath);
        if (!dirInf.Exists)
        {
            dirInf.Create();
        }        
    }
        
    /// <summary>
    /// Select interesting units with mouse input during manual evolution-
    /// I seem unable to make this work within NeatManualEvolution.ManualEvaluation
    /// as it should. 
    /// </summary>
    void ManualSelection()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            // Did the mouse click get a unit?
            RaycastHit hit;
            if (FindUnit(out hit))
            {
                ProcessHit(hit.collider);
            }
        }        
    }

    /// <summary>
    /// Creates rays (called after mouse click) and returns hit information.
    /// </summary>
    /// <returns><c>true</c>, if unit was found, <c>false</c> otherwise.</returns>
    bool FindUnit(out RaycastHit hit)
    {
        // If there is a mouse click, cast a ray to the environment
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
         if (Physics.Raycast(ray, out hit, Mathf.Infinity, for_mouse_input))
        // You may use the simple form if there are no problems with layers
        // if (Physics.Raycast(ray, out hit))
        {
            // This draws a line to the object that was hit, only in the editor
            // Debug.DrawLine(ray.origin, hit.point);      
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a RaycastHit and sees if it corresponds to a unit (or a child) 
    /// in which case it is send to be marked (or unmarked if already marked) as selected.
    /// </summary>
    void ProcessHit(Collider hit_collider)
    {
        // If there are some units created by "best unit" we are not interested 
        // in them! (It MIGHT work as expected, but this should be really 
        // clear before allowing these units to be selected. 
        // if (hit.collider.tag == "Unit" || hit.collider.tag == "BestUnit") { 

        // First we get the game object
        GameObject targetObject = null;
        targetObject = hit_collider.gameObject;

        if (hit_collider.tag == unit_tag)
        {
            ProcessHit2(targetObject);
        }
        else if (hit_collider.tag == "UnitChild")
        {          
            // This only works correctly if there is only one level of children
            // objects!
            //targetObject = hit_collider.transform.parent.gameObject;

            // Remember transform.root won't work either if the game object
            // has been instantiated as a child itself (within a folder, for 
            // example!)
            while(true)
            {
                targetObject = targetObject.transform.parent.gameObject;
                // When we reach the original root of the prefab we can exit the loop.
                if (targetObject.tag == unit_tag)
                {
                    break;
                }
            }

            ProcessHit2(targetObject);          
        }  
    }

    /// <summary>
    /// We have a valid target, here we decide what to do with it. If we are
    /// in normal interactive evolution the unit is marked/unmarked and added
    /// or removed from the list of selected units (in the script NeatManuaEvolution)
    /// 
    /// If we are trying to select a champion, we send the selected unit to
    /// NeatManualEvolution (using a different method) and we do not mark or
    /// unmark the unit.
    /// </summary>
    void ProcessHit2(GameObject targetObject)
    {
        // If we are in normal interactive evolution, we process the unit
        if (isSetChampion == false)
        {
            // Let us highlight this units
            CreateBubble(targetObject);            
            // This function will mark units as selected and will send them to 
            // NeatManualEvolution
            MarkAndSend(targetObject.GetComponent<UnitController>()); 
        }
        else
        {                   
            // If we were looking for a champion, we also pass the unit for
            // (different) processing:
            manual_ea.SelectChampion(
                targetObject.GetComponent<UnitController>().GetBox());
        }  
    }

    /// <summary>
    /// Given units are marked as selected (visually and in NeatManualEvolution)
    /// or they are deselected if they were already selected (toggle selection)
    /// </summary>
    /// <param name="chosen_unit_controller">Chosen unit controller.</param>
    void MarkAndSend(UnitController chosen_unit_controller)
    {
        // If the unit is not already marked 
        if (!chosen_unit_controller.Selected)
        {         
            chosen_unit_controller.Selected = true;
            // We need to send back the box (phenome) of the GameObject hit by the ray. 
            manual_ea.GetChosenPhenome(chosen_unit_controller.GetBox()); 

        }
        // If it was already marked, then we deselect it
        else
        {             
            chosen_unit_controller.Selected = false;
            manual_ea.DeselectPhenome(chosen_unit_controller.GetBox()); 
        }   
    }

    /// <summary>
    /// Creates a highlight bubble around the selected unit.
    /// </summary>
    /// <param name="this_unit">This unit.</param>
    void CreateBubble(GameObject this_unit)
    {
        // We create the sphere for selected units, and remove it when deselected
        if (!this_unit.GetComponent<UnitController>().Selected)
        {           
            // Create and label
            GameObject bubble;
            if (isReward)
            {
                bubble = (GameObject)Instantiate(Resources.Load("Prefabs/HighlightBubble")); 
            }
            else
            {
                bubble = (GameObject)Instantiate(Resources.Load("Prefabs/HighlightBubblePunish"));  
            }
            bubble.transform.position = this_unit.transform.position;
            bubble.transform.parent = this_unit.transform;
            bubble.transform.localScale = new Vector3(selection_sphere_radius,
                                                      selection_sphere_radius,
                                                      selection_sphere_radius);
        }
        else
        {            
            // Destroy the GameObject used for highlighting!
            Destroy(FindChildWithTag(this_unit.transform, "HighlightBubble"));
        } 
    }

    /// <summary>
    /// Searchs for the tag "HighlightBubble" among the children of a parent unit
    /// so that the highlight bubble can be destroyed.
    /// </summary>
    GameObject FindChildWithTag(Transform parent_transform, string tag)
    {
        foreach (Transform child_transform in parent_transform)
        {
            if (child_transform.tag == tag)
            {
                return child_transform.gameObject;
            }
        }
        return null;
    }

    #endregion
}

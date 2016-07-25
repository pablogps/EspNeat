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

	// In the editor we conveniently define all our styles within a GUISkin.
    // This needs to be assigned to a public variable in the game object's
    // Unity inspector. We will use this skins in Visualizer, but this script
    // is instantiated at runtime, so we cannot directly assign the skin there.
    // Instead, we do it here and pass the variable via a public function.
	public GUISkin mySkin;

    // Used in Update to calculate frames per second.
    private DateTime startTime;
    int frames = 0;
    float timeLeft = 0.0f;
    float accum = 0.0f;

	private UImanager uiManager;
    private GuiManager guiManager;
    private Visualizer visualizer;

    private uint generation;
    private double fitness;
    private bool manualWait = false; // not used in this version.
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
    private string popFileSavePath, champFileSavePath;
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

    public string TimeStamp
    {
        set { timeStamp = value; }
    }

    public bool Manual
    {
        get { return manual; }
        set { manual = value; }
    }

    /// <summary>
    /// This is not needed.
    /// TODO: Delete this (which requires changes in Optimizer). 
    /// </summary>
    public override bool ManualWait
    {
        get { return manualWait; }
        set { manualWait = value; }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts the evolutionary process. Note that the automatic evolutionary 
    /// algorithm is also used as the base of the manual process! 
    /// (Since only the evaluation of units differs.)
    /// </summary>
    public void StartEA()
    {         
        Utility.DebugLog = true;
        Utility.Log("Starting neuroevolution");

        StartGenomePopulation();

        // We do it here, so we do not try to access this before the 
        // algorithm is set up.
        if (manual == true)
        {
            _ea.Manual = true;
        }
        startTime = DateTime.Now;

        // Here we subscribe the function ea_UpdateEvent to the event UpdateEvent
        // which is handled by the evolutionary algorithm (which is our object _ea, 
        // of type NeatEvolutionAlgorithm : AbstracGenerationalAlgorithm
        // The same for PausedEvent.
        // When _ea notifies there is an update or a pause these events will notify
        // optimizer, which will then call the functions we are subscribing.
        _ea.UpdateEvent += new EventHandler(ea_UpdateEvent);
        _ea.PausedEvent += new EventHandler(ea_PauseEvent);
        // The above can be simplified in Unity: _ea.UpdateEvent += ea_UpdateEvent is valid
        // TODO: There should be an unsubscribe line somewhere, but right now 
        // the program ends with Optimizer, so it is not needed. 

        // This feature is old: now this is chosen with a UI slider
        // speed is increased so evolution is faster in real time
        //var evoSpeed = 25;
        //Time.timeScale = evoSpeed;

        // Starts the algorithm running. The algorithm will switch to the Running 
        // state from either the Ready or Paused states.
        _ea.StartContinue();
        EARunning = true;
    }

    /// <summary>
    /// Sets the current menu screen selector.
    /// </summary>
    public void SetMenuScreen(MenuScreens chosenScreen)
    {
        //guiManager.SetMenuScreen(chosenScreen);
    }

    /// <summary>
    /// Overload so we can specify other save paths (used from NeatGenomeFactory
    /// before adding a new module so the genome diversity is not lost if we
    /// want to reset the addition of the new module, which overwrites every
    /// genome with the champion!)
    /// </summary>
    public void SavePopulation(string directoryPath, string popPath, string champPath)
    {
        XmlWriterSettings _xwSettings = new XmlWriterSettings();
        _xwSettings.Indent = true;

        IsThereDirectory(directoryPath);

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

        SaveResearch(directoryPath);
    }
    /// <summary>
    ///  Simple saving: only affects the current population and will be
    ///  overwritten. To really save use 
    ///  SavePopulation(string directoryPath, string popPath, string champPath)
    /// </summary>
    public void SimpleSavePopulation()
    {
        SavePopulation(Application.persistentDataPath);  
    }
    /// <summary>
    /// Saves the population and champion genome in the default path.
    /// </summary>
    void SavePopulation(string directoryPath)
    {
        SavePopulation(directoryPath, popFileSavePath, champFileSavePath);
    }

    /// <summary>
    /// This is used in research experiments, so all generations are saved.
    /// timeStamp allows to classify different experiments.
    /// Note that in any case we have the normal files, because this may be
    /// needed to load a population.
    /// </summary>
    public void SaveResearch(string directoryPath)
    {
        if (writeAllGenerations)
        {
            XmlWriterSettings _xwSettings = new XmlWriterSettings();
            _xwSettings.Indent = true;

            // There is probably a cleverer way to do this.
            string newDirectory = directoryPath + "/" + timeStamp;
            string popPath = newDirectory + "/" + generation.ToString() +
                             experiment_name + ".pop.xml";            
            string champPath = newDirectory + "/" + generation.ToString() +
                               experiment_name + ".champ.xml";

            IsThereDirectory(newDirectory);

            using (XmlWriter xw = XmlWriter.Create(popPath, _xwSettings))
            {
                experiment.SavePopulation(xw, _ea.GenomeList);
            }
            using (XmlWriter xw = XmlWriter.Create(champPath, _xwSettings))
            {
                experiment.SavePopulation(xw, new NeatGenome[] { _ea.CurrentChampGenome });
            }            
        }        
    }

    /// <summary>
    /// Resets GUI elements and analyses a new genome instance (in case the
    /// structure has changed).
    /// </summary>
    public void ResetGUI()
    {
        // We update the genome used in visualizer. For the schematic view all 
        // genomes are exactly the same, so we can pass any we like.
        // visualizer.UpdateModelGenome(_ea.GenomeList[0]);
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

    /// <summary>
    /// Proper sequence to end an interactive evolution process
    /// </summary>
    public void StopInteractiveEvolution()
    {
        // Old fitness values will be recovered and used to produce offspring.
        SetAborted();
        EndManual();        
        // Automatic testing of units disabled (we will abort, this is not needed).
        EaIsNextManual(true);
        GoThenAuto();
        StopEA(); 
    }

    /// <summary>
    /// Called by SimpleEvaluator
    ///****Why is this called "Evaluate" when it instantiates?
    /// Instantiates and activates a unit. Adds its controller (box parameter)
    /// to the dictionary ControllerMap.
    /// DO NOT confuse this function with SimpleEvaluator.Evaluate.
    /// </summary>
    /// <param name="box">Box.</param>
    // TODO: Change name. Maybe "InstantiateUnit"?
	public override void Evaluate(SharpNeat.Phenomes.IBlackBox box)
    {
        GameObject obj = Instantiate(Unit, Unit.transform.position, 
            Unit.transform.rotation) as GameObject;
        UnitController controller = obj.GetComponent<UnitController>();
        ControllerMap.Add(box, controller);
        controller.Activate(box);
    }

    /// <summary>
    /// Called by SimpleEvaluator and NeatManualEvolution.
    /// Destroys the unit that uses a given controller (parameter box)
    /// </summary>
    /// <param name="box">Box.</param>
	public override void StopEvaluation(SharpNeat.Phenomes.IBlackBox box)
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
        // The actual unit is now created, added to the dictionary and activated.
        GameObject obj = Instantiate(Unit, Unit.transform.position, 
            Unit.transform.rotation) as GameObject;
        UnitController controller = obj.GetComponent<UnitController>();
        ControllerMap.Add(phenome, controller);
        controller.Activate(phenome);
        // Special tag so we can selectively actuate on these units.
        // For instance to kill them while neuroevolution is active
        obj.tag = "BestUnit";
        // Also removes the tag from child components (no granchildren, but since 
        // these have no colliders we don't need to worry about that during manual
        // selection (rays will never interact with these components!)
        // These cannot use "BestUnit" or we will be in trouble when we destroy
        // the unit! 
        foreach (Transform child in obj.transform)
        {
            child.gameObject.tag = "Untagged";
        }
    }

    /// <summary>
    /// Gets the fitness corresponding to the unit which uses the controller "box"
    /// </summary>
	public override float GetFitness(SharpNeat.Phenomes.IBlackBox box)
    {
        // If the provided controller is in the dictionary, we retrieve the fitness
        // of the unit that uses said controller
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

        // We need to remove units created by the button "Run Best"
        // First we identify all remaining units (not taken care by StopEA)
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
    /// Sets whichModule as active (clones the current champion and moves
    /// whichModule to the end of the genome, then produces mutations)
    /// </summary>
    public void AskSetActive(UIvariables uiVar, int whichModule)
    {
        _ea.GenomeList[0].GenomeFactory.SetModuleActive(
                _ea.GenomeList, Application.persistentDataPath, experiment_name,
                uiVar, whichModule);

        // Sets whichModule as the current module in the factory!
        _ea.GenomeList[0].GenomeFactory.CurrentModule = whichModule;

        // UImanager will call MutateOnce, which already updates the champion
        // and saves the progress.
    }

    /// <summary>
    /// Used from UImanager to finally proceed with the creation of the
    /// new module (once we have all the details!)
    /// </summary>
    public void AskCreateModule(UIvariables uiVar)
    {
        _ea.GenomeList[0].GenomeFactory.AddNewModule(
                _ea.GenomeList, Application.persistentDataPath, experiment_name, uiVar);

		UpdateChampion();
        SavePopulation(Application.persistentDataPath);    

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
    /// 2) Complex modules (such as regulation modules) will need some further
    /// work to make sure the connexions among modules are correct.
    /// </summary>
    public void AskCloneModule(UIvariables uiVar, int whichModule)
    {
        _ea.GenomeList[0].GenomeFactory.CloneModule(
                _ea.GenomeList, Application.persistentDataPath, experiment_name,
				uiVar, whichModule);   
		UpdateChampion();
		SavePopulation(Application.persistentDataPath);   
    }

    /// <summary>
    /// Used to rewire cloned regulation modules (note that while cloning them
    /// in factory we do not know the IDs for the cloned children)
    /// </summary>
    public uint RegIdFromModId(int moduleId)
    {
        // The base is the same for all genomes, so we use the first one.
        // First finds the index for the regulatory neuron, then returns
        // the ID of this neuron.
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
                _ea.GenomeList, Application.persistentDataPath, experiment_name,
                uiVar, localOutInfo);
        UpdateChampion();
        SavePopulation(Application.persistentDataPath);  
        
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
    /// Given a module and target, returns the ID of a protected connection with
    /// such target, as well as the source.
    /// </summary>
    public void ConnectionIdFromModAndTarget(int moduleId, uint targetId,
                                             out uint connectionId, out uint sourceId)
    {
        connectionId = 0;
        sourceId = 0;

        foreach (ConnectionGene connection in _ea.GenomeList[0].ConnectionGeneList)
        {
            if (connection.ModuleId == moduleId &&
                connection.TargetNodeId == targetId)
            {
                connectionId = connection.InnovationId;
                sourceId = connection.SourceNodeId;
                return;
            }
        }
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
        Debug.Log("RECORDAR ELIMINAR REFERENCIAS ANTIGUAS A ASKRESETACTIVEMODULE");
        Debug.Log("añadir aquí el panel de confirmación");
        Debug.Log("RECORDAR ELIMINAR REFERENCIAS ANTIGUAS A ASKRESETACTIVEMODULE");

        // Stops current evolution. Note this will not return the cameras to
        // the editing menu
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
        SavePopulation(Application.persistentDataPath);

        // Starts a new evolutionary process
        Manual = true;
        StartEA();        
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
		SavePopulation(Application.persistentDataPath); 		
	}

    public void AskMutateOnce()
    {
        ++generation;

/*        Debug.Log("enter mutate once!");
        for (int i = 0; i < _ea.GenomeList[0].NeuronGeneList.Count; ++i)
        {
            UnityEngine.Debug.Log("index " + i + " id " + _ea.GenomeList[0].NeuronGeneList[i].Id);
        }*/

        foreach (NeatGenome genome in _ea.GenomeList)
        {
            genome.SimpleMutation();
        }

        UpdateChampion();
        SavePopulation(Application.persistentDataPath);     
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
        // NeatManualEvolution extension will not be executed next time.
        _ea.Manual = false;
        // No more need for the manual GUI.
        manual = false;    
        // Get time to its normal speed (old feature).
        //ResetTime(); 
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

        // We pass a genome to the visualizer. For the schematic view all 
		// genomes are exactly the same, so we can pass any we like.
        //visualizer.UpdateModelGenome(_ea.GenomeList[0]);
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

    #endregion

    #region Private Methods

    /// <summary>
    /// Reads the configuration document and passes some object instances
    /// around so different objects can refer to each other. 
    /// </summary>
    void Start()
    {		
        /*
        SharpNeat.Network.InverseAbsoluteSteepSigmoid funcion1 = new SharpNeat.Network.InverseAbsoluteSteepSigmoid();
        SharpNeat.Network.SteepenedSigmoid function2 = new SharpNeat.Network.SteepenedSigmoid();

		System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
		TimeSpan ts;
		double ans;    
        
		stopWatch.Start();
		for (int i = 0; i < 10000000; ++i)
		{
			ans = funcion1.Calculate(0.5, null);
		}
		stopWatch.Stop();
		// Get the elapsed time as a TimeSpan value.
		ts = stopWatch.Elapsed;
		stopWatch.Reset();
		Debug.Log("inv abs " + ts);

		stopWatch.Start();
		for (int i = 0; i < 10000000; ++i)
		{
			ans = function2.Calculate(0.5, null);
		}
		stopWatch.Stop();
		// Get the elapsed time as a TimeSpan value.
		ts = stopWatch.Elapsed;
		stopWatch.Reset();
		Debug.Log("normal " + ts);
        */

        // DateTime.Now.ToString("HH:mm:ss tt");
        // If writeAllGenerations == true then all generations will be saved.
        // The time stamp will allow to distinguish different experiments.
        timeStamp = DateTime.Now.ToString("HHmm");

        // Initializes GUI-related classes.
        InitGUI();

        Utility.DebugLog = true;

        // We get rid of spaces in the experiment name to avoid trouble with paths
        experiment_name = experiment_name.Replace(' ', '_');
        experiment = new SimpleExperiment(this);
        XmlDocument xmlConfig = new XmlDocument();
        // Loads some experiment info.  
        // Population size, number of species, cyclic or feedforward networks, 
        // complexity regulation strategy and threshold.
        TextAsset textAsset = (TextAsset)Resources.Load("experiment.config");
        xmlConfig.LoadXml(textAsset.text);

        // Manual selection extension for NeatEvolutionAlgorithm.
        manual_ea = new NeatManualEvolution<NeatGenome>(this, fitness_in_manual);

        // Loads the experiment parameters from the xml file "experiment.config" 
        experiment.Initialize(experiment_name, xmlConfig.DocumentElement, 
                              num_inputs, num_outputs);
        champFileSavePath = Application.persistentDataPath + 
                            string.Format("/{0}.champ.xml", experiment_name);
        popFileSavePath   = Application.persistentDataPath + 
        	                string.Format("/{0}.pop.xml", experiment_name);
        // Useful reference! Delete this files for a fresh start.
        print(champFileSavePath);
        // We pass this path to guiManager (it will neeed to check if it exists
        // at different times, and the files may be deleted!)
        //guiManager.SavePath = popFileSavePath;

        // Use if you need your rays in manual selection to interact only with
        // a given set of layers. 
        // In this exammple we want to exclude layer 9 "SeenByWorker":
        for_mouse_input = (1 << LayerMask.NameToLayer("SeenByWorker"));
        // We can exclude multiple layers:
        // for_mouse_input |= (1 << 13);
        // If we omit this line raycast will only interact with the previous layers!
        for_mouse_input = ~for_mouse_input;

        // In case units are tagged as something different than "Unit":
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
        // Finally we start an evolutionary algorithm and load or create an 
        // initial population.
        // Note NeatGenomeFactory will set AddModule screen if CreateGenomeList
        // is called and there is not a saved population.
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

    /// <summary>
    /// Instantiates and initializes elements needed for the program GUI.
    /// </summary>
    void InitGUI()
    {
		// Here we start all the GUI-related variables.
        //guiManager = transform.gameObject.AddComponent<GuiManager>();
        //visualizer = transform.gameObject.AddComponent<Visualizer>();
        //guiManager.Optimizer = this;
        //guiManager.Initialize(visualizer);
        //guiManager.SetSkin(mySkin);
        //visualizer.SetGuiManager(guiManager);
        //visualizer.SetSkin(mySkin);
        //visualizer.SetNamesPath(Application.persistentDataPath + 
        //                        string.Format("/{0}.names.xml", experiment_name));



		uiManager = GetComponent<UImanager>();
        uiManager.SetNamesPath(Application.persistentDataPath + 
                               string.Format("/{0}.names.xml", experiment_name));

		uiManager.SetHierarchyPath(Application.persistentDataPath + 
			                       string.Format("/{0}.hierarchy.xml", experiment_name));
	}

    /// <summary>
    /// Calculates frames per second and reduces the time scale if they are too
    /// low. Also in Update we call the manual selection function in
    /// optimizer (the actual selection of units with mouse input) when this 
    /// mode is selected
    /// </summary>
    void Update()
    {
        // CheckFPS();

        // Allows to select units only if the interactive evolution is chosen.
        if (manual)
        {
            ManualSelection(); 
        }
    }

    /// <summary>
    /// Calculates frames per second and reduces the time step if they are too
    /// low. Currently not in use.
    /// </summary>
    /*void CheckFPS()
    {
        const int min_fps = 10;
        const float updateInterval = 12;

        // calculates frames per second
        timeLeft -= Time.deltaTime;
        accum += Time.timeScale / Time.deltaTime;
        ++frames;
        // every updateInterval fps are checked
        if (timeLeft <= 0.0)
        {
            var fps = accum / frames;
            timeLeft = updateInterval;
            accum = 0.0f;
            frames = 0;
            // if fps are too low the time scale is reduced
            if (fps < min_fps)
            {
                Time.timeScale = Time.timeScale - 1;
                print("Lowering time scale to " + Time.timeScale);
            }
        }        
    }*/

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
                genome = NeatGenomeXmlIO.ReadCompleteGenomeList(xr, false, 
                    (NeatGenomeFactory)experiment.CreateGenomeFactory())[0];
        } 
        catch (Exception e1) 
        {
            // print(champFileLoadPath + " Error loading genome from file!\nLoading aborted.\n"
            //                        + e1.Message + "\nJoe: " + champFileLoadPath);
            return genome;
        }

        return genome;
    }

    /// <summary>
    /// Updates some information for the user. This is subscribed to the UpdateEvent
    /// event used in the evolutionary algorithm.
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">E.</param>
    void ea_UpdateEvent(object sender, EventArgs e)
    {
        Utility.Log(string.Format("gen={0:N0} bestFitness={1:N6}",
                                  _ea.CurrentGeneration, 
                                  _ea.Statistics._maxFitness));
        fitness = _ea.Statistics._maxFitness;
        generation = _ea.CurrentGeneration;
        // Utility.Log(string.Format("Moving average: {0}, N: {1}", 
                                   // _ea.Statistics._bestFitnessMA.Mean, 
        	                       // _ea.Statistics._bestFitnessMA.Length));

        SaveResearch(Application.persistentDataPath);
    }

    /// <summary>
    /// Pause the evolutionary process and save the current genomes. This is 
    /// subscribed to the PauseEvent event used in the evolutionary algorithm.
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">E.</param>
    void ea_PauseEvent(object sender, EventArgs e)
    {
        //Time.timeScale = 1;      
        SavePopulation(Application.persistentDataPath);
        DateTime endTime = DateTime.Now;
        Utility.Log("Total time elapsed: " + (endTime - startTime));
		// Why this line??
        // System.IO.StreamReader stream = new System.IO.StreamReader(popFileSavePath);           
        EARunning = false;   
    }

    /// <summary>
    /// Checks if the directory path provided exists, and creates it if it does not.
    /// </summary>
    void IsThereDirectory(string directoryPath)
    {
        System.IO.DirectoryInfo dirInf = new System.IO.DirectoryInfo(directoryPath);
        if (!dirInf.Exists)
        {
            dirInf.Create();
        }        
    }

    /// <summary>
    /// Now EARunning is going to be true every time we call this function 
    /// because Manual starts the evolutionary process. Change eventually?
    /// </summary>
    /*void ResetTime()
    {
        if (EARunning == true)
        {
            Time.timeScale = 25;
        }
        else
        {
            Time.timeScale = 1;
        }      
    }*/
        
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
        if (hit_collider.tag == unit_tag)
        {
            // Let us highlight this units
            CreateBubble(hit_collider.gameObject);            
            // This function will mark units as selected and will send them to 
            // NeatManualEvolution
            MarkAndSend(hit_collider.gameObject.GetComponent<UnitController>()); 
        }
        else if (hit_collider.tag == "UnitChild")
        {          
            // The same, but accessing the parent GameObject
            CreateBubble(hit_collider.transform.parent.gameObject);   
            MarkAndSend(hit_collider.transform.parent.GetComponent<UnitController>());          
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

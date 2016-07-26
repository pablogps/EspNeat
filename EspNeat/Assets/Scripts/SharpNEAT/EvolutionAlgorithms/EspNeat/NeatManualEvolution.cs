using UnityEngine;
using System.Collections;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.Genomes.Neat;
using System.Collections.Generic;
using SharpNeat.Core; //Can this be more specific? --> SharpNeat.Core.Specie.cs?
using SharpNeat.Decoders;
using SharpNeat.Decoders.Neat;
using SharpNeat.Phenomes;
using SharpNeat.Utility;

/// <summary>
/// This class takes care of the manual evolution process. Instead of being a
/// compleat evolutionary class derived from AbstractGenerationalAlgorithm we
/// make it depend on the NeatEvolutionAlgorithm process. In fact, this class 
/// will only change the fitness values associated to different genomes. 
/// First they are set to zero, then the user judges the unit's behaviour 
/// and selects those that are interesting, which then take a given fitness. 
/// This modified fitness values are passed back to NeatEvolutionAlgorithm, 
/// which creates offspring accordingly.
/// </summary>
public class NeatManualEvolution<TGenome>
    where TGenome : class, IGenome<TGenome> 
    {
	// We need a reference to optimizer so we can instantiate objects!
	private EspNeatOptimizer optimizer;
    private IGenomeDecoder<TGenome,IBlackBox> decoder;
    IList<TGenome> genome_list;
    private double[] saved_fitness;
    private IBlackBox[] unit_brains;
    private bool next_generation = false;
    private bool is_aborted = false;
	private Dictionary<int, bool> selected_genomes;
	private Dictionary<IBlackBox, int> brain_index = new Dictionary<IBlackBox, int>();
	private double fitness_for_chosen = 10.0;
    private bool isReward = true;

    // These are used when the user wants to choose the champion genome (but
    // not to proceed with evolution!)
    private bool isSetChampion = false;
    // Mouse texture
    private Texture2D cursorTexture = (Texture2D)Resources.Load("Textures/NewUI/championCursor");
    private Vector2 cursorCentreOffset = new Vector2(100f, 100f);

    #region Properties

    public NeatManualEvolution(EspNeatOptimizer sourceOptimizer, double fitness)
    {
        optimizer = sourceOptimizer;
        fitness_for_chosen = fitness;
    }

    public IGenomeDecoder<TGenome,IBlackBox> Decoder 
	{
		set { decoder = value; }
	}

    public bool NextGeneration
    {
        set { next_generation = value; }
    }

    public bool IsAborted
    {
        get { return is_aborted; }
        set { is_aborted = value; }
	}

    public bool IsReward
    {
        set { isReward = value; }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// This function coordinates the manual selection process
    /// </summary>
    /// <param name="genome_list">Genome list.</param>
	public void ManualSelection(IList<TGenome> given_genome_list)
    {
        genome_list = given_genome_list;

        // Resets the abort trigger!
        is_aborted = false;

		// First we copy the fitness values in case we want to abort, 
        // then we set fitness to 0
		saved_fitness = CopyAndClear();
        // Here we will save the brains (which we will need to kill our units)
        if (unit_brains == null)
        {
            unit_brains = new IBlackBox[genome_list.Count];            
        }
        // Then the user can choose the units that display a more interesting 
        // behaviour. These will be marked to change their fitness value.
        // The new fitness values will be used to determine the next generation's population.
        // Resets the list for selected genomes.
        selected_genomes = new Dictionary<int, bool>();
        // Creates the units...
        WakeUp();
        // Now we need to wait while the useer chooses different units and we 
        // let optimizer that this process can start.
        optimizer.ManualWait = false;
        // We call this function from coroutiner since it is an IEnumerator
		Coroutiner.StartCoroutine(ManualEvaluation());
        // Then units are killed and NeatEvolutionAlgorithm continues 
        // creating the offspring!
        // Kill is called at the end of ManualEvaluation
    }

    /// <summary>
    /// In manual selection we sometimes want to select an interesting individual
    /// as champion, but NOT to continue evolution. This option allows to do
    /// this (and affects GetChosenPhenome)
    /// </summary>
    public void ActivateSetChampion()
    {
        isSetChampion = true;
        Cursor.SetCursor(cursorTexture, cursorCentreOffset, CursorMode.Auto);
    }

	/// <summary>
	/// The user returns the brain (phenome) of a selected unit. We need to find
    /// the index of the corresponding genome and add it to our list 
    /// (the fitness of this genome will be modified at the end of the process)
	/// </summary>
	/// <param name="unit_brain">Unit brain.</param>
	public void GetChosenPhenome(IBlackBox unit_brain)
	{
		// TODO: Prepare for exception if the brain is not found in the dictionary!
        int genome_idx = brain_index[unit_brain]; 
        // UnityEngine.Debug.Log("chosen brain " + genome_idx);

        //If this genome is not already included
        if (!selected_genomes.ContainsKey(genome_idx))
        {
            selected_genomes.Add(genome_idx, isReward);          
        }  
	}

    /// <summary>
    /// The user may need to deselect some units!
    /// </summary>
    /// <param name="unit_brain">Unit brain.</param>
    public void DeselectPhenome(IBlackBox unit_brain)
    {
        // TODO: Prepare for exception if the brain is not found in the dictionary!
        int genome_idx = brain_index[unit_brain];
        // UnityEngine.Debug.Log("dechosen brain " + genome_idx);

        //See if it was already there
        if (selected_genomes.ContainsKey(genome_idx))
        {           
            selected_genomes.Remove(genome_idx);      
        }
        else
        {
            // TODO: Create exception, if it needs to be deselected it SHOULD
            // be found!!
        }           
    }

    /// <summary>
    /// Gets the champion from optimizer, and updates this information in
    /// Factory.
    /// </summary>
    public void SelectChampion(IBlackBox unit_brain)
    {
        // TODO: Prepare for exception if the brain is not found in the dictionary!
        int genome_idx = brain_index[unit_brain];

        // Perhaps surprisingly this script does not have access to
        // NeatEvolutionAlgorithm, so we will use Otimizer to update the
        // champion!
        uint champId = genome_list[genome_idx].Id;
        optimizer.UpdateChampion(champId);

        // We have the champion, so we can deactivate the search.
        DeactivateSetChampion();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// We already have a champion, or the user changed her mind and decided
    /// to cancel evolution or create a new generation.
    /// </summary>
    void DeactivateSetChampion()
    {
        isSetChampion = false;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        optimizer.IsSetChampion = false;
    }

    /// <summary>
    /// Creates a copy of the fitness values in case manual selection is aborted
    /// before creating any offspring.
	/// Also resets the original genome's fitness values to 1. 
    /// Since this list will not be modified, we use an array.
	/// We use fitness 1 and not 0 so unselected genomes will still have some
	/// probability of reproduction (otherwise diversity is lost too easily).
	/// "fitness_for_chosen" will determine how much more fit are the chosen 
	/// units. For example, fitness_for_chosen = 2 will make selected units
	/// two times as desirable for reproduction, a value of 3 will make them 3
	/// times as desirable and so on.
    /// </summary>
    double[] CopyAndClear() 
    {
		double[] copy = new double[genome_list.Count];
		for (int index = 0; index < genome_list.Count; ++index) 
		{
			copy[index] = genome_list[index].EvaluationInfo.Fitness;
			genome_list[index].EvaluationInfo.SetFitness(1);
		}
        return copy;
    }

	/// <summary>
	/// Decodes genomes into phenomes, which are used by optimizer to 
    /// instantiate units. Phenomes are saved (to call the unit's killing)
	/// </summary>
	/// <param name="genome_list">Genome list.</param>
    void WakeUp()
	{
        for (int index = 0; index < genome_list.Count; ++index)
        {
            // Decodes the genomes into phenomes (neural networks)
            unit_brains[index] = decoder.Decode(genome_list[index]);
            // And units are then instantiated in optimizer
            optimizer.Evaluate(unit_brains[index]);
			// And we update our brain-to-genome dictionary
			brain_index.Add(unit_brains[index], index);
        }
	}
		
	/// <summary>
	/// Creates a waiting time for the user to choose units. Informs 
    /// NeatEvolutionAlgorithm when this is completed and calls for the 
    /// destruction of the units.
	/// The actual choosing of the units with mouse clicks is done in organizer. 
    /// It would fit here best, but I could not figure it out. A way around it 
    /// would be to make this class Monobehaviour and instantiate it in the 
    /// GameObject "Evaluator" using AddComponent, and then call the selection 
    /// function in Update() (not available now) (does this make sense?)
	/// </summary>
	/// <returns>The evaluation.</returns>
	IEnumerator ManualEvaluation() 
    {
        while (next_generation == false)
        {
            yield return new WaitForSeconds(0.1f);                        
        }
        // When this is finished, we reset the waiting variable
        next_generation = false;

        // Just in case we deactivate "set champion"
        DeactivateSetChampion();

        // User evaluation is complete, now units are not needed
        Kill();
		// Now it is time to update fitness values
		if (is_aborted) 
		{
			Abort();
		}
		else
		{
			UpdateFitness();
		}
    }

	/// <summary>
	/// Once the units have been evaluated by the user they are not needed, so
	/// they are destroyed (and NeatEvolutionAlgorithm creates their offspring)
	/// </summary>
	void Kill()
	{
		for (int index = 0; index < unit_brains.Length; ++index)
		{
			optimizer.StopEvaluation(unit_brains[index]);
		}  
	}

	/// <summary>
	/// In case we need to exit the manual selection without completing the
	/// process (maybe the user changed his/her mind?)
	/// Reset fitness values:
	/// maybe change so that evolution stops completely
	/// </summary>
	void Abort()
	{
		for (int index = 0; index < genome_list.Count; ++index) 
		{
			genome_list[index].EvaluationInfo.SetFitness(saved_fitness[index]);
		}
	}

	void UpdateFitness()
	{
        foreach (KeyValuePair<int, bool> pair in selected_genomes)
        {
            if (pair.Value)
            {
                // Reward
                genome_list[pair.Key].EvaluationInfo.SetFitness(fitness_for_chosen); 
            }
            else
            {
                // Punishment!
                genome_list[pair.Key].EvaluationInfo.SetFitness(0.0); 
            } 
        }
    }

    #endregion
}

/*
// Why would this not work? (The total number of species seems way too low??
IList<Specie<NeatGenome>> specie_list = _ea.SpecieList;

Debug.Log(specie_list.Count + " " + 
    specie_list[0].GenomeList.Count + " " + 
    specie_list[1].GenomeList.Count + " " + 
    specie_list[2].GenomeList.Count + " " + 
    specie_list[3].GenomeList.Count + " " + 
    specie_list[4].GenomeList.Count + " " + 
    specie_list[5].GenomeList.Count + " " + 
    specie_list[6].GenomeList.Count + " " + 
    specie_list[7].GenomeList.Count + " " + 
    specie_list[8].GenomeList.Count + " " + 
    specie_list[9].GenomeList.Count);

Debug.Log(specie_list[0].GenomeList[0].EvaluationInfo.Fitness + " " +
    specie_list[1].GenomeList[0].EvaluationInfo.Fitness + " " +
    specie_list[2].GenomeList[0].EvaluationInfo.Fitness);
*/
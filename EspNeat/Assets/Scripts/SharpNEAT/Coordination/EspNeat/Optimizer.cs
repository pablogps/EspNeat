using UnityEngine;
using SharpNeat.Phenomes;

/// <summary>
/// This is an abstract class for the script that will coordinate all the 
/// evolutionary processes.
/// 
/// Use NeatOptimizer for Neat evolution with manual or automatic fitness 
/// evaluation.
/// 
/// Use EspOptimizer for a more complex evolutionary process with separate 
/// modules for different tasks.
/// 
/// Note: using an interface does not inherit MonoBehaviour.
/// </summary>
public abstract class Optimizer : MonoBehaviour
{
	// Evolution algorithms will need access to a few variables.

    // Number of trials per generation (to get an average fitness value).
    private int trials = 1;
    // Reference durations:
    // XOR - 10
	// CheapLabour - 80
	// Short tracks 26;
    private float trial_duration = 150;
    // Fitness goal. If reached the evolutionary process stops.
    private float stopping_fitness = 500;

    public int Trials
    {
        get { return trials; }
    }

    public float TrialDuration
    {
        get { return trial_duration; }

    }

    public float StoppingFitness
    {
        get { return stopping_fitness; }
    }

    /// <summary>
    /// NeatManualEvolution will use this to notify when manual selection can
    /// start (any ongoing automatic generation is finished)
    /// </summary>
    public abstract bool ManualWait { set; get; } 

    /// <summary>
    /// Called by SimpleEvaluator
    ///****Why is this called "Evaluate" when it instantiates?
    /// Instantiates and activates a unit. Adds its controller (box parameter)
    /// to the dictionary ControllerMap.
    /// DO NOT confuse this function with SimpleEvaluator.Evaluate.
    /// </summary>
    // TODO: Change name. Maybe "InstantiateUnit"?
	public abstract void Evaluate(IBlackBox box);

    /// <summary>
    /// Called by SimpleEvaluator and NeatManualEvolution.
    /// Destroys the unit that uses a given controller (parameter box)
    /// </summary>
	public abstract void StopEvaluation(IBlackBox box);

    /// <summary>
    /// Gets the fitness corresponding to the unit which uses the controller "box"
    /// </summary>
	public abstract float GetFitness(IBlackBox box);
}

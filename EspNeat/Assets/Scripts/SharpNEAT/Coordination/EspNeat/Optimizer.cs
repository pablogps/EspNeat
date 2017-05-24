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
    private int trials = 3;
    // Reference durations:
    // XOR - 10
	// CheapLabour - 80
	// Short tracks 26;
	// For CheapLabour, used 150
	// For Cars, used 27
    // For robot arm, used 5.2
    // For artist arm?
	private float trial_duration = 500f;
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
    /// Called by SimpleEvaluator and NeatManualEvolution.
    /// Instantiates and activates a unit. Adds its controller (box parameter)
    /// to the dictionary ControllerMap.
    /// </summary>
    public abstract void InstantiateCandidate(IBlackBox box);

	public abstract void DestroyCandidate(IBlackBox box);

    /// <summary>
    /// Gets the fitness corresponding to the unit which uses the controller "box"
    /// </summary>
	public abstract float GetFitness(IBlackBox box);
}

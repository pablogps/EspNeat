using UnityEngine;
using System.Collections;
using SharpNeat.Core;
using SharpNeat.Phenomes;
using System.Collections.Generic;

/// <summary>
/// Simple evaluator is passed to the IGenomeListEvaluator (UnityParallelListEvaluator 
/// in our case) in SimpleExperiment. 
/// The main function here is Evaluate, but this does several things:
/// it instantiates units (from Optimizer), waits while units play around during
/// the trial time and only then does it get a fitness value for the genome.
/// </summary>
public class SimpleEvaluator : IPhenomeEvaluator<IBlackBox> {
    ulong _evalCount;
    bool _stopConditionSatisfied;
    Optimizer optimizer;
    FitnessInfo fitness;

    Dictionary<IBlackBox, FitnessInfo> dict = new Dictionary<IBlackBox, FitnessInfo>();

    public ulong EvaluationCount {
        get {return _evalCount;}
    }

    public bool StopConditionSatisfied {
        get {return _stopConditionSatisfied;}
        set {_stopConditionSatisfied = value;}
    }

    // Constructor. SimpleEvaluator needs to access public functions from an optimizer object. 
    public SimpleEvaluator(Optimizer se) {
        this.optimizer = se;
        _stopConditionSatisfied = false;
    }

    public IEnumerator Evaluate(IBlackBox box) {
        if (optimizer != null) {
            optimizer.InstantiateCandidate(box);
            yield return new WaitForSeconds(optimizer.TrialDuration);
            optimizer.DestroyCandidate(box);

            float fit = optimizer.GetFitness(box);
               
            FitnessInfo fitness = new FitnessInfo(fit, fit);
            dict.Add(box, fitness);           
        }
    }

    public void Reset() {
        this.fitness = FitnessInfo.Zero;
        dict = new Dictionary<IBlackBox, FitnessInfo>();
    }

    public FitnessInfo GetLastFitness() {        
        return this.fitness;
    }

    public FitnessInfo GetLastFitness(IBlackBox phenome) {
        if (dict.ContainsKey(phenome)) {
            FitnessInfo fit = dict[phenome];
            dict.Remove(phenome); // why?          
            return fit;
        }        
        return FitnessInfo.Zero;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpNeat.Core;
using System.Collections;
using UnityEngine;

namespace SharpNEAT.Core
{
    /// <summary>
    /// Evolution algorithms need evaluators in order to assign fitness values
    /// to the genomes (except in manual mode, where the user will take this
    /// role).
    /// evaluateList (why lower case?) instantiates a unit for every genome, 
    /// then waits some time and finally gets a fitness value for each. This is 
    /// needed for automatic evaluation of the genomes, and is performed by
    /// the IPhenomeEvaluator's function "Evaluate".
    /// </summary>
    class UnityParallelListEvaluator<TGenome, TPhenome> : IGenomeListEvaluator<TGenome>
        where TGenome : class, IGenome<TGenome>
        where TPhenome : class
    {
        // Returns a phenome (unit controller) from a genome
		readonly IGenomeDecoder<TGenome, TPhenome> _genomeDecoder;
        // Instantiates units with a phenome and assigns fitness to a genome.
        IPhenomeEvaluator<TPhenome> _phenomeEvaluator;
        //readonly IPhenomeEvaluator<TPhenome> _phenomeEvaluator;
        // This is to access information about trial length and stopping fitness.
        Optimizer _optimizer;

        #region Constructor

        /// <summary>
        /// Construct with the provided IGenomeDecoder and IPhenomeEvaluator.
        /// We can use several decoders depending, for instance, on whether 
        /// loops are allowed in the networks (feedforward or cyclic).
        /// PhenomeEvaluator will likely be SimpleEvaluator.
        /// </summary>
        public UnityParallelListEvaluator(IGenomeDecoder<TGenome, TPhenome> genomeDecoder,
                                          IPhenomeEvaluator<TPhenome> phenomeEvaluator,
                                          Optimizer opt)
        {
            _genomeDecoder = genomeDecoder;
            _phenomeEvaluator = phenomeEvaluator;
            _optimizer = opt;
        }

        #endregion

        public ulong EvaluationCount
        {
            get { return _phenomeEvaluator.EvaluationCount; }
        }

        public bool StopConditionSatisfied
        {
            get { return _phenomeEvaluator.StopConditionSatisfied; }
        }

        /*
		public IGenomeDecoder<TGenome, TPhenome> GenomeDecoder
		{
			get { return _genomeDecoder; }
		}
        */

        public IEnumerator Evaluate(IList<TGenome> genomeList)
        {
            // As a coroutine so we have good control of real-time waiting periods.
            yield return Coroutiner.StartCoroutine(evaluateList(genomeList));
        }

        /// <summary>
        /// This is the main function in this class. It translates every genome
        /// (neural network description) into a phenome (functional description
        /// of the genome which gets input values and returns output values). 
        /// Then it instantiates units with this phenomes as controllers and 
        /// allows them a time to do whatever they must. Finally fitness values
        /// are assigned for each genome and units are destroyed.
        /// </summary>
        private IEnumerator evaluateList(IList<TGenome> genomeList)
        {
            // We do not want to lose track of which phenome corresponds to each
            // genome!
            Dictionary<TGenome, TPhenome> dict = new Dictionary<TGenome, TPhenome>();
            // If there is more than one trial we need to write down the fitness
            // value obtained by units in each case.
            Dictionary<TGenome, FitnessInfo[]> fitnessDict = 
                    new Dictionary<TGenome, FitnessInfo[]>();

            // Units can be tested in several trials to get an average evaluation
            for (int i = 0; i < _optimizer.Trials; i++)
            {
                Utility.Log("Iteration " + (i + 1));                
                _phenomeEvaluator.Reset();
                dict = new Dictionary<TGenome, TPhenome>();
                foreach (TGenome genome in genomeList)
                {          
                    // Makes a phenome from each genome. In some cases non-viable
                    // genomes are allowed. Those get fitness = 0.
                    TPhenome phenome = _genomeDecoder.Decode(genome);
                    if (null == phenome)
                    {   // Non-viable genome.
                        genome.EvaluationInfo.SetFitness(0.0);
                        genome.EvaluationInfo.AuxFitnessArr = null;
                    }
                    else
                    {
                        // There may be more than one trial. Every genome needs
                        // a fitness result for each. So we allocate enough space
                        // in the dictionary for this (this is why the value in
                        // the dictionary is an array). Allocation needs only 
                        // happen once, thus if (i == 0).
                        if (i == 0)
                        {
                            fitnessDict.Add(genome, new FitnessInfo[_optimizer.Trials]);
                        }
                        dict.Add(genome, phenome);
                        //if (!dict.ContainsKey(genome))
                        //{
                        //    dict.Add(genome, phenome);
                        //    fitnessDict.Add(phenome, new FitnessInfo[_optimizer.Trials]);
                        //}
                        // This is where the unit is actually instantiated and
                        // where its fitness is evaluated
                        Coroutiner.StartCoroutine(_phenomeEvaluator.Evaluate(phenome));
                    }
                }

                // The previous coroutine will wait this period of time before
                // calculating the fitness values.
                yield return new WaitForSeconds(_optimizer.TrialDuration);

                // Now we can store the fitness values.
                foreach (TGenome genome in dict.Keys)
                {
                    TPhenome phenome = dict[genome];
                    if (phenome != null)
                    {
                        FitnessInfo fitnessInfo = _phenomeEvaluator.GetLastFitness(phenome);
                        fitnessDict[genome][i] = fitnessInfo;
                    }
                }
            }
            // Every genome has now a fitness value for each trial. Here we 
            // process that information and get an average result.
            foreach (TGenome genome in dict.Keys)
            {
                TPhenome phenome = dict[genome];
                if (phenome != null)
                {
                    double fitness = 0;
                    for (int i = 0; i < _optimizer.Trials; i++)
                    {
                     
                        fitness += fitnessDict[genome][i]._fitness;
                       
                    }
                    var fit = fitness;
                    fitness /= _optimizer.Trials; // Averaged fitness
                    // Is any average fitness greated than the fitness target?
                    if (fit >= _optimizer.StoppingFitness)
                    {
                      //  Utility.Log("Fitness is " + fit + ", 
                      //  stopping now because stopping fitness is " + 
                      //  _optimizer.StoppingFitness);
                        _phenomeEvaluator.StopConditionSatisfied = true;
                    }
                    genome.EvaluationInfo.SetFitness(fitness);
                    genome.EvaluationInfo.AuxFitnessArr = fitnessDict[genome][0]._auxFitnessArr;
                }
            }
        }

        public void Reset()
        {
            _phenomeEvaluator.Reset();
        }
    }
}

/* ***************************************************************************
 * This file is part of SharpNEAT - Evolution of Neural Networks.
 * 
 * Copyright 2004-2006, 2009-2010 Colin Green (sharpneat@gmail.com)
 *
 * SharpNEAT is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * SharpNEAT is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with SharpNEAT.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using SharpNeat.Core;
using SharpNeat.Network;
using SharpNeat.Utility;

namespace SharpNeat.Genomes.Neat
{
    /// <summary>
    /// An IGenomeFactory for EspNeatGenomes. We use the factory as a means of 
    /// generating an initial population either randomly or using a seed genome
    /// or genomes. Subsequently all NeatGenome objects keep a reference to this
    /// factory object for convenient access to NeatGenome parameters and ID 
    /// generator objects.
    /// 
    /// We also use this factory to add modules to the system.
    /// </summary>
    public class NeatGenomeFactory : IGenomeFactory<NeatGenome>
    {
        private EspNeatOptimizer _optimizer;
        
        const int __INNOVATION_HISTORY_BUFFER_SIZE = 0x20000;
        /// <summary>NeatGenomeParameters currently in effect.</summary>
        protected NeatGenomeParameters _neatGenomeParamsCurrent;
        readonly NeatGenomeParameters _neatGenomeParamsComplexifying;
        readonly NeatGenomeParameters _neatGenomeParamsSimplifying;
        readonly NeatGenomeStats _stats = new NeatGenomeStats();
        readonly int _inputNeuronCount;
        readonly int _outputNeuronCount;
        private static int _currentModule;
        readonly UInt32IdGenerator _genomeIdGenerator;
        readonly UInt32IdGenerator _innovationIdGenerator;
        int _searchMode;

        readonly KeyedCircularBuffer<ConnectionEndpointsStruct,uint?> _addedConnectionBuffer 
                = new KeyedCircularBuffer<ConnectionEndpointsStruct,uint?>(__INNOVATION_HISTORY_BUFFER_SIZE);

        readonly KeyedCircularBuffer<uint,AddedNeuronGeneStruct> _addedNeuronBuffer 
                = new KeyedCircularBuffer<uint,AddedNeuronGeneStruct>(__INNOVATION_HISTORY_BUFFER_SIZE);

        /// <summary>Random number generator associated with this factory.</summary>
        protected readonly FastRandom _rng = new FastRandom();
        readonly ZigguratGaussianSampler _gaussianSampler = new ZigguratGaussianSampler();

        /// <summary>Activation function library associated with this factory.</summary>
        protected readonly IActivationFunctionLibrary _activationFnLibrary;

        #region Inner Class [ConnectionDefinition]

        struct ConnectionDefinition
        {
            public readonly uint _innovationId;
            public readonly uint _sourceNeuronIdx;
            public readonly uint _targetNeuronIdx;

            public ConnectionDefinition(uint innovationId, uint sourceNeuronIdx, 
                uint targetNeuronIdx)
            {
                _innovationId = innovationId;
                _sourceNeuronIdx = sourceNeuronIdx;
                _targetNeuronIdx = targetNeuronIdx;
            }
        }

        #endregion

        #region Constructors [NEAT]

        /// <summary>
        /// Constructs with default NeatGenomeParameters and ID generators 
        /// initialized to zero.
        /// </summary>
        public NeatGenomeFactory(int inputNeuronCount, int outputNeuronCount,
                                 EspNeatOptimizer optimizer)
        {
            _inputNeuronCount = inputNeuronCount;
            _outputNeuronCount = outputNeuronCount;
            _currentModule = 0;

            _neatGenomeParamsCurrent = new NeatGenomeParameters();
            _neatGenomeParamsComplexifying = _neatGenomeParamsCurrent;
            _neatGenomeParamsSimplifying = 
                    NeatGenomeParameters.CreateSimplifyingParameters(_neatGenomeParamsComplexifying);

            _genomeIdGenerator = new UInt32IdGenerator();
            _innovationIdGenerator = new UInt32IdGenerator();

            _activationFnLibrary = DefaultActivationFunctionLibrary.CreateLibraryNeat(
                    _neatGenomeParamsCurrent.NormalNeuronActivFn,
                    _neatGenomeParamsCurrent.RegulatoryActivFn,
                    _neatGenomeParamsCurrent.OutputNeuronActivFn);
            
            _optimizer = optimizer;
        }

        /// <summary>
        /// Constructs a NeatGenomeFactory with the provided NeatGenomeParameters 
        /// and ID generators initialized to zero.
        /// </summary>
        public NeatGenomeFactory(int inputNeuronCount, int outputNeuronCount,
                                 NeatGenomeParameters neatGenomeParams,
                                 EspNeatOptimizer optimizer)
        {
            _inputNeuronCount = inputNeuronCount;
            _outputNeuronCount = outputNeuronCount;
            _currentModule = 0;
            _activationFnLibrary = DefaultActivationFunctionLibrary.CreateLibraryNeat(
                    neatGenomeParams.NormalNeuronActivFn,
                    neatGenomeParams.RegulatoryActivFn,
                    neatGenomeParams.OutputNeuronActivFn);

            _neatGenomeParamsCurrent = neatGenomeParams;
            _neatGenomeParamsComplexifying = _neatGenomeParamsCurrent;
            _neatGenomeParamsSimplifying = 
                    NeatGenomeParameters.CreateSimplifyingParameters(_neatGenomeParamsComplexifying);

            _genomeIdGenerator = new UInt32IdGenerator();
            _innovationIdGenerator = new UInt32IdGenerator();

            _optimizer = optimizer;
        }

        /// <summary>
        /// Constructs a NeatGenomeFactory with the provided NeatGenomeParameters 
        /// and ID generators.
        /// </summary>
        public NeatGenomeFactory(int inputNeuronCount, int outputNeuronCount,
                                 NeatGenomeParameters neatGenomeParams,
                                 UInt32IdGenerator genomeIdGenerator,
                                 UInt32IdGenerator innovationIdGenerator,
                                 EspNeatOptimizer optimizer)
        {
            _inputNeuronCount = inputNeuronCount;
            _outputNeuronCount = outputNeuronCount;
            _currentModule = 0;
            _activationFnLibrary = DefaultActivationFunctionLibrary.CreateLibraryNeat(
                    neatGenomeParams.NormalNeuronActivFn,
                    neatGenomeParams.RegulatoryActivFn,
                    neatGenomeParams.OutputNeuronActivFn);

            _neatGenomeParamsCurrent = neatGenomeParams;
            _neatGenomeParamsComplexifying = _neatGenomeParamsCurrent;
            _neatGenomeParamsSimplifying = 
                    NeatGenomeParameters.CreateSimplifyingParameters(_neatGenomeParamsComplexifying);

            _genomeIdGenerator = genomeIdGenerator;
            _innovationIdGenerator = innovationIdGenerator;

            _optimizer = optimizer;
        }

        #endregion

        #region Constructors [CPPN/HyperNEAT]

        /// <summary>
        /// NOT READY FOR ESP
        /// Constructs with default NeatGenomeParameters, ID generators 
        /// initialized to zero and the provided IActivationFunctionLibrary.
        /// This overload required for CPPN support.
        /// </summary>
        public NeatGenomeFactory(int inputNeuronCount, int outputNeuronCount,
                                 IActivationFunctionLibrary activationFnLibrary)
        {
            _inputNeuronCount = inputNeuronCount;
            _outputNeuronCount = outputNeuronCount;
            _activationFnLibrary = activationFnLibrary;

            _neatGenomeParamsCurrent = new NeatGenomeParameters();
            _neatGenomeParamsComplexifying = _neatGenomeParamsCurrent;
            _neatGenomeParamsSimplifying = 
                    NeatGenomeParameters.CreateSimplifyingParameters(_neatGenomeParamsComplexifying);

            _genomeIdGenerator = new UInt32IdGenerator();
            _innovationIdGenerator = new UInt32IdGenerator();
        }

        /// <summary>
        /// NOT READY FOR ESP
        /// Constructs with ID generators initialized to zero and the provided
        /// IActivationFunctionLibrary and NeatGenomeParameters.
        /// This overload required for CPPN support.
        /// </summary>
        public NeatGenomeFactory(int inputNeuronCount, int outputNeuronCount, 
                                 IActivationFunctionLibrary activationFnLibrary,
                                 NeatGenomeParameters neatGenomeParams)
        {
            _inputNeuronCount = inputNeuronCount;
            _outputNeuronCount = outputNeuronCount;
            _activationFnLibrary = activationFnLibrary;

            _neatGenomeParamsCurrent = neatGenomeParams;
            _neatGenomeParamsComplexifying = _neatGenomeParamsCurrent;
            _neatGenomeParamsSimplifying = 
                    NeatGenomeParameters.CreateSimplifyingParameters(_neatGenomeParamsComplexifying);

            _genomeIdGenerator = new UInt32IdGenerator();
            _innovationIdGenerator = new UInt32IdGenerator();
        }

        /// <summary>
        /// NOT READY FOR ESP
        /// Constructs with the provided IActivationFunctionLibrary, NeatGenomeParameters and
        /// ID Generators.
        /// This overload required for CPPN support.
        /// </summary>
        public NeatGenomeFactory(int inputNeuronCount, int outputNeuronCount,
                                 IActivationFunctionLibrary activationFnLibrary,
                                 NeatGenomeParameters neatGenomeParams,
                                 UInt32IdGenerator genomeIdGenerator,
                                 UInt32IdGenerator innovationIdGenerator)
        {
            _inputNeuronCount = inputNeuronCount;
            _outputNeuronCount = outputNeuronCount;
            _activationFnLibrary = activationFnLibrary;

            _neatGenomeParamsCurrent = neatGenomeParams;
            _neatGenomeParamsComplexifying = _neatGenomeParamsCurrent;
            _neatGenomeParamsSimplifying = 
                    NeatGenomeParameters.CreateSimplifyingParameters(_neatGenomeParamsComplexifying);

            _genomeIdGenerator = genomeIdGenerator;
            _innovationIdGenerator = innovationIdGenerator;
        }

        #endregion

        #region IGenomeFactory<NeatGenome> Members

        /// <summary>
        /// Gets the factory's genome ID generator.
        /// </summary>
        public UInt32IdGenerator GenomeIdGenerator
        {
            get { return _genomeIdGenerator; }
        }

        /// <summary>
        /// Gets or sets a mode value. This is intended as a means for an 
        /// evolution algorithm to convey changes in search mode to genomes, and 
        /// because the set of modes is specific to each concrete implementation
        /// of an IEvolutionAlgorithm the mode is defined as an integer (rather 
        /// than an enum[eration]). E.g. SharpNEAT's implementation of NEAT uses
        /// an evolutionary algorithm that alternates between a complexifying 
        /// and simplifying mode, in order to do this the algorithm class needs 
        /// to notify the genomes of the current mode so that the CreateOffspring() 
        /// methods are able to generate offspring appropriately - e.g. we avoid 
        /// adding new nodes and connections and increase the rate of deletion 
        /// mutations when in simplifying mode.
        /// </summary>
        public int SearchMode 
        { 
            get { return _searchMode; }
            set 
            {
                // Store the mode and switch to a set of NeatGenomeParameters 
                // appropriate to the mode. Note. we don't reference the 
                // ComplexityRegulationMode enum directly so as not to introduce a
                // compile time dependency between this class and the 
                // NeatEvolutionaryAlgorithm - we may wish to use NeatGenome 
                // with other algorithm classes in the future.
                _searchMode = value; 
                switch(value)
                {
                    case 0: // ComplexityRegulationMode.Complexifying
                        _neatGenomeParamsCurrent = _neatGenomeParamsComplexifying;
                        break;
                    case 1: // ComplexityRegulationMode.Simplifying
                        _neatGenomeParamsCurrent = _neatGenomeParamsSimplifying;
                        break;
                    default:
                        throw new SharpNeatException("Unexpected SearchMode");
                }
            }
        }

        #endregion

        #region Properties [NeatGenome Specific]

        /// <summary>
        /// Gets or sets the current active module, which does not need to be
        /// the youngest module in the genomes (if evolving older modules
        /// is considered).
        /// </summary>
        public int CurrentModule
        {
            get { return _currentModule; }
            set { _currentModule = value; }
        }

        /// <summary>
        /// Gets the factory's NeatGenomeParameters currently in effect.
        /// </summary>
        public NeatGenomeParameters NeatGenomeParameters
        {
            get { return _neatGenomeParamsCurrent; }
        }

        /// <summary>
        /// Gets the number of input neurons expressed by the genomes related to this factory.
        /// </summary>
        public int InputNeuronCount
        {
            get { return _inputNeuronCount; }
        }

        /// <summary>
        /// Gets the number of output neurons expressed by the genomes related to this factory.
        /// </summary>
        public int OutputNeuronCount
        {
            get { return _outputNeuronCount; }
        }

        /// <summary>
        /// Gets the factory's activation function library.
        /// </summary>
        public IActivationFunctionLibrary ActivationFnLibrary
        {
            get { return _activationFnLibrary; }
        }

        /// <summary>
        /// Gets the factory's innovation ID generator.
        /// </summary>
        public UInt32IdGenerator InnovationIdGenerator
        {
            get { return _innovationIdGenerator; }
        }

        /// <summary>
        /// Gets the history buffer of added connections. Used when adding new connections to check if an
        /// identical connection has been added to a genome elsewhere in the population. This allows re-use
        /// of the same innovation ID for like connections.
        /// </summary>
        public KeyedCircularBuffer<ConnectionEndpointsStruct,uint?> AddedConnectionBuffer 
        {
            get { return _addedConnectionBuffer; }
        }

        /// <summary>
        /// Gets the history buffer of added neurons. Used when adding new neurons to check if an
        /// identical neuron has been added to a genome elsewhere in the population. This allows re-use
        /// of the same innovation ID for like neurons.
        /// </summary>
        public KeyedCircularBuffer<uint,AddedNeuronGeneStruct> AddedNeuronBuffer
        {
            get { return _addedNeuronBuffer; }
        }

        /// <summary>
        /// Gets a random number generator associated with the factory. 
        /// Note. The provided RNG is not thread safe, if concurrent use is required then sync locks
        /// are necessary or some other RNG mechanism.
        /// </summary>
        public FastRandom Rng
        {
            get { return _rng; }
        }

        /// <summary>
        /// Gets a Gaussian sampler associated with the factory. 
        /// Note. The provided RNG is not thread safe, if concurrent use is required then sync locks
        /// are necessary or some other RNG mechanism.
        /// </summary>
        public ZigguratGaussianSampler GaussianSampler
        {
            get { return _gaussianSampler; }
        }

        /// <summary>
        /// Gets some statistics assocated with the factory and NEAT genomes that it has spawned.
        /// </summary>
        public NeatGenomeStats Stats
        {
            get { return _stats; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a list of randomly initialised genomes.
        /// </summary>
        /// <param name="length">The number of genomes to create.</param>
        /// <param name="birthGeneration">The current evolution algorithm generation. 
        /// Assigned to the new genomes as their birth generation.</param>
        public List<NeatGenome> CreateGenomeList(int length, uint birthGeneration)
        {   
            List<NeatGenome> genomeList = new List<NeatGenome>(length);

            // We create the base for the first genome: bias, input and output
            // neurons, as well as ID, birthgeneration and empty connections.
             genomeList.Add(CreateGenomeBase(birthGeneration));

            // We set the number of input and output neurons in the genomes.
            genomeList[0].Input = _inputNeuronCount;
            genomeList[0].Output = _outputNeuronCount;

            // The base is the same for all genomes, so we copy the genome.
            // length - 1 because we already have 1 copy!
            for (int i = 0; i < length - 1; ++i)
            {
                genomeList.Add(new NeatGenome(genomeList[0], 
                                              _genomeIdGenerator.NextId, 
                                              birthGeneration));
            }

            // Now we add the first module. 
			_optimizer.SetMenuScreen(SharpNeat.Coordination.MenuScreens.AddModuleInit);

            return genomeList;
        }

        /// <summary>
        /// Creates a list of genomes spawned from a seed genome. Spawning uses 
        /// asexual reproduction.
        /// </summary>
        /// <param name="length">The number of genomes to create.</param>
        /// <param name="birthGeneration">The current evolution algorithm generation. 
        /// Assigned to the new genomes as their birth generation.</param>
        /// <param name="seedGenome">The seed genome to spawn new genomes from.</param>
        public List<NeatGenome> CreateGenomeList(int length, uint birthGeneration,
                                                 NeatGenome seedGenome)
        {   
            Debug.Assert(this == seedGenome.GenomeFactory, 
                         "seedGenome is from a different genome factory.");

            List<NeatGenome> genomeList = new List<NeatGenome>(length);
            
            // Add an exact copy of the seed to the list.
            NeatGenome newGenome = CreateGenomeCopy(seedGenome, 
                                                    _genomeIdGenerator.NextId, 
                                                    birthGeneration);
            genomeList.Add(newGenome);

            // For the remainder we create mutated offspring from the seed.
            for(int i=1; i<length; i++) {
                genomeList.Add(seedGenome.CreateOffspring(birthGeneration));
            }
            return genomeList;
        }

        /// <summary>
        /// Creates a list of genomes spawned from a list of seed genomes. 
        /// Spawning uses asexual reproduction and typically we would simply 
        /// repeatedly loop over (and spawn from) the seed genomes until we have
        /// the required number of spawned genomes.
        /// </summary>
        /// <param name="length">The number of genomes to create.</param>
        /// <param name="birthGeneration">The current evolution algorithm generation. 
        /// Assigned to the new genomes as their birth generation.</param>
        /// <param name="seedGenomeList">A list of seed genomes from which to 
        /// spawn new genomes from.</param>
        public List<NeatGenome> CreateGenomeList(int length, uint birthGeneration,
                                                 List<NeatGenome> seedGenomeList)
        {   
            if (seedGenomeList.Count == 0) {
                throw new SharpNeatException("CreateGenomeList() requires at" + 
                                             "least on seed genome in seedGenomeList.");
            }

            // Create a copy of the list so that we can shuffle the items 
            // without modifying the original list.
            seedGenomeList = new List<NeatGenome>(seedGenomeList);
            Utilities.Shuffle(seedGenomeList, _rng);

            // Make exact copies of seed genomes and insert them into our new genome list.
            List<NeatGenome> genomeList = new List<NeatGenome>(length);
            int idx=0;
            int seedCount = seedGenomeList.Count;
            for(int seedIdx=0; idx<length && seedIdx<seedCount; idx++, seedIdx++)
            {
                // Add an exact copy of the seed to the list.
                NeatGenome newGenome = CreateGenomeCopy(seedGenomeList[seedIdx],
                                                        _genomeIdGenerator.NextId,
                                                        birthGeneration);
                genomeList.Add(newGenome);
            }

            // Keep spawning offspring from seed genomes until we have the 
            // required number of genomes.
            for(; idx<length;) {
                for(int seedIdx=0; idx<length && seedIdx<seedCount; idx++, seedIdx++) {
                    genomeList.Add(seedGenomeList[seedIdx].CreateOffspring(birthGeneration));
                }
            }
            return genomeList;
        }

        /// <summary>
        /// Prepares the genome list for a new module: Saves the current
        /// population, gets the champion and clones it for all genomes.
        /// Once they are all the same, we can make the new module.
        /// </summary>
        public void AddNewModule(IList<NeatGenome> genomeList, string genericFilePath,
                                 string experimentName,
                                 SharpNeat.Coordination.GuiVariables guiVar)
        {
			SavePopulation(genomeList, genericFilePath, experimentName);
			NeatGenome champion = _optimizer.EvolutionAlgorithm.CurrentChampGenome;
			UpdateChampionsProtectedWeights(champion, guiVar);
			CloneChampion(genomeList, champion);
			NewModule(genomeList, guiVar);
            _optimizer.ResetGUI();
        }

        /// <summary>
        /// This resets the active module: deletes every hidden neuron and
        /// non-protected connection and re-populates connections.
        /// </summary>
        public void ResetActiveModule(IList<NeatGenome> genomeList, uint generation)
        {
            // First we need any genome.
            NeatGenome champion = _optimizer.EvolutionAlgorithm.CurrentChampGenome;

            // This is probably not necessary
            _currentModule = champion.ConnectionGeneList[champion.ConnectionGeneList.Count - 1].ModuleId;

            List<uint> localInputId;
            List<uint> localOutputId;
            EmptyModuleInGenome(champion, out localInputId, out localOutputId);

            CloneChampion(genomeList, champion);

            // Gets the highest Id (the previous highest has been likely deleted)
            uint lastId = champion.FindLastId() + 1;
            _innovationIdGenerator.Reset(lastId);

            foreach (NeatGenome genome in genomeList)
            {
                genome.BirthGeneration = generation;
                PopulateModule(genome, localInputId, localOutputId);
            }
            _optimizer.ResetGUI();
        }

        /// <summary>
        /// Here we can change the protected weights of an existing population.
        /// </summary>
        public void ChangeWeights(IList<NeatGenome> genomeList,
                                  SharpNeat.Coordination.GuiVariables guiVar)
        {
            foreach (NeatGenome genome in genomeList)
            {
                UpdateChampionsProtectedWeights(genome, guiVar);
            }
        }

        /// <summary>
        /// Here we can update pandemonium values.
        /// </summary>
        public void UpdatePandem(SharpNeat.Coordination.GuiVariables guiVar)
        {
            // This method will already loop through the population of genomes.
            UpdatePandemoniums(guiVar.Pandemonium);
        }

        /// <summary>
        /// Updates the input used by a regulatory neuron.
        /// </summary>
        public void UpdateInToReg(IList<NeatGenome> genomeList,
                                  SharpNeat.Coordination.GuiVariables guiVar,
                                  int moduleThatCalled)
        {
            // We find first the Id of the regulatory neuron (the following
            // values are reserved for the bias/input-to-regulatory connections).
            // Also finds the first index used in the connections.
            // These two are common values for all our genomes, so we do it before
            // updating each.
            int regIndex = 0;
            uint firstId = 0;
            int firstIndex = 0;

            // Returns true if found... which should be always, really.
            if (genomeList[0].NeuronGeneList.FindRegulatory(moduleThatCalled, out regIndex))
            {
                firstId = genomeList[0].NeuronGeneList[regIndex].Id + 1;
                firstIndex = genomeList[0].ConnectionGeneList.FindFirstInModule(moduleThatCalled);
                foreach (NeatGenome genome in genomeList)
                {
                    DoUpdateInToReg(genome, guiVar, moduleThatCalled, firstId, firstIndex);
                }   
            }

            // Makes sure the auxiliary connection (bias-to-regulatory, used
            // in the active module when no input has been selected for the
            // regulatory neuron) is switched off if the caller is the active
            // module.
            if (moduleThatCalled == _currentModule)
            {
                foreach (NeatGenome genome in genomeList)
                {
                    genome.ConnectionGeneList[0].TargetNodeId = 0;
                }                
            }
        }

        /// <summary>
        /// DELETE THIS FUNCTION
        /// It has been substitued by CreateGenomeBase + NewModule/MakeModule
        /// </summary>
        public NeatGenome CreateGenome(uint birthGeneration)
        {   
            // DELETE THIS FUNCTION
            return CreateGenome(0, 0, new NeuronGeneList(), new ConnectionGeneList(),
                false);
        }

        /// <summary>
        /// Supports debug/integrity checks. Checks that a given genome object's
        /// type is consistent with the genome factory. Typically the wrong type
        /// of object may occur where factorys are subtyped and not all of the
        /// relevant virtual methods are overriden. Returns true if OK.
        /// </summary>
        public virtual bool CheckGenomeType(NeatGenome genome)
        {
            return genome.GetType() == typeof(NeatGenome);
        }

        #endregion

        #region Public Methods [NeatGenome Specific]

        /// <summary>
        /// Convenient method for obtaining the next genome ID.
        /// </summary>
        public uint NextGenomeId()
        {
            return _genomeIdGenerator.NextId;
        }

        /// <summary>
        /// Convenient method for obtaining the next innovation ID.
        /// </summary>
        public uint NextInnovationId()
        {
            return _innovationIdGenerator.NextId;
        }

        /// <summary>
        /// Convenient method for generating a new random connection weight that
        /// conforms to the connection weight range defined by the NeatGenomeParameters.
        /// </summary>
        public double GenerateRandomConnectionWeight()
        {
            return ((_rng.NextDouble()*2.0) - 1.0) * _neatGenomeParamsCurrent.ConnectionWeightRange;
        }

        /// <summary>
        /// Gets a variable from the Gaussian distribution with the provided mean and standard deviation.
        /// </summary>
        public double SampleGaussianDistribution(double mu, double sigma)
        {
            return _gaussianSampler.NextSample(mu, sigma);
        }

        /// <summary>
        /// Create a genome with the provided internal state/definition data/objects.
        /// Overridable method to allow alternative NeatGenome sub-classes to be used.
        /// </summary>
        public virtual NeatGenome CreateGenome(uint id, uint birthGeneration,
                                               NeuronGeneList neuronGeneList, 
                                               ConnectionGeneList connectionGeneList, 
                                               bool rebuildNeuronGeneConnectionInfo)
        {
            return new NeatGenome(this, id, birthGeneration, neuronGeneList, 
                                  connectionGeneList, rebuildNeuronGeneConnectionInfo);
        }

        /// <summary>
        /// Create a copy of an existing NeatGenome, substituting in the 
        /// specified ID and birth generation. Overridable method to allow 
        /// alternative NeatGenome sub-classes to be used.
        /// </summary>
        public virtual NeatGenome CreateGenomeCopy(NeatGenome copyFrom, uint id,
                                                   uint birthGeneration)
        {
            return new NeatGenome(copyFrom, id, birthGeneration);
        }

        /// <summary>
        /// Overridable method to allow alternative NeuronGene sub-classes to be used.
        /// </summary>
        public virtual NeuronGene CreateNeuronGene(uint innovationId, NodeType neuronType, 
            int module, int pandemonium)
        {
            // EspNEAT uses three different activation functions for different
            // types of neurons.
            if (neuronType == NodeType.Regulatory)
            {
                return new NeuronGene(innovationId, neuronType, 1, module, pandemonium);
            }
            else if (neuronType == NodeType.Output)
            {
                return new NeuronGene(innovationId, neuronType, 2, module, pandemonium);
            }
            else
            {
                return new NeuronGene(innovationId, neuronType, 0, module, pandemonium); 
            }
        }

        /// <summary>
        /// Overridable method to allow alternative NeuronGene sub-classes to be used.
        /// Used from mutations to create hidden neurons (which are never 
        /// regulatory, so we know their pandemonium = -1) in the active module.
        /// </summary>
        public virtual NeuronGene CreateNeuronGene(uint innovationId, NodeType neuronType)
        {
            // EspNEAT uses three different activation functions for different
            // types of neurons.
            if (neuronType == NodeType.Regulatory)
            {
                return new NeuronGene(innovationId, neuronType, 1, _currentModule, -1);
            }
            else if (neuronType == NodeType.Output)
            {
                return new NeuronGene(innovationId, neuronType, 2, _currentModule, -1);
            }
            else
            {
                return new NeuronGene(innovationId, neuronType, 0, _currentModule, -1);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Creates the genome base: bias, input and output neurons, as well 
        /// as an empty connection gene list.
        /// Note: Neurons must be arranged according to the following layout:
        ///   Bias - single neuron. Innovation ID = 0
        ///   Input neurons
        ///   Output neurons
        ///   Regulatory neurons
        ///   Modules:
        ///       Local output neurons
        ///       Hidden neurons
        /// Create a single bias neuron.
        /// </summary>
        /// <param name="birthGeneration">The current evolution algorithm generation. 
        /// Assigned to the new genome as its birth generation.</param>
        NeatGenome CreateGenomeBase(uint birthGeneration)
        {
            // Allocates space for bias too. In any case the list will certainly
            // grow when we add modules, so this is not relevant.
            NeuronGeneList neuronGeneList = new NeuronGeneList(_inputNeuronCount + 
                                                               _outputNeuronCount + 1);

            // We need to reset this variables, in case we have deleted the
            // network and we are creating a new base.
            ResetGenomeStatistics();

            uint biasNeuronId = _innovationIdGenerator.NextId;
            NeuronGene neuronGene = CreateNeuronGene(biasNeuronId, NodeType.Bias, 
                                                     _currentModule, -1);
            neuronGeneList.Add(neuronGene);

            // Creates input neuron genes.
            for (int i = 0; i < _inputNeuronCount; i++)
            {
                neuronGene = CreateNeuronGene(_innovationIdGenerator.NextId, 
                                              NodeType.Input, _currentModule, -1);
                neuronGeneList.Add(neuronGene);
            }

            // Creates output neuron genes. 
            for (int i = 0; i < _outputNeuronCount; i++)
            {
                neuronGene = CreateNeuronGene(_innovationIdGenerator.NextId, 
                                              NodeType.Output, _currentModule, -1);
                neuronGeneList.Add(neuronGene);
            }    

            // Updates information about the size of the base. This is 
            // a static variable, we only need this once.
            neuronGeneList.LastBase = neuronGeneList.Count - 1;

            // We need a connection list for the genome. Before adding any 
            // modules this list will only contain an auxiliary connection
            // that will be used to keep active new evolving modules if we 
            // wish to regulate them with modules that will be later produced
            // (instead of, for example, with some input neuron).
            // This auxiliary connection will always have bias as source, and
            // forms a trivial loop with bias when it is not needed.
            // We give it maximum weight and module 0.
            ConnectionGeneList connectionGeneList = new ConnectionGeneList();
            connectionGeneList.Add(new ConnectionGene(
                    _innovationIdGenerator.NextId,  0, 0,
                    _neatGenomeParamsCurrent.ConnectionWeightRange, 0, true));

            return CreateGenome(_genomeIdGenerator.NextId, birthGeneration,
                                neuronGeneList, connectionGeneList,
                                false);
        }

        /// <summary>
        /// If the user deletes the genomes and this factory is called again, 
        /// we need to reset some values.
        /// </summary>
        void ResetGenomeStatistics()
        {
            _innovationIdGenerator.Reset(0);
            _currentModule = 0;
            NeatGenome.ResetStatistics();
        }

        /// <summary>
        /// Saves the population of genomes in an specific folder.
        /// </summary>
        void SavePopulation(IList<NeatGenome> genomeList, string genericFilePath,
                            string experimentName)
        {
            // We need to provide a new directory path, adding /Module* where
            // * is the youngest module number.
            int module = genomeList[0].FindYoungestModule();
            string newFolder = genericFilePath + "/Module" + module.ToString();

            // We then need to provide paths for the population and champion.
            string champFileSavePath = newFolder + string.Format("/{0}.champ.xml",
                                                                 experimentName);
            string popFileSavePath   = newFolder + string.Format("/{0}.pop.xml",
                                                                 experimentName);
            _optimizer.SavePopulation(newFolder, popFileSavePath, champFileSavePath);
        }

        /// <summary>
        /// Overwrites every genome in a genomeList with the provided champion.
        /// </summary>
        void CloneChampion(IList<NeatGenome> genomeList, NeatGenome champion)
        {
            // We are going to overwrite each genome with champion. Champion
            // is itself a genome from the list, but overwriting it with itself
            // will not lose information (otherwise we should create a new
            // genome and copy champion there.
            // We get a complaint if we use a foreach loop, so basic for instead.
            for (int i = 0; i < genomeList.Count; ++i)
            {
                // We want to preserve the Ids and birth generations!
                genomeList[i] = new NeatGenome(champion, genomeList[i].Id, 
                                               genomeList[i].BirthGeneration);
            }
            // Updates the reference to the champion (for example, in case we
            // try to add a new module without evolving the previous one, in
            // which case the current champion is an old network without the
            // last module!)
            // All are clones, so any genome is good.
            _optimizer.EvolutionAlgorithm.CurrentChampGenome = genomeList[0];
        }

        /// <summary>
        /// This method will delete every non-protected connection and every
        /// hidden neuron in the module. Local input and local output neurons
        /// are also added to a list.
        /// </summary>
        void EmptyModuleInGenome(NeatGenome genome, out List<uint> localInputId, out List<uint> localOutputId)
        {
            _currentModule = genome.ConnectionGeneList[genome.ConnectionGeneList.Count - 1].ModuleId;

            localInputId = new List<uint>();
            localOutputId = new List<uint>();
            // First, neurons:
            NeuronGeneList geneList = genome.NeuronGeneList;
            for (int i = geneList.Count - 1; i > -1; --i)
            {
                if (geneList[i].ModuleId != _currentModule ||
                    geneList[i].NodeType == NodeType.Regulatory)
                {
                    break;
                }
                else
                {
                    if (geneList[i].NodeType == NodeType.Hidden)
                    {
                        geneList.RemoveAt(i);
                    }
                    else if (geneList[i].NodeType == NodeType.Local_Output)
                    {
                        localOutputId.Add(geneList[i].Id);
                    }
                    else if (geneList[i].NodeType == NodeType.Local_Input)
                    {
                        localInputId.Add(geneList[i].Id);
                    }                    
                }
            }

            // Connections:
            for (int i = genome.ConnectionGeneList.Count - 1; i > -1; --i)
            {
                if (!genome.ConnectionGeneList[i].Protected)
                {
                    genome.ConnectionGeneList.RemoveAt(i);
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Updates the protected weights in the champion genome. This is in
        /// case the user wants to change protected weights in old modules.
        /// For instance, this could be used to reactivate a connection that
        /// was temporarily switched-off to evolve another module.
        /// </summary>
        void UpdateChampionsProtectedWeights(NeatGenome champion,
                                             SharpNeat.Coordination.GuiVariables guiVar)
        {
            // Module 0 is empty (the only protected connection there is not in
            // guiVar lists).
            // The last module is processed when the new module is created.
            // The last module is given, for example, by the size of the
            // local input list. Perhaps this is a bit unsafe.
            for (int i = 1; i < guiVar.LocalInputList.Count - 1; ++i)
            {
                // Connections weights from Bias/Input neurons to local input
                // neurons do not play any role at all and may be ignored here.
                foreach (SharpNeat.Coordination.newLink connection in guiVar.LocalOutputList[i])
                {
                    UpdateOldProtected(champion, connection);
                }
                foreach (SharpNeat.Coordination.newLink connection in guiVar.RegulatoryInputList[i])
                {
                    UpdateOldProtected(champion, connection);
                }
            }
        }

        /// <summary>
        /// Updates the weight of a connection in a genome.
        /// </summary>
        void UpdateOldProtected(NeatGenome champion,
                                SharpNeat.Coordination.newLink connection)
        {
            int index = champion.ConnectionGeneList.IndexFormId(connection.id);
            if (index > 0)
            {
                champion.ConnectionGeneList[index].Weight = connection.weight;
            }
        }

        /// <summary>
        /// Updates the input used by a regulatory neuron in an old module.
        /// This can be used, for example, to use bias for regulation during
        /// evolution (so the new module is always active).
        /// </summary>
        void DoUpdateInToReg(NeatGenome genome,
                           SharpNeat.Coordination.GuiVariables guiVar,
                           int moduleThatCalled, uint firstId, int firstIndex)
        {
            List<SharpNeat.Coordination.newLink> inToRegList =
                    guiVar.RegulatoryInputList[moduleThatCalled];

            // The easiest approach is to simply delete all in-to-reg connections
            // and create them again. This way we do not have to compare old
            // connections with the list, and we do not have to worry about
            // indices.

            // firstId is the Id of the regulatory neuron + 1, and corresponds
            // to the Id for the first "in-to-reg" connection.
            uint lastPossibleId = firstId + (uint)genome.Input;
            // There can be up to (bias + input) connections to delete.
            for (int i = 0; i <= genome.Input; ++i)
            {
                if (genome.ConnectionGeneList[firstIndex].InnovationId <=
                    lastPossibleId)
                {
                    // This Id MUST belong to an in-to-reg connection, so it
                    // is deleted. After this is removed, the next one will
                    // also take index = firstIndex!
                    genome.ConnectionGeneList.RemoveAt(firstIndex);
                }
                else
                {
                    // There are no more connections that should be deleted.
                    break;
                }
            }

            // Now we create the connections given in the list.
            foreach (SharpNeat.Coordination.newLink link in inToRegList)
            {
                // Id = firstId + link.otherNeuron (remember bias = 0)
                // Source node = link.otherNeuron
                // Target neuron = firstId - 1
                // Weight = link.weight
                // Module = moduleThatCalled
                // Protected = true
                ConnectionGene newConnection;
                newConnection = new ConnectionGene(
                        firstId + link.otherNeuron, link.otherNeuron,
                        firstId - 1, link.weight, moduleThatCalled, true);
                genome.ConnectionGeneList.Insert(firstIndex, newConnection);
                ++firstIndex;
            }
        }

        /// <summary>
        /// Adds a new module to a list of identical genomes (usually the 
        /// champion of a previous evolutionary process).
        /// First it collects information about the network and about the future
        /// module (pandemonium status, number and target of local outputs)
        /// then calls MakeModule to actually expand every genome with a unique
        /// version of the new module.
        /// Uses Unity GUI from the EspNeatOptimizer script.
        /// </summary>
        void NewModule(IList<NeatGenome> genomeList, 
                       SharpNeat.Coordination.GuiVariables guiVar)
        {
            // Before we start, we need to know 
            //     -Last ID used
            //     -Number of global output (_outputNeuronCount)
            //     -Number of modules
            // Gets the highest module used (we explicitly search for it in case
            // we allow evolving older modules, which does not guarantee
            // the _currentModule value is what we need).
            _currentModule = genomeList[0].FindYoungestModule() + 1;

            // Gets the last innovation ID used (so we do not repeat used values!)
            // We add 1 because we do not want to use the last ID again.
            uint lastId = genomeList[0].FindLastId() + 1;

            // Increases the count of regulatory neurons in the genomes:
            ++genomeList[0].Regulatory;
            // Sets the number of local input neurons in the new module:
            genomeList[0].LocalIn = guiVar.LocalInputList[_currentModule].Count;
            // Sets the number of local output neurons in the new module:
            genomeList[0].LocalOut = guiVar.LocalOutputList[_currentModule].Count;

            // Calculates the number of local output and hidden neurons in 
            // encapsulated modules (that is, the active module is not included).
            // Since the active module is empty yet, we count all neurons, 
            // except those in the base!
            // We substract an extra 1 because LastBase is the last index, which
            // includes 0, while inHiddenModules and Count count items (starting
            // with 1, not with 0).
            genomeList[0].InHiddenModules = genomeList[0].NeuronGeneList.Count -
                                            genomeList[0].NeuronGeneList.LastBase - 1;

			// We are going to increase the genome base by 1 regulatory neuron.
			++genomeList[0].NeuronGeneList.LastBase;

            // This has almost no cost and is needed in case we reset the evolution.
			genomeList[0].NeuronGeneList.LocateFirstIndex();
			genomeList[0].ConnectionGeneList.LocateFirstId();

            foreach (NeatGenome genome in genomeList)
            {           
                _innovationIdGenerator.Reset(lastId);
                MakeModule(genome, guiVar);
			}

            // Changes the pandemonium value for the regulatory neurons in the 
            // dictionary. Including the one we just created!
            // This dictionary is created by the user in the
            // AddModule interface.
            UpdatePandemoniums(guiVar.Pandemonium);

            // Before we return we update some accounting variables in 
            // the neuron and connection lists. These are static, so we may 
            // update them only once, from any genome.
			genomeList[0].NeuronGeneList.LocateFirstIndex();
            genomeList[0].ConnectionGeneList.LocateFirstId();
        }

        /// <summary>
        /// In the AddModule interface (see GuiManager) the user may select
        /// several regulatory neurons to update their pandemonium field (that
        /// is, to add or remove them from a pandemonium). Here we go through 
        /// the dictionary with this information and update the network.
        /// </summary>
        void UpdatePandemoniums(Dictionary<int, int> pandemonium)
        {
            foreach (KeyValuePair<int, int> entry in pandemonium)
            {
                // We need the index of the regulatory neuron belonging 
                // to the module given by entry.Key.
                int idx;
                if (_optimizer.EvolutionAlgorithm.GenomeList[0].NeuronGeneList.FindRegulatory(
                            entry.Key, out idx))
                {
                    // We have to update this neuron in every genome of the population!
                    foreach (NeatGenome genome in _optimizer.EvolutionAlgorithm.GenomeList)
                    {
                        genome.NeuronGeneList[idx].Pandemonium = entry.Value;
                    }                    
                }
            }
        }

        /// <summary>
        /// Takes a genome and expands the network with a new module, randomly
        /// initialized.
        /// 
        /// A random set of connections are made form the input to the local_output 
        /// neurons. The number of connections is based on the 
        /// NeatGenomeParameters.InitialInterconnectionsProportion,
        /// which specifies the proportion of all posssible input-output 
        /// connections to be made in initial genomes.
        /// 
        /// The connections that are made are allocated innovation IDs in a 
        /// consistent manner across the initial population of genomes. To do 
        /// this we allocate IDs sequentially to all possible interconnections 
        /// and then randomly select some proportion of connections for inclusion 
        /// in the genome. In addition, the innovation ID generator must be 
        /// reset to a correct value prior to each call to MakeModule. This value
        /// is the highest ID yet, which is not zero, so we cannot enforce here
        /// this has been done.
        /// 
        /// The consistent allocation of innovation IDs ensures that equivalent 
        /// connections in different genomes have the same innovation ID, and 
        /// although this isn't strictly necessary it is required for sexual 
        /// reproduction to work effectively - because structures are detected by 
        /// comparing innovation IDs only.
        /// </summary>
        void MakeModule(NeatGenome genome, SharpNeat.Coordination.GuiVariables guiVar)
        {
            // Resets the number of non-protected connections in the new module.
            // Note this is NOT a static variable! This is why we do it in this
            // function, for each genome.
            genome.ActiveConnections = 0;

            // Reserves the first IDs for bias/input-to-regulatory connections,
            // so it is easy to add or remove these connections later (here we
            // get the Id value to reset the generator).
            // +2 takes the regulatory neuron and the bias neuron into account.
            uint afterRegulatory = _innovationIdGenerator.Peek + (uint)genome.Input + 2;

            // The regulatory neuron will always take the first Id value of the
            // module. After that, the possible bias-to-regulatory would take
            // the next (which is reserved), for input1-to-regulatory we reserve
            // the next Id, and so on. Connections from bias/input to regulatory
            // will take always the same Id values (so we can add or remove these
            // easily). After these connections we have local input connections.
            MakeRegulatory(genome, guiVar.RegulatoryInputList[_currentModule]);

            // Reserves the first IDs for bias/input-to-regulatory connections,
            // (using the value we got a moment ago).
            _innovationIdGenerator.Reset(afterRegulatory);

            // Creates local_input neurons (and provisionally write down their
            // innovationId values) and their connections, marked as "protected".
            // We also count how many come from local output (this is used to
            // spare a predictable amount of IDs in the next lines).
            int fromLocalOut = 0;
            List<uint> localInputId = MakeLocalInput(genome, guiVar,
                                                     out fromLocalOut);

            // Reserves some IDs so there can be up to one local input per
            // global input + bias in the network. Doing this we can guarantee
            // that local input will always have lower IDs than local outputs, 
            // even if we edit old modules.
            // We add 1 to account for the bias neuron.
			// Although this feels very wasteful, we reserve 5*2 more for
			// local_output-to-local_input connections and other future
            // eventualities.
            uint extraIn = (uint)(genome.Input + 1 -
                                  (localInputId.Count - fromLocalOut));
            extraIn += 5;
			// *2 so we have space for neurons and their protected connection.
            _innovationIdGenerator.Reset(_innovationIdGenerator.Peek +  extraIn * 2);

            // Here we create local_output neurons (and provisionally write down their
            // innovationId values) and their connections, marked as "protected".
            List<uint> localOutputId = MakeLocalOutput(genome, guiVar);

            // We reserve some IDs so local output (and input) neurons and protected
            // connections will have the lowest IDs in the module even if we
            // add more local output neurons in the future.
            const uint extraLocal = 8;
            // *2 so we have space for neurons and their protected connection.
            _innovationIdGenerator.Reset(_innovationIdGenerator.Peek +
                                         extraLocal * 2);

            // Would likely work fine without this update, but better safe than
            // sorry, specially if we implement features like evolving older
            // modules, which will make the list not sorted!
            genome.NeuronGeneList.LocateFirstIndex();
            // Adds hidden connections.
            PopulateModule(genome, localInputId, localOutputId);
        }

        /// <summary>
        /// Creates the regulatory neuron in the new module.
        /// newLink strcut is defined in GuiManager script.
        /// </summary>
        void MakeRegulatory(NeatGenome genome,
                            List<SharpNeat.Coordination.newLink> regulatoryInputList)
        {
            uint regulatoryId = _innovationIdGenerator.NextId;

            // Adds the regulatory neuron. Remember these are writen after
            // the local output neurons, before the module neurons.
            // Last base index has already been increased, so it gives the 
            // correct list-index where we should insert the new neuron.
            // We assing pandemonium value = 0, in UpdatePandemoniums we will
            // give it the correct value.
            genome.NeuronGeneList.Insert(genome.NeuronGeneList.LastBase, 
                                         CreateNeuronGene(regulatoryId, 
                                                          NodeType.Regulatory, 
                                                          _currentModule, 0));

            // If there are no input connections for the regulatory neuron, 
            // we use the auxiliary connection (connecting with bias).
            // This will be overwritten when a new module is created.
            if (regulatoryInputList.Count == 0)
            {
                genome.ConnectionGeneList[0].TargetNodeId = regulatoryId;          
            }
            else
            {
                // Resets the auxiliary connection to a trivial loop with bias.
                genome.ConnectionGeneList[0].TargetNodeId = 0;

                // GetNeuronByIdAll is probably faster for regulatory neurons, 
                // because they come up early in the list.
                NeuronGene regNeuron = 
                        genome.NeuronGeneList.GetNeuronByIdAll(regulatoryId);

                foreach (SharpNeat.Coordination.newLink element in regulatoryInputList)
                {
                    // Creates the new connection. Here we can only be adding
                    // connections from bias or input (local_out-to-regulatory
                    // are part of the outgoing connections from the given 
                    // local output neuron). Remember bias and input have the
                    // same index and Id.
                    // Adds 1 so bias-to-reg does not have the same Id as the
                    // regulatory neuron!
                    uint connectionId = regulatoryId + element.otherNeuron + 1;
                    genome.AddConnection(new ConnectionGene(
                            connectionId, element.otherNeuron, regulatoryId,
                            element.weight, _currentModule, true)); 

                    // Adds the regulatory as the target for the used input.
                    // Bias and input neurons have the same index and Id.
                    genome.NeuronGeneList[(int)element.otherNeuron].TargetNeurons.Add(regulatoryId);

                    // Adds the input as a source for the regulatory
                    regNeuron.SourceNeurons.Add(element.otherNeuron);
                }
            }
        }

        /// <summary>
        /// Here we create local_input neurons (and provisionally write down
        /// their innovationId values) and their connections, marked as 
        /// "protected".
        /// Notice input will be sources for local inputs, whereas
        /// local outputs are sources for outputs (and regulatory).
        /// </summary>
        List<uint> MakeLocalInput(NeatGenome genome, 
                                  SharpNeat.Coordination.GuiVariables guiVar,
                                  out int fromLocalOut)
        {
            fromLocalOut = 0;
            List<uint> localInputId = new List<uint>();
            for (int k = 0; k < guiVar.LocalInputList[_currentModule].Count; ++k)
			{
				// If the source is within input + bias we add a new in-to-local_in
                // connection and the local in node.
                if (guiVar.LocalInputList[_currentModule][k].otherNeuron < genome.Input + 1)
				{
                    AddNormalLocalIn(genome, guiVar, k, localInputId);
				}
                else
                {
                    // This is a local_out-to-local_in case. We need to create
                    // the new local in node and rewire the old
                    // local_out-to-target connection.
                    AddLocalOutToLocalIn(genome, guiVar, k, localInputId);
                    ++fromLocalOut;
                }
             
            }
            return localInputId;
        }

        /// <summary>
        /// Adds one input to local input connection as well as the new local in
        /// node.
        /// </summary>
        void AddNormalLocalIn(NeatGenome genome, SharpNeat.Coordination.GuiVariables guiVar,
                              int k, List<uint> localInputId)
        {
            // Peek gets the next ID to be used, but does not advance the counter.
            // The next ID will be used in the local_input neuron.
            genome.AddConnection(new ConnectionGene(_innovationIdGenerator.NextId,
                                                    guiVar.LocalInputList[_currentModule][k].otherNeuron,
                                                    _innovationIdGenerator.Peek,
                                                    guiVar.LocalInputList[_currentModule][k].weight,
                                                    _currentModule, true));  

            localInputId.Add(_innovationIdGenerator.Peek);
            genome.AddNeuron(CreateNeuronGene(_innovationIdGenerator.NextId, 
                                              NodeType.Local_Input, 
                                              _currentModule, -1)); 

            // Register connection with endpoint neurons.
            NeuronGeneList neuronList = genome.NeuronGeneList;
            // The source of the connection is given by LocalInputList[_currentModule][k].otherNeuron.
            // We need to find its index in neuronList:
            NeuronGene sourceNeuron = 
                    neuronList.GetNeuronById(guiVar.LocalInputList[_currentModule][k].otherNeuron);
            

			// The new target for this neuron is the local input neuron we 
            // have just created (and that has used the last ID!)
            // We add the target neuron to the list of targets of this neuron:
            sourceNeuron.TargetNeurons.Add(_innovationIdGenerator.Peek - 1);

            // The target neuron in the new connection is the last in the list.
            // The new source we need to add to this neuron is given by  
            // LocalInputList[_currentModule][k].otherNeuron.
            neuronList[neuronList.Count - 1].SourceNeurons.Add(guiVar.LocalInputList[_currentModule][k].otherNeuron);  
		}

        /// <summary>
        /// This is a local_out-to-local_in case. We need to create the new
        /// local in node and rewire the old local_out-to-target connection.
        /// </summary>
        void AddLocalOutToLocalIn(NeatGenome genome, SharpNeat.Coordination.GuiVariables guiVar,
                              int k, List<uint> localInputId)
        {
            // Finds the connection we need to rewire. This connection is in the
            // local out list of the module of the local out source neuron.
            NeuronGene source = genome.NeuronGeneList.GetNeuronByIdAll(guiVar.LocalInputList[_currentModule][k].otherNeuron);
            int sourceModule = source.ModuleId;
            ConnectionGene connection = genome.ConnectionGeneList.FindProtectedWithSource(source.Id);

            // Changes the target list in the local out neuron.
            source.TargetNeurons.Remove(connection.TargetNodeId);
            source.TargetNeurons.Add(_innovationIdGenerator.Peek);

            // Rewires the connection.
            connection.TargetNodeId = _innovationIdGenerator.Peek;

            // The connection weight should NOT be updated. Note the connection
            // already existed, so this weight has been already updated before
            // clonning the champion to get the common part for the genome
            // population. If for some other reason we wanted to do it, here
            // is the line:
            // connection.Weight = guiVar.LocalInputList[_currentModule][k].weight;

            // Creates the new local in neuron and adds it to the list.
            localInputId.Add(_innovationIdGenerator.Peek);
            genome.AddNeuron(CreateNeuronGene(_innovationIdGenerator.NextId, 
                                              NodeType.Local_Input, 
                                              _currentModule, -1)); 
            
            // Updates sources list.

            // The target neuron in the new connection is the last in the list.
            // The new source we need to add to this neuron is given by  
            // LocalInputList[_currentModule][k].otherNeuron.
            NeuronGeneList neuronList = genome.NeuronGeneList;
            neuronList[neuronList.Count - 1].SourceNeurons.Add(guiVar.LocalInputList[_currentModule][k].otherNeuron);
        }

        /// <summary>
        /// Here we create local_output neurons (and provisionally write down
        /// their innovationId values) and their connections, marked as 
        /// "protected".
        /// Notice input will be sources for local inputs, whereas
        /// local outputs are sources for outputs (and regulatory).
        /// </summary>
        List<uint> MakeLocalOutput(NeatGenome genome, 
                                   SharpNeat.Coordination.GuiVariables guiVar)
        {
            NeuronGeneList neuronList = genome.NeuronGeneList;

            List<uint> localOutputId = new List<uint>();
            for (int k = 0; k < guiVar.LocalOutputList[_currentModule].Count; ++k)
            {
                // Peek gets the next ID to be used, but does not advance the counter.
                // The next ID will be used in the local_output neuron.
                genome.AddConnection(new ConnectionGene(_innovationIdGenerator.NextId,
                                                        _innovationIdGenerator.Peek,
                                                        guiVar.LocalOutputList[_currentModule][k].otherNeuron,
                                                        guiVar.LocalOutputList[_currentModule][k].weight,
                                                        _currentModule, true));  

                localOutputId.Add(_innovationIdGenerator.Peek);
                genome.AddNeuron(CreateNeuronGene(_innovationIdGenerator.NextId, 
                                                  NodeType.Local_Output, 
                                                  _currentModule, -1)); 

                // Register connection with endpoint neurons.

                // The source of the connection (local_output) is the last in 
                // NeuronGeneList! The new target for this neuron is LocalOutputList[_currentModule][k].otherNeuron.
                neuronList[neuronList.Count - 1].TargetNeurons.Add(guiVar.LocalOutputList[_currentModule][k].otherNeuron);

                // We need to look for the index for the target neuron
                // with ID LocalOutputList[_currentModule][k].otherNeuron.
                // We cannot use GetNeuronById because the statistics are not
                // updated during the creation of a new module!
                NeuronGene targetNeuron = 
                    neuronList.GetNeuronByIdAll(guiVar.LocalOutputList[_currentModule][k].otherNeuron);
                // The new neuron source we need to add to the source list is
                // the last ID we used: _innovationIdGenerator.Peek - 1
                targetNeuron.SourceNeurons.Add(_innovationIdGenerator.Peek - 1);                
            }
            return localOutputId;            
        }

        /// <summary>
        /// Populates the new module with a unique set of hidden connections.
        /// </summary>
        void PopulateModule(NeatGenome genome, List<uint> localInputId,
                            List<uint> localOutputId)
        {
            // Defines all possible connections between the local_input and 
            // local_output neurons (fully interconnected).
            ConnectionDefinition[] connectionDefArr = 
                    new ConnectionDefinition[localInputId.Count * 
                                             localOutputId.Count];
            int possibleConnection = 0;
            for (int input = 0; input < localInputId.Count; ++input)
            {
                // For local output we need a list with their innovation values
                for (int output = 0; output < localOutputId.Count; ++output)
                {
                    connectionDefArr[possibleConnection] = 
                            new ConnectionDefinition(_innovationIdGenerator.NextId, 
                                                     localInputId[input], 
                                                     localOutputId[output]);
                    ++possibleConnection;
                }
            }

            // Shuffles the array of possible connections.
            Utilities.Shuffle(connectionDefArr, _rng);

            // Selects connection definitions from the head of the list and 
            // converts them to real connections. 

            // The number of connections is a given proportion of all the possible
            // connections, but we need at least one.
            int connectionCount = (int)Utilities.ProbabilisticRound(
                    (double)connectionDefArr.Length * 
                    _neatGenomeParamsComplexifying.InitialInterconnectionsProportion,
                    _rng);
            connectionCount = Math.Max(1, connectionCount);

            // Finally populates the new module with its hidden connections.
            genome.ActiveConnections += connectionCount;

            for (int i = 0; i < connectionCount; i++)
            {
                ConnectionDefinition def = connectionDefArr[i];
                genome.AddConnection(new ConnectionGene(def._innovationId,
                                                        def._sourceNeuronIdx,
                                                        def._targetNeuronIdx,
                                                        GenerateRandomConnectionWeight(),
                                                        _currentModule));
                // Register connection with endpoint neurons.
                // First we get the NeuronGeneList from the genome:
                NeuronGeneList neuronList = genome.NeuronGeneList;

                // We need to look for the source neuron (local input).
                NeuronGene sourceNeuron = neuronList.GetNeuronById(def._sourceNeuronIdx);
                // Its targetId is def._targetNeuronIdx.
                sourceNeuron.TargetNeurons.Add(def._targetNeuronIdx);

                // We need to look for the target neuron.
                NeuronGene targetNeuron = neuronList.GetNeuronById(def._targetNeuronIdx);
                // Its sourceId is def._sourceNeuronIdx.
                targetNeuron.SourceNeurons.Add(def._targetNeuronIdx); 
            }

            // Ensure connections are sorted (this will only affect the 
            // non-protected connections in the new module!)
            ConnectionGeneList connectionList = genome.ConnectionGeneList;
            connectionList.SortByInnovationId();  
        }

        #endregion
    }
}
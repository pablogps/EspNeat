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
using SharpNeat.Coordination;

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

        private bool usingAuxArguments;
        
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

            // Checks if any of the activation functions accepts auxiliary
            // arguments.
            usingAuxArguments = false;
            if (_neatGenomeParamsCurrent.NormalNeuronActivFn.AcceptsAuxArgs ||
                _neatGenomeParamsCurrent.RegulatoryActivFn.AcceptsAuxArgs ||
                _neatGenomeParamsCurrent.OutputNeuronActivFn.AcceptsAuxArgs)
            {
                usingAuxArguments = true;
            }

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

            // Checks if any of the activation functions accepts auxiliary
            // arguments.
            usingAuxArguments = false;
            if (_neatGenomeParamsCurrent.NormalNeuronActivFn.AcceptsAuxArgs ||
                _neatGenomeParamsCurrent.RegulatoryActivFn.AcceptsAuxArgs ||
                _neatGenomeParamsCurrent.OutputNeuronActivFn.AcceptsAuxArgs)
            {
                usingAuxArguments = true;
            }
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

            // Checks if any of the activation functions accepts auxiliary
            // arguments.
            usingAuxArguments = false;
            if (_neatGenomeParamsCurrent.NormalNeuronActivFn.AcceptsAuxArgs ||
                _neatGenomeParamsCurrent.RegulatoryActivFn.AcceptsAuxArgs ||
                _neatGenomeParamsCurrent.OutputNeuronActivFn.AcceptsAuxArgs)
            {
                usingAuxArguments = true;
            }

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
            // NOT READY TO USE! Until then uxingAuxArguments will receive 
            // a safe value
            usingAuxArguments = true;

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
            // NOT READY TO USE! Until then uxingAuxArguments will receive 
            // a safe value
            usingAuxArguments = true;

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
            // NOT READY TO USE! Until then uxingAuxArguments will receive 
            // a safe value
            usingAuxArguments = true;

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
                                 string experimentName, UIvariables uiVar)
        {
            SavePopulation(genomeList, genericFilePath, experimentName);
			NeatGenome champion = _optimizer.EvolutionAlgorithm.CurrentChampGenome;
			UpdateChampionsProtectedWeights(champion, uiVar);
			CloneChampion(genomeList, champion);
			NewModule(genomeList, uiVar);

            _optimizer.ResetGUI();
        }

		public void InitializeGeneratorAfterLoad(IList<NeatGenome> genomeList)
		{
            uint lastId = 0;
            uint maxLastId = 0;
            foreach (NeatGenome genome in genomeList)
            {
                lastId = genome.FindLastId();
                if (maxLastId < lastId)
                {
                    maxLastId = lastId;
                }
            }
            _innovationIdGenerator.Reset(maxLastId + 1);
		}


        /// <summary>
        /// Adds a module to a regulation module. Before calling this method,
        /// we ensured that the regulation module is now the active module 
        /// (if it was not so before!)
        /// </summary>
        public void AddModuleToRegModule(IList<NeatGenome> genomeList,
                                         string genericFilePath,
                                         string experimentName, UIvariables uiVar,
                                         newLink newLocalOut)
        {
            // Gets the current champion and (just in case) updates protected
            // connections.
            NeatGenome champion = _optimizer.EvolutionAlgorithm.CurrentChampGenome;
            UpdateChampionsProtectedWeights(champion, uiVar);

            // Gets the index for the last local output in the active module
            // (so we know where to insert the new neuron!)
            int localOutIndex;
            champion.NeuronGeneList.FindLastLocalOut(_currentModule, out localOutIndex);

            // Checks if the ID for the last local output neuron is the same as
            // the one in newLocalOut (in which case, this is the first addition
            // and the method is a bit different).
            // Remember newLocalOut.id is the Id for the connection, which is 
            // the Id before the local output neuron.
            bool isNewOut = false;
            if (champion.NeuronGeneList[localOutIndex].Id == newLocalOut.id + 1)
            {
                isNewOut = true;
            }

            foreach (NeatGenome genome in genomeList)
            {
                if (isNewOut)
                {
                    // Same ID: we need to update the current local output values,
                    // but we need to do this for all genomes in the population
                    UpdateLocalOut(genome, newLocalOut, localOutIndex);
                }
                else
                {
                    // Different ID: we need to create a new local output neuron
                    // and connection
                    AddLocalOut(genome, newLocalOut, localOutIndex);

                    // Makes the new internal connections
                    AddConnectToNewLOut(genome, localOutIndex);
                }
            }
            _optimizer.ResetGUI();
        }

        /// <summary>
        /// Makes whichModule the active module (takes it to the end of the genome)
        /// Afterwards mutates genomes so there is some diversity in the population
        /// </summary>
        public void SetModuleActive(IList<NeatGenome> genomeList, string genericFilePath,
                                    string experimentName, UIvariables uiVar, int whichModule)
        {
            SavePopulation(genomeList, genericFilePath, experimentName);
            NeatGenome champion = _optimizer.EvolutionAlgorithm.CurrentChampGenome;
            // This is probably not needed
            UpdateChampionsProtectedWeights(champion, uiVar);

            // Gets a second copy of the champion
            // Notice we use the copy constructor, so we get clones of the
            // elements rather than references!
            NeatGenome champion2 = new NeatGenome(champion, champion.Id,
                                                  champion.BirthGeneration);

            // Deletes the old module from one copy:
            // FROM DeleteModule:
            // Checks if any of the activation functions accepts auxiliary
            // arguments and exits until this is accepted by this method.
            if (usingAuxArguments)
            {
                UnityEngine.Debug.Log("DeleteModule does not work yet with " +
                                      "activation functions that accept" +
                                      "\nauxiliary values. Fix this in " +
                                      "NeatGenomeFactory");
                return;
            }
            champion.ConnectionGeneList.RemoveAll(x => x.ModuleId == whichModule);
            champion.NeuronGeneList.RemoveAll(x => x.ModuleId == whichModule);

            // Copies old module (from the second copy) at the end of the genome
            // (of the first copy).
            CopyModuleFromGenomeToGenome(champion2, champion, whichModule);

            // Statistics works only with static variables, so we can use 
            // "champion", the only genome that is up to date
            UpdateStatistics(champion);
            // Updates _activeConnections count in the genomes (before mutations
            // this should be the same for all!)
            champion.ActiveConnectionsFromLoad();
            int activeConnections = champion.ActiveConnections;
            foreach (NeatGenome genome in genomeList)
            {
                genome.ActiveConnections = activeConnections;
            }

            // Clones the champion. This is done AFTER updating statistics,
            // because cloning prompts an integrity check that will fail with
            // out-of-date statistics.
            CloneChampion(genomeList, champion);

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
        /// Asks the genome factory to remove a given module from all genomes. Note
        /// that this option may only be called from a module that is not currently
        /// beeing evolved! In that case the option "reset" is offered instead.
        /// 
        /// NOTICE! This method is not currently compatible with activation
        /// funtions that work with auxiliary state parameters (simply because
        /// it does not check if the deleted neurons use these activation
        /// functions). This should be easy to fix if desired.
        /// </summary>
        public void DeleteModule(IList<NeatGenome> genomeList, uint generation,
                                 int whichModule)
        {
            // Checks if any of the activation functions accepts auxiliary
            // arguments and exits until this is accepted by this method.
            if (usingAuxArguments)
            {
                UnityEngine.Debug.Log("DeleteModule does not work yet with " +
                                      "activation functions that accept" +
                                      "\nauxiliary values. Fix this in " +
                                      "NeatGenomeFactory");
                return;
            }

            foreach (NeatGenome genome in genomeList)
            {
                // Removes all elements for which the module property equals
                // the target module.
                genome.ConnectionGeneList.RemoveAll(x => x.ModuleId == whichModule);
                genome.NeuronGeneList.RemoveAll(x => x.ModuleId == whichModule);
            }

            // There is one less regulatory neuron in the genomes (static property)
            --genomeList[0].Regulatory;

            // --genome.Regulatory is not included in the method so everything 
            // that is done in UpdateStatistics can be called after any other
            // process (after adding a module or changing the active module, etc)
            UpdateStatistics(genomeList[0]);
            _optimizer.ResetGUI();
        }

        /// <summary>
        /// Here we can change the protected weights of an existing population.
        /// </summary>
        public void ChangeWeights(IList<NeatGenome> genomeList, UIvariables uiVar)
        {
            foreach (NeatGenome genome in genomeList)
            {
                UpdateChampionsProtectedWeights(genome, uiVar);
            }
        }

        /// <summary>
        /// Here we can update pandemonium values.
        /// </summary>
        public void UpdatePandem(UIvariables uiVar)
        {
            // This method will already loop through the population of genomes.
            UpdatePandemoniums(uiVar.pandemonium);
        }

        /// <summary>
        /// Updates the input used by regulatory neurons.
        /// </summary>
        public void UpdateInToReg(IList<NeatGenome> genomeList, UIvariables uiVar)
        {
            // We find first the Id of the regulatory neuron (the following
            // values are reserved for the bias/input-to-regulatory connections).
            // Also finds the first index used in the connections.
            // These two are common values for all our genomes, so we do it before
            // updating each.
            int regIndex = 0;
            uint firstId = 0;
            int firstIndex = 0;

            // Goes through all modules
            for (int i = 0; i < uiVar.moduleIdList.Count; ++i)
            {
                // i counts modules, but their Id values do not need to be in order!
                int modId = uiVar.moduleIdList[i];
                // Returns true if found... which should be always, really.
                if (genomeList[0].NeuronGeneList.FindRegulatory(modId, out regIndex))
                {
                    firstId = genomeList[0].NeuronGeneList[regIndex].Id + 1;
                    firstIndex = genomeList[0].ConnectionGeneList.FindFirstInModule(modId);
                    foreach (NeatGenome genome in genomeList)
                    {
                        DoUpdateInToReg(genome, uiVar, modId, firstId, firstIndex);
                    }   
                }
            }

            /*
            // This connection is no longer in use.

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
            */
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

        /// <summary>
        /// Updates the statistics after deleting a module. This only affects
        /// static properties, so it is fine to only update it for one genome.
        /// </summary>
        public void UpdateStatistics(NeatGenome genome)
        {
            // Count again the number of neurons in hidden modules
            genome.InHiddenModulesFromLoad();

            genome.LocalIn = genome.NeuronGeneList.CountLocalIn();
            genome.LocalOut = genome.NeuronGeneList.CountLocalOut();

            // The size of the base for the neuron list has changed (-1) and
            // so has the index for the first element in the active module
            genome.NeuronGeneList.LocateLastBase(); // or use --
            genome.NeuronGeneList.LocateFirstIndex();

            genome.ConnectionGeneList.LocateFirstId();  
        }

        #endregion

        #region Private Methods

        // Allows to find a module in a genome and copy it at the end of another.
        // Used (at the moment) to set an old module as active (remember the
        // active module is always the last)
        void CopyModuleFromGenomeToGenome(NeatGenome sourceGenome,
                                          NeatGenome targetGenome, int whichModule)
        {
            // Copies the regulatory neuron
            NeuronGeneList sourceNeuronList = sourceGenome.NeuronGeneList;
            NeuronGeneList targetNeuronList = targetGenome.NeuronGeneList;
            // Backwards from the last regulatory neuron!
            for (int i = sourceNeuronList.LastBase; i >= 0; --i)
            {
                // The first instance of the module should ALWAYS be the 
                // regulatory neuron, but it does not hurt to check
                if (sourceNeuronList[i].ModuleId == whichModule &&
                    sourceNeuronList[i].NodeType == NodeType.Regulatory)
                {
                    // Copied after the last regulatory neuron
                    targetNeuronList.Insert(targetNeuronList.LastBase,
                                            sourceNeuronList[i]);
                }
            }

            // Now gets the rest of the elements
            for (int i = sourceNeuronList.LastBase + 1; i < sourceNeuronList.Count; ++i)
            {
                if (sourceNeuronList[i].ModuleId == whichModule)
                {
                    targetNeuronList.Add(sourceNeuronList[i]);
                }                
            }

            // Copies all connections:
            ConnectionGeneList sourceConnectionList = sourceGenome.ConnectionGeneList;
            ConnectionGeneList targetConnectionList = targetGenome.ConnectionGeneList;
            for (int i = 0; i < sourceConnectionList.Count; ++i)
            {
                if (sourceConnectionList[i].ModuleId == whichModule)
                {
                    targetConnectionList.Add(sourceConnectionList[i]);
                }                
            }
        }

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
            // genome and copy champion there).
            // We get a complaint if we use a foreach loop, so basic "for" instead.
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
        /// hidden neuron in the active module. Local input and local output neurons
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
        /// Updates the protected weights in the given genome. This is in
        /// case the user wants to change protected weights in old modules.
        /// For instance, this could be used to reactivate a connection that
        /// was temporarily switched-off to evolve another module.
        /// </summary>
        void UpdateChampionsProtectedWeights(NeatGenome champion, UIvariables uiVar)
        {
            // Goes through all modules:
            for (int i = 0; i < uiVar.moduleIdList.Count; ++i)
            {
                int modId = uiVar.moduleIdList[i];
                // Connections weights from Bias/Input neurons to local input
                // neurons do not play any role at all and may be ignored here.
                foreach (newLink connection in uiVar.localOutputList[modId])
                {
                    UpdateOldProtected(champion, connection);
                }
                foreach (newLink connection in uiVar.regulatoryInputList[modId])
                {
                    UpdateOldProtected(champion, connection);
                }
            }
        }

        /// <summary>
        /// Updates the weight of a connection in a genome.
        /// </summary>
        void UpdateOldProtected(NeatGenome champion, newLink connection)
        {
            int index = champion.ConnectionGeneList.IndexForId(connection.id);
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
        void DoUpdateInToReg(NeatGenome genome, UIvariables uiVar,
                           int moduleThatCalled, uint firstId, int firstIndex)
        {
            List<newLink> inToRegList = uiVar.regulatoryInputList[moduleThatCalled];

            // The easiest approach is to simply delete all in-to-reg connections
            // and create them again. This way we do not have to compare old
            // connections with the list, and we do not have to worry about
            // indices.

            // Notice that local-out-to-regulatory connections are treated as
            // normal local output connections, so they are avoided in this
            // method.

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
            foreach (newLink link in inToRegList)
            {
                // Here we are only interested in connections from input or bias
                if (link.otherNeuron <= genome.Input)
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
        void NewModule(IList<NeatGenome> genomeList, UIvariables uiVar)
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
            genomeList[0].LocalIn = uiVar.localInputList[_currentModule].Count;
            // Sets the number of local output neurons in the new module:
            genomeList[0].LocalOut = uiVar.localOutputList[_currentModule].Count;

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
                MakeModule(genome, uiVar);
			}

            // Changes the pandemonium value for the regulatory neurons in the 
            // dictionary. Including the one we just created!
            // This dictionary is created by the user in the
            // AddModule interface.
            UpdatePandemoniums(uiVar.pandemonium);

            // Before we return we update some accounting variables in 
            // the neuron and connection lists. These are static, so we may 
            // update them only once, from any genome.
			genomeList[0].NeuronGeneList.LocateFirstIndex();
            genomeList[0].ConnectionGeneList.LocateFirstId();
        }

        /// <summary>
        /// The pandemonium group to which modules belong may change.
        /// Here we go through the dictionary with this information and
        /// update the network.
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
        void MakeModule(NeatGenome genome, UIvariables uiVar)
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
            MakeRegulatory(genome, uiVar.regulatoryInputList[_currentModule]);

            // Reserves the first IDs for bias/input-to-regulatory connections,
            // (using the value we got a moment ago).
            _innovationIdGenerator.Reset(afterRegulatory);

            // Creates local_input neurons (and provisionally write down their
            // innovationId values) and their connections, marked as "protected".
            // We also count how many come from local output (this is used to
            // spare a predictable amount of IDs in the next lines).
            int fromLocalOut = 0;
            List<uint> localInputId = MakeLocalInput(genome, uiVar, out fromLocalOut);

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
            List<uint> localOutputId = MakeLocalOutput(genome, uiVar);

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
        void MakeRegulatory(NeatGenome genome, List<newLink> regulatoryInputList)
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

                foreach (newLink element in regulatoryInputList)
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
        List<uint> MakeLocalInput(NeatGenome genome, UIvariables uiVar,
                                  out int fromLocalOut)
        {
            fromLocalOut = 0;
            List<uint> localInputId = new List<uint>();
            for (int k = 0; k < uiVar.localInputList[_currentModule].Count; ++k)
			{
				// If the source is within input + bias we add a new in-to-local_in
                // connection and the local in node.
                if (uiVar.localInputList[_currentModule][k].otherNeuron < genome.Input + 1)
				{
                    AddNormalLocalIn(genome, uiVar, k, localInputId);
				}
                else
                {
                    // This is a local_out-to-local_in case. We need to create
                    // the new local in node and rewire the old
                    // local_out-to-target connection.
                    AddLocalOutToLocalIn(genome, uiVar, k, localInputId);
                    ++fromLocalOut;
                }
             
            }
            return localInputId;
        }

        /// <summary>
        /// Adds one input to local-input connection as well as the new local-in
        /// node.
        /// </summary>
        void AddNormalLocalIn(NeatGenome genome, UIvariables uiVar, int k,
                              List<uint> localInputId)
        {
            // Peek gets the next ID to be used, but does not advance the counter.
            // The next ID will be used in the local_input neuron.
            genome.AddConnection(new ConnectionGene(_innovationIdGenerator.NextId,
                                                    uiVar.localInputList[_currentModule][k].otherNeuron,
                                                    _innovationIdGenerator.Peek,
                                                    uiVar.localInputList[_currentModule][k].weight,
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
                    neuronList.GetNeuronByIdAll(uiVar.localInputList[_currentModule][k].otherNeuron);
            

			// The new target for this neuron is the local input neuron we 
            // have just created (and that has used the last ID!)
            // We add the target neuron to the list of targets of this neuron:
            sourceNeuron.TargetNeurons.Add(_innovationIdGenerator.Peek - 1);

            // The target neuron in the new connection is the last in the list.
            // The new source we need to add to this neuron is given by  
            // LocalInputList[_currentModule][k].otherNeuron.
            neuronList[neuronList.Count - 1].SourceNeurons.Add(uiVar.localInputList[_currentModule][k].otherNeuron);  
		}

        /// <summary>
        /// This is a local_out-to-local_in case. We need to create the new
        /// local in node and rewire the old local_out-to-target connection.
        /// </summary>
        void AddLocalOutToLocalIn(NeatGenome genome, UIvariables uiVar,
                                  int k, List<uint> localInputId)
        {
            // Finds the connection we need to rewire. This connection is in the
            // local-out list of the module of the local-out source-neuron.
            NeuronGene source = genome.NeuronGeneList.GetNeuronByIdAll(
                    uiVar.localInputList[_currentModule][k].otherNeuron);
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
            // connection.Weight = uiVar.localInputList[_currentModule][k].weight;

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
            neuronList[neuronList.Count - 1].SourceNeurons.Add(uiVar.localInputList[_currentModule][k].otherNeuron);
        }

        /// <summary>
        /// Here we create local_output neurons (and provisionally write down
        /// their innovationId values) and their connections, marked as 
        /// "protected".
        /// Notice input will be sources for local inputs, whereas
        /// local outputs are sources for outputs (and regulatory).
        /// </summary>
        List<uint> MakeLocalOutput(NeatGenome genome, UIvariables uiVar)
        {
            NeuronGeneList neuronList = genome.NeuronGeneList;

            List<uint> localOutputId = new List<uint>();
            for (int k = 0; k < uiVar.localOutputList[_currentModule].Count; ++k)
            {
                // Peek gets the next ID to be used, but does not advance the counter.
                // The next ID will be used in the local_output neuron.
                genome.AddConnection(new ConnectionGene(_innovationIdGenerator.NextId,
                                                        _innovationIdGenerator.Peek,
                                                        uiVar.localOutputList[_currentModule][k].otherNeuron,
                                                        uiVar.localOutputList[_currentModule][k].weight,
                                                        _currentModule, true));  

                localOutputId.Add(_innovationIdGenerator.Peek);
                genome.AddNeuron(CreateNeuronGene(_innovationIdGenerator.NextId, 
                                                  NodeType.Local_Output, 
                                                  _currentModule, -1)); 

                // Register connection with endpoint neurons.

                // The source of the connection (local_output) is the last in 
                // NeuronGeneList! The new target for this neuron is LocalOutputList[_currentModule][k].otherNeuron.
                neuronList[neuronList.Count - 1].TargetNeurons.Add(uiVar.localOutputList[_currentModule][k].otherNeuron);

                // We need to look for the index for the target neuron
                // with ID LocalOutputList[_currentModule][k].otherNeuron.
                // We cannot use GetNeuronById because the statistics are not
                // updated during the creation of a new module!
                NeuronGene targetNeuron = 
                    neuronList.GetNeuronByIdAll(uiVar.localOutputList[_currentModule][k].otherNeuron);
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
            // Resets the ID generator:
            uint lastId = genome.FindLastId();
            _innovationIdGenerator.Reset(lastId + 1);

            // Defines all possible connections between the local_input and 
            // local_output neurons (fully interconnected).
            // TODO: A more optimal approach may be to do this outside of the 
            // loop for each genome in the population, since this step is the
            // same for all. 
            ConnectionDefinition[] connectionDefArr =
                    DefineAllConnections(localInputId, localOutputId);

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
                NeuronGene sourceNeuron = neuronList.GetNeuronByIdAll(def._sourceNeuronIdx);
                // Its targetId is def._targetNeuronIdx.
                sourceNeuron.TargetNeurons.Add(def._targetNeuronIdx);

                // We need to look for the target neuron.
                NeuronGene targetNeuron = neuronList.GetNeuronByIdAll(def._targetNeuronIdx);
                // Its sourceId is def._sourceNeuronIdx.
                targetNeuron.SourceNeurons.Add(def._targetNeuronIdx); 
            }

            // Ensure connections are sorted (this will only affect the 
            // non-protected connections in the new module!)
            ConnectionGeneList connectionList = genome.ConnectionGeneList;
            connectionList.SortByInnovationId();  
        }

        /// <summary>
        /// Defines all possible connections between the local_input and 
        /// local_output neurons (fully interconnected).
        /// </summary>
        ConnectionDefinition[] DefineAllConnections(List<uint> localInputId,
                                                    List<uint> localOutputId)
        {
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

            return connectionDefArr;
        }

        /// <summary>
        /// Regulation modules are created with a place-holder local output neuron.
        /// The first time a module is added to the regulation module we need 
        /// to update that neuron (as opposed to creating a new one!)
        /// </summary>
        void UpdateLocalOut(NeatGenome genome, newLink connectionInfo, int localOutIndex)
        {
            // Update's the list of targets for the local output neuron
            // (it should be connected to the first output neuron with
            // weight 0)
            // Cleans the old target (this was a place-holder connection, there
            // should NOT be other targets!)

            genome.NeuronGeneList[localOutIndex].TargetNeurons.Clear();
            // Adds new target
            genome.NeuronGeneList[localOutIndex].TargetNeurons.Add(connectionInfo.otherNeuron);

            // Gets the index for the protected connection (ID is the one before
            // the ID of the local output neuron!)
            int connectionIndex = FindConnectionIndexForId(
                    genome, genome.NeuronGeneList[localOutIndex].Id - 1);

            // Updates the weight (from 0 to 1)
            genome.ConnectionGeneList[connectionIndex].Weight = 1.0;

            // Updates the target
            genome.ConnectionGeneList[connectionIndex].TargetNodeId =
                    connectionInfo.otherNeuron;
        }

        /// <summary>
        /// Adds a new local output neuron to a genome.
        /// </summary>
        void AddLocalOut(NeatGenome genome, newLink connectionInfo, int oldLocalOutIndex)
        {
            // Neuron
            // We have the index:
            int index = oldLocalOutIndex + 1;
            // ID (reserved at module creation!):
            uint soureceId = genome.NeuronGeneList[oldLocalOutIndex].Id + 2;
            // Source list: (no changes: empty since it is a new neuron)
            // Target list: connectionInfo.otherNeuron

            NeuronGene newNeuron = CreateNeuronGene(soureceId, NodeType.Local_Output, 
                                                    _currentModule, -1);
            genome.InsertNeuron(newNeuron, index);
            genome.NeuronGeneList[index].TargetNeurons.Add(connectionInfo.otherNeuron);

            // Connection
            // Gets the index for the protected connection (ID is the one before
            // the ID of the old local output neuron)
            int oldIndex = FindConnectionIndexForId(
                    genome, genome.NeuronGeneList[oldLocalOutIndex].Id - 1);
            // ID: connectionInfo.id
            // Source: local out just created: id
            // Target: connectionInfo.otherNeuron
            ConnectionGene newConnection = new ConnectionGene(
                    connectionInfo.id, soureceId, connectionInfo.otherNeuron,
                    connectionInfo.weight, _currentModule, true);
            genome.InsertConnection(newConnection, oldIndex + 2);
        }

        /// <summary>
        /// This method is used to create random connections from local input
        /// neurons to the new local output neuron added to an active module.
        /// </summary>
        void AddConnectToNewLOut(NeatGenome genome, int oldLocalOutIndex)
        {
            // We need the local input list and a list with only the new local
            // output neuron (we do not want to interfere with connections to
            // older local output neurons!)
            List<uint> localInputList = new List<uint>();
            List<uint> localOutputList = new List<uint>();

            localInputList = ReturnLocalIn(genome.NeuronGeneList);
            localOutputList.Add(genome.NeuronGeneList[oldLocalOutIndex + 1].Id);

            PopulateModule(genome, localInputList, localOutputList);
        }

        /// <summary>
        /// Returns the local inputs for the ACTIVE module.
        /// </summary>
        List<uint> ReturnLocalIn(NeuronGeneList neuronList)
        {
            List<uint> returnList = new List<uint>();

            for (int i = neuronList.FirstIndex; i < neuronList.Count; ++i)
            {
                if (neuronList[i].NodeType == NodeType.Local_Input)
                {
                    returnList.Add(neuronList[i].Id);
                }
            }

            return returnList;
        }

        // Finds the first connection with the given source (when the first
        // module is added to a regulation module, this method is used to
        // find the place-holder connection that needs to be modified so it
        // takes the new module's regulatory neuron as target)
        int FindConnectionIndexForId(NeatGenome genome, uint connectionId)
        {
            int connectionIndex = 0;
            for (int i = genome.ConnectionGeneList.Count - 1; i > 0; --i)
            {
                if (genome.ConnectionGeneList[i].InnovationId == connectionId)
                {
                    connectionIndex = i;
                    break;
                }
            }
            return connectionIndex;
        }

        #endregion
    }
}
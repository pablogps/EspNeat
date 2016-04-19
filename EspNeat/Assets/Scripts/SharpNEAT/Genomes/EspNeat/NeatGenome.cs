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
    /// A genome class for EspNeat.
    /// 
    /// Note that neuron genes must be arranged according to the following layout plan.
    ///      Bias - single neuron. Innovation ID = 0
    ///      Input neurons.
    ///      Output neurons.
    ///      Regulatory neurons.
    ///      Local output neurons (module 1).
    ///      Hidden neurons (module 1).
    ///      Local output neurons (module 2).
    ///      Hidden neurons (module 2).
    ///             .
    ///             .
    ///             . 
    ///      Local output neurons (active module).
    ///      Hidden neurons (active module).
    /// 
    /// Connection genes must be arranged by module, with protected connections
    /// first in each module.
    ///      Protected connections (module i).
    ///      Free connections (module i).
    /// 
    /// This allows us to add and remove hidden neurons without affecting the 
    /// genome; this is convenient because everything but the active module is
    /// fixed and must remain constant. In fact they are only stored in the same
    /// list as hidden nodes as an efficiency measure when producing offspring 
    /// and decoding genomes, otherwise it would probably make sense to store 
    /// them in readonly lists.
    /// </summary>
    public class NeatGenome : IGenome<NeatGenome>, INetworkDefinition
    {
        #region Instance Variables

        NeatGenomeFactory _genomeFactory;
        readonly uint _id;
        int _specieIdx;
        private uint _birthGeneration;
        EvaluationInfo _evalInfo;
        CoordinateVector _position;
        object _cachedPhenome;

        // We ensure that the connectionGenes are sorted by innovation ID at all
        // times (with the possible exception of modifying an old module, which
        // would be taken to the end of the list). This allows significant 
        // optimisations to be made in crossover and decoding routines.
        readonly NeuronGeneList _neuronGeneList;
        readonly ConnectionGeneList _connectionGeneList;

        // For efficiency we store the number of input and output neurons. These
        // two quantities do not change throughout the life of a genome. Note 
        // that inputNeuronCount does NOT include the bias neuron; Use
        // inputAndBiasNeuronCount.

        int _auxStateNeuronCount;
        private static int _inputNeuronCount = 0;
        private static int _outputNeuronCount = 0;
		private static int _regulatory = 0;
		private static int _localIn = 0;
        private static int _localOut = 0;
        private static int _inHiddenModules = 0;
        private int _activeConnections = 0;

        // Created in a just-in-time manner and cached for possible re-use.
        NetworkConnectivityData _networkConnectivityData;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs with the provided ID, birth generation and gene lists.
        /// </summary>
        public NeatGenome(NeatGenomeFactory genomeFactory,
                          uint id, 
                          uint birthGeneration,
                          NeuronGeneList neuronGeneList, 
                          ConnectionGeneList connectionGeneList, 
                          bool rebuildNeuronGeneConnectionInfo)
        {
            _genomeFactory = genomeFactory;
            _id = id;
            _birthGeneration = birthGeneration;
            _neuronGeneList = neuronGeneList;
            _connectionGeneList = connectionGeneList;

            // Rebuild per neuron connection info if caller has requested it.
            if (rebuildNeuronGeneConnectionInfo) {
                RebuildNeuronGeneConnectionInfo();
            }

            // If we have a factory then create the evaluation info object now, 
            // also count the nodes that have auxiliary state.
            // Otherwise wait until the factory is provided through the property setter.
            if (null != _genomeFactory) 
            {
                _evalInfo = new EvaluationInfo(genomeFactory.NeatGenomeParameters.FitnessHistoryLength);
                _auxStateNeuronCount = CountAuxStateNodes();
            }
            Debug.Assert(PerformIntegrityCheck());
        }

        /// <summary>
        /// Copy constructor.
        /// </summary>
        public NeatGenome(NeatGenome copyFrom, uint id, uint birthGeneration)
        {
            _genomeFactory = copyFrom._genomeFactory;
            _id = id;
            _birthGeneration = birthGeneration;

            // These copy constructors make clones of the genes rather than 
            // copies of the object references.
            _neuronGeneList = new NeuronGeneList(copyFrom._neuronGeneList);
            _connectionGeneList = new ConnectionGeneList(copyFrom._connectionGeneList);
            
            // Copy precalculated values.
            _activeConnections = copyFrom._activeConnections;
            _auxStateNeuronCount = copyFrom._auxStateNeuronCount;
            
            _evalInfo = new EvaluationInfo(copyFrom.EvaluationInfo.FitnessHistoryLength);

            Debug.Assert(PerformIntegrityCheck());
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the number of input neurons. This does not include the bias.
        /// </summary>
        public int Input
        {
            get { return _inputNeuronCount; }
            set { _inputNeuronCount = value; }
        }

        /// <summary>
        /// Gets the number of output neurons.
        /// </summary>
        public int Output
        {
            get { return _outputNeuronCount; }
            set { _outputNeuronCount = value; }
        }

        /// <summary>
        /// Gets the number of regulatory neurons.
        /// </summary>
        public int Regulatory
        {
            get { return _regulatory; }
            set { _regulatory = value; }
        }

		/// <summary>
		/// Gets the number of local input neurons in the active module.
		/// </summary>
		public int LocalIn
		{
			get { return _localIn; }
			set { _localIn = value; }
		}

        /// <summary>
        /// Gets the number of local output neurons in the active module.
        /// </summary>
        public int LocalOut
        {
            get { return _localOut; }
            set { _localOut = value; }
        }

        /// <summary>
        /// Gets the number of local input, output and hidden neurons in 
        /// encapsulated modules (all but the base and the active module).
        /// </summary>
        public int InHiddenModules
        {
            get { return _inHiddenModules; }
            set { _inHiddenModules = value; }
        }

        /// <summary>
        /// Gets the number of connections in the active module, excluding
        /// protected connections.
        /// </summary>
        public int ActiveConnections
        {
            get { return _activeConnections; }
            set { _activeConnections = value; }
        }

        /// <summary>
        /// Gets the genome's unique ID. IDs are unique across all genomes 
        /// created from a single IGenomeFactory and all ancestor genomes 
        /// spawned from those genomes.
        /// </summary>
        public uint Id
        {
            get { return _id; }
        }

        /// <summary>
        /// Gets or sets a specie index. This is the index of the species that 
        /// the genome is in. Implementing this is required only when using 
        /// evolution algorithms that speciate genomes.
        /// </summary>
        public int SpecieIdx 
        { 
            get { return _specieIdx; }
            set { _specieIdx = value; }
        }

        /// <summary>
        /// Gets the generation that this genome was born/created in. Used to 
        /// track genome age.
        /// </summary>
        public uint BirthGeneration
        {
            get { return _birthGeneration; }
            set { _birthGeneration = value; }
        }

        /// <summary>
        /// Gets evaluation information for the genome, including its fitness.
        /// </summary>
        public EvaluationInfo EvaluationInfo
        {
            get { return _evalInfo; }
        }

        /// <summary>
        /// Gets a value that indicates the magnitude of a genome's complexity. 
        /// For a NeatGenome we return the number of connection genes since a 
        /// neural network's complexity is approximately proportional to the 
        /// number of connections - the number of neurons is less important and
        /// can be viewed as being a limit on the possible number of connections.
        /// </summary>
        public double Complexity 
        { 
            get { return _connectionGeneList.Count; }
        }

        /// <summary>
        /// Gets a coordinate that represents the genome's position in the search 
        /// space (also known as the genetic encoding space). This allows 
        /// speciation/clustering algorithms to operate on an abstract cordinate
        /// data type rather than being coded against specific IGenome types.
        /// </summary>
        public CoordinateVector Position 
        { 
            get
            {
                if (null == _position)
                {
                    // Consider each connection gene as a dimension where the 
                    // innovation ID is the dimension's ID and the weight is the
                    // position within that dimension. The coordinate elements 
                    // in the resulting array must be sorted by 
                    // innovation/dimension ID, this requirement is met by the
                    // connection gene list also requiring to be sorted at all times.

                    // We copy only the active connections in the active module.
					//ConnectionGeneList list = _connectionGeneList.GetRange(_connectionGeneList.FirstId,
                    //    _activeConnections);
                    ConnectionGeneList list = new ConnectionGeneList();
                    for (int i = _connectionGeneList.FirstId;
                         i < _connectionGeneList.Count; ++i)
                    {
                        list.Add(_connectionGeneList[i]);
                    }

                    int count = list.Count;
                    KeyValuePair<ulong, double>[] coordElemArray = 
                            new KeyValuePair<ulong, double>[count];

                    for (int i = 0; i < count; i++) {
                        coordElemArray[i] =
                                new KeyValuePair<ulong, double>(list[i].InnovationId, 
                                                                list[i].Weight);
                    }
                    _position = new CoordinateVector(coordElemArray);
                }
                return _position;
            }
        }

        /// <summary>
        /// Gets or sets a cached phenome obtained from decodign the genome.
        /// Genomes are typically decoded to Phenomes for evaluation. This 
        /// property allows decoders to cache the phenome in order to avoid 
        /// decoding on each re-evaluation; However, this is optional.
        /// The phenome in un-typed to prevent the class framework from becoming
        /// overly complex.
        /// </summary>
        public object CachedPhenome 
        { 
            get { return _cachedPhenome; }
            set { _cachedPhenome = value; }
        }

        /// <summary>
        /// Gets or sets the NeatGenomeFactory associated with the genome. A 
        /// reference to the factory is passed to spawned genomes, this allows 
        /// all genomes within a population to have access to common data such 
        /// as NeatGenomeParameters and an ID generator. Setting the genome 
        /// factory after construction is allowed in order to resolve 
        /// chicken-and-egg scenarios when loading genomes from storage.
        /// </summary>
        public NeatGenomeFactory GenomeFactory
        {
            get { return _genomeFactory; }
            set 
            {
                if(null != _genomeFactory) {
                    throw new SharpNeatException("NeatGenome already has an assigned GenomeFactory.");
                }
                _genomeFactory = value;
                _evalInfo = new EvaluationInfo(_genomeFactory.NeatGenomeParameters.FitnessHistoryLength);
                _auxStateNeuronCount = CountAuxStateNodes();
            }
        }

        /// <summary>
        /// Gets the genome's list of neuron genes.
        /// </summary>
        public NeuronGeneList NeuronGeneList
        {
            get { return _neuronGeneList; } 
        }

        /// <summary>
        /// Gets the genome's list of connection genes.
        /// </summary>
        public ConnectionGeneList ConnectionGeneList
        {
            get { return _connectionGeneList; }
        }

        #endregion

        #region Public Functions

        public static void ResetStatistics()
        {
            _regulatory = 0;
			_localIn = 0;
            _localOut = 0;
            _inHiddenModules = 0;          
        }

        /// <summary>
        /// Finds the latest innovation ID used. This is needed in Factory in 
        /// order to create new modules with compatible innovation ID values.
        /// Bear in mind we want it to be possible to eddit an old module, 
        /// which would break a strict sorting.
        /// </summary>
        public uint FindLastId()
        {
            // This is assuming the last module (if there are modules yet) has
            // the latest Id values! Maybe check this? Create
            // return Math.Max(_neuronGeneList[_neuronGeneList.Count - 1].InnovationId, 
            //                _connectionGeneList[_connectionGeneList.Count - 1].InnovationId);

            // This is slower, but will always work fine.
            // This function should not be used often, so it should not really
            // affect performance. (Used in NeatGenomeFactory.)
            uint maxValue = 0;
            for (int i = 0; i < _neuronGeneList.Count; ++i)
            {
                if (_neuronGeneList[i].InnovationId > maxValue)
                {
                    maxValue = _neuronGeneList[i].InnovationId;                    
                }
            }
            for (int j = 0; j < _connectionGeneList.Count; ++j)
            {
                if (_connectionGeneList[j].InnovationId > maxValue)
                {
                    maxValue = _connectionGeneList[j].InnovationId;
                }
            }
            return maxValue;
        }

        /// <summary>
        /// Similar to FindLastId, finds the youngest module in the genome.
        /// If evolving older modules is allowed this does not need to be the
        /// last module in the list or match the variable _currentModule in
        /// the factory.
        /// </summary>
        public int FindYoungestModule()
        {
            int maxValue = 0;
            // It is enough to sarch nodes, because there will be always at
            // least one local output neuron per module!
            for (int i = 0; i < _neuronGeneList.Count; ++i)
            {
                if (_neuronGeneList[i].ModuleId > maxValue)
                {
                    maxValue = _neuronGeneList[i].ModuleId;                    
                }
            }
            return maxValue;
        }

        /// <summary>
        /// When we load a genome we need to know which is the last module in 
        /// the list, because this will be the active module if we want to 
        /// keep evolving it instead of adding a new module. This function is
        /// relevant (and different than FindYoungestModule) if evolving older
        /// modules is allowed.
        /// </summary>
        public int FindActiveModule()
        {
            return _neuronGeneList[_neuronGeneList.Count - 1].ModuleId;
        }

        /// <summary>
        /// Allows to add a neuron to the genome from another script. Used in
        /// Factory for new modules.
        /// </summary>
        public void AddNeuron(NeuronGene newNeuron)
        {
            _neuronGeneList.Add(newNeuron);           
        }

        /// <summary>
        /// Allows to add a connection to the genome from another script. Used in
        /// Factory for new modules.
        /// </summary>
        public void AddConnection(ConnectionGene newConnection)
        {
            _connectionGeneList.Add(newConnection); 
        }

        /// <summary>
        /// When we load genomes from an Xml file we need to get the number of 
        /// local input, local output and hidden neurons in encapsulated modules
        /// (all but the last). This is needed in case we want to continue 
        /// evolving this module. If we add a new module this value will 
        /// be overwritten.
        /// </summary>
        public void InHiddenModulesFromLoad()
        {
            int active = _neuronGeneList[_neuronGeneList.Count - 1].ModuleId;
            for (int idx = _neuronGeneList.LastBase + 1;
                 idx < _neuronGeneList.Count; ++idx)
            {
                // When we get to the active module we can return.
                if (_neuronGeneList[idx].ModuleId == active)
                { 
                    // The result we want is the number of iterations we needed,
                    // except the last one.
                    _inHiddenModules = idx - _neuronGeneList.LastBase - 1;
                    return;
                }
            }
        }

        /// <summary>
        /// When we load genomes from an Xml file we need to get the number of 
        /// active connections in the active module (this excludes protected
        /// connections). This is needed in case we want to continue evolving 
        /// this module. If we add a new module this value will be overwritten. 
        /// </summary>
        public void ActiveConnectionsFromLoad()
        {
            int count = 0;
            for (int idx = _connectionGeneList.Count - 1; idx > -1; --idx)
            {
                // We count backwards until we find a protected connection.
                // Count will not add the first protected, so we can return
                // its value directly.
                if (_connectionGeneList[idx].Protected)
                {
                    _activeConnections = count;
                    return;
                }
                ++count;
            }
        }

        /// <summary>
        /// Asexual reproduction.
        /// </summary>
        /// <param name="birthGeneration">The current evolution algorithm generation. 
        /// Assigned to the new genome at its birth generation.</param>
        public NeatGenome CreateOffspring(uint birthGeneration)
        {
            // Make a new genome that is a copy of this one but with a new genome ID.
            NeatGenome offspring = _genomeFactory.CreateGenomeCopy(
                    this, _genomeFactory.NextGenomeId(), birthGeneration);

            // Mutate the new genome.
            offspring.Mutate();
            return offspring;
        }

        /// <summary>
        /// Sexual reproduction.
        /// </summary>
        /// <param name="parent">The other parent genome (mates with the 
        /// current genome).</param>
        /// <param name="birthGeneration">The current evolution algorithm generation. 
        /// Assigned to the new genome at its birth generation.</param>
        public NeatGenome CreateOffspring(NeatGenome parent, uint birthGeneration)
        {
            // NOTE for feed-forward only networks. Due to how this crossover 
            // method works the resulting offsprign will never have recurrent
            // conenctions if the two parents are feed-forward only, this is 
            // because we do not actually mix the connectivity of the two parents
            // (only the connection weights were there is a match). Therefore
            // any changes to this method must take feed-forward networks into account.

            CorrelationResults correlationResults = 
                    CorrelateConnectionGeneLists(_connectionGeneList, 
                                                 parent._connectionGeneList);
            Debug.Assert(correlationResults.PerformIntegrityCheck(), 
                         "CorrelationResults failed integrity check.");

            // Construct a ConnectionGeneListBuilder with its capacity set the
            // the maximum number of connections that could be added to it (all
            // ACTIVE connection genes from both parents). This eliminates the
            // possiblity of having to re-allocate list memory, improving 
            // performance at the cost of a little additional allocated memory
            // on average.
            ConnectionGeneListBuilder connectionListBuilder = 
                    new ConnectionGeneListBuilder(_activeConnections +
                                                  parent._activeConnections);

            // First step: copy the genome base and old modules into the 
            // offspring. We work with the active module (including local input
            // and output neurons) as with a classic genome. We also copy here
            // the protected connections of the current module.
            NeatGenome genome = CopyOldModules(birthGeneration);
          
            // Pre-register all of the fixed neurons (local in/out) with the
            // ConnectionGeneListBuilder's neuron ID dictionary. We do this so
            // that we can use the dictionary later on as a complete list of
            // all neuron IDs required by the offspring genome - if we didn't 
            // do this we might miss some of the fixed neurons that happen to 
            // not be connected to or from.
			SortedDictionary<uint, NeuronGene> neuronDictionary = 
                    connectionListBuilder.NeuronDictionary;
            for (int i = _neuronGeneList.FirstIndex; 
                 i < _neuronGeneList.FirstIndex + _localIn + _localOut; ++i)
            {
                neuronDictionary.Add(_neuronGeneList[i].InnovationId,
                                     _neuronGeneList[i].CreateCopy(false));
            }

            // A variable that stores which parent is fittest, 1 or 2. We 
            // pre-calculate this value because this fitness test needs to be
            // done in subsequent sub-routine calls for each connection gene.
            int fitSwitch;
            if (_evalInfo.Fitness > parent._evalInfo.Fitness) {
                fitSwitch = 1;
            }
            else if (_evalInfo.Fitness < parent._evalInfo.Fitness) {
                fitSwitch = 2;
            }
            else
            {   // Select one of the parents at random to be the 'master' genome during crossover.
                fitSwitch = (_genomeFactory.Rng.NextDouble() < 0.5) ? 1 : 2;
            }

			// TODO: Reconsider this approach. (Agreed)
            // Pre-calculate a flag that indicates if excess and disjoint genes
            // (from the weakest parent)should be copied into the offspring
            // genome. Excess and disjoint genes are either copied altogether
            // or none at all.
            bool combineDisjointExcessFlag = _genomeFactory.Rng.NextDouble() < 
                                             _genomeFactory.NeatGenomeParameters.DisjointExcessGenesRecombinedProbability;

            // Loop through the items within the CorrelationResults, processing
            // each one in turn. Where we have a match between parents we select
            // which parent's copy (effectively which connection weight) to 
            // use probabilistically with even chance.
            // For disjoint and excess genes, if they are on the fittest parent
            // (as indicated by fitSwitch) we always take that gene. If the 
            // disjoint/excess gene is on the least fit parent then we take that
            // gene also but only when combineDisjointExcessFlag is true.
            
            // Loop 1: Get all genes that are present on the fittest parent. 
            // Note. All accepted genes are accumulated within connectionListBuilder.
            // Note. Any disjoint/excess genes that we wish to select from the 
            // least fit parent are stored in a second list for processing later 
            // (this avoids having to do another complete pass through the correlation 
            // results). The principal reason for this is handling detection of
            // cyclic connections when combining two genomes when evolving 
            // feedforward-only networks. Each genome by itself will be acyclic, 
            // so can safely copy all genes from any one parent, but for any 
            // genes from the other parent we then need to check each one as we 
            // add it to the offspring genome to check if it would create a cycle.

            // TODO: Avoid this approach, at least for cyclic networks.
            List<CorrelationItem> disjointExcessGeneList = 
                    combineDisjointExcessFlag ? 
                    new List<CorrelationItem>(correlationResults.CorrelationStatistics.DisjointConnectionGeneCount + 
                                              correlationResults.CorrelationStatistics.ExcessConnectionGeneCount) : null;

            foreach(CorrelationItem correlItem in correlationResults.CorrelationItemList)
            {
                // Determine which genome to copy from (if any)
                int selectionSwitch;
                if (CorrelationItemType.Match == correlItem.CorrelationItemType) 
                {   // For matches pick a parent genome at random (they both 
                    // have the same connection gene, but with a different
                    // connection weight)   
                    selectionSwitch = RouletteWheel.SingleThrow(0.5, _genomeFactory.Rng) ? 1 : 2;
                }
                else if (1 == fitSwitch && null != correlItem.ConnectionGene1) 
                {   // Disjoint/excess gene on the fittest genome (genome #1).
                    selectionSwitch = 1;
                }
                else if (2 == fitSwitch && null != correlItem.ConnectionGene2) 
                {   // Disjoint/excess gene on the fittest genome (genome #2).
                    selectionSwitch = 2;
                }
                else 
                {   // Disjoint/excess gene on the least fit genome. 
                    if (combineDisjointExcessFlag) 
                    {   // Put to one side for processing later.
                        disjointExcessGeneList.Add(correlItem);
                    }
                    // Skip to next gene.
                    continue;
                }

                // Get ref to the selected connection gene and its source target
                // and neuron genes.
                ConnectionGene connectionGene;
                NeatGenome parentGenome;
                if (1 == selectionSwitch) {
                    connectionGene = correlItem.ConnectionGene1;
                    parentGenome = this;
                } else {
                    connectionGene = correlItem.ConnectionGene2;
                    parentGenome = parent;
                }

                // Add connection gene to the offspring's genome. For genes from
                // a match we set a flag to force an override of any existing 
                // gene with the same innovation ID (which may have come from a 
                // previous disjoint/excess gene). We prefer matched genes as 
                // they will tend to give better fitness to the offspring - this
                // logic if based purely on the fact that the gene has clearly 
                // been replicated at least once before and survived within at 
                // least two genomes.
                connectionListBuilder.TryAddGene(connectionGene, parentGenome, 
												 (CorrelationItemType.Match == 
                                                  correlItem.CorrelationItemType));
            }

            // Loop 2: Add disjoint/excess genes from the least fit parent 
            // (if any). These may create connectivity cycles, hence we need to
            // test for this when evoloving feedforward-only networks.
            if (null != disjointExcessGeneList && 0 != disjointExcessGeneList.Count)
            {
                foreach (CorrelationItem correlItem in disjointExcessGeneList)
                {
                    // Get ref to the selected connection gene and its source
                    // target neuron genes.
                    ConnectionGene connectionGene;
                    NeatGenome parentGenome;
                    if (null != correlItem.ConnectionGene1) {
                        connectionGene = correlItem.ConnectionGene1;
                        parentGenome = this;
                    } else {
                        connectionGene = correlItem.ConnectionGene2;
                        parentGenome = parent;
                    }

                    // We are effectively adding connections from one genome to
                    // another, as such it is possible to create cyclic 
                    // conenctions here. Thus only add the connection if we allow
                    // cyclic connections *or* the connection does not form a cycle.
                    if (!_genomeFactory.NeatGenomeParameters.FeedforwardOnly || 
						!connectionListBuilder.IsConnectionCyclic(connectionGene.SourceNodeId,
                                                                  connectionGene.TargetNodeId))
                    {
                        // Add connection gene to the offspring's genome.
                        connectionListBuilder.TryAddGene(connectionGene, 
                                                         parentGenome, false);
                    }
                }
            }

            // Add the connection builders definitive list of neurons to the genome.
            foreach (NeuronGene neuronGene in neuronDictionary.Values) {   
                genome.AddNeuron(neuronGene);
            }

            // Note that connectionListBuilder.ConnectionGeneList is already 
            // sorted by connection gene innovation ID because it was generated 
            // by passing over the correlation items generated by 
            // CorrelateConnectionGeneLists() - which returns correlation items in order.
            // Also note this is Ok for the active module.
            foreach (ConnectionGene connectionGene in 
                     connectionListBuilder.ConnectionGeneList)
            {
                genome.AddConnection(connectionGene);
            }

            // We update the number of active connections.
            genome.ActiveConnectionsFromLoad();

            return genome;           
        }

        #endregion

        #region Private Methods [Reproduction / Mutation]

        /// <summary>
        /// Copies the genome base and old modules into a new genome.
        /// List.GetRange does not work because we are using generic variables.
        /// </summary>
        private NeatGenome CopyOldModules(uint birthGeneration)
        {
            // FirstIndex is the index of the first neuron in the current
            // module. This value is also the number of old elements!
            NeuronGeneList neuronCommonList = new NeuronGeneList();
            for (int i = 0; i < _neuronGeneList.FirstIndex; ++i)
            {
                neuronCommonList.Add(_neuronGeneList[i]);
            }

            // The number of connections in old modules is the total minus the
            // active connections. We also want to include in this copy 
            // the protected connections of the active module.
            int oldConnections = _connectionGeneList.Count - _activeConnections;
            ConnectionGeneList connectionCommonList = new ConnectionGeneList();
            for (int i = 0; i < oldConnections; ++i)
            {
                connectionCommonList.Add(_connectionGeneList[i]);
            }

            return _genomeFactory.CreateGenome(_genomeFactory.NextGenomeId(), 
                                               birthGeneration, neuronCommonList, 
                                               connectionCommonList, false);  
        }

        private void Mutate()
        {
            // If we have fewer than two active connections in the active module
            // then use an alternative RouletteWheelLayout that avoids 
            // destructive mutations. This prevents the creation of genomes with
            // no connections.
            RouletteWheelLayout rwlInitial = (_activeConnections < 2) ?
                    _genomeFactory.NeatGenomeParameters.RouletteWheelLayoutNonDestructive :
                    _genomeFactory.NeatGenomeParameters.RouletteWheelLayout;

            // Select a type of mutation and attempt to perform it. If that 
            // mutation is not possible then we eliminate that possibility from 
            // the roulette wheel and try again until a mutation is successful 
            // or we have no mutation types remaining to try.
            RouletteWheelLayout rwlCurrent = rwlInitial;
            bool success = false;
            bool structureChange = false;
            for(;;)
            {
                int outcome = RouletteWheel.SingleThrow(rwlCurrent, _genomeFactory.Rng);
                switch(outcome)
                {
                    case 0:
                        Mutate_ConnectionWeights();
                        // Connection weight mutation is assumed to always succeed—
                        //genomes should always have at least one connection to mutate.
                        success = true;
                        break;
                    case 1:
                        success = structureChange = (null != Mutate_AddNode());
                        break;
                    case 2:
                        success = structureChange = (null != Mutate_AddConnection());
                        break;
                    case 3:
                        success = Mutate_NodeAuxState();
                        break;
                    case 4:
                        success = structureChange = (null != Mutate_DeleteConnection());
                        break;
                    default:
                        throw new SharpNeatException(string.Format("NeatGenome.Mutate(): " +
                                                     "Unexpected outcome value [{0}]", outcome));
                }

                // Success. Break out of loop.
                if(success) 
                {
                    if(structureChange) 
                    {   // Discard any cached connectivity data. It is now invalidated.
                        _networkConnectivityData = null;
                    }
                    break;
                }

                // Mutation did not succeed. Remove attempted type of mutation 
                // from set of possible outcomes.
                rwlCurrent = rwlCurrent.RemoveOutcome(outcome);
                if (0.0 == rwlCurrent.ProbabilitiesTotal)
                {   // Nothing left to try. Do nothing.
                    return;
                }
            }

            // Mutation succeeded. Check resulting genome.
            Debug.Assert(PerformIntegrityCheck());
        }

        /// <summary>
        /// Add a new node to the Genome. We do this by removing an active
        /// connection at random and inserting a new node and two new connections
        /// that make the same circuit as the original connection, that is, we
        /// split an existing connection. This way the new node is integrated
        /// into the network from the outset.
        /// </summary>
        /// <returns>Returns the added NeuronGene if successful, otherwise null.</returns>
        private NeuronGene Mutate_AddNode()
        {
            // Can this ever be the case, though?
            if (0 == _activeConnections) 
            {   // Nodes are added by splitting an existing connection into two
                // and placing a new node between the two new connections. 
                // Since we don't have any connections to split we indicate failure.
                return null;
            }

            // Select an active connection at random, keep a reference to it
            // and delete it from the genome.
            // Active connections are at the end of the list. So for the random
            // value = 0 we want Count - 1, and for random = active connections
            // we want the first connection.
            int connectionToReplaceIdx = _connectionGeneList.Count - 1 -
                                         _genomeFactory.Rng.Next(_activeConnections);
            ConnectionGene connectionToReplace = _connectionGeneList[connectionToReplaceIdx];
            _connectionGeneList.RemoveAt(connectionToReplaceIdx);

            // Get IDs for the two new connections and a single neuron. This call
            // will check the history buffer (AddedNeuronBuffer) for matching 
            // structures from previously added neurons (for the search as a 
            // whole, not just on this genome).
            AddedNeuronGeneStruct idStruct;
            bool reusedIds = Mutate_AddNode_GetIDs(connectionToReplace.InnovationId, 
                                                   out idStruct);

            // Replace connection with two new connections and a new neuron. 
            // The first connection uses the weight from the replaced connection
            // (so it's functionally the same connection, but the ID is new).
            //
            // Ideally we want the functionality of the new structure to match 
            // as closely as possible the replaced connection, but that depends 
            // on the neuron activation function. As a cheap/quick approximation
            // we make the second connection's weight full strength 
            // (_genomeFactory.NeatGenomeParameters.ConnectionWeightRange). This
            // maps the range 0..1 being output from the new neuron to something
            // close to 0.5..1.0 when using a unipolar sigmoid (depending on exact
            // sigmoid function in use). Weaker weights reduce that range, ultimately 
            // a zero weight always gives an output of 0.5 for a unipolar sigmoid.
            NeuronGene newNeuronGene = 
                    _genomeFactory.CreateNeuronGene(idStruct.AddedNeuronId, NodeType.Hidden);
            ConnectionGene newConnectionGene1 = 
                    new ConnectionGene(idStruct.AddedInputConnectionId,
                                       connectionToReplace.SourceNodeId,
                                       idStruct.AddedNeuronId,
                                       connectionToReplace.Weight,
                                       _genomeFactory.CurrentModule);

            ConnectionGene newConnectionGene2 = 
                    new ConnectionGene(idStruct.AddedOutputConnectionId,
                                       idStruct.AddedNeuronId,
                                       connectionToReplace.TargetNodeId,
                                       _genomeFactory.NeatGenomeParameters.ConnectionWeightRange,
                                       _genomeFactory.CurrentModule);

            // If we are re-using innovation numbers from elsewhere in the 
            // population they are likely to have lower values than other genes 
            // in the current genome. Therefore we need to be careful to ensure 
            // the genes lists remain sorted by innovation ID. The most efficient 
            // means of doing this is to insert the new genes into the correct 
            // location (as opposed to adding them to the list ends and 
            // re-sorting the lists).
            if (reusedIds) 
            {
                _neuronGeneList.InsertIntoPosition(newNeuronGene);
                _connectionGeneList.InsertIntoPosition(newConnectionGene1);
                _connectionGeneList.InsertIntoPosition(newConnectionGene2);
            }
            else
            {   // The genes have new innovation IDs - so just add them to the 
                // ends of the gene lists.
                _neuronGeneList.Add(newNeuronGene);
                _connectionGeneList.Add(newConnectionGene1);
                _connectionGeneList.Add(newConnectionGene2);
            }

            // Track connections associated with each neuron.
            // Original source neuron.
            NeuronGene srcNeuronGene = 
                    _neuronGeneList.GetNeuronById(connectionToReplace.SourceNodeId);
            srcNeuronGene.TargetNeurons.Remove(connectionToReplace.TargetNodeId);
            srcNeuronGene.TargetNeurons.Add(newNeuronGene.Id);

            // Original target neuron.
            NeuronGene tgtNeuronGene = 
                    _neuronGeneList.GetNeuronById(connectionToReplace.TargetNodeId);
            tgtNeuronGene.SourceNeurons.Remove(connectionToReplace.SourceNodeId);
            tgtNeuronGene.SourceNeurons.Add(newNeuronGene.Id);

            // New neuron.
            newNeuronGene.SourceNeurons.Add(connectionToReplace.SourceNodeId);
            newNeuronGene.TargetNeurons.Add(connectionToReplace.TargetNodeId);

            // Track aux state node count and update stats.
            if (_genomeFactory.ActivationFnLibrary.GetFunction(newNeuronGene.ActivationFnId).AcceptsAuxArgs)
            {
                _auxStateNeuronCount++;
            }
            _genomeFactory.Stats._mutationCountAddNode++;

            // Do not forget to update the active connections counter!
            ++_activeConnections;

            // Indicate success.
            return newNeuronGene;
        }

        /// <summary>
		/// Gets innovation IDs for a new neuron and two connections for 
        /// Mutate_AddNode(). We add neurons by splitting an existing connection,
        /// here we check if the connection to be split has previously been split
        /// (in any other genome during evolution!) and if so attempt to re-use 
        /// the IDs assigned during that split.
        /// </summary>
        /// <param name="connectionToReplaceId">ID of the connection that is 
        /// being replaced.</param>
        /// <param name="idStruct">Conveys the required IDs back to the caller.</param>
        /// <returns>Returns true if the IDs are existing IDs from a matching 
        /// structure in the history buffer (AddedNeuronBuffer).</returns>
        private bool Mutate_AddNode_GetIDs(uint connectionToReplaceId, 
                                           out AddedNeuronGeneStruct idStruct)
        {
            bool registerNewStruct = false;
            if (_genomeFactory.AddedNeuronBuffer.TryGetValue(connectionToReplaceId, 
                                                             out idStruct))
            {   
                // Found existing matching structure.
                // However we can only re-use the IDs from that structrue if they
                // aren't already present in the current genome; this is possible
                // because genes can be acquired from other genomes via sexual 
                // reproduction (and when a node is inserted the old connection 
                // is deleted and the new ones have new IDs, so there is no conflict).
                // Therefore we only re-use IDs if we can re-use all three together,
                // otherwise we aren't assigning the IDs to matching structures 
                // throughout the population, which is the reason for ID re-use.
				if ((_neuronGeneList.BinarySearch(idStruct.AddedNeuronId) == -1) &&
					(_connectionGeneList.BinarySearch(idStruct.AddedInputConnectionId) == -1) &&
					(_connectionGeneList.BinarySearch(idStruct.AddedOutputConnectionId) == -1))             
                {
                    // Return true to allow re-use of existing IDs.
                    return true;
                }
            }
            else
            {   // ConnectionID not found. This connectionID has not been split 
                // to add a neuron in the past, or at least as far back as the 
                // history buffer goes. Therefore we register the structure with
                // the history buffer.
                registerNewStruct = true;
            }

            // No pre-existing matching structure or if there is we already have
            // some of its genes (from sexual reproduction).
            // Generate new IDs for this structure.
            idStruct = new AddedNeuronGeneStruct(_genomeFactory.InnovationIdGenerator);

            // If the connectionToReplaceId was not found (above) then we register 
            // it along with the new structure it is being replaced with.
            if (registerNewStruct) {   
                _genomeFactory.AddedNeuronBuffer.Enqueue(connectionToReplaceId, idStruct);
            }
            return false;
        }

        /// <summary>
        /// Attempt to perform a connection addition mutation. Returns the added 
        /// connection gene if successful.
        /// </summary>
        private ConnectionGene Mutate_AddConnection()
        {
            // We attempt to find a pair of neurons with no connection between 
            // them in one or both directions. We disallow multiple connections 
            // between the same two neurons going in the same direction, but we 
            // *do* allow connections going in opposite directions (one connection 
            // each way). We also allow a neuron to have a single recurrent 
            // connection, that is, a connection that has the same neuron as its
            // source and target neuron.

            // ENHANCEMENT: Test connection 'density' and use alternative 
            // connection selection method if above some threshold.

            // Because we always have at least one local input and one local
            // output neuron, we always have candidate pairs of neurons to 
            // which we can consider adding connections, but if all 
            // neurons are already fully interconnected then we should handle 
            // this case where there are no possible neuron pairs to add a 
            // connection to. To handle this we use a simple strategy of testing
            // the suitability of randomly selected pairs and after some number 
            // of failed tests we bail out of the routine and perform weight 
            // mutation as a last resort - so that we did at least some form of 
            // mutation on the genome.

            // NOTE this means the probability of forming new connections depends
            // on the current connection density. This is valid, but must be
            // taken into account.

            // This check should be irrelevant in the classic NEAT, and more so
            // in ESP. Performance here is not as crucial as in the phenome, 
            // so we leave the updated version for optional use.
            // Original conditional: if (_neuronGeneList.Count < 3) 
            /*
            if (_localIn + _localOut < 2) 
            {
                return null;
            }
            */
            int firstModuleIndex = _neuronGeneList.FirstIndex;
            int firstLocalOutIndex = firstModuleIndex + _localIn;
            int moduleNeuronCount = _neuronGeneList.Count - firstModuleIndex;
            int hiddenCount = moduleNeuronCount - _localIn - _localOut;
            int localInHiddenCount = _localIn + hiddenCount;
            int localOutHiddenCount = _localOut + hiddenCount;

            // Use slightly different logic when evolving feedforward only networks.
            const int number_attempts = 5;
            if (_genomeFactory.NeatGenomeParameters.FeedforwardOnly)
            {
                // Feeforward networks.
                for (int attempts = 0; attempts < number_attempts; ++attempts)
                {
                    // Select candidate source and target for the connection. 
                    // Valid source nodes local input and hidden nodes. 
                    // Local output nodes are not source candidates in acyclic 
                    // nets (because that can prevent future conenctions from 
                    // targeting the local output if it would create a cycle).

                    // We get a random number up to local in + hidden and add
                    // firstModuleIndex.
                    // If the result is greater than firstLocalOutIndex we have
                    // a local output, so we add _localOut to jump over these
                    // unwanted neurons.
                    int srcNeuronIdx = firstModuleIndex + 
                                       _genomeFactory.Rng.Next(localInHiddenCount);
                    if (srcNeuronIdx >= firstLocalOutIndex)
                    {
                        srcNeuronIdx += _localOut;
                    }

                    // Valid target nodes are all hidden and local output nodes.
                    // We get a random number for hidden + localOut -1, and then
                    // add firstModuleIndex. If target equals source then we
                    // have a trivial loop, unwanted in feedforward only networks.
                    // The new target is then the last neuron in the list. This
                    // way all neurons have an equal chance.
                    int tgtNeuronIdx = firstLocalOutIndex +
                                       _genomeFactory.Rng.Next(localOutHiddenCount - 1);
                    // Check trivial auto-loop:
                    if (srcNeuronIdx == tgtNeuronIdx)
                    {
                        tgtNeuronIdx = _neuronGeneList.Count - 1;
                    }

                    // Test if this connection already exists or is recurrent
                    NeuronGene sourceNeuron = _neuronGeneList[srcNeuronIdx];  
                    NeuronGene targetNeuron = _neuronGeneList[tgtNeuronIdx];
                    if (sourceNeuron.TargetNeurons.Contains(targetNeuron.Id) || 
                        IsConnectionCyclic(sourceNeuron, targetNeuron.Id)) 
                    {   // Try again.
                        continue;
                    }
                    return Mutate_AddConnection_CreateConnection(sourceNeuron, 
                                                                 targetNeuron);
                }
            }
            else
            // Recurrent networks.
            {
                for (int attempts = 0; attempts < number_attempts; ++attempts)
                {
                    // Select candidate source and target for the connection.
                    // Any local input, output or hidden neuron can be used as
                    // the source. 
                    // Valid targets are hidden neurons and local output neurons.
                    int srcNeuronIdx = firstModuleIndex +
                            _genomeFactory.Rng.Next(moduleNeuronCount);
                    int tgtNeuronIdx = firstLocalOutIndex + 
                            _genomeFactory.Rng.Next(localOutHiddenCount);

                    NeuronGene sourceNeuron = _neuronGeneList[srcNeuronIdx];            
                    NeuronGene targetNeuron = _neuronGeneList[tgtNeuronIdx];

                    // Test if this connection already exists.
                    if (sourceNeuron.TargetNeurons.Contains(targetNeuron.Id)) 
                    {   // Try again.
                        continue;
                    }
                    return Mutate_AddConnection_CreateConnection(sourceNeuron, 
                                                                 targetNeuron);
                }
            }
            // No valid connection to create was found. 
            // Indicate failure.
            return null;
        }

        /// <summary>
        /// Tests if adding the specified connection would cause a cyclic 
        /// pathway in the network connectivity of the current module.
        /// Returns true if the connection would form a cycle.
        /// </summary>
        private bool IsConnectionCyclic(NeuronGene sourceNeuron, uint targetNeuronId)
        {
            // Quick test. Is connection connecting a neuron to itself.
            if (sourceNeuron.Id == targetNeuronId) {
                return true;
            }

            // Trace backwards through sourceNeuron's source neurons. If 
            // targetNeuron is encountered then it feeds signals into sourceNeuron
            // already and therefore a new connection between sourceNeuron and 
            // targetNeuron would create a cycle.

            // Maintain a set of neurons that have been visited. This allows us
            // to avoid unnecessary re-traversal of the network and detection of
            // cyclic connections.
            HashSet<uint> visitedNeurons = new HashSet<uint>();
            visitedNeurons.Add(sourceNeuron.Id);

            // This search uses an explicitly created stack instead of function 
            // recursion, the logic here is that this may be more efficient 
            // through avoidance of multiple function calls (but not sure).
            Stack<uint> workStack = new Stack<uint>();

            // Push source neuron's sources onto the work stack. We could just 
            // push the source neuron but we choose to cover that test above to 
            // avoid the one extra neuronID lookup that would require.
            foreach (uint neuronId in sourceNeuron.SourceNeurons) {
                workStack.Push(neuronId);
            }

            // While there are neurons to check/traverse.
            while(0 != workStack.Count)
            {
                // Pop a neuron to check from the top of the stack, and then check it.
                uint currNeuronId = workStack.Pop();
                if (visitedNeurons.Contains(currNeuronId)) {
                    // Already visited (via a different route).
                    continue;
                }

                if (currNeuronId == targetNeuronId) {
                    // Target neuron already feeds into the source neuron.
                    return true;
                }

                // Register visit of this node.
                visitedNeurons.Add(currNeuronId);                

                // Push the current neuron's source neurons onto the work stack.
                NeuronGene currNeuron = _neuronGeneList.GetNeuronById(currNeuronId);
                foreach (uint neuronId in currNeuron.SourceNeurons) {
                    workStack.Push(neuronId);
                }
            }

            // Connection not cyclic.
            return false;
        }

		/// <summary>
		/// Creates a connection between a source and target neurons. If this has
        /// already been done during the evolution (even in a different genome) 
        /// then the ID is reused. Called from Mutate_AddConnection.
		/// </summary>
        private ConnectionGene Mutate_AddConnection_CreateConnection(
                NeuronGene sourceNeuron, NeuronGene targetNeuron)
        {
            // We increase the counter of active connections.
            ++_activeConnections;

            uint sourceId = sourceNeuron.Id;
            uint targetId = targetNeuron.Id;

            // Check if a matching mutation has already occured on another genome. 
            // If so then re-use the connection ID.
            ConnectionEndpointsStruct connectionKey = 
                    new ConnectionEndpointsStruct(sourceId, targetId);
            uint? existingConnectionId;
            ConnectionGene newConnectionGene;
            if (_genomeFactory.AddedConnectionBuffer.TryGetValue(connectionKey, 
                    out existingConnectionId))
            {   
                // Create a new connection, re-using the ID from 
                // existingConnectionId, and add it to the Genome.
                newConnectionGene = new ConnectionGene(existingConnectionId.Value,
                                                       sourceId, targetId,
                                                       _genomeFactory.GenerateRandomConnectionWeight(),
                                                       _genomeFactory.CurrentModule);

                // Add the new gene to this genome. We are re-using an ID so we 
                // must ensure the connection gene is inserted into the correct 
                // position (sorted by innovation ID). The ID is most likely an 
                // older one with a lower value than recent IDs, and thus it 
                // probably doesn't belong on the end of the list.
                _connectionGeneList.InsertIntoPosition(newConnectionGene);
            }
            else
            {   
                // Create a new connection with a new ID and add it to the Genome.
                newConnectionGene = new ConnectionGene(_genomeFactory.NextInnovationId(),
                                                       sourceId, targetId,
                                                       _genomeFactory.GenerateRandomConnectionWeight(),
                                                       _genomeFactory.CurrentModule);

                // Add the new gene to this genome. We have a new ID so we can 
                // safely append the gene to the end of the list without risk of
                // breaking the innovation ID sort order.
                _connectionGeneList.Add(newConnectionGene);

                // Register the new connection with the added connection history buffer.
                _genomeFactory.AddedConnectionBuffer.Enqueue(
                        new ConnectionEndpointsStruct(sourceId, targetId),
                        newConnectionGene.InnovationId);
            }

            // Track connections associated with each neuron.
            sourceNeuron.TargetNeurons.Add(targetId);
            targetNeuron.SourceNeurons.Add(sourceId);

            // Update stats.
            _genomeFactory.Stats._mutationCountAddConnection++;
            return newConnectionGene;
        }

        /// <summary>
        /// Mutate a neuron's auxiliary state. Returns true if successfull 
        /// (failure can occur if there are no neuron's 
		/// with auxiliary state).
        /// As always, these mutations may only take place in the active module.
        /// </summary>
        private bool Mutate_NodeAuxState()
        {
            if (_auxStateNeuronCount == 0)
            {   // No nodes with aux state. Indicate failure.
                return false;
            }

            int firstModuleIndex = _neuronGeneList.FirstIndex;
            // int hiddenCount = _neuronGeneList.Count - firstModuleIndex;
            int moduleNeuronCount = _localIn + _localOut + 
                                    _neuronGeneList.Count - firstModuleIndex;

            // ENHANCEMENT: Target for performance improvement.
            // Select neuron to mutate. Depending on the genome type it may be 
            // the case that not all genomes have mutable state, hence we may
            // have to scan for mutable neurons.
            // SingleThrowEven returns an in from 0 to _auxStateNeuronCount - 1.
            int auxStateNodeIdx = RouletteWheel.SingleThrowEven(_auxStateNeuronCount,
                                                                _genomeFactory.Rng);

            IActivationFunctionLibrary fnLib = _genomeFactory.ActivationFnLibrary;
            NeuronGene gene;

            // If all neurons are eligible, we just use our index.
            if (_auxStateNeuronCount == moduleNeuronCount)
            {
                gene = _neuronGeneList[firstModuleIndex + auxStateNodeIdx];
            }
            else
            {
                // Scan for selected gene.
                int i = 0;
                // NOTE: ++i, NOT ++j!!
                // We count how many "i"s we go through before we encounter
                // auxStateNodeIdx + 1 "j"s.
                for (int j = 0; j <= auxStateNodeIdx; ++i)
                {
                    if (fnLib.GetFunction(_neuronGeneList[i].ActivationFnId).AcceptsAuxArgs) {
                        ++j;
                    }
                }
                gene = _neuronGeneList[firstModuleIndex + i - 1];
            }

            Debug.Assert(fnLib.GetFunction(gene.ActivationFnId).AcceptsAuxArgs);

            // Invoke mutation method (specific to each activation function).
            fnLib.GetFunction(gene.ActivationFnId).MutateAuxArgs(
                    gene.AuxState, _genomeFactory.Rng, _genomeFactory.GaussianSampler,
                    _genomeFactory.NeatGenomeParameters.ConnectionWeightRange);
            // Indicate success.
            return true;
        }

        /// <summary>
        /// Attempt to perform a connection deletion mutation. Returns the deleted
        /// connection gene if successful.
		/// If as a result of this process a neuron is no longer connected it is
        /// also removed (this indirect removal of neurons is currently the only
        /// way to delete nodes)
        /// </summary>
        private ConnectionGene Mutate_DeleteConnection()
        {
            if (_connectionGeneList.Count < 2) 
            {   // Either no connections to delete or only one. Indicate failure.
                return null;
            }

            // Select an active connection at random. These are always the last
            // in the list, so we get the index from the back.
            int connectionToDeleteIdx = _connectionGeneList.Count - 1 - 
                                        _genomeFactory.Rng.Next(_activeConnections);
            ConnectionGene connectionToDelete = _connectionGeneList[connectionToDeleteIdx];

            // Delete the connection.
            _connectionGeneList.RemoveAt(connectionToDeleteIdx);
            --_activeConnections;

            // Track connections associated with each neuron and remove neurons 
            // that are no longer connected to anything.

            // Source neuron.
            int srcNeuronIdx = _neuronGeneList.BinarySearch(connectionToDelete.SourceNodeId);
            NeuronGene srcNeuronGene = _neuronGeneList[srcNeuronIdx];
            srcNeuronGene.TargetNeurons.Remove(connectionToDelete.TargetNodeId);

            if (IsNeuronRedundant(srcNeuronGene))
            {   
                // Remove neuron.
                _neuronGeneList.RemoveAt(srcNeuronIdx);

                // Track aux state node count.
                if (_genomeFactory.ActivationFnLibrary.GetFunction(srcNeuronGene.ActivationFnId).AcceptsAuxArgs) {
                    _auxStateNeuronCount--;
                } 
            }

            // Target neuron.
            int tgtNeuronIdx = _neuronGeneList.BinarySearch(connectionToDelete.TargetNodeId);
            NeuronGene tgtNeuronGene = _neuronGeneList[tgtNeuronIdx];
            tgtNeuronGene.SourceNeurons.Remove(connectionToDelete.SourceNodeId);

            // Note. Check that source and target neurons are not the same neuron.
			if (srcNeuronGene != tgtNeuronGene &&
                IsNeuronRedundant(tgtNeuronGene))
            {   
                // Remove neuron.
                _neuronGeneList.RemoveAt(tgtNeuronIdx);

                // Track aux state node count.
                if (_genomeFactory.ActivationFnLibrary.GetFunction(tgtNeuronGene.ActivationFnId).AcceptsAuxArgs)
				{
                    _auxStateNeuronCount--;
                } 
            }

            _genomeFactory.Stats._mutationCountDeleteConnection++;

            // Indicate success.
            return connectionToDelete;
        }

        private void Mutate_ConnectionWeights()
        {
            // Determine the type of weight mutation to perform.
            ConnectionMutationInfo mutationInfo = 
					_genomeFactory.NeatGenomeParameters.ConnectionMutationInfoList.GetRandomItem(_genomeFactory.Rng);
    
            // Get a delegate that performs the mutation specified by mutationInfo.
            // The alternative is to use a switch statement to test the purturbance
            // type on each connection-weight mutation—which creates a lot of 
            // unnecessary branch instructions.
            MutateWeightMethod mutateWeigthMethod = 
                    Mutate_ConnectionWeights_GetMutateWeightMethod(mutationInfo);

            // Perform mutations of the required type:
            // Mutations on a proportion of the connections:
            if (mutationInfo.SelectionType == ConnectionSelectionType.Proportional)
            {
                bool mutationOccured = false;

                // ENHANCEMENT: The fastest approach here depends on 
                // SelectionProportion and the number of connections...
                // ... implement a simple heuristic.
                // We go through the connections backwards, because these are 
                // the non-protected connections of the active module.
                for (int i = _connectionGeneList.Count - 1;
                     i >= _connectionGeneList.Count - _activeConnections; --i)
                {
                    if (_genomeFactory.Rng.NextDouble() < mutationInfo.SelectionProportion)
                    {
                        _connectionGeneList[i].Weight = 
                                mutateWeigthMethod(_connectionGeneList[i].Weight,
                                                   mutationInfo);
                        mutationOccured = true;
                    }
                }

                // If no mutations happened (and there are connections, as there
                // always should), then perform one mutation at random.
                if (!mutationOccured && 0 != _activeConnections)
                {   
                    ConnectionGene connectionGene = _connectionGeneList[
                            _connectionGeneList.Count - 1 - 
                            _genomeFactory.Rng.Next(_activeConnections)];
                    connectionGene.Weight = 
                            mutateWeigthMethod(connectionGene.Weight, mutationInfo);
                }
            }
            else // if (mutationInfo.SelectionType == ConnectionSelectionType.FixedQuantity)
            // Mutations on a fixed number of connections:
            {
                // Determine how many mutations to perform. In case there are 
                // fewer connections than intended mutations.
                int mutations = Math.Min(_activeConnections, 
                                         mutationInfo.SelectionQuantity);
                // Again, this should NEVER be the case!
                if (0 == mutations) {
                    return;
                }

                // We want connections that have not yet been mutated. If this
                // happens, we simply try again. This should not be too slow, 
                // since at most we will aim for 3 mutations (1, 2 or 3 in the
                // current version).

                // Ensure all IsMutated flags are reset prior to entering the 
                // loop. Not doing so introduces the possibility of getting 
                // stuck in the inner while loop forever, as well as preventing 
                // previously mutated connections from being mutated again.

                // NOTICE! ResetIsMutatedFlags only resets non-protected 
                // connections in the active module. If other connections are
                // intended to be eligible for weight mutations they should be
                // included there.
                _connectionGeneList.ResetIsMutatedFlags();

                int current_mutated = 0;
                while (current_mutated < mutations)
                {
                    // Pick an index at random.
                    int index = _connectionGeneList.Count - 1 - 
                                _genomeFactory.Rng.Next(_activeConnections);

                    // If the connection is not yet mutated, proceed:
                    if (_connectionGeneList[index].IsMutated == false)
                    {
                        // Mutate the gene at 'index'.
                        _connectionGeneList[index].Weight = 
                            mutateWeigthMethod(_connectionGeneList[index].Weight, 
                                mutationInfo);
                        _connectionGeneList[index].IsMutated = true;  

                        ++current_mutated;
                    }   
                }
            }
            _genomeFactory.Stats._mutationCountConnectionWeights++;
        }

        delegate double MutateWeightMethod(double weight, ConnectionMutationInfo info);

        /// <summary>
        /// Method that returns a delegate to perform connection weight mutation
        /// based on the provided ConnectionMutationInfo object. Re-using such 
        /// a delegate obviates the need to test the type of mutation on each 
        /// weight mutation operation, thus eliminating many branch execution operations.
        /// </summary>
        private MutateWeightMethod Mutate_ConnectionWeights_GetMutateWeightMethod(
                ConnectionMutationInfo mutationInfo)
        {
            // ENHANCEMENT: Can we use something akin to a closure here to 
            // package up mutation params with the delegate code?
            switch(mutationInfo.PerturbanceType)
            {
                case ConnectionPerturbanceType.JiggleUniform:
                {
					// Maybe it would be best to define this function elsewere 
                    // (like in the case "Reset" below)
					return delegate(double weight, ConnectionMutationInfo info)
                    {
                        return CapWeight(weight + 
                                         (((_genomeFactory.Rng.NextDouble()*2.0) - 1.0) * 
                                          info.PerturbanceMagnitude));
                    };
                }
                case ConnectionPerturbanceType.JiggleGaussian:
                {
                    return delegate(double weight, ConnectionMutationInfo info)
                    {
                        return CapWeight(weight + 
                                         _genomeFactory.SampleGaussianDistribution(0, info.Sigma));
                    };
                }
                case ConnectionPerturbanceType.Reset:
                {
                    return delegate {
                        return _genomeFactory.GenerateRandomConnectionWeight();
                    };
                }
            }
            throw new SharpNeatException("Unexpected ConnectionPerturbanceType");
        }

        /// <summary>
        /// Takes a value and makes sure it is within a given range.
        /// </summary>
        private double CapWeight(double weight)
        {
            double weightRange = _genomeFactory.NeatGenomeParameters.ConnectionWeightRange;
            if (weight > weightRange) 
            {
                weight = weightRange;
            }
            else if (weight < -weightRange) 
            {
                weight = -weightRange;
            }
            return weight;
        }

        /// <summary>
        /// Redundant neurons are hidden neurons with no connections attached to them.
        /// </summary>
        private bool IsNeuronRedundant(NeuronGene neuronGene)
        {
            if (neuronGene.NodeType != NodeType.Hidden) {
                return false;
            }
            return (0 == (neuronGene.SourceNeurons.Count + neuronGene.TargetNeurons.Count));
        }

        #endregion

        #region Private Methods [Genome Comparison]

        /// <summary>
		/// Correlates the ConnectionGenes from two distinct genomes based upon 
        /// gene innovation numbers. That is, organizes the connections as 
        /// matching or disjoint.
        /// </summary>
        private static CorrelationResults CorrelateConnectionGeneLists(
                ConnectionGeneList list1Full, 
                ConnectionGeneList list2Full)
        {
            // We are only interested in the active connections!
            ConnectionGeneList list1 = new ConnectionGeneList();
            ConnectionGeneList list2 = new ConnectionGeneList();
            for (int i = list1Full.FirstId; i < list1Full.Count; ++i)
            {
                list1.Add(list1Full[i]);
            }
            for (int i = list2Full.FirstId; i < list2Full.Count; ++i)
            {
                list2.Add(list2Full[i]);
            }

            // If none of the connections match up then the number of correlation 
            // items will be the sum of the two connections list counts, so this
            // is a maximum value used for the size
            CorrelationResults correlationResults = 
                    new CorrelationResults(list1.Count + list2.Count);

        //----- Test for special cases.
            int list1Count = list1.Count;
            int list2Count = list2.Count;
            if (0 == list1Count && 0 == list2Count)
            {   // Both lists are empty!
                return correlationResults;
            }

            if (0 == list1Count)
            {   // All list2 genes are excess.
                correlationResults.CorrelationStatistics.ExcessConnectionGeneCount = list2Count;
                foreach (ConnectionGene connectionGene in list2)
                {
                    correlationResults.CorrelationItemList.Add(
                            new CorrelationItem(CorrelationItemType.Excess, 
                                                null, connectionGene));
                }
                return correlationResults;
            }

            if (0 == list2Count)
            {   // All list1 genes are excess.
                correlationResults.CorrelationStatistics.ExcessConnectionGeneCount = list1Count;
                foreach (ConnectionGene connectionGene in list1)
                {
                    correlationResults.CorrelationItemList.Add(
                            new CorrelationItem(CorrelationItemType.Excess, 
                                                connectionGene, null));
                }
                return correlationResults;
            }

        //----- Both connection genes lists contain genes - compare their contents.
            int list1Idx = 0;
            int list2Idx = 0;
            ConnectionGene connectionGene1 = list1[list1Idx];
            ConnectionGene connectionGene2 = list2[list2Idx];
            for (;;)
            {
                if (connectionGene2.InnovationId < connectionGene1.InnovationId)
                {   
                    // connectionGene2 is disjoint.
                    correlationResults.CorrelationItemList.Add(
                            new CorrelationItem(CorrelationItemType.Disjoint, 
                                                null, connectionGene2));
                    correlationResults.CorrelationStatistics.DisjointConnectionGeneCount++;

                    // Move to the next gene in list2.
                    list2Idx++;
                }
                else if (connectionGene1.InnovationId == connectionGene2.InnovationId)
                {
					// This is a match!
                    correlationResults.CorrelationItemList.Add(
                            new CorrelationItem(CorrelationItemType.Match, 
                                                connectionGene1, connectionGene2));
                    correlationResults.CorrelationStatistics.ConnectionWeightDelta += 
                            Math.Abs(connectionGene1.Weight - connectionGene2.Weight);
                    correlationResults.CorrelationStatistics.MatchingGeneCount++;

                    // Move to the next gene in both lists.
                    list1Idx++;
                    list2Idx++;
                }
                else // (connectionGene2.InnovationId > connectionGene1.InnovationId)
                {   
                    // connectionGene1 is disjoint.
                    correlationResults.CorrelationItemList.Add(
                            new CorrelationItem(CorrelationItemType.Disjoint, 
                                                connectionGene1, null));
                    correlationResults.CorrelationStatistics.DisjointConnectionGeneCount++;

                    // Move to the next gene in list1.
                    list1Idx++;
                }
                
                // Check if we have reached the end of one (or both) of the lists. If we have reached the end of both then 
                // although we enter the first 'if' block it doesn't matter because the contained loop is not entered if both 
                // lists have been exhausted.
                if (list1Count == list1Idx)
                {   
                    // All remaining list2 genes are excess.
                    for(; list2Idx < list2Count; list2Idx++)
                    {
                        correlationResults.CorrelationItemList.Add(
                                new CorrelationItem(CorrelationItemType.Excess, 
                                                    null, list2[list2Idx]));
                        correlationResults.CorrelationStatistics.ExcessConnectionGeneCount++;
                    }
                    return correlationResults;
                }

                if (list2Count == list2Idx)
                {
                    // All remaining list1 genes are excess.
                    for (; list1Idx < list1Count; list1Idx++)
                    {
                        correlationResults.CorrelationItemList.Add(
                                new CorrelationItem(CorrelationItemType.Excess, 
                                                    list1[list1Idx], null));
                        correlationResults.CorrelationStatistics.ExcessConnectionGeneCount++;
                    }
                    return correlationResults;
                }

                connectionGene1 = list1[list1Idx];
                connectionGene2 = list2[list2Idx];
            }
        }

        #endregion

        #region Private Methods [Initialisation]

        /// <summary>
        /// Modified version, so it only counts aux nodes in the active module!
        /// </summary>
        private int CountAuxStateNodes()
        {
            IActivationFunctionLibrary fnLib = _genomeFactory.ActivationFnLibrary;
            int auxNodeCount = 0;
            for (int i = _neuronGeneList.FirstIndex; i < _neuronGeneList.Count; ++i)
            {
                if (fnLib.GetFunction(_neuronGeneList[i].ActivationFnId).AcceptsAuxArgs) {
                    auxNodeCount++;
                }
            }
            return auxNodeCount;
        }

        /// <summary>
        /// Rebuild the connection info on each neuron gene. This info is created
        /// by genome factories and maintained during evolution, but requires 
        /// building after loading genomes from storage.
        /// </summary>
        private void RebuildNeuronGeneConnectionInfo()
        {
            // Ensure data is cleared down.
            for (int i = 0; i < _neuronGeneList.Count; i++)
            {
                NeuronGene nGene = _neuronGeneList[i];
                nGene.SourceNeurons.Clear();
                nGene.TargetNeurons.Clear();
            }

            // Loop connections and register the source and target neurons in 
            // each corresponding NeuronGene
            for (int i = 0; i < _connectionGeneList.Count; i++)
            {
                ConnectionGene cGene = _connectionGeneList[i];
                NeuronGene srcNeuronGene = _neuronGeneList.GetNeuronByIdAll(cGene.SourceNodeId);
                NeuronGene tgtNeuronGene = _neuronGeneList.GetNeuronByIdAll(cGene.TargetNodeId);
                srcNeuronGene.TargetNeurons.Add(cGene.TargetNodeId);
                tgtNeuronGene.SourceNeurons.Add(cGene.SourceNodeId);
            }
        }

        #endregion

        #region Private Methods [Debug Code / Integrity Checking]

        /// <summary>
        /// Performs an integrity check on the genome's internal data.
        /// Returns true if OK.
        /// </summary>
        public bool PerformIntegrityCheck()
        {
            // Checks the genome's class type (can only do this if we have a genome factory).
            if (null != _genomeFactory && !_genomeFactory.CheckGenomeType(this)) {
                Debug.WriteLine(string.Format("Invalid genome class type [{0}]", 
                                              this.GetType().Name));
                return false;
            }

            // Checks number of neuron genes in the active module.
            int count = _neuronGeneList.Count - _neuronGeneList.FirstIndex;
            
            // We will always have at least a local input and a local output.
            if (count < 2) {
                Debug.WriteLine(string.Format("NeuronGeneList has less than the " + 
                                              "minimum number of neuron genes [{0}]", count));
                return false;
            }

            // Checks bias neuron. This is not so useful in ESP.
            /*
            if (NodeType.Bias != _neuronGeneList[0].NodeType) {
                Debug.WriteLine("Missing bias gene");
                return false;
            }
            if (0u != _neuronGeneList[0].InnovationId) {
                Debug.WriteLine(string.Format("Bias neuron ID != 0. [{0}]", 
                                              _neuronGeneList[0].InnovationId));
                return false;
            }
            */

            // Checks local input neurons.
            // prevId is set to the first Id in the module - 1, and then we
            // check that all elements have a higher Id than the one before.
            uint prevId = _neuronGeneList[_neuronGeneList.FirstIndex].Id - 1;
            for (int i = _neuronGeneList.FirstIndex;
                 i < _neuronGeneList.FirstIndex + _localIn; ++i)
            {
                // Checks type
                if (NodeType.Local_Input != _neuronGeneList[i].NodeType) {
                    Debug.WriteLine(string.Format(
                            "Invalid neuron gene type. Expected Local_Input, got [{0}]", 
                            _neuronGeneList[i].NodeType));
                    return false;
                }
                // Checks Id order
                if (_neuronGeneList[i].Id <= prevId) {
                    Debug.WriteLine("Input neuron gene is out of order and/or a duplicate.");
                    return false;
                }
                prevId = _neuronGeneList[i].Id;
            }

            // Checks local output neurons. prevId is taken from the last
            // local input.
            for (int i = _neuronGeneList.FirstIndex + _localIn;
                 i < _neuronGeneList.FirstIndex + _localIn + _localOut; ++i)
            {
                // Checks type
                if (NodeType.Local_Output != _neuronGeneList[i].NodeType) {
                    Debug.WriteLine(string.Format(
                            "Invalid neuron gene type. Expected Local_Output, got [{0}]", 
                            _neuronGeneList[i].NodeType));
                    return false;
                }
                // Checks Id order
                if (_neuronGeneList[i].Id <= prevId) {
                    Debug.WriteLine("Output neuron gene is out of order and/or a duplicate.");
                    return false;
                }
                prevId = _neuronGeneList[i].Id;
            }

            // Checks hidden neurons.
            // All remaining neurons should be hidden neurons.
            for (int i = _neuronGeneList.FirstIndex + _localIn + _localOut;
                i < _neuronGeneList.Count; ++i)
            {
                // Checks type
                if (NodeType.Hidden != _neuronGeneList[i].NodeType) {
                    Debug.WriteLine(string.Format(
                            "Invalid neuron gene type. Expected Hidden, got [{0}]", 
                            _neuronGeneList[i].NodeType));
                    return false;
                }
                // Checks Id order
                if (_neuronGeneList[i].InnovationId <= prevId) {
                    Debug.WriteLine("Hidden neuron gene is out of order and/or a duplicate.");
                    return false;
                }
                prevId = _neuronGeneList[i].InnovationId;
            }

            // Count nodes with aux state (can only do this if we have a genome factory).
            // This should be updated to account only aux in the active module
            // which is how they are counted here, but perhaps not yet in the
            // factory.
            /*
            if (null != _genomeFactory)
            {
                IActivationFunctionLibrary fnLib = _genomeFactory.ActivationFnLibrary;
                int auxStateNodeCount = 0;
                for (int i = 0; i < count; i++) {
                    if (fnLib.GetFunction(_neuronGeneList[i].ActivationFnId).AcceptsAuxArgs) {
                        auxStateNodeCount++;
                    }
                }
                if (_auxStateNeuronCount != auxStateNodeCount) {
                    Debug.WriteLine("Aux state neuron count is incorrect.");
                    return false;
                }
            }
            */

            // Check connection genes.
            count = _connectionGeneList.Count;
            if (count < 4) 
            {   // At least four connections are required (local input,
                // local output, the auxiliary connection to power evolving 
                // modules and one active connection). 
                // (A) Connectionless genomes are pointless and 
                // (B) Connections form the basis for defining a genome's 
                // position in the encoding space.
                // Without a position speciation will be sub-optimal and may 
                // fail (depending on the speciation strategy).
                Debug.WriteLine("Missing essential connection genes.");
                return false;
            }

            Dictionary<ConnectionEndpointsStruct, object> endpointDict = 
                    new Dictionary<ConnectionEndpointsStruct,object>(count);
            
            // Initialise with the first connection's details.
            ConnectionGene connectionGene = _connectionGeneList[0];
            int module = connectionGene.ModuleId;
            prevId = connectionGene.InnovationId;
            endpointDict.Add(new ConnectionEndpointsStruct(connectionGene.SourceNodeId, 
                                                           connectionGene.TargetNodeId), null);

            // Loop over remaining connections.
            for (int i = 1; i < count; ++i)
            {
                connectionGene = _connectionGeneList[i];
                // We only enforce order within each module.
                if (module == connectionGene.ModuleId)
                {
                    if (connectionGene.InnovationId <= prevId) {
                        Debug.WriteLine("Connection gene is out of order and/or a duplicate.");
                        return false;
                    }                    
                }
                else
                {
                    module = connectionGene.ModuleId;
                }

                ConnectionEndpointsStruct key = 
                        new ConnectionEndpointsStruct(connectionGene.SourceNodeId, 
                                                      connectionGene.TargetNodeId);
                if (endpointDict.ContainsKey(key)) {
                    Debug.WriteLine("Connection gene error. A connection between the specified endpoints already exists.");
                    return false;
                }

                endpointDict.Add(key, null);
                prevId = connectionGene.InnovationId;
            }

            // Checks each neuron gene's list of source and target neurons.
            // Init connection info per neuron.
            int nCount = _neuronGeneList.Count - _inHiddenModules;
            Dictionary<uint,NeuronConnectionInfo> conInfoByNeuronId = 
                    new Dictionary<uint,NeuronConnectionInfo>(count);
            int j = 0;
            for (int i = 0; i < nCount; i++)
            {
                // This way of avoiding the hidden modules seems a bit
                // cumbersome. There must be a cleverer way.
                j = i;
                if (j > _neuronGeneList.LastBase)
                {
                    j += _inHiddenModules;
                }
                NeuronConnectionInfo conInfo = new NeuronConnectionInfo();
                conInfo._srcNeurons = new HashSet<uint>();
                conInfo._tgtNeurons = new HashSet<uint>();
                conInfoByNeuronId.Add(_neuronGeneList[j].Id, conInfo);
            }

            // Compile connectivity info. We only use connections that are either
            // protected (they affect the base) or active.
            int cCount = _connectionGeneList.Count;
            module = _connectionGeneList[_connectionGeneList.Count - 1].ModuleId;
            for (int i = 0; i < cCount; ++i)
            {
                // Only consider active connections or protected connections!
                if (_connectionGeneList[i].ModuleId == module &&
                    _connectionGeneList[i].Protected)
                {
                    ConnectionGene cGene = _connectionGeneList[i];
                    conInfoByNeuronId[cGene.SourceNodeId]._tgtNeurons.Add(cGene.TargetNodeId);
                    conInfoByNeuronId[cGene.TargetNodeId]._srcNeurons.Add(cGene.SourceNodeId);                    
                }
            }

            // Compare connectivity info with that recorded in each NeuronGene.
            j = 0;
            for (int i = 0; i < nCount; i++)
            {
                // This way of avoiding the hidden modules seems a bit
                // cumbersome. There must be a cleverer way.
                j = i;
                if (j > _neuronGeneList.LastBase)
                {
                    j += _inHiddenModules;
                }

                NeuronGene nGene = _neuronGeneList[j];
                NeuronConnectionInfo conInfo = conInfoByNeuronId[nGene.Id];

                // Check source node count.
                if (nGene.SourceNeurons.Count != conInfo._srcNeurons.Count) {
                    Debug.WriteLine("NeuronGene has incorrect number of source neurons recorded.");
                    return false;
                }

                // Check target node count.
                if (nGene.TargetNeurons.Count != conInfo._tgtNeurons.Count) {
                    Debug.WriteLine("NeuronGene has incorrect number of target neurons recorded.");
                    return false;
                }

                // Check that the source node IDs match up.
                foreach (uint srcNeuronId in nGene.SourceNeurons)
                {
                    if (!conInfo._srcNeurons.Contains(srcNeuronId)) {
                        Debug.WriteLine("NeuronGene has incorrect list of source neurons recorded.");
                        return false;
                    }
                }

                // Check that the target node IDs match up.
                foreach (uint tgtNeuronId in nGene.TargetNeurons)
                {
                    if (!conInfo._tgtNeurons.Contains(tgtNeuronId)) {
                        Debug.WriteLine("NeuronGene has incorrect list of target neurons recorded.");
                        return false;
                    }
                }
            }

            // Check that network is acyclic if we are evolving feedforward only networks 
            // (can only do this if we have a genome factory).
            if (null != _genomeFactory && _genomeFactory.NeatGenomeParameters.FeedforwardOnly)
            {
                if (CyclicNetworkTest.IsNetworkCyclic(this)) {
                    Debug.WriteLine("Feedforward only network has one or more cyclic paths.");
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Inner Classes

        /// <summary>
        /// Holds sets of source and target neurons for a given neuron.
        /// </summary>
        struct NeuronConnectionInfo
        {
            /// <summary>
            /// Gets a set of IDs for the source neurons that directly connect into a given neuron.
            /// </summary>
            public HashSet<uint> _srcNeurons;
            /// <summary>
            /// Gets a set of IDs for the target neurons that a given neuron directly connects out to.
            /// </summary>
            public HashSet<uint> _tgtNeurons;
        }

        #endregion

        #region INetworkDefinition Members

        /// <summary>
        /// Gets the number of input nodes. This does not include the bias node which is always present.
        /// </summary>
        public int InputNodeCount
        {
            get { return _inputNeuronCount; }
        }

        /// <summary>
        /// Gets the number of output nodes.
        /// </summary>
        public int OutputNodeCount
        {
            get { return _outputNeuronCount; }
        }

        /// <summary>
        /// Gets the network's activation function library. The activation function at each node is 
        /// represented by an integer ID, which refers to a function in this activation function library.
        /// </summary>
        public IActivationFunctionLibrary ActivationFnLibrary 
        {
            get { return _genomeFactory.ActivationFnLibrary; }
        }

        /// <summary>
        /// Gets a bool flag that indicates if the network is acyclic.
        /// </summary>
        public bool IsAcyclic 
        { 
            get { return _genomeFactory.NeatGenomeParameters.FeedforwardOnly;  }
        }

        /// <summary>
        /// Gets the list of network nodes.
        /// </summary>
        public INodeList NodeList
        {
            get { return _neuronGeneList; }
        }

        /// <summary>
        /// Gets the list of network connections.
        /// </summary>
        public IConnectionList ConnectionList
        {
            get { return _connectionGeneList; }
        }

        /// <summary>
        /// Gets NetworkConnectivityData for the network.
        /// </summary>
        public NetworkConnectivityData GetConnectivityData()
        {
            if (null != _networkConnectivityData) 
            {   // Return cached data.
                return _networkConnectivityData;
            }

            int nodeCount = _neuronGeneList.Count;
            NodeConnectivityData[] nodeConnectivityDataArr = 
                    new NodeConnectivityData[nodeCount];
            Dictionary<uint,NodeConnectivityData> nodeConnectivityDataById = 
                    new Dictionary<uint,NodeConnectivityData>(nodeCount);

            // NeatGenome(s) have connectivity data pre-calculated, as such we 
            // point to this data rather than copying or rebuilding it. 
            // Essentially NetworkConnectivityData becomes a useful general-purpose
            // layer over the connectivity data.
            for (int i = 0; i < nodeCount; i++)
            {
                NeuronGene neuronGene = _neuronGeneList[i];
                NodeConnectivityData ncd = 
                        new NodeConnectivityData(neuronGene.Id, neuronGene.SourceNeurons,
                                                 neuronGene.TargetNeurons);
                nodeConnectivityDataArr[i] = ncd;
                nodeConnectivityDataById.Add(neuronGene.Id, ncd);
            }
            _networkConnectivityData = 
                    new NetworkConnectivityData(nodeConnectivityDataArr, nodeConnectivityDataById);
            return _networkConnectivityData;
        }

        #endregion
    }
}

using UnityEngine;
using System.Collections;
using SharpNeat.Domains;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.Genomes.Neat;
using SharpNeat.Decoders;
using System.Collections.Generic;
using System.Xml;
using SharpNeat.Core;
using SharpNeat.Phenomes;
using SharpNeat.Decoders.Neat;
using SharpNeat.DistanceMetrics;
using SharpNeat.SpeciationStrategies;
using SharpNeat.EvolutionAlgorithms.ComplexityRegulation;
using SharpNEAT.Core;
using System;
using System.IO;

public class SimpleExperiment : INeatExperiment
{
	EspNeatOptimizer _optimizer;
	NeatEvolutionAlgorithmParameters _eaParams;
	NeatGenomeParameters _neatGenomeParams;
	NetworkActivationScheme _activationScheme;
	string _name;
	string _complexityRegulationStr;
	string _description;
	int _populationSize;
	int _specieCount;
	int _inputCount;
	int _outputCount;
	int? _complexityThreshold;

	public SimpleExperiment(EspNeatOptimizer optimizer)
	{
		_optimizer = optimizer;
	}

	public string Name
	{
		get { return _name; }
	}

	public string Description
	{
		get { return _description; }
	}

	public int InputCount
	{
		get { return _inputCount; }
	}

	public int OutputCount
	{
		get { return _outputCount; }
	}

	public int DefaultPopulationSize
	{
		get { return _populationSize; }
	}

	public NeatEvolutionAlgorithmParameters NeatEvolutionAlgorithmParameters
	{
		get { return _eaParams; }
	}

	public NeatGenomeParameters NeatGenomeParameters
	{
		get { return _neatGenomeParams; }
	}

	public void Initialize(string name, XmlElement xmlConfig)
	{
		Initialize(name, xmlConfig, 6, 3);
	}

	//Loads the experiment parameters from the xml file "experiment.config" in the Resources folder
	public void Initialize(string name, XmlElement xmlConfig, int input, int output)
	{
		_name = name;
		_populationSize = XmlUtils.GetValueAsInt(xmlConfig, "PopulationSize");
		_specieCount = XmlUtils.GetValueAsInt(xmlConfig, "SpecieCount");
		_activationScheme = ExperimentUtils.CreateActivationScheme(xmlConfig, "Activation");
		_complexityRegulationStr = XmlUtils.TryGetValueAsString(xmlConfig, "ComplexityRegulationStrategy");
		_complexityThreshold = XmlUtils.TryGetValueAsInt(xmlConfig, "ComplexityThreshold");
		_description = XmlUtils.TryGetValueAsString(xmlConfig, "Description");

		_eaParams = new NeatEvolutionAlgorithmParameters();
		_eaParams.SpecieCount = _specieCount;
		_neatGenomeParams = new NeatGenomeParameters();
		_neatGenomeParams.FeedforwardOnly = _activationScheme.AcyclicNetwork;

		_inputCount = input;
		_outputCount = output;
	}

	public void SavePopulation(XmlWriter xw, IList<NeatGenome> genomeList)
	{
		NeatGenomeXmlIO.WriteComplete(xw, genomeList, false);
	}

	public IGenomeDecoder<NeatGenome, IBlackBox> CreateGenomeDecoder()
	{
		return new NeatGenomeDecoder(_activationScheme);
	}

	public IGenomeFactory<NeatGenome> CreateGenomeFactory()
	{
		return new NeatGenomeFactory(InputCount, OutputCount, _neatGenomeParams, 
			_optimizer);
	}

	/// <summary>
	/// Creates and returns a NeatEvolutionAlgorithm object ready for running 
	/// the NEAT algorithm/search. An initial genome population is read from 
	/// the file provided. If this file does not exist, it is created and 
	/// the genomeFactory builds an initial population.
	/// </summary>
	public NeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm(string fileName)
	{
		List<NeatGenome> genomeList = null;
		IGenomeFactory<NeatGenome> genomeFactory = CreateGenomeFactory();
		genomeList = LoadPopulationFromFile(fileName);
		if (genomeList == null)
		{
			genomeList = CreateNewGenome(genomeFactory);
		}
		return CreateEvolutionAlgorithm(genomeFactory, genomeList);
	}

	public List<NeatGenome> LoadPopulation(XmlReader xr)
	{
		NeatGenomeFactory genomeFactory = (NeatGenomeFactory)CreateGenomeFactory();
		return NeatGenomeXmlIO.ReadCompleteGenomeList(xr, false, genomeFactory);
	}

	public NeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm()
	{
		return CreateEvolutionAlgorithm(_populationSize);
	}

	public NeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm(int populationSize)
	{
		IGenomeFactory<NeatGenome> genomeFactory = CreateGenomeFactory();
		List<NeatGenome> genomeList = genomeFactory.CreateGenomeList(populationSize, 0);
		return CreateEvolutionAlgorithm(genomeFactory, genomeList);
	}

	public NeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm(IGenomeFactory<NeatGenome> genomeFactory, 
		                                                               List<NeatGenome> genomeList)
	{
		IDistanceMetric distanceMetric = new ManhattanDistanceMetric(1.0, 0.0, 10.0);
		ISpeciationStrategy<NeatGenome> speciationStrategy = new KMeansClusteringStrategy<NeatGenome>(distanceMetric);
		IComplexityRegulationStrategy complexityRegulationStrategy = 
				ExperimentUtils.CreateComplexityRegulationStrategy(_complexityRegulationStr, _complexityThreshold);
		NeatEvolutionAlgorithm<NeatGenome> ea = new NeatEvolutionAlgorithm<NeatGenome>(_eaParams, speciationStrategy, 
																					   complexityRegulationStrategy);
		// Creates black box evaluator       
		SimpleEvaluator evaluator = new SimpleEvaluator(_optimizer);
		IGenomeDecoder<NeatGenome, IBlackBox> genomeDecoder = CreateGenomeDecoder();
		IGenomeListEvaluator<NeatGenome> innerEvaluator = new UnityParallelListEvaluator<NeatGenome, IBlackBox>(
				genomeDecoder, evaluator, _optimizer);
		IGenomeListEvaluator<NeatGenome> selectiveEvaluator = new SelectiveGenomeListEvaluator<NeatGenome>(
				innerEvaluator, SelectiveGenomeListEvaluator<NeatGenome>.CreatePredicate_OnceOnly());
		ea.Initialize(innerEvaluator, genomeFactory, genomeList);
		return ea;
	}

	public void SetDecoderInManualEA(NeatManualEvolution<NeatGenome> manual_ea)
	{
		IGenomeDecoder<NeatGenome, IBlackBox> genomeDecoder = CreateGenomeDecoder();
		manual_ea.Decoder = genomeDecoder;
	}

	List<NeatGenome> LoadPopulationFromFile(string fileName)
	{
		List<NeatGenome> returnList = null;
		if (File.Exists(fileName))   
		{
			returnList = ReadXMLfile(fileName);
		}
		return returnList;
	}

	List<NeatGenome> ReadXMLfile(string fileName)
	{
		using (XmlReader xr = XmlReader.Create(fileName))
		{
			// LoadPopulation is a method imposed by the interface.
			return LoadPopulation(xr);
		}        
	}

	List<NeatGenome> CreateNewGenome(IGenomeFactory<NeatGenome> genomeFactory)
	{
		Utility.Log("Saved genome not found, creating new files.");
		return genomeFactory.CreateGenomeList(_populationSize, 0);
	}
}

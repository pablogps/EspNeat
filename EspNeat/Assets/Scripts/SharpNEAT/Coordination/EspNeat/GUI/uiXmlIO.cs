using UnityEngine;
using System.Collections.Generic;
using SharpNeat.Genomes.Neat;
using System.Xml;
using SharpNeat.Utility;

namespace SharpNeat.Coordination
{
	/// <summary>
	/// This class reads and writes the label names used in UI to a file.
	/// </summary>
	public static class uiXmlIO {
		private static string namesPath;
		private static string hierarchyPath;
        private static NeatGenome genome;
        private static string __ElemRoot = "Root";
        private static string __ElemInputs = "Input_Labels";
        private static string __ElemOutputs = "Output_Labels";
        private static string __ElemModules = "Module_Labels";
        private static string __Input = "Input";
        private static string __Output = "Output";
        private static string __Module = "Module";
        private static string __Index = "Index";
        private static string __ID = "ID";
		private static string __Label = "Label";
		private static string __RegulationModule = "Regulation_module";
		private static string __Contains = "Contains";

        public static string NamesPath
        {
            set { namesPath = value; }
		}

		public static string HierarchyPath
		{
			set { hierarchyPath = value; }
		}

        public static NeatGenome Genome
        {
            set { genome = value; }
        }

        /// <summary>
        /// Reads labels from the Xml file. Note the Root level is required!
        /// </summary>
        public static void ReadLabels(out List<string> inputLabels,
                                      out List<string> outputLabels,
                                      out Dictionary<int, string> moduleLabels)
        {
            inputLabels = new List<string>();
            outputLabels = new List<string>();
            moduleLabels = new Dictionary<int, string>();

            // We checked that the path exists before calling this method.
            // If the path does not exist, creates an exception.
            using (XmlReader xr = XmlReader.Create(namesPath))
            {
                // Find <Root>
                XmlIoUtils.MoveToElement(xr, false, __ElemRoot);

                // Find <Input_Labels>.
                XmlIoUtils.MoveToElement(xr, true, __ElemInputs);
                // Create a reader over the <Input Labels> sub-tree.
                using(XmlReader xrSubtree = xr.ReadSubtree())
                {
                    // Re-scan for the root <Input_Labels> element.
                    XmlIoUtils.MoveToElement(xrSubtree, false);
                    // Move to first input elem.
                    XmlIoUtils.MoveToElement(xrSubtree, true, __Input);
                    // Read input elements.
                    do
                    {
                        inputLabels.Add(xrSubtree.GetAttribute(__Label));
                    } 
                    while(xrSubtree.ReadToNextSibling(__Input));                    
                }

                // Find <Output_Labels>.
                XmlIoUtils.MoveToElement(xr, false, __ElemOutputs);
                // Create a reader over the <Output_Labels> sub-tree.
                using(XmlReader xrSubtree = xr.ReadSubtree())
                {
                    // Re-scan for the root <Input Labels> element.
                    XmlIoUtils.MoveToElement(xrSubtree, false);
                    // Move to first element.
                    XmlIoUtils.MoveToElement(xrSubtree, true, __Output);
                    // We have at least one output.
                    // Read output elements.
                    do
                    {
                        outputLabels.Add(xrSubtree.GetAttribute(__Label));
                    } 
                    while(xrSubtree.ReadToNextSibling(__Output));                   
                }

                // Find <Module_Labels>.
                XmlIoUtils.MoveToElement(xr, false, __ElemModules);
                // Create a reader over the <Module_Labels> sub-tree.
                using(XmlReader xrSubtree = xr.ReadSubtree())
                {
                    // Re-scan for the root <Module_Labels> element.
                    XmlIoUtils.MoveToElement(xrSubtree, false);
                    // Move to first element.
                    string localName = XmlIoUtils.MoveToElement(xrSubtree, true);
                    if(localName == __Module)
                    {
                        // We have at least one module.
                        // Read module elements.
                        do
                        {
                            string paco = xrSubtree.GetAttribute(__ID);
                            paco = xrSubtree.GetAttribute(__Label);
							int id = XmlIoUtils.ReadAttributeAsInt(xrSubtree, __ID);
                            moduleLabels.Add(id, xrSubtree.GetAttribute(__Label));
                        } 
                        while(xrSubtree.ReadToNextSibling(__Module));
                    }                    
                }
            }
        }

        /// <summary>
        /// Writes labels to the Xml file. Note the Root level is required!
        /// </summary>
        public static void WriteLabels(List<string> inputLabels,
                                       List<string> outputLabels,
                                       Dictionary<int, string> moduleLabels)
        {
            XmlWriterSettings _xwSettings = new XmlWriterSettings();
            _xwSettings.Indent = true;

            using (XmlWriter xw = XmlWriter.Create(namesPath, _xwSettings))
            {
                // A root element is needed for correct reading.
                // <Root>
                xw.WriteStartElement(__ElemRoot);

                // <Inputs>
                xw.WriteStartElement(__ElemInputs);
                for (int i = 0; i < inputLabels.Count; ++i)
                {
                    xw.WriteStartElement(__Input);
                    xw.WriteAttributeString(__Index, i.ToString());
                    xw.WriteAttributeString(__Label, inputLabels[i]);
                    xw.WriteEndElement();                    
                }
                // </Inputs>
                xw.WriteEndElement();

                // <Outputs>
                xw.WriteStartElement(__ElemOutputs);
                for (int i = 0; i < outputLabels.Count; ++i)
                {
                    xw.WriteStartElement(__Output);
                    xw.WriteAttributeString(__Index, i.ToString());
                    xw.WriteAttributeString(__Label, outputLabels[i]);
                    xw.WriteEndElement();                    
                }
                // </Outputs>
                xw.WriteEndElement();

                // <Modules>
                xw.WriteStartElement(__ElemModules);
                foreach (KeyValuePair<int, string> entry in moduleLabels)
                {
                    xw.WriteStartElement(__Module);
                    xw.WriteAttributeString(__ID, entry.Key.ToString());
                    xw.WriteAttributeString(__Label, entry.Value);
                    xw.WriteEndElement();
                }
                // </Modules>
                xw.WriteEndElement();

                // </Root>
                xw.WriteEndElement();
            }            
        }

		/// <summary>
		/// Writes the genome hierarchy to the Xml file. Note the Root level is required!
		/// </summary>
		public static void WriteHierarchy(Dictionary<int, List<int>> hierarchy)
		{
			XmlWriterSettings _xwSettings = new XmlWriterSettings();
			_xwSettings.Indent = true;

			using (XmlWriter xw = XmlWriter.Create(hierarchyPath, _xwSettings))
			{
				// A root element is needed for correct reading.
				// <Root>
				xw.WriteStartElement(__ElemRoot);

				foreach (KeyValuePair<int, List<int>> entry in hierarchy)
				{
					// First, write the key (the regulation module Id):
                    // <Regulation_module>
                    xw.WriteStartElement(__RegulationModule);
                    xw.WriteAttributeString(__ID, entry.Key.ToString());
					// Now all the contained modules!
					for (int i = 0; i < hierarchy[entry.Key].Count; ++i)
					{
                        // <Contains>
						xw.WriteStartElement(__Contains);
						xw.WriteAttributeString(__ID, hierarchy[entry.Key][i].ToString());
						// </Contains>
                        xw.WriteEndElement();   
                    }
                    // </Regulation_module>
					xw.WriteEndElement();
				}
				// </Root>
				xw.WriteEndElement();
			} 			
		}

		/// <summary>
		/// Reads the hierarchy from the Xml file. Note the Root level is required!
        /// 
        /// NOTICE!
        /// I do not really understand XML syntax. As far as I am concerned
        /// these are all magic runes. If you do know what is going on, perhaps
        /// this could be improved (I only made sure it works as intended, but
        /// it may be ugly or very inefficient!)
		/// </summary>
		public static void ReadHierarchy(out Dictionary<int, List<int>> hierarchy)
		{
			hierarchy = new Dictionary<int, List<int>>();
            int index;
            List<int> list = new List<int>();

            // We checked that the path exists before calling this method.
            using (XmlReader xr = XmlReader.Create(hierarchyPath))
            {
                // Finds <Root>
                XmlIoUtils.MoveToElement(xr, false, __ElemRoot);

                // Creates subtree
                using(XmlReader xrSubtree0 = xr.ReadSubtree())
                {
                    // Re-scans subtree
                    XmlIoUtils.MoveToElement(xrSubtree0, false);

                    // Tries to move to Regulation_module elements
                    if (MoveIfFound(xrSubtree0, __RegulationModule))
                    {
                        // Do loop over al regulation_module elements
                        do
                        {
                            // Reads regulation module ID
                            index = XmlIoUtils.ReadAttributeAsInt(xr, __ID);

                            // Creates a reader over the <Regulation_module> sub-tree.
                            using(XmlReader xrSubtree = xrSubtree0.ReadSubtree())
                            {
                                // Re-scans for the root of each <Regulation_module> element.
                                XmlIoUtils.MoveToElement(xrSubtree, false);

                                list = new List<int>();

                                // Moves to first contained element 
                                if (MoveIfFound(xrSubtree, __Contains))
                                {
                                    // Loops over all "Contains" elements
                                    do
                                    {
                                        // Contained module's ID
                                        int contains = XmlIoUtils.ReadAttributeAsInt(xrSubtree, __ID);
                                        list.Add(contains);
                                    } 
                                    while (xrSubtree.ReadToNextSibling(__Contains));
                                }                
                            }
                            // Adds list to the dictionary!
                            hierarchy.Add(index, list);
                        } 
                        while (xrSubtree0.ReadToNextSibling(__RegulationModule));
                    }
                }
            }
		}

        /// <summary>
        /// Moves the XML reader to the next element, and retrieves the name.
        /// If it is not the expected result (nothing found, for example)
        /// the method returns false.
        /// </summary>
        static bool MoveIfFound(XmlReader xr, string elementName)
        {
            string localName = XmlIoUtils.MoveToElement(xr, true);            
            if (localName != elementName)
            {
                // No element or unexpected element.
                return false;
            }
            // Return success!
            return true;
        }
	}
}

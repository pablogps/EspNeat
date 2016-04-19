using UnityEngine;
using System.Collections.Generic;
using SharpNeat.Genomes.Neat;
using System.Xml;
using SharpNeat.Utility;

namespace SharpNeat.Coordination
{
	/// <summary>
	/// This class reads and writes the label names used in GUI to a file.
	/// </summary>
	public static class GuiXmlIO {
        private static string namesPath;
        private static NeatGenome genome;
        private static string __ElemRoot = "Root";
        private static string __ElemInputs = "Input_Labels";
        private static string __ElemOutputs = "Output_Labels";
        private static string __ElemModules = "Module_Labels";
        private static string __Input = "Input";
        private static string __Output = "Output";
        private static string __Module = "Module";
        private static string __Index = "Index ";
        private static string __Label = "Label";

        public static string NamesPath
        {
            set { namesPath = value; }
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
                                      out List<string> moduleLabels) {
            inputLabels = new List<string>();
            outputLabels = new List<string>();
            moduleLabels = new List<string>();

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
                            moduleLabels.Add(xrSubtree.GetAttribute(__Label));
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
                                       List<string> moduleLabels)
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
                for (int i = 0; i < moduleLabels.Count; ++i)
                {
                    xw.WriteStartElement(__Module);
                    xw.WriteAttributeString(__Index, i.ToString());
                    xw.WriteAttributeString(__Label, moduleLabels[i]);
                    xw.WriteEndElement();                    
                }
                // </Modules>
                xw.WriteEndElement();

                // </Root>
                xw.WriteEndElement();
            }            
        }
	}
}

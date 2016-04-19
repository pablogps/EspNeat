using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SharpNeat.Genomes.Neat;
using SharpNeat.Network;

namespace SharpNeat.Coordination
{
/// <summary>
/// This class creates a basic scheme of the network, with its modules and
/// connections. Detailed structure within modules is hidden.
/// Only uses static methods so we do not need any instances to use it.
/// Draws the scheme using GUI elements, but this approach may be changed
/// (and we could use objects in our scene for this task).
/// 
/// For simplicity, we use elements with fixed size. If the screen is too 
/// small this will be problematic.
/// </summary>
public class VisualizerSlow : MonoBehaviour {
    
    #region Variables

    private NeatGenome genome;

    private Texture2D moduleTexture;
    private Texture2D inputNeuronTexture;
    private Texture2D outputNeuronTexture;
    private Texture2D regulatoryNeuronTexture;
    // Why so many lines? Rotating textures is not trivial. Also, if we change
    // the length it also affects the width. It is not relevant except for the 
    // vertical line in regulatory neurons, much longer than others.
    // TODO: There must be a better way to draw rectangles of a given size 
    // (perhaps using only part of a texture)
    private Texture2D lineTexture;
    private Texture2D orizontalLineTexture;
    private Texture2D longLineTexture;

    private GUIStyle normalText = new GUIStyle();
    private GUIStyle inputNeuronText = new GUIStyle();
    private GUIStyle outputNeuronText = new GUIStyle();
    private GUIStyle moduleText = new GUIStyle();

    // Perhaps some information does not need to be in a list, but this way
    // we avoid repeating calculations every frame!
    private List<string> inputLabels;
    private List<string> outputLabels;
    private List<string> moduleLabels;
    private List<string> pandemoniums;
    // List with the number of local output neurons in each module.
    private List<int> localOutInModule;
    // List with the list of local output targets (as string) in each module.
    private List<List<string>> localOutTargets;
    // This dictionary has the string with type and order value of output and
    // regulatory neurons. The first local output neuron will be "O1", the
    // third regulatory will be "R3", and so on.
    private Dictionary<uint, string> targetStringById;

    private int moduleSizeX;
    private int moduleSizeY;
    private int neuronSize;
    private const int lineWidth = 3;
    private const int longLineWidth = 2;

    #endregion

    #region Initialize

    void Awake()
    {
        moduleSizeX = 150;
        moduleSizeY = 70;
        neuronSize = 35;
        InitializeTextures();
        InitializeStyles();

        inputLabels = new List<string>();
        outputLabels = new List<string>();
        moduleLabels = new List<string>();
        // We declare them here to avoid trouble if there are any frames
        // before we receive the genome.
        localOutInModule = new List<int>();
        localOutTargets = new List<List<string>>();
        // We initialize labels after receiving a genome.
    }

    public void GetGenome(NeatGenome inGenome)
    {
        genome = inGenome;
        // We must wait until we get our genome to create default labels
        // (otherwise we do not know how many neurons to expect).
        DefaultLabels();
        UpdateLocalOutInfo();
        UpdatePandemoniums();
    }

    #endregion

    #region Public Methods

    public void DrawScheme()
    {
        DrawInputNeurons();
        DrawModules();
        DrawOutputNeurons();
    }

    /// <summary>
    /// Allows to change in real-time the pandemonium state of the modules.
    /// </summary>
    public void UpdatePandemonium(int module, int newPan)
    {
        // Remember the module 1 takes index 0
        pandemoniums[module - 1] = newPan.ToString();
    }

    /// <summary>
    /// Allows to show the new local output neurons in the scheme.
    /// </summary>
    public void UpdateLocalOut(uint targetID)
    {
		// Our target string is: targetStringById[targetID]
        // Our current module index (the new one) is:
        int current = genome.Regulatory;
        localOutTargets[current][localOutInModule[current] - 1] = 
                targetStringById[targetID];
        // And we add a new possible connection.
        ++localOutInModule[current];
        localOutTargets[current].Add("?");
    }

    #endregion

    #region Draw Methods

    /// <summary>
    /// Draws the input neurons, with a label on top (which may be edited) and
    /// an ID within (which cannot be edited).
    /// </summary>
    void DrawInputNeurons()
    {
        const int horizontalSpace = 100;
        const int y = 130;
        const int textOffset = 9;
        const int labelOffset = 20;
        const int lineOffsetX = 14;
        const int lineOffsetY = 28;

        for (int i = 0; i < genome.Input; ++i)
        {
            int x = 25 + i * horizontalSpace;

            Circle(x, y, inputNeuronTexture);
            Text(x + textOffset, y + textOffset, inputNeuronText, 
                 "I" + (i + 1).ToString());
            Text(x, y - labelOffset, normalText, inputLabels[i]); 

            // Finally adds a line hinting input neurons are connected to the
            // modules below.
            Line(x + lineOffsetX, y + lineOffsetY, 20, lineTexture);
        }
    }

    /// <summary>
    /// Draws the output neurons, with a label on top (which may be edited) and
    /// an ID within (which cannot be edited).
    /// </summary>
    void DrawOutputNeurons()
    {
        const int horizontalSpace = 100;
        const int textOffsetX = 6;
        const int textOffsetY = 8;
        const int labelOffset = 40;
        const int lineOffsetX = 14;
        const int lineOffsetY = 14;

        for (int i = 0; i < genome.Output; ++i)
        {
            int x = 25 + i * horizontalSpace;
            int y = Screen.height - 80;

            Circle(x, y, outputNeuronTexture);
            Text(x + textOffsetX, y + textOffsetY, outputNeuronText, 
                "O" + (i + 1).ToString());
            Text(x, y + labelOffset, normalText, outputLabels[i]); 

            // Finally adds a line hinting input neurons are connected to the
            // modules above.
            Line(x + lineOffsetX, y - lineOffsetY, 20, lineTexture);
        }
    }

    /// <summary>
    /// Draws the modules, with ID and label, as well as local output with their
    /// targets and their regulatory neuron.
    /// </summary>
    void DrawModules()
    {
        const int horizontalSpace = 250;
        const int y = 250;
        const int textOffset = 8;
        const int labelOffset = 25;
        const int label2Offset = 30;
        const int label3Offset = 45;
        const int lineOffsetX = 40;
        const int lineOffsetY = 14;
        const int localOutOffsetX = 15;
        int localOutOffsetY;
        const int lineLocalOutOffsetY = 64;
        const int lineRegulatoryOffsetX = 14;
        const int lineRegulatoryOffsetY = 36;
        const int line2RegulatoryOffsetX = -15;
        const int line2RegulatoryOffsetY = 74;
        const int regulatoryIdX = 6;

        for (int i = 0; i <= genome.Regulatory; ++i)
        {
            // UPGRADE: These two calculations might be stored in lists, so they
            // are not computed each frame. However, it seems we do not have 
            // trouble with this, and this will not affect performance outside 
            // of this menu, so this is not pressing.
            int x = 25 + i * horizontalSpace;
			int localOutInterval = (int)((double)moduleSizeX / 
                                         ((double)(localOutInModule[i]) + 1.0));

            Rectangle(x, y);

            // Here go the module labels (id, name and pandemonium)
            Text(x + textOffset, y + textOffset, moduleText, 
                "M" + (i + 1).ToString());
            Text(x + textOffset, y + labelOffset, normalText, moduleLabels[i]);
            Text(x + textOffset, y + label3Offset, normalText, 
                 "Pandem " + pandemoniums[i]);

            // Adds line to show its input (just one, generic for "all inputs")
            Line(x + lineOffsetX, y - lineOffsetY, 20, lineTexture);
            Text(x + textOffset, y - label2Offset, normalText, "All inputs"); 

            // We need to add the local output now.
            int localX = x - localOutOffsetX;
            for (int j = 0; j < localOutInModule[i]; ++j)
            {   
                localX += localOutInterval; 
                // Do not forget lines to these local out labels.
                // Lines will alternate length if there are many, for legibility.
                if (localOutInModule[i] < 4)
                {
                    Line(localX + textOffset, y + lineLocalOutOffsetY, 
                         20, lineTexture);  
                    localOutOffsetY = 83;                  
                }
                else
                {
                    // If j is even...
                    if (j % 2 == 0)
                    {
                        Line(localX + textOffset, y + lineLocalOutOffsetY, 
                             20, lineTexture);   
                        localOutOffsetY = 83;                    
                    }
                    else
                    {
                        Line(localX + textOffset, y + lineLocalOutOffsetY, 
                             50, longLineTexture); 
                        localOutOffsetY = 113;
                    }                    
                }	
                Text(localX, y + localOutOffsetY, normalText, localOutTargets[i][j]);
			}

            // Finally adds the regulatory neuron with its ID and pandemonium.
            Circle(x + moduleSizeX, y + textOffset, regulatoryNeuronTexture);
            Text(x + moduleSizeX + regulatoryIdX,y + textOffset + textOffset,
                 moduleText, "R" + (i + 1).ToString());
            Line(x + moduleSizeX + lineRegulatoryOffsetX,
                 y + lineRegulatoryOffsetY, 47, longLineTexture); 
            OrizontalLine(x + moduleSizeX + line2RegulatoryOffsetX,
                          y + line2RegulatoryOffsetY); 
        }        
    }

    // This draws a rectangle, for modules.
	void Rectangle(int x, int y)
    {
        GUI.Label(new Rect(x, y, moduleSizeX, moduleSizeY), moduleTexture);	
    }

    // This draws a circle, for neurons.
    void Circle(int x, int y, Texture2D texture)
    {
        GUI.Label(new Rect(x, y, neuronSize, neuronSize), texture);   
    }

    // This displays a text message.
    void Text(int x, int y, GUIStyle style, string textString)
    {
        GUI.Label(new Rect(x, y, 1, 1), textString, style);
    }

    // This draws a line.
    void Line(int x, int y, int length, Texture2D texture)
    {
        GUI.Label(new Rect(x, y, lineWidth, length), texture);   
    }
    void OrizontalLine(int x, int y)
    {
        // Why is the Rect size 20, 10 when the width is less than that?
        // I do not know. But if I try using the real size it will not work.
        // TODO: Understand what magic is going on here.
        GUI.Label(new Rect(x, y, 30, 10), orizontalLineTexture);   
    }

    #endregion

    #region Update Structure Methods

    /// <summary>
    /// Here we create our textures. It is important that we only do this once, 
    /// otherwise textures may have a huge impact in performance!
    /// We can use this method to determine the size of our objects on program
    /// execution!
    /// 
    /// Easier still: we can use
    /// GUI.Label(new Rect(x, y, someSize, someSize), moduleTexture);
    /// to draw rectangles up to the size created here.
    /// </summary>
    void InitializeTextures()
    {
        InitializeRectangleTextures();
        InitializeNeuronTexture();
    }

    /// <summary>
    /// Initializes all textures that are rectangles.
    /// </summary>
    void InitializeRectangleTextures()
    {
        const int lineSize = 20;
        const int longLineSize = 50;
        const int orizontalLineSize = 30;

        moduleTexture = new Texture2D(moduleSizeX, moduleSizeY);
        lineTexture = new Texture2D(lineWidth, lineSize);
        longLineTexture = new Texture2D(lineWidth, longLineSize);
        orizontalLineTexture = new Texture2D(orizontalLineSize, lineWidth);

        Color moduleColour = new Color(0f,0.6706f,1f,1f);   

        RectangleTexture(moduleSizeX, moduleSizeY, moduleTexture, moduleColour);
        RectangleTexture(lineWidth, lineSize, lineTexture, Color.white);
        RectangleTexture(lineWidth, longLineSize, longLineTexture, Color.white);
        RectangleTexture(orizontalLineSize, 2, orizontalLineTexture, Color.white);
    }

    void RectangleTexture(int length, int width, Texture2D texture, Color color)
    {
        for (int i = 0; i < length; ++i)
        {
            for (int j = 0; j < width; ++j)
            {
                texture.SetPixel(i, j, color);
                texture.Apply();
            }            
        }        
    }

    /// <summary>
    /// Here we are going to draw a circle for our neurons. If the points are 
    /// within the circumference they get colour, otherwise they are transparent.
    /// Each type of neuron gets its own colour.
    void InitializeNeuronTexture()
    {
        // Remember the equation for the circumference:
        // x^2 + y^2 = r^2
        // With center in a, b it becomes
        // (x - a)^2 + (y - b)^2 = r^2
        double a = (double)neuronSize * 0.5;
        double radSquare = a * a;

        Color inputNeuronColour = new Color(0.8f,0f,0f,1f);
        Color outputNeuronColour = new Color(0f,0.6588f,0.1765f,1f);
        Color regulatoryNeuronColour = new Color(0f,0.6706f,1f,1f);
        Color transparent = new Color(0f,0f,0f,0f);

        inputNeuronTexture = new Texture2D(neuronSize, neuronSize);
        outputNeuronTexture = new Texture2D(neuronSize, neuronSize);
        regulatoryNeuronTexture = new Texture2D(neuronSize, neuronSize);

        for (int i = 0; i < neuronSize; ++i)
        {
            for (int j = 0; j < neuronSize; ++j)
            {
                if (((double)i - a) * ((double)i - a) + 
                    ((double)j - a) * ((double)j - a) < radSquare)
                {
                    inputNeuronTexture.SetPixel(i, j, inputNeuronColour);
                    inputNeuronTexture.Apply(); 
                    outputNeuronTexture.SetPixel(i, j, outputNeuronColour);
                    outputNeuronTexture.Apply();   
                    regulatoryNeuronTexture.SetPixel(i, j, regulatoryNeuronColour);
                    regulatoryNeuronTexture.Apply();                      
                }
                else
                {
                    inputNeuronTexture.SetPixel(i, j, transparent);
                    inputNeuronTexture.Apply();    
                    outputNeuronTexture.SetPixel(i, j, transparent);
                    outputNeuronTexture.Apply();  
                    regulatoryNeuronTexture.SetPixel(i, j, transparent);
                    regulatoryNeuronTexture.Apply();                  
                }
            }            
        }        
    }

    void InitializeStyles()
    {
        normalText.fontSize = 16;
        normalText.normal.textColor = Color.white;

        Color inputNeuronIDColour = new Color(1f,1f,1f,1f);
        Color outputNeuronIDColour = new Color(1f,0.7f,1f,1f);
        Color moduleTextColor = new Color(0.98f,0.8314f,0f,1f);

        inputNeuronText.fontSize = 16;
        inputNeuronText.normal.textColor = inputNeuronIDColour;
        inputNeuronText.fontStyle = FontStyle.Bold;

        outputNeuronText.fontSize = 16;
        outputNeuronText.normal.textColor = outputNeuronIDColour;
        outputNeuronText.fontStyle = FontStyle.Bold;

        moduleText.fontSize = 16;
        moduleText.normal.textColor = moduleTextColor;
        moduleText.fontStyle = FontStyle.Bold;        
    }

    /// <summary>
    /// Input, output and regulatory neurons, as well as modules, may have
    /// custom labels (as well as their ID values). Here we get default
    /// labels (like "Input1" and so on).
    /// </summary>
    void DefaultLabels()
    {
        // In case we get a new genome (for example, after creating a module) we
        // avoid overwriting information!
        if (inputLabels.Count == 0)
        {
            for (int i = 0; i < genome.Input; ++i)
            {
                inputLabels.Add("Input" + (i + 1).ToString()); 
            } 
            for (int i = 0; i < genome.Output; ++i)
            {
                outputLabels.Add("Output" + (i + 1).ToString()); 
            }           
        }

        while (moduleLabels.Count < genome.Regulatory)
        {
            moduleLabels.Add("Module" + moduleLabels.Count.ToString());
        }
        moduleLabels.Add("New Module");
    }

    /// <summary>
    /// In order to represent our modules we need to know how many local output
    /// neurons each module has, and which targets they have. Obviously this 
    /// should NOT be computed each frame, so we save the information in 
    /// localOutInModule and localOutTargets. We only update this information
    /// if we get a new genome (with a possibly new structure).
    /// </summary>
    void UpdateLocalOutInfo()
    {
        localOutInModule = new List<int>();
        localOutTargets = new List<List<string>>();
        targetStringById = new Dictionary<uint, string>();

        MakeTargetStringById();

        for (int i = 0; i < genome.Regulatory; ++i)
        {
            // Firts gets the number of local outputs.
            localOutInModule.Add(GetLocalOutInModule(i + 1));

            // For each local output gets a target.
			List<string> innerList = new List<string>();
            for (int j = 0; j < localOutInModule[i]; ++j)
            {
				innerList.Add(GetTarget(i + 1, j + 1));
            }
            localOutTargets.Add(innerList);
        }  
        // We add extra information for the new module!
        localOutInModule.Add(1);
        List<string> extraList = new List<string>(); 
        extraList.Add("?");
        localOutTargets.Add(extraList);
    }

    /// <summary>
    /// Gets the number of local out neurons in each module.
    /// </summary>
    int GetLocalOutInModule(int module)
    {
        bool started = false;
        int count = 0;
        for (int i = genome.NeuronGeneList.LastBase + 1; 
             i < genome.NeuronGeneList.Count; ++i)
        {
            // When we find the first local output of the given module, we 
            // start counting.
            if (!started && genome.NeuronGeneList[i].ModuleId == module)
            {
                started = true;
            }
            if (started)
            {
                // If we find a new module or a hidden neuron, we stop counting.
                if (genome.NeuronGeneList[i].ModuleId != module ||
                    genome.NeuronGeneList[i].NodeType != NodeType.Local_Output)
                {
                    return count;
                }
                ++count;
            }
        }
        // If there are no hidden neurons (should not be the case) we exit here:
        return count;
    }

    /// <summary>
    /// Given a module and a local output number, searchs for its target and
    /// returns a string with this information.
    /// </summary>
    string GetTarget(int module, int localOutNumber)
    {
        uint targetID = 0;
        // Index for the local output we are looking for:
        // The first local output neuron is:
        int index = genome.NeuronGeneList.LastBase + 1;
        // Adds all the local output in previous modules. Remember the first
        // module is "module = 1", while it is the index 0 for the list. That
        // is why we substract 1 in the loop condition. Careful with this!
        for (int i = 0; i < module - 1; ++i)
        {
            index += localOutInModule[i];
        }
        // Finally adds the previous local output neurons in this module:
        index += localOutNumber - 1;

        // With this index, we know its ID.
        uint ID = genome.NeuronGeneList[index].Id;
        // Now looks for a connection with this ID as source:
        foreach (ConnectionGene connection in genome.ConnectionGeneList)
        {
            if (connection.SourceNodeId == ID)
            {
                targetID = connection.TargetNodeId;
            }
        }
        //NeuronGene targetNeuron = genome.NeuronGeneList.GetNeuronByIdAll(targetID);

        // We have our target ID.
        // We can now return our string with the target ID, which information
        // is stored in a list.
        return targetStringById[targetID];
    }

    /// <summary>
    /// Counts the order of output and regulatory neurons, and writes in a
    /// dictionary a string with its ID (for example, "R3" for the third 
    /// regulatory neuron in the list). The keys are innovation ID values for
    /// these neurons.
    /// </summary>
    void MakeTargetStringById()
    {
        bool startOut = false;
        bool startReg = false;
        int countOut = 0;
        int countReg = 0;
        for (int i = 0; i <= genome.NeuronGeneList.LastBase; ++i)
        {
            // If we find an output neuron, starts counting.
            if (!startOut && genome.NeuronGeneList[i].NodeType == NodeType.Output)
            {
                startOut = true;
            }
            // If we find a regulatory neuron, starts counting.
            if (!startReg && genome.NeuronGeneList[i].NodeType == NodeType.Regulatory)
            {
                startReg = true;
            }
            // If we are in the output neurons region...
            if (startOut && !startReg)
            {
                ++countOut;
                targetStringById.Add((uint)i, "O" + countOut.ToString());
            }
            // If we are in the regulatory neurons region...
            if (startReg)
            {
                ++countReg;
                // We do not write "i" as the ID because, unlike output neurons, 
                // regulatory neurons may take non-consecutive values.
                targetStringById.Add(genome.NeuronGeneList[i].Id,
                                     "R" + countReg.ToString());
            }
        }
    }

    /// <summary>
    /// In this list we keep track of the pandemonium state in each module.
    /// </summary>
    void UpdatePandemoniums()
    {
        pandemoniums = new List<string>();

        for (int i = 0; i <= genome.NeuronGeneList.LastBase; ++i)
        {
            if (genome.NeuronGeneList[i].NodeType == NodeType.Regulatory)
            {
                pandemoniums.Add(genome.NeuronGeneList[i].Pandemonium.ToString());
            }
        }
        // We add the pandemonium state for the new module:
        pandemoniums.Add("?");
    }

    #endregion
}
}

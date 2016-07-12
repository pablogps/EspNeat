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
    public class Visualizer : MonoBehaviour {
        
        #region Variables

        private NeatGenome genome;

        private MenuScreens currentScreen = MenuScreens.Edit;
        private GuiManager guiManager;

        private Texture2D moduleTexture;
        private Texture2D inputNeuronTexture;
        private Texture2D outputNeuronTexture;
        private Texture2D regulatoryNeuronTexture;
        private Texture2D lineTexture;
        private Texture2D longLineTexture;
        private Texture2D transparentTexture;

        private GUIStyle normalText = new GUIStyle();
        private GUIStyle inputNeuronText = new GUIStyle();
        private GUIStyle outputNeuronText = new GUIStyle();
        private GUIStyle moduleText = new GUIStyle();

        private int moduleCount = 0;

        // Perhaps some information does not need to be in a list, but this way
        // we avoid repeating calculations every frame!
        private List<string> inputLabels;
        private List<string> outputLabels;
        private List<string> moduleLabels;
        private List<string> pandemoniums;
        private List<int> pandemoniumsInt;
        private List<ModuleCoords> moduleCoords;
        private List<NeuronCoords> inputNeuronCoords;
        private List<NeuronCoords> outputNeuronCoords;
        // List with the number of local in/out neurons in each module.
        //private List<int> localInputInModule;
        //private List<int> localOutInModule;
        // List with the list of local in/out targets (as string) in each module.
        private List<List<string>> localInSources;
        private List<List<string>> localOutTargets;
        // This dictionary has the string with type and order value of input,
        // output and regulatory neurons. The first local output neuron will 
        // be "O1", the third regulatory will be "R3", and so on.
        private Dictionary<uint, string> targetSourceStringById;

        // Consider making objects of variable size to adapt to different screens.
        // For best performance now we will assume constant sizes.
        private const int lineWidth = 3;

        private bool plusRegulation = true;
        private int moduleThatCalled = 1;

		private GUISkin myLocalSkins;

		private string namesPath;

        #endregion

        #region Initialize

        /// <summary>
        /// Initializes variables and loads textures.
        /// </summary>
        void Awake()
        {
			InitializeTextures();
			InitializeStyles();

			Reset();
        }

		public void Reset()
		{
			inputLabels = new List<string>();
			outputLabels = new List<string>();
			moduleLabels = new List<string>();
			localInSources = new List<List<string>>();
			localOutTargets = new List<List<string>>();
			moduleCoords = new List<ModuleCoords>();
			inputNeuronCoords = new List<NeuronCoords>();
			outputNeuronCoords = new List<NeuronCoords>();	
		}

        /// <summary>
        /// Gets the genome to represent. Note that for this crude representation
        /// any genome in the population is the same.
        /// </summary>
        public void UpdateModelGenome(NeatGenome inGenome)
        {
            // We must wait until we get our genome to create default labels
            // (otherwise we do not know how many neurons to expect).
            genome = inGenome;
            GuiXmlIO.Genome = genome;

            UpdateLocalInOutInfo();
            InitializeCoords();
            guiManager.Initialize(this);
            UpdatePandemoniums();
            UpdateLabels();
            InitializeRegulatoryInputList();

            moduleCount = genome.Regulatory;
        }

        #endregion

        #region Properties

        public List<ModuleCoords> ModuleCoords
        {
            get { return moduleCoords; }
        }

        public List<NeuronCoords> InputNeuronCoords
        {
            get { return inputNeuronCoords; }
        }

        public List<NeuronCoords> OutputNeuronCoords
        {
            get { return outputNeuronCoords; }
        }

        public Dictionary<uint, string> TargetSourceStringById
        {
            get { return targetSourceStringById; }
        }

        public List<string> ModuleLabels
        {
            get { return moduleLabels; }
        }

        public bool PlusRegulation
        {
            set { plusRegulation = value; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the reference to the GuiManager component created in
        /// EspNeatOptimizer.
        /// </summary>
        public void SetGuiManager(GuiManager referenceGuiManager)
        {
            guiManager = referenceGuiManager;
        }

        /// <summary>
        /// We get the skin with all the styles from Optimizer, because this
        /// script is instantiated at runtime and the skin must be assigned
        /// through the game object's inspector menu in Unity.
        /// </summary>
		public void SetSkin(GUISkin mySkins)
		{
			myLocalSkins = mySkins;
		}

        public void SetNamesPath(string path)
        {
            namesPath = path;
            GuiXmlIO.NamesPath = path;
        }

        /// <summary>
        /// Updates the current screen.
        /// </summary>
        public void UpdateScreen(MenuScreens newScreen)
        {
            currentScreen = newScreen;
        }

        /// <summary>
        /// Main method that draws all the elements on the screen.
        /// Depending on the screen menu different elements will be highlighted,
        /// as an interface hint.
        /// </summary>
        public void DrawScheme()
		{
            GUI.skin = myLocalSkins;

            switch (currentScreen)
            {


            case MenuScreens.Edit:
                //TransparentLayer();
                //HeadingBackground();

                //DrawInputNeurons();
                //DrawOutputNeurons();
                // DrawModules();
                break;









            // To be upgraded OR DELETED (cleaning unused features!) 


            case MenuScreens.AddModule:
                TransparentLayer();
                DrawInputNeurons();
                DrawOutputNeurons();
                DrawModules();
                break;
            case MenuScreens.AddModuleLocalIn:
                DrawModules();
                DrawOutputNeurons();
                TransparentLayer();
                DrawInputNeurons();
                LocalOutButtons();
                break;
            case MenuScreens.AddModuleLocalOut:
                DrawInputNeurons();
                DrawModules();
                TransparentLayer();
                DrawOutputNeurons();

                HighlightRegulatory();
                break;
            case MenuScreens.AddModuleRegulation:
                DrawModules();
                DrawOutputNeurons();
                TransparentLayer();

                DisplayRegulatoryInput(moduleThatCalled);

                DrawInputNeurons();
                break;
            case MenuScreens.AddModuleLabels:
                TransparentLayer();
                DrawInputNeurons();
                DrawOutputNeurons();
                DrawModules();

                NewLabels();
                break;
            case MenuScreens.ProtectedWeights:
                TransparentLayer();
                break;
            case MenuScreens.PlayEditWeights:
                TransparentLayer();
                break;
            case MenuScreens.EditInToReg:
                TransparentLayer();
                for (int i = 0; i < genome.Regulatory; ++i)
                {
                    ModulesBasic(i);
                    PandemButtons(i);
                }
                ShowInToReg(genome.Regulatory);
                break;
            case MenuScreens.EditInToRegGetInfo:
                for (int i = 0; i < genome.Regulatory; ++i)
                {
                    ModulesBasic(i);
                }
                TransparentLayer();
                DisplayRegulatoryInput(moduleThatCalled);
                DrawInputNeurons();
                break;
            default:
                TransparentLayer();
                break;
            }
        }

        /// <summary>
        /// Allows to change in real time the pandemonium state of the modules.
        /// </summary>
        public void UpdatePandemonium(int module, int newPan)
        {
            // Remember that the first module takes index 0!
            if (newPan != 0)
            {
                pandemoniums[module - 1] = newPan.ToString(); 
            }
            else
            {
                pandemoniums[module - 1] = "N";
            }
            pandemoniumsInt[module - 1] = newPan;
        }

        // Removes all currently selected local input neurons.
        public void RemoveAllLocalIn()
        {
            int current = genome.Regulatory;
            localInSources[current] = new List<string>();
            // Adds a new possible connection.
            localInSources[current].Add("Edit");
            // Updates the input count in moduleCoords.
            moduleCoords[genome.Regulatory].NewInputCount(1);
        }

        /// <summary>
        /// Removes all currently selected local output neurons.
        /// </summary>
        public void RemoveAllLocalOut()
        {
            int current = genome.Regulatory;
            localOutTargets[current] = new List<string>();
            // Adds a new possible connection.
            localOutTargets[current].Add("Edit");
            // Updates the input count in moduleCoords.
            moduleCoords[genome.Regulatory].NewOutputCount(1);  
        }

        /// <summary>
        /// Adds all inputs as local input. Note there can also be local output
        /// neurons as input for local input neurons.
        /// </summary>
        public void AddAllLocalIn()
        {
            int current = genome.Regulatory;
            int LastIndex = localInSources[current].Count - 1;

            for (uint i = 0; i <= (uint)genome.Input; ++i)
            {
                // Remember, for input and output neurons Id = index.
                string neuronStr = targetSourceStringById[i];

                // If it is not in the list yet, it is added. "Edit" label is
                // kept at the end.
                if (!localInSources[current].Contains(neuronStr))
                {
                    localInSources[current].Insert(LastIndex, neuronStr);
                    ++LastIndex;
                }
            }
            // Updates the input count in moduleCoords.
            moduleCoords[current].NewInputCount(localInSources[current].Count);
        }

        /// <summary>
        /// Adds all outputs as local output.
        /// </summary>
        public void AddAllLocalOut()
        {
            int current = genome.Regulatory;
            int LastIndex = localOutTargets[current].Count - 1;

            for (uint i = (uint)genome.Input + 1;
                 i <= (uint)genome.Input + (uint)genome.Output; ++i)
            {
                // Remember, for input and output neurons Id = index.
                string neuronStr = targetSourceStringById[i];

                // If it is not in the list yet, it is added. "Edit" label is
                // kept at the end.
                if (!localOutTargets[current].Contains(neuronStr))
                {
                    localOutTargets[current].Insert(LastIndex, neuronStr);
                    ++LastIndex;
                }
            }
            // Updates the output count in moduleCoords.
            moduleCoords[current].NewOutputCount(localOutTargets[current].Count);
        }

        /// <summary>
        /// Allows to show in the scheme local output changes to the new module.
        /// </summary>
        public void UpdateLocalOut(uint targetID, bool isAdded)
        {
            UpdateList(localOutTargets, targetID, isAdded);
            int current = genome.Regulatory;
            // Updates the output count in moduleCoords.
            moduleCoords[current].NewOutputCount(localOutTargets[current].Count); 
        }

        /// <summary>
        /// Allows to show in the scheme local input changes to the new module.
        /// </summary>
        public void UpdateLocalIn(uint sourceID, bool isNew)
        {
            UpdateList(localInSources, sourceID, isNew);
            int current = genome.Regulatory;
            // Updates the input count in moduleCoords.
            moduleCoords[current].NewInputCount(localInSources[current].Count);
        }

        /// <summary>
        /// Used to avoid code repetition in UpdateLocalIn and UpdateLocalOut.
        /// </summary>
        void UpdateList(List<List<string>> localList, uint sourceID, bool isNew)
        {
            // Our current module index (the new one) is:
            int current = genome.Regulatory;

            string neuronStr = targetSourceStringById[sourceID];

            // Adds or removes the source.
            if (isNew)
            {
                // Updates the last local out with the new target.
                // Our target string is: targetSourceStringById[targetID]
                localList[current][localList[current].Count - 1] = neuronStr;
                // Adds a new possible connection.
                localList[current].Add("Edit");
            }
            else
            {
                localList[current].Remove(neuronStr);
            }           
        }

        #endregion

        #region Draw Methods

        /// <summary>
        /// Adds a layer with some opacity. We use this at different places
        /// to hightlight the desired elements.
        /// </summary>
        void TransparentLayer()
        {
            // This creates a skin with a uniform colour for background.
            GUI.skin.box.normal.background = transparentTexture;
            // Creates an empty element with this screen.
            // For the whole screen:
            // GUI.Box(new Rect(1, 1, Screen.width, Screen.height), GUIContent.none);
            // For a custom size (60% of the screen, fitted to the right):
            GUI.Box(new Rect(Screen.width * 0.4f, 0, Screen.width * 0.6f, Screen.height),
                    GUIContent.none);
        }

        /// <summary>
        /// Adds a transparent background for the menu options on the top of the
        /// screen. Because the colour is (semi) transparent, it will look 
        /// darker over the right side of the screen (which already has a dark
        /// background). This could be avoided, but is found desirable at present.
        /// </summary>
        void HeadingBackground()
        {
            GUI.skin.box.normal.background = transparentTexture;
            GUI.Box(new Rect(0, 0, Screen.width, 50),
                GUIContent.none);
            
        }

        /// <summary>
        /// Draws the input neurons, with a label on top (which may be edited) and
        /// an ID within (which cannot be edited).
        /// </summary>
        void DrawInputNeurons()
        {            
            for (int i = 0; i <= genome.Input; ++i)
            {
                GUI.Label(inputNeuronCoords[i].neuronRect, inputNeuronTexture);
                Text(inputNeuronCoords[i].idX, inputNeuronCoords[i].idY,
                     inputNeuronText, "I" + (i).ToString());
                Text(inputNeuronCoords[i].labelX, inputNeuronCoords[i].labelY,
                     normalText, inputLabels[i]); 
            }
        }

        /// <summary>
        /// Draws the output neurons, with a label on top (which may be edited) and
        /// an ID within (which cannot be edited).
        /// </summary>
        void DrawOutputNeurons()
        {
            for (int i = 0; i < genome.Output; ++i)
            {
                GUI.Label(outputNeuronCoords[i].neuronRect, outputNeuronTexture); 
                Text(outputNeuronCoords[i].idX, outputNeuronCoords[i].idY,
                     outputNeuronText, "O" + (i + 1).ToString());
                Text(outputNeuronCoords[i].labelX, outputNeuronCoords[i].labelY,
                     normalText, outputLabels[i]); 
            }
        }

        /// <summary>
        /// Draws the modules, with ID and label, as well as local output with their
        /// targets and their regulatory neuron.
        /// </summary>
        void DrawModules()
        {
            for (int i = 0; i < moduleCount; ++i)
            {
                // Draws modules with their labels and regulatory neuron.
                ModulesBasic(i);

                // Adds local input elements.
                LocalInput(i);

                // Adds local output elements.
                LocalOutput(i);

                // We need buttons to change the pandemonium group.
                PandemButtons(i);
            }

            // Shows input for regulatory neurons.
            ShowInToReg(moduleCount);
        }

        /// <summary>
        /// Draws modules with their labels and regulatory neurons.
        /// </summary>
        void ModulesBasic(int i)
        {
            Module(moduleCoords[i].x, moduleCoords[i].y);

            // Here go the module labels (id, name and pandemonium)
            Text(moduleCoords[i].idX, moduleCoords[i].idY, moduleText, 
                "M" + (i + 1).ToString());
            Text(moduleCoords[i].labelX, moduleCoords[i].labelY, normalText,
                moduleLabels[i]);
            Text(moduleCoords[i].pandLabelX, moduleCoords[i].pandLabelY,
                normalText, "Pandem        " + pandemoniums[i]);
            // We included some empty space after Pandem to make space for a
            // button. Maybe not elegant, but probably more efficient than
            // rendering a new text!

            // Adds the regulatory neuron ID.
            Text(moduleCoords[i].regulatoryIdX, moduleCoords[i].regulatoryIdY, 
                moduleText, "R" + (i + 1).ToString());     
        }

        /// <summary>
        /// Shows the input to regulatory neurons (and creates buttons where
        /// required).
        /// lastModule allows to decide if we are showing information for the
        /// new module (from AddModule menu) or if this module should not be
        /// considered (from the input to regulatory edition menu).
        /// </summary>
        void ShowInToReg(int lastModule)
        {
            // We have a new for loop so these elements come on top of
            // everything else!
            for (int i = 0; i < lastModule; ++i)
            {
                // And to determine the regulation input.
                RegulationInput(i);                
            }            
        }

        /// <summary>
        /// Creates buttons at each local output neuron for the menu to select
        /// the sources for new local inputs.
        /// This is very similar code to the method in LocalOutput(int current),
        /// so perhaps there is a more clever way to avoid repetition.
        /// </summary>
        void LocalOutButtons()
        {
            for (int i = 0; i < genome.Regulatory; ++i)
            {
                int currentLabelX = moduleCoords[i].outputLabelX - 7;

                // All this is because the position for the local output
                // alternates in height (so that it is easier to read).
                int last = localOutTargets[i].Count;
                for (int j = 0; j < last; ++j)
                {   
                    currentLabelX += moduleCoords[i].outputInterval;
                    int currentLine = moduleCoords[i].outputLabelY;
                    if (localOutTargets[i].Count < 4)
                    {
                        currentLine = moduleCoords[i].outputLabelY;          
                    }
                    else
                    {
                        // If j is even...
                        if (j % 2 == 0)
                        {
                            currentLine = moduleCoords[i].outputLabelY;              
                        }
                        else
                        {
                            currentLine = moduleCoords[i].outputLabelLongY;    
                        }                    
                    }

                    if (GUI.Button(new Rect(currentLabelX, currentLine, 35, 27), 
                        localOutTargets[i][j], "LocalOutput"))
                    {
                        // Finds the Id corresponding to the selected local output.
                        uint sourceId = genome.NeuronGeneList.GetNeuronByModAndPosition(i, j);

                        // Updates the Id to string dictionary as well as the
                        // selected local-output target-string
                        UpdateDictionaryAndLocalOut(sourceId, i, j);

                        // Takes that Id to guiManager to update the local input
                        // list (and that will call back to Visualizer to update
                        // the local-inputs string-list.
                        guiManager.CallAddRemoveLocalIn(sourceId);
                    }
                }                
            }
        }

        /// <summary>
        /// Updates the Id-to-string dictionary as well as the selected
        /// local-output target-string
        /// </summary>
        void UpdateDictionaryAndLocalOut(uint sourceId, int module, int position)
        {
            // Position 1 corresponds to index 0:
            ++position;
                
            // If the key is not found in the dictionary, includes it.
            if (!targetSourceStringById.ContainsKey(sourceId))
            {
                targetSourceStringById.Add(sourceId, "M" + (module + 1).ToString() +
                                           "o" + position.ToString());  
            }
        }

        // Takes care of the local output. If there are many elements they will
        // alternate height so the result is more compact and easier to read.
        void LocalOutput(int current)
        {
            int currentLabelX = moduleCoords[current].outputLabelX;
            int currentLineX = moduleCoords[current].outputLineX;

            int last = localOutTargets[current].Count;
            for (int j = 0; j < last; ++j)
            {   
                currentLabelX += moduleCoords[current].outputInterval;
                currentLineX += moduleCoords[current].outputInterval;
                // Do not forget lines to these local out labels.
                // Lines will alternate length if there are many, for legibility.
                int currentLine = moduleCoords[current].outputLabelY;
                if (localOutTargets[current].Count < 4)
                {
                    Line(currentLineX, moduleCoords[current].outputLineY, 20);
                    currentLine = moduleCoords[current].outputLabelY;          
                }
                else
                {
                    // If j is even...
                    if (j % 2 == 0)
                    {
                        Line(currentLineX, moduleCoords[current].outputLineY, 20);
                        currentLine = moduleCoords[current].outputLabelY;              
                    }
                    else
                    {
                        LongLine(currentLineX, moduleCoords[current].outputLineY, 50); 
                        currentLine = moduleCoords[current].outputLabelLongY;    
                    }                    
                }

                // The last text for the last module corresponds to a button
                if (current == genome.Regulatory && j == last - 1)
                {
                    if (GUI.Button(new Rect(currentLabelX - 5, currentLine, 35, 30), 
                        "Edit"))
                    {
                        // Go to AddModuleLocalOut screen (disables the button
                        // that shows input for regulation).
                        plusRegulation = false;
                        currentScreen = MenuScreens.AddModuleLocalOut;
                        guiManager.CurrentMenu = MenuScreens.AddModuleLocalOut;  
                    }
                }
                else
                {
                    // Otherwise it is a normal text
                    Text(currentLabelX, currentLine, normalText, localOutTargets[current][j]); 
                }
            }
        }

        // Takes care of the local input. If there are many elements they will
        // alternate height so the result is more compact and easier to read.
        void LocalInput(int current)
        {            
            int currentLabelX = moduleCoords[current].outputLabelX +
                                moduleCoords[current].inputInterval;
            int currentLineX = moduleCoords[current].outputLineX +
                               moduleCoords[current].inputInterval;;

            int last = localInSources[current].Count;

            // Checks if the module uses all inputs, to display a simplified
            // label. This is not done for the last module.
            if (current != genome.Regulatory && last == genome.Input + 1)
            {
                Line(moduleCoords[current].outputLabelX + moduleCoords[current].inputInterval2,
                     moduleCoords[current].inputLineY, 20);
                Text(currentLabelX + 10, moduleCoords[current].inputLabelY,
                     normalText, "All inputs");  
            }
            else
            {
                for (int j = 0; j < last; ++j)
                {   
                    // Do not forget lines to these local out labels.
                    // Lines will alternate length if there are many, for legibility.
                    int currentLine = moduleCoords[current].inputLabelY;
                    if (localInSources[current].Count < 4)
                    {
                        Line(currentLineX, moduleCoords[current].inputLineY, 20);
                        currentLine = moduleCoords[current].inputLabelY;          
                    }
                    else
                    {
                        // If j is even...
                        if (j % 2 == 0)
                        {
                            Line(currentLineX, moduleCoords[current].inputLineY, 20);
                            currentLine = moduleCoords[current].inputLabelY;              
                        }
                        else
                        {
                            LongLine(currentLineX, moduleCoords[current].inputLineLongY, 50); 
                            currentLine = moduleCoords[current].inputLabelLongY;    
                        }                    
                    }

                    // The last text for the last module corresponds to a button
                    if (current == genome.Regulatory && j == last - 1)
                    {
                        EditButtonForLocalIn(currentLabelX, currentLine);
                    }
                    else
                    {
                        // Otherwise it is a normal text. But "bias" needs a different
                        // offset:
                        if (localInSources[current][j] != "Bias")
                        {
                            Text(currentLabelX, currentLine, normalText, localInSources[current][j]);  
                        }
                        else
                        {
                            Text(currentLabelX - 9, currentLine, normalText, localInSources[current][j]);
                        }
                    }
                    currentLabelX += moduleCoords[current].inputInterval;
                    currentLineX += moduleCoords[current].inputInterval;
                }                
            }
        }

        void EditButtonForLocalIn(int x, int y)
        {
            if (GUI.Button(new Rect(x - 6, y - 10, 35, 30), "Edit"))
            {
                // Go to AddModuleLocalIn screen (disable input to regulation button)
                plusRegulation = false;
                currentScreen = MenuScreens.AddModuleLocalIn;
                guiManager.CurrentMenu = MenuScreens.AddModuleLocalIn;  
            }
        }

        /// <summary>
        /// This buttons allow to change the pandemonium group for a regulatory
        /// neuron in a given module.
        /// </summary>
        void PandemButtons(int module)
        {
            // These buttons will be shown only in certain menu options, so that
            // they do not distract when they are not needed.
            if (currentScreen == MenuScreens.AddModule ||
                currentScreen == MenuScreens.EditInToReg)
            {
                // Left button to decrease the value
                if (GUI.Button(moduleCoords[module].leftButton, "", "leftButton"))
                {
                    // We pass "module + 1" since the moduleId for our module
                    // index here is not the same: moduleId = moduleIdx + 1.
                    // ModuleId = 0 corresponds to the base, which we do not
                    // represent in the scheme!
                    guiManager.ModifyPandemonium(genome.Regulatory + 1,
                        pandemoniumsInt[module],
                        module + 1, false); 

                } 
                // Right button to increase the value
                if (GUI.Button(moduleCoords[module].rightButton, "", "rightButton"))
                {
                    guiManager.ModifyPandemonium(genome.Regulatory + 1,
                        pandemoniumsInt[module],
                        module + 1, true);    
                }                 
            }
        }

        /// <summary>
        /// Allows to modify the input controlling regulatory neurons.
        /// </summary>
        void RegulationInput(int module)
        {
            if (plusRegulation)
            {
                string tooltipId = "isHovering" + module.ToString();
                if (GUI.Button(moduleCoords[module].plusButton, 
                    new GUIContent ("", tooltipId), "plusButton"))
                {
                    moduleThatCalled = module;
                    // Remember: here the module 1 takes index 0!
                    // The last method uses different methods (there is no need
                    // to change anything in the existing population, only take
                    // notes for the new one).
                    if (module == genome.Regulatory)
                    {
                        currentScreen = MenuScreens.AddModuleRegulation;
                        guiManager.CurrentMenu = MenuScreens.AddModuleRegulation;  
                    }
                    else
                    {
                        // guiManager needs to know which module called, and
                        // also needs a copy of the in-to-reg list in case
                        // the changes are not saved.
                        guiManager.ModuleThatCalled = module;
                        // We do NOT want to pass a reference, because modifications
                        // to the list would also affect the copy!
                        guiManager.InToRegCopy = new List<newLink>();
                        foreach (newLink link in guiManager.RegulatoryInputList[module + 1])
                        {
                            guiManager.InToRegCopy.Add(link);
                        }

                        currentScreen = MenuScreens.EditInToRegGetInfo;
                        guiManager.CurrentMenu = MenuScreens.EditInToRegGetInfo;
                    }
                }                 
                var hover = GUI.tooltip;
                if (hover == tooltipId)
                {
                    DisplayRegulatoryInput(module);
                }
            }
        }

        /// <summary>
        /// Displays the input towards the selected regulatory neuron. To keep
        /// the screen from bloating we do this with a box of text only when
        /// the mouse is hovering over the button for editing this information.
        /// </summary>
        void DisplayRegulatoryInput(int module)
        {
            int lines = 8;
            int heightPerLine = 16;

            string message = "Click to edit!\n\n";

            const string intro = "These are the current inputs for this regulatory neuron.\n";
            const string lastModule = "\nA provisional connection with bias will " +
                                      "be made if you do not select any inputs.\n";
            message += intro;
            if (module == genome.Regulatory)
            {
                message += lastModule;
                lines += 4;
            }
            message += GetRegInput(module);

            // Depending on the number of lines, we adjust the size of the text box.
            moduleCoords[module].plusButtonInfo.height = lines * heightPerLine;

            // Avoids displaying the box outside of the screen
            Rect rect = moduleCoords[module].plusButtonInfo;
            if (moduleCoords[module].plusButtonInfo.xMax > Screen.width)
            {
                rect.x = rect.x - rect.width - 35;
            }
            if (moduleCoords[module].plusButtonInfo.yMax > Screen.height)
            {
                rect.y = rect.y - rect.height;
            }
            GUI.Label(rect, message, "Box2");
        }

        string GetRegInput(int module)
        {
            string stringList = "\n{";

            for (int j = 0; j < guiManager.RegulatoryInputList[module + 1].Count; ++j)
            {
                stringList += " " + targetSourceStringById[guiManager.RegulatoryInputList[module + 1][j].otherNeuron];
            }

            stringList += " }\n";
            return stringList;
        }

        /// <summary>
        /// In AddModuleLocalOut we want to highlight regulatory neurons, but not
        /// the modules. So the easiest way is to render the regulatory neurons
        /// again on top of the dimmed modules. The cost of this should be less
        /// than rendering the regulatory neurons always as different entities.
        /// </summary>
        void HighlightRegulatory()
        {
            for (int i = 0; i <= genome.Regulatory; ++i)
            {
                GUI.Label(moduleCoords[i].regulatoryNeuron, regulatoryNeuronTexture); 
                // Adds the regulatory neuron ID.
                Text(moduleCoords[i].regulatoryIdX, moduleCoords[i].regulatoryIdY, 
                     moduleText, "R" + (i + 1).ToString());
            }            
        }

        // This the module texture.
    	void Module(int x, int y)
        {
            // Beware textures are resized (at loading) to fit length and height
            // power of 2. The process is not perfect and may affect aspect ratio.
            // In our case we used extra transparent pixels to fit 256x128.
            GUI.Label(new Rect(x - 6, y - 16, moduleTexture.width, moduleTexture.height),
                moduleTexture); 
        }

        // This displays a text message.
        void Text(int x, int y, GUIStyle style, string textString)
        {
            GUI.Label(new Rect(x, y, 1, 1), textString, style);
        }

        // This draws a line.
        void Line(int x, int y, int length)
        {
            GUI.Label(new Rect(x, y, lineWidth, length), lineTexture);   
        }
        void LongLine(int x, int y, int length)
        {
            GUI.Label(new Rect(x, y, lineWidth, length), longLineTexture);   
        }

        #endregion

        #region Update Structure Methods

        /// <summary>
        /// We create and initialize ModuleCoords and NeuronCoords instances
        /// with the coordinates for all the elements we need to represent
        /// on screen for each module and input/output neurons.
        /// We declare one extra module in case we are in the screen for adding
        /// new modules.
        /// </summary>
        void InitializeCoords()
        {
            int x;
            int y = Screen.height - 60;
            int horizontalSpace = 100;

            // These are constant, so only the first time!
            if (inputNeuronCoords.Count == 0)
            {
                // Coordinates for input neuron elements
                for (int i = 0; i <= genome.Input; ++i)
                {
                    x = (int)(Screen.width * 0.4f) + 30 + i * horizontalSpace;
                    inputNeuronCoords.Add(new NeuronCoords(true, x, 80));
                }

                // Coordinates for output neuron elements
                for (int i = 0; i < genome.Output; ++i)
                {
                    x = (int)(Screen.width * 0.4f) + 30 + i * horizontalSpace;
                    outputNeuronCoords.Add(new NeuronCoords(false, x, y));
                }                
            }

            // Coordinates for elements in modules
            horizontalSpace = 250;
            y = 250;
            // Again, avoid repeating this process:
            int nowDone = moduleCoords.Count;
            for (int i = nowDone; i <= genome.Regulatory; ++i)
            {
                //x = (int)(Screen.width * 0.4f) + 30 + i * horizontalSpace;
                x = (int)(Screen.width * 0.4f) - 60 + i * horizontalSpace;
                moduleCoords.Add(new ModuleCoords());
                moduleCoords[i].NewOutputCount(localOutTargets[i].Count);
                moduleCoords[i].NewInputCount(localInSources[i].Count);
                moduleCoords[i].ResetTo(x, y);
            }
        }

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
            moduleTexture = (Texture2D)Resources.Load("Textures/Module");
            inputNeuronTexture = (Texture2D)Resources.Load("Textures/InputNeuron");
            outputNeuronTexture = (Texture2D)Resources.Load("Textures/OutputNeuron");
            regulatoryNeuronTexture = (Texture2D)Resources.Load("Textures/RegulatoryNeuron");
            InitializeRectangleTextures();

            // Color transparentWhite = new Color(1f,1f,1f,0.4f);
            Color myColuor = new Color(0.1f,0.1f,0.1f,0.6f);
            transparentTexture = new Texture2D(1, 1);
            transparentTexture.SetPixel(0, 0, myColuor);
            transparentTexture.Apply();
        }

        /// <summary>
        /// Initializes all textures that are rectangles.
        /// </summary>
        void InitializeRectangleTextures()
        {
            const int lineSize = 20;
            const int longLineSize = 50;

            lineTexture = new Texture2D(lineWidth, lineSize);
            longLineTexture = new Texture2D(lineWidth, longLineSize);  

            RectangleTexture(lineWidth, lineSize, lineTexture, Color.white);
            RectangleTexture(lineWidth, longLineSize, longLineTexture, Color.white);
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
        /// Initializes the list with the inputs for regulatory neurons.
        /// Otherwise we take the information from guiManager.
        /// </summary>
        void InitializeRegulatoryInputList()
        {
            guiManager.RegulatoryInputList = new List<List<newLink>>();

            // Module index = 0 is left empty.
            guiManager.RegulatoryInputList.Add(new List<newLink>());

            for (int i = 0; i < genome.Regulatory; ++i)
            {
                guiManager.RegulatoryInputList.Add(GetInputToReg(i));
            }

			// For the new module.
            guiManager.RegulatoryInputList.Add(new List<newLink>());
		}

        /// <summary>
        /// Creates a list with all the neurons that target the regulatory
        /// neuron in a module (also gets the connection weights).
        /// </summary>
        List<newLink> GetInputToReg(int module)
        {
            // Remember the first module takes index 0 in this script.
            List<newLink> returnList = new List<newLink>();

            int regIndex;
            // Gets the index for the regulatory neuron in the module.
            if (genome.NeuronGeneList.FindRegulatory(module + 1, out regIndex))
            {
                // With the index we get the Id.
                uint regId = genome.NeuronGeneList[regIndex].Id;
                // Looks for connections with regId as target
                foreach (ConnectionGene connection in genome.ConnectionGeneList)
                {
                    if (connection.TargetNodeId == regId)
                    {
                        newLink toBeAdded = new newLink();
                        toBeAdded.otherNeuron = connection.SourceNodeId;
                        toBeAdded.weight = connection.Weight;
                        toBeAdded.id = connection.InnovationId;
						returnList.Add(toBeAdded);
                    }
                }
            }
            return returnList;
        }

        void UpdateLabels()
        {
            // Checks if the list for names has already been created (so we avoid
            // overwirting).
            if (inputLabels.Count == 0)
            {
                // If there is a file with names, we load them
                if (System.IO.File.Exists(namesPath))
                {
                    GuiXmlIO.ReadLabels(out inputLabels, out outputLabels, out moduleLabels);
                }
                else
                // Otherwise we create some default labels
                {
                    DefaultLabels();
                }
            }
            // Even if we have already read this genome, we are adding a module.
            // But beware in case we entered the AddModule menu and then aborted, 
            // because then this has already been done!
            if (moduleLabels.Count < genome.Regulatory + 1)
            {
                moduleLabels.Add("New Module");
            }
        }

        /// <summary>
        /// Input, output and regulatory neurons, as well as modules, may have
        /// custom labels (as well as their ID values). Here we get default
        /// labels (like "Input1" and so on).
        /// </summary>
        void DefaultLabels()
        {
            inputLabels.Add("Bias"); 
            for (int i = 1; i <= genome.Input; ++i)
            {
                inputLabels.Add("Input" + (i).ToString()); 
            } 
            for (int i = 0; i < genome.Output; ++i)
            {
                outputLabels.Add("Output" + (i + 1).ToString()); 
            } 
            while (moduleLabels.Count < genome.Regulatory)
            {
                moduleLabels.Add("Module" + (moduleLabels.Count + 1).ToString());
            }  
        }

        /// <summary>
        /// In order to represent our modules we need to know how many local in/out
        /// neurons each module has, and which targets they have. Obviously this 
        /// should NOT be computed each frame, so we save the information. 
        /// We only update this information if we get a new genome (with a 
        /// possibly new structure).
        /// </summary>
        void UpdateLocalInOutInfo()
        {
            localInSources = new List<List<string>>();
            localOutTargets = new List<List<string>>();
            targetSourceStringById = new Dictionary<uint, string>();

            guiManager.GuiVarReset();
            // Empty list for module = 0 (the base fo the genome).
            guiManager.LocalOutputList.Add(new List<newLink>());
            guiManager.LocalInputList.Add(new List<newLink>());

            MakeTargetSourceStringById();

            // Goes through the different modules (genome.Regulatory is the last)
            for (int i = 0; i < genome.Regulatory; ++i)
            {
                // Adds an empty list for guiVar.LocalOutputList and
                // guiVar.LocalInputList (accessed through guiManager).
                // The contents will be written in
                // GetSourceTargetsInModule --> GetSourceTarget.
                guiManager.LocalOutputList.Add(new List<newLink>());
                guiManager.LocalInputList.Add(new List<newLink>());

                // Gets the sources for each local input.
                localInSources.Add(GetSourceTargetsInModule(i + 1, NodeType.Local_Input));
                // Gets the targets for each local output.
                localOutTargets.Add(GetSourceTargetsInModule(i + 1, NodeType.Local_Output));
            }  

            // Adds extra information for the new module!
            guiManager.LocalOutputList.Add(new List<newLink>());
            guiManager.LocalInputList.Add(new List<newLink>());
            List<string> extraList = new List<string>(); 
            extraList.Add("Add");
            localOutTargets.Add(extraList);
            extraList = new List<string>(); 
            extraList.Add("Edit");
            localInSources.Add(extraList);
        }

        /// <summary>
        /// Gets the source or target for each local input or output neuron
        /// in a module.
        /// </summary>
        List<string> GetSourceTargetsInModule(int module, NodeType inOrOut)
        {
            List<string> innerList = new List<string>();
            bool started = false;
            // Starts after bias + input + output + regulatory.
            for (int i = genome.NeuronGeneList.LastBase + 1; 
                 i < genome.NeuronGeneList.Count; ++i)
            {
                // Starts after the first local input/output of the module.
                if (!started && genome.NeuronGeneList[i].ModuleId == module &&
                    genome.NeuronGeneList[i].NodeType == inOrOut)
                {
                    started = true;
                }
                if (started)
                {
                    // If we find a new module or a different type, we stop counting.
                    if (genome.NeuronGeneList[i].ModuleId != module ||
                        genome.NeuronGeneList[i].NodeType != inOrOut)
                    {
                        return innerList;
                    }
                    // Otherwise we add this neuron
                    innerList.Add(GetSourceTarget(module, i, inOrOut));
                }
            }
            // If there are no hidden neurons (should NOT be the case) we exit here:
            return innerList;
        }

        /// <summary>
        /// Given an index (and type), searchs for its source (local in) or 
        /// target (local out) and returns a string with this information.
        /// Module is given so guiManager.LocalOutputList and guiManager.LocalInputList
        /// can be updated here.
        /// </summary>
        string GetSourceTarget(int module, int index, NodeType inOrOut)
        {
            // This is the protected connection we will add to LocalOutputList
            // and LocalInputList.
            newLink protectedConnect = new newLink();

            // With the given index, we know the neuron ID.
            uint ID = genome.NeuronGeneList[index].Id;

			// Looks for the source or target:
            if (inOrOut == NodeType.Local_Input)
            {
                // Looks for a connection with this ID as target:
                foreach (ConnectionGene connection in genome.ConnectionGeneList)
                {
                    if (connection.TargetNodeId == ID)
                    {
                        protectedConnect.otherNeuron = connection.SourceNodeId;
                        protectedConnect.weight = connection.Weight;
                        protectedConnect.id = connection.InnovationId;
                        guiManager.LocalInputList[module].Add(protectedConnect);
                        break;
                    }
                }                
            }
            else
            {
                // Looks for a connection with this ID as source:
                foreach (ConnectionGene connection in genome.ConnectionGeneList)
                {
                    if (connection.SourceNodeId == ID)
                    {
                        protectedConnect.otherNeuron = connection.TargetNodeId;
                        protectedConnect.weight = connection.Weight;
                        protectedConnect.id = connection.InnovationId;
                        guiManager.LocalOutputList[module].Add(protectedConnect);
                        break;
                    }
                }                
            }

            // We have our target ID.
            // We can now return our string with the target ID. This information
            // is stored in a list.
            return targetSourceStringById[protectedConnect.otherNeuron];
        }

        /// <summary>
        /// Counts the order of input, output and regulatory neurons, and writes
        /// in a dictionary a string with its ID (for example, "R3" for the third 
        /// regulatory neuron in the list). The keys are innovation ID values for
        /// these neurons.
        /// Note input includes bias.
        /// </summary>
        void MakeTargetSourceStringById()
        {

            // IMPORTANT: For bias, input and output neurons the index in the
            // list and their ID are the same, but not necessarily for regulatory.

            // First, we add the bias neuron:
            targetSourceStringById.Add(0, "Bias");
            // Use <= so we include the bias neuron, in case InputAndBiasNeuronCount
            // is not used anymore.
            for (int i = 1; i <= genome.Input; ++i)
            {
                targetSourceStringById.Add((uint)i, "I" + i.ToString());                
            }

            // Output neurons.
            // Again, <= accounts for the bias neuron.
            int count = 1;
            for (int i = genome.Input + 1; i <= genome.Input + genome.Output; ++i)
            {
                targetSourceStringById.Add((uint)i, "O" + count.ToString());
                ++count;
            }

            NeuronGeneList neuronList = genome.NeuronGeneList;

            // Regulatory neurons.
            count = 1;
            for (int i = genome.Input + genome.Output + 1;
                 i <= neuronList.LastBase; ++i)
            {
                // We do not write the variable "i" as the ID because, unlike
                // output neurons, regulatory neurons may take non-consecutive
                // values.
                targetSourceStringById.Add(neuronList[i].Id, "R" + count.ToString());
                ++count;
            }
            // We also need to include the regulatory neuron for the new module.
            // But is does not exist yet! However, we know it will be the first
            // new element, so its Id must be the latest Id used + 1!
            uint newRegId = genome.FindLastId() + 1;
            targetSourceStringById.Add(newRegId, "R" + count.ToString());

            // Includes local input neurons in the dictionary in case there is
            // a connection from local output to local in (in which case we need
            // that target in the dictionary!)
            // For the same reason, includes local output neurons, also if there
            // is a connection from a local output to a regulatory neuron.
            count = 1;
            uint firstRegIndx = (uint)genome.Input + (uint)genome.Output;
            NodeType currentType = NodeType.Local_Input;
            for (int i = neuronList.LastBase + 1;
                i < neuronList.Count; ++i)
            {
                // Resets the count for new neuron types.
                if (neuronList[i].NodeType != currentType)
                {
                    currentType = neuronList[i].NodeType;
                    count = 1;
                }

                // We are only interested in local_in with local_out sources.
                // These have an Id that is always > input + 1.
                // These connections (at least in this version) have only one
                // source, but there is no easy way to go through hashSets.
                if (neuronList[i].NodeType == NodeType.Local_Input)
                {
                    if (HashSetContainsBiggerThan(neuronList[i].SourceNeurons,
                                                  (uint)(genome.Input + 1)))
                    {
                        targetSourceStringById.Add(neuronList[i].Id, "M" +
                                                   neuronList[i].ModuleId.ToString() +
                                                   "i" + count.ToString());
                    }
                }

                // We are only interested in local_out with local_in as targets.
                // These will always have an Id > LastBase.
                if (neuronList[i].NodeType == NodeType.Local_Output)
                {
                    if (HashSetContainsBiggerThan(neuronList[i].TargetNeurons,
                                                  firstRegIndx))
                    {
                        targetSourceStringById.Add(neuronList[i].Id, "M" + 
                                                   neuronList[i].ModuleId.ToString() +
                                                   "o" + count.ToString());
                    }
                }
                ++count;
            }
        }

        /// <summary>
        /// Determines whether the provided hashSet contains any elements bigger
        /// than a given value.
        /// There is a copy in EspCyclicNetworkFactory.
        /// </summary>
        bool HashSetContainsBiggerThan(HashSet<uint> hashSet, uint value)
        {
            foreach (uint element in hashSet)
            {
                if (element > value)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// In this list we keep track of the pandemonium state in each module.
        /// </summary>
        void UpdatePandemoniums()
        {
            pandemoniums = new List<string>();
            pandemoniumsInt = new List<int>();

            for (int i = 0; i <= genome.NeuronGeneList.LastBase; ++i)
            {
                if (genome.NeuronGeneList[i].NodeType == NodeType.Regulatory)
                {
                    int pandValue = genome.NeuronGeneList[i].Pandemonium;
                    string valueToString;
                    if (pandValue == 0)
                    {
                        valueToString = "N";
                    }
                    else
                    {
                        valueToString = pandValue.ToString();
                    }
                    pandemoniums.Add(valueToString);
                    pandemoniumsInt.Add(pandValue);
                    guiManager.SetPandemonium((int)genome.NeuronGeneList[i].ModuleId,
                                              pandValue);
                }
            }
            // We add the pandemonium state for the new module:
            pandemoniums.Add("N");
            pandemoniumsInt.Add(0);
        }

        #endregion

        #region Other Private Methods

        /// <summary>
        /// This method allows to change all the labels in the scheme.
        /// </summary>
        void NewLabels()
        {
            // Input labels (includes bias!):
            for (int i = 0; i <= genome.Input; ++i)
            {
                inputLabels[i] = GUI.TextField(
                        new Rect(inputNeuronCoords[i].labelX,
                                 inputNeuronCoords[i].labelY, 90, 23),
                        inputLabels[i], 10);
            }

            // Output labels:
            for (int i = 0; i < genome.Output; ++i)
            {
                outputLabels[i] = GUI.TextField(
                    new Rect(outputNeuronCoords[i].labelX,
                        outputNeuronCoords[i].labelY, 90, 23),
                    outputLabels[i], 10);
            }

            // Module labels:
            for (int i = 0; i < genome.Regulatory + 1; ++i)
            {
                moduleLabels[i] = GUI.TextField(moduleCoords[i].moduleLabelButton,
                                               moduleLabels[i], 13); 
            }

            // Saves the new labels
            GuiXmlIO.WriteLabels(inputLabels, outputLabels, moduleLabels);
        }

        #endregion
    }
}

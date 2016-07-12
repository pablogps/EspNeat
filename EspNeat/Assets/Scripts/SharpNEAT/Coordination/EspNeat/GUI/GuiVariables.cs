using UnityEngine;
using System.Collections.Generic;

namespace SharpNeat.Coordination
{
	/// <summary>
	/// This class simply encapsulates the multiple variables used in GUI 
	/// management in EspNeatOrganizer, to keep things tidy and less troublesome.
	/// We include a reset method.
	/// </summary>
	public class GuiVariables {
	    private MenuScreens currentMenu = MenuScreens.Edit;

	    private Dictionary<int, int> pandemonium;
        // See "newLink" struct deffinition at the end of this script!
        // But it is only an uint (otherNeuron) and a double (weight)
        private List<List<newLink>> localOutputList;
        private List<List<newLink>> localInputList;
        private List<List<newLink>> regulatoryInputList;

	    private GUIStyle bigText = new GUIStyle();
	    private GUIStyle normalText = new GUIStyle();
	    private string warningMessage; 
	    private bool tryReset = false;

	    #region Constructor

	    public GuiVariables()
	    {
	        Reset();

	        // Here we define our style for GUI labels:
	        bigText.fontSize = 24;
	        bigText.fontStyle = FontStyle.Bold;
	        bigText.normal.textColor = Color.white;
	        normalText.fontSize = 16;
	        normalText.normal.textColor = Color.white;

	        warningMessage = "Do you really want to delete save files?" + 
	                         "\n" +"This change cannot be undone." + "\n\n" +
	                         "You can access and backup these files here:" +
	                         "\n" + Application.persistentDataPath;
	    }

	    #endregion

	    #region Properties

	    public MenuScreens CurrentMenu
	    {
	        get { return currentMenu; }
	        set { currentMenu = value; }
	    }

	    public GUIStyle BigText
	    {
	        get { return bigText; }
	    }

	    public GUIStyle NormalText
	    {
	        get { return normalText; }
	    }

	    public Dictionary<int, int> Pandemonium
	    {
	        get { return pandemonium; }
	        set { pandemonium = value; }
	    }

        public List<List<newLink>> LocalOutputList
	    {
	        get { return localOutputList; }
			set { localOutputList = value; }
		}

        public List<List<newLink>> LocalInputList
		{
            get { return localInputList; }
            set { localInputList = value; }
		}

        public List<List<newLink>> RegulatoryInputList
		{
            get { return regulatoryInputList; }
            set { regulatoryInputList = value; }
		}

	    public bool TryReset
	    {
	        get { return tryReset; }
	        set { tryReset = value; }
	    }

	    public string WarningMessage
	    {
	        get { return warningMessage; }
	    }

	    #endregion

	    #region Methods

	    public void Reset()
	    {
	        pandemonium = new Dictionary<int, int>();
            localOutputList = new List<List<newLink>>(); 
            localInputList = new List<List<newLink>>();  
            regulatoryInputList = new List<List<newLink>>();        
	    }
	    #endregion
	}

    /// <summary>
    /// This struct is used so we can have the complete information for new
    /// links in the same list. We need a source or target (other neuron) and
    /// the weight. (Located in the GuiVariables script.)
    /// </summary>
    public struct newLink
    {
        public uint otherNeuron;
        public double weight;
        public uint id;
    }
}

using UnityEngine;
using System.Collections.Generic;

namespace SharpNeat.Coordination
{
	/// <summary>
	/// This class simply encapsulates the multiple variables used in GUI 
	/// management in EspNeatOrganizer, to keep things tidy and less troublesome.
	/// We include a reset method.
	/// </summary>
	public class GuiButtonRects {
        // Main menu
        public readonly Rect editModules = new Rect(Screen.width/2 - 300, Screen.height - 300, 182, 218);
        public readonly Rect exit = new Rect(Screen.width - 120, 60, 110, 40);
        public readonly Rect goToEvolution = new Rect(Screen.width/2 + 100, Screen.height - 300, 182, 218);
        public readonly Rect resetEvolution = new Rect(Screen.width - 120, 10, 110, 40);

        // Edit modules menu
        public readonly Rect addModule = new Rect(Screen.width/2 + 100, Screen.height - 300, 182, 218);
        public readonly Rect resetActive = new Rect(Screen.width/2 - 300, Screen.height - 300, 182, 218);

        // Play GUI
        public readonly Rect clearBest = new Rect(25, 240, 60, 40);
        public readonly Rect confirmReset = new Rect(Screen.width / 2 - 300, Screen.height / 2 + 100, 100, 40);
        public readonly Rect info = new Rect(10, Screen.height - 70, 100, 60);
        public readonly Rect inToReg = new Rect(20, 430, 110, 48);
        public readonly Rect manualSelection = new Rect(10, 90, 88, 64);
        public readonly Rect returnToMain = new Rect(33, 380, 40, 32);
        public readonly Rect runBest = new Rect(25, 190, 60, 40);
        public readonly Rect safeReturn = new Rect(Screen.width / 2, Screen.height / 2 + 100, 120, 40);
        public readonly Rect startEvolution = new Rect(10, 10, 88, 64);
        public readonly Rect stopEvolution = new Rect(32, 20, 44, 44);
        public readonly Rect onlyWeights = new Rect(20, 310, 73, 48);

        // Other Methods
        public readonly Rect inToRegApply = new Rect(Screen.width - 170, Screen.height - 130, 130, 40);
        public readonly Rect inToRegDiscard = new Rect(Screen.width - 170, Screen.height - 70, 130, 40);

        //Manual selection GUI
        public readonly Rect advanceGeneration = new Rect(12, 150, 120, 40);
        public readonly Rect punishReward = new Rect(17, 84, 73, 48);
        public readonly Rect hourGlass = new Rect(Screen.width / 2 - 110, Screen.height - 75, 54, 60);
        public readonly Rect timeScale = new Rect (Screen.width / 2 - 45, Screen.height - 50, 200, 10);
        public readonly Rect wait = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 30, 1, 1);

        // Add module GUI
        public readonly Rect acceptAndCreate = new Rect(Screen.width - 170, Screen.height - 70, 130, 40);
        public readonly Rect allScreen = new Rect(0, 0, Screen.width, Screen.height);
        public readonly Rect backFromAdd = new Rect(Screen.width - 100, Screen.height - 215, 40, 32);
        public readonly Rect fromInputToLabelEdit = new Rect(5, 33, Screen.width - 10, 20);
        public readonly Rect fromOutputToLabelEdit = new Rect(5, Screen.height - 111, Screen.width - 10, 20);
        public readonly Rect weights = new Rect(Screen.width - 115, Screen.height - 150, 73, 48);

        // Edit weights screen
        public readonly Rect allowNewLocal;
        public readonly Rect allowProtected;
        public readonly Rect changeWeights;
        public readonly Rect currentModule;
        public readonly Rect downButton;
        public readonly Rect fromText;
        public readonly Rect leftButton;
        public readonly Rect noConnectionsText;
        public readonly Rect one;
        public readonly Rect rightButton;
        public readonly Rect selectTypeText;
        //public readonly Rect showLocalInput;
        public readonly Rect showLocalOutput;
        public readonly Rect showRegInput;
        public readonly Rect upButton;
        public readonly Rect weightsBackground;
        public readonly Rect weightText;
        public readonly Rect zero;


        // Used in rects that adapt to the screen size (at creation, at the
        // moment not truly dynamic.
        public readonly int x;
        public readonly int y;
        public readonly int maxSliders;

		/// <summary>
		/// Constructor that adjusts some dimensions. 
		/// </summary>
		public GuiButtonRects()
		{
            int menuWidth = 1000;
            int menuHeight;
            if (Screen.height - 200 < 600)
            {
                menuHeight = Screen.height - 200;
            }
            else
            {
                menuHeight = 600;
            }

            // Adjusts edit weights screen rects:
            if (Screen.width < menuWidth)
            {
                UnityEngine.Debug.Log("Screen too narrow");
            }
            if (Screen.height < menuHeight)
            {
                UnityEngine.Debug.Log("Screen too short");
            }

            x = (Screen.width - menuWidth)/2;
            y = (Screen.height - menuHeight)/2;

            // (Menu's height - starting height for sliders) / space required
            // per slider.
            maxSliders = (menuHeight - 190)/50; 

            allowNewLocal = new Rect(x + 50, y + 100, 370, 40);
            allowProtected = new Rect(x + 450, y + 100, 320, 40);
            changeWeights =  new Rect(x + 495, y + 40, 110, 40);
            currentModule = new Rect(x + 635, y + 50, 100, 40);
            downButton =  new Rect(x + 935, y + maxSliders * 50 + 160, 20, 20);
			fromText = new Rect(x + 45, y + 150, 100, 40);
            leftButton = new Rect(x + 760, y + 50, 20, 20);
            noConnectionsText = new Rect(x + 230, y + 150, 100, 40);
            one = new Rect(x + 870, y + 100, 105, 30);
            rightButton = new Rect(x + 935, y + 50, 20, 20);
			selectTypeText = new Rect(x + 45, y + 50, 100, 40);
			//showLocalInput = new Rect(x + 215, y + 40, 100, 40);
			showLocalOutput = new Rect(x + 215, y + 40, 100, 40);
            showRegInput = new Rect(x + 335, y + 40, 140, 40);
            upButton =  new Rect(x + 935, y + 160, 20, 20);
            weightsBackground = new Rect(x, y, menuWidth, menuHeight);
			weightText = new Rect(x + 125, y + 150, 100, 40);	
            zero = new Rect(x + 755, y + 100, 105, 30);
		}
	}
}

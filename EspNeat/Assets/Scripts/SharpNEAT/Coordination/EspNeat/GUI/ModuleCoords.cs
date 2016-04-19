using UnityEngine;
using System.Collections;

namespace SharpNeat.Coordination
{
	public class ModuleCoords{		

    	// Making all these private with accessors for all would be a bit safer,
        // but probably not really worth it (this calss was originally meant
        // to be a struct simply gathering data).
    	public int outputCount;
    	public int inputCount;

    	public int x;
    	public int y;

    	public int outputInterval;
        public int inputInterval;
        public int inputInterval2 = 50;

    	public int idX;
    	public int idY;

    	public int labelX;
    	public int labelY;

    	public int pandLabelX;
    	public int pandLabelY;

    	public int inputLabelX;
        public int inputLabelY;
        public int inputLabelLongY;
        public int inputLineY;
        public int inputLineLongY;

    	public int outputLineX;
    	public int outputLineY;
        public int outputLabelX;
        public int outputLabelY;
        public int outputLabelLongY;

        public int regulatoryIdX;
        public int regulatoryIdY;
        public Rect regulatoryNeuron;

        public Rect dragButton;
        public bool dragPressed;
        public Rect highLightArea;

        public Rect leftButton;
        public Rect rightButton;

        public Rect plusButton;
        public Rect plusButtonInfo;

        public Rect moduleLabelButton;

		public Rect addAllIn;
		public Rect removeAllIn;
		public Rect addAllOut;
		public Rect removeAllOut;

        private const int moduleSizeX = 150;
        private const int textOffset = 8;
        private const int labelOffsetY = 27;
        private const int pandOffsetY = 47;
        private const int localOutOffsetX = -15;

        public ModuleCoords()
        {   
            outputCount = 1;
            inputCount = 1;
            outputInterval = (int)((double)moduleSizeX / 
                             ((double)(outputCount) + 1.0));
            inputInterval = (int)((double)moduleSizeX / 
                            ((double)(inputCount) + 1.0));

            dragPressed = false;

            ResetTo(50, 250);
        }

        public void ResetTo(int newX, int newY)
        {            
            x = newX;
            y = newY;

            idX = x + textOffset;
            idY = y + 10;

            labelX = x + textOffset;
            labelY = y + labelOffsetY;

            pandLabelX = x + textOffset;
            pandLabelY = y + pandOffsetY;

            inputLabelX = x + textOffset;
            inputLabelY = y - 30;
            inputLabelLongY = y - 60;
            inputLineY = y - 13;
            inputLineLongY = y - 42;

            outputLineX = x + localOutOffsetX + textOffset;
            outputLineY = y + 66;
            outputLabelX = x + localOutOffsetX;
            outputLabelY =  y + 85;
            outputLabelLongY =  y + 115;

            regulatoryIdX = x + 161;
            regulatoryIdY =  y + 17;

            dragButton = new Rect(x - 10, y, 150, 80);
            highLightArea = new Rect(x - 10, y - 76, 206, 226);
            leftButton = new Rect(x + 75, y + 46, 20, 20);
            rightButton = new Rect(x + 113, y + 46, 20, 20);
            plusButton = new Rect(x + 162, y - 13, 20, 20);
            plusButtonInfo = new Rect(x + 190, y - 10, 200, 200);
            moduleLabelButton = new Rect(x + textOffset, y + labelOffsetY, 115, 23);
            regulatoryNeuron = new Rect(x + 153, y + 8, 37, 37);

            addAllIn = new Rect(outputLineX + 90, y - 110, 80, 30);
            removeAllIn = new Rect(outputLineX, y - 110, 80, 30);
            addAllOut = new Rect(outputLabelX + 90,  y + 152, 80, 30);
            removeAllOut = new Rect(outputLabelX,  y + 152, 80, 30);
        }

        public void NewOutputCount(int newCount)
        {
            outputInterval = (int)((double)moduleSizeX / 
                             ((double)(newCount) + 1.0));            
        }

        public void NewInputCount(int newCount)
        {
            inputInterval = (int)((double)moduleSizeX / 
                            ((double)(newCount) + 1.0));
        }
	}	
}
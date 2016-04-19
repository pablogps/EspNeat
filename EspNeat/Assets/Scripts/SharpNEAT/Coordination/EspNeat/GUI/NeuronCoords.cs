using UnityEngine;
using System.Collections;

namespace SharpNeat.Coordination
{
	public class NeuronCoords{		

        public int x = 25;
        public int y = 130;
        public Rect neuronRect;
        public int idX;
        public int idY;
        public int labelY;
        public int labelX;

        private int neuronRectOffsetX;
        private int neuronRectOffsetY;
        private int idOffsetX;
        private int labelOffsetY;

        private const int inneuronRectOffsetX = 7;
        private const int inneuronRectOffsetY = 9;
        private const int inIdOffsetx = 16;
        private const int inLabelOffsetY = -15;

        private const int outneuronRectOffsetX = 7;
        private const int outneuronRectffsetY = -6;
        private const int outIdOffsetx = 13;
        private const int outLabelOffsetY = -30;

        public NeuronCoords(bool isInput, int iniX, int iniY)
        {
            // We set the offset variables depending on the neuron type.
            // Alternatively we could create different classes.
            if (isInput)
            {
                neuronRectOffsetX = inneuronRectOffsetX;
                neuronRectOffsetY = inneuronRectOffsetY;
                idOffsetX = inIdOffsetx;
                labelOffsetY = inLabelOffsetY;
            }
            else
            {
                neuronRectOffsetX = outneuronRectOffsetX;
                neuronRectOffsetY = outneuronRectffsetY;
                idOffsetX = outIdOffsetx;
                labelOffsetY = outLabelOffsetY;
            }
            ResetTo(iniX, iniY);
        }

        public void ResetTo(int newX, int newY)
        {            
            x = newX;
            y = newY;
            neuronRect = new Rect(x + neuronRectOffsetX, y + neuronRectOffsetY, 32, 64);
            idX = x + idOffsetX;
            idY = y + 18;
            labelX = x;
            labelY = y + labelOffsetY;
        }
	}	
}
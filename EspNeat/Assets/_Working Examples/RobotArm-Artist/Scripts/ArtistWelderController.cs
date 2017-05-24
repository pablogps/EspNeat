using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SharpNeat.Phenomes;

namespace RobotArtist
{
    public class ArtistWelderController : UnitController {

        private static int ID_counter = 0;
        private int ID;

    	private class CurrentCamera
    	{
    		public GameObject cameraObject;
    		public Vector3 initialPosition;
    		public Quaternion initialOrientation;
    		public Vector3 newPosition = new Vector3(0f, 20f, 0f);
    	}

    	private class PixelPoint
    	{
    		public int x;
    		public int y;

    		public PixelPoint(int px, int py)
    		{
    			x = px;
    			y = py;
    		}

            public float Distance(PixelPoint neighbour)
            {
                float x1 = (float)x;
                float y1 = (float)y;
                float x2 = (float)neighbour.x;
                float y2 = (float)neighbour.y;
                return Mathf.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
            }
    	}

        private struct NeuralNetwork
        {
            public IBlackBox box;
            public ISignalArray inputArr;
            public ISignalArray outputArr;        
        }

        NeuralNetwork neuralNetwork = new NeuralNetwork();

        CurrentCamera currentCamera = new CurrentCamera();

        private GameObject paintingsBackground;

        private float startTime;
        // we may allow some extra time so that Tmax != 1/TmaxInverse
        private float timeForPainting = 20f; 
        private float maxTimeInverse = 0.0625f; //0.0625 for t = 16, 
        private bool isShowingPainting = false;

    	private GameObject bench;
    	private Material benchMaterial;
    	private Texture2D benchTexture;
        private const float pixelSizeX = 0.04f;
        private const float pixelSizeXinv = 25f;
        private const float pixelSizeZ = 0.032916667f;
        private const float pixelSizeZinv = 30.3797f;

        private static List<List<List<PixelPoint>>> pixelNeighbours =
                new List<List<List<PixelPoint>>>();
        private static bool isListCreated = false;

        private const int canvasX = 75;
        private const float canvasXinv = 0.0133333333f;
        private const int canvasY = 48;
        private const float canvasYinv = 0.0208333333f;

    	// We want a virtual representation of the canvas
        private float virtualCanvasCornerX = 0f;
        private float virtualCanvasCornerZ = 0f;

        private int targetId = -1;
        private bool pointingAtCanvas = false;

        // Note the shoulder has no boundaries!
        Joint jointShoulder = new Joint(null, 0f, 5f, 0f, 0f, 0f);
        Joint jointArm = new Joint(null, 0f, 5f, -38f, 35f, 0f);
        Joint jointElbow = new Joint(null, 0f, 5f, -68f, 69f, 0f);
        Joint jointPiston = new Joint(null, 0f, 1f, -0.72f, 0.34f, 0f);
        Joint jointManipulator = new Joint(null, 0f, 60f, 210f, 330f, 0f);

        bool paintNow = false;
        int brushSize = 0;

        // The manipulator tip is not a joint!
        private GameObject manipulatorTip;

        bool IsRunning;
    //------------------------------------------------------------------------------
    	void Start()
        {
            GetCameraSettings();

            paintingsBackground = GameObject.Find("BackGround").transform.
                                  Find("PaintingsBackground").gameObject;

            FindJointElements();

    		SetPosition();
            ++ID_counter;
            ID = ID_counter;

            CalculateCanvasCorner();

    		InstantiateBench();

            // Let's give the joints a little initial offset (to avoid overfitting!)
            jointPiston.toMove = Random.Range(-0.5f, 0.1f);
            MoveAll();

            if (isListCreated == false)
            {
                CreateNeighbourList();
                isListCreated = true;
            }

            // We want to know the time elapsed since the robot arm started drawing.
            // After the allocated time painting will stop and we will display the
            // result!
            startTime = Time.time;
        }
    //------------------------------------------------------------------------------
        /// <summary>
        /// The canvas corner is used in PointToId
        /// </summary>
        void CalculateCanvasCorner()
        {
            Vector3 temp = transform.position;
            virtualCanvasCornerX = temp.x + ((float)canvasX / 2f) * pixelSizeX;
            virtualCanvasCornerZ = temp.z + 1.9f + ((float)canvasY / 2f) * pixelSizeZ;         
        }
    //------------------------------------------------------------------------------
        void GetCameraSettings()
        {
            currentCamera.cameraObject = GameObject.Find("EvolutionCamera");
            // During evolution we want "EvolutionCamera", otherwise we want
            // "EditingCamera" (and "EvolutionCamera" will not be found outside
            // of evolution, because it will be inactive, so we can test if the
            // previous .Find returned an empty object to distinguish both situations!
            if (!currentCamera.cameraObject)
            {
                Debug.Log("camera not found!");
                currentCamera.cameraObject = GameObject.Find("EditingCamera");
            }
            currentCamera.initialPosition = currentCamera.cameraObject.transform.position;
            currentCamera.initialOrientation = currentCamera.cameraObject.transform.rotation;        
        }
    //------------------------------------------------------------------------------
        void FindJointElements()
        {
            jointShoulder.jointObject = transform.
                    Find("RobotArm_Welder_Base/RobotArm_Welder_Shoulder").gameObject;

            jointArm.jointObject = jointShoulder.jointObject.transform.
                    Find("RobotArm_Welder_Arm").gameObject;
            jointArm.currentValue = jointArm.jointObject.transform.localEulerAngles.x;

            jointElbow.jointObject = jointArm.jointObject.transform.
                    Find("RobotArm_Welder_Joint").gameObject;
            jointElbow.currentValue = jointElbow.jointObject.transform.localEulerAngles.z;

            jointPiston.jointObject = jointElbow.jointObject.transform.
                    Find("RobotArm_Welder_Piston").gameObject;
            jointPiston.currentValue = jointPiston.jointObject.transform.localPosition.x;

            jointManipulator.jointObject = jointPiston.jointObject.transform.
                    Find("RobotArm_Welder_Manipulator").gameObject;
            jointManipulator.currentValue = jointManipulator.jointObject.transform.localEulerAngles.z;

            manipulatorTip = jointManipulator.jointObject.transform.Find("ManipulatorTip").gameObject;        
        }
    //------------------------------------------------------------------------------
        /// <summary>
        /// After painting time is done:
        /// Sets "is showing painting" true, so we don't call this method again.
        /// Sets time scale very slow (so that we alleviate performance issues with
        /// the many, many pixel elements we are going to instantiate!)
        /// Destroys the workbench (which is substitued for the real pixelized version)
        /// Instantiates the pixels.
        /// </summary>
        void ShowPainting()
        {
            // New position for the camera, to see the paintings properly.
            currentCamera.cameraObject.transform.position = currentCamera.newPosition;
            currentCamera.cameraObject.transform.eulerAngles = new Vector3(90f, 180f, 0f);

            // Special background!
            paintingsBackground.SetActive(true);

            BenchDisplayPosition();

            isShowingPainting = true;
        }
    //------------------------------------------------------------------------------
        /// <summary>
        /// Sets a new position for the drawing canvases so that they can be seen
        /// better diring evaluation.
        /// </summary>
        void BenchDisplayPosition()
        {
            float row = (float)(1 + (ID - 1) / 4);
            float column = (float)(ID - 4 * ((int)row - 1));

            // This will be the reference position to build the canvas.
            Vector3 temp = new Vector3(4f, 12.5f, -3f);
            //Vector3 temp = new Vector3(-4.2f, 8f, 12.5f);
            // The increment for columns is 2.67f
            temp.x -= ((column - 1f) * 2f) * 1.65f;
            // The increment for rows is 2.3f
            temp.z += ((row - 1f) * 2f) * 1.2f;

            bench.transform.position = temp;        
        }
    //------------------------------------------------------------------------------
        void SetPosition()
        {
            Vector3 temp = transform.position;
            // x: between 20 and -20, at intervals of 8 (fits 4)
            temp.x = 12.0f - (ID_counter % 4f) * 8f;
            temp.z = -14.4f + (Mathf.Floor(ID_counter / 4f) + 1f) * 7.2f;
            transform.position = temp;
        }
    //------------------------------------------------------------------------------
        /// <summary>
        /// When this object is destroyed (for example, at the end of a generation)
        /// we need to adjust the ID counter. We also want to remove the accesory
        /// elements we have created.
        /// </summary>
        void OnDestroy()
        {
            // Reset the camera for normal view.
            currentCamera.cameraObject.transform.position = currentCamera.initialPosition;
            currentCamera.cameraObject.transform.rotation = currentCamera.initialOrientation;

            // Resets background.
            paintingsBackground.SetActive(false);

    		Destroy(bench);

            --ID_counter;
        }
    //------------------------------------------------------------------------------
        // Used (hopefully well) in Optimizer --> DestroyBest
        public override IBlackBox GetBox() {
            return neuralNetwork.box;
        }
    //------------------------------------------------------------------------------
        public override void Stop() {
            IsRunning = false;
        }
    //------------------------------------------------------------------------------  
        public override void Activate(IBlackBox box) {
            neuralNetwork.box = box;
            IsRunning = true;
        }
    //------------------------------------------------------------------------------  
        public override float GetFitness() {
            return 0f;
        } 
    //------------------------------------------------------------------------------
        public void UndoMovement() {
            jointShoulder.toMove *= -1f;
            jointArm.toMove *= -1f;
            jointElbow.toMove *= -1f;
            jointPiston.toMove *= -1f;
            jointManipulator.toMove *= -1f;
            MoveAll();
        }
    //------------------------------------------------------------------------------ 
        void FixedUpdate()
        {
            CheckTime();

            // Input signals are used in the neural controller
            neuralNetwork.inputArr = neuralNetwork.box.InputSignalArray;

            UpdateInputArray();
            PaintPixel();

            // The neural controller is activated
            neuralNetwork.box.Activate();
            // And produces output signals (also in an array)
            neuralNetwork.outputArr = neuralNetwork.box.OutputSignalArray;     

            // The first processing step returns outputs from -1 to +1 (instead of
            // from 0 to 1) and also checks if inputs needed to enable some joints
            // are active.
            AutomaticControl();
            //ManualControl();

            // Multiplies outputs by the speed of joints and the elapsed time and
            // then calls MoveAll.
            ProcessOutput();
        }
    //------------------------------------------------------------------------------
        /// <summary>
        /// Checks the elapsed time and triggers ShowPainting after the required time.
        /// </summary>
        void CheckTime()
        {
            if (Time.time - startTime > timeForPainting)
            {
                if (!isShowingPainting)
                {
                    ShowPainting();
                }
            }
        }
    //-----------------------------------------------------------------------------
        /// <summary>
        /// This method takes the output array from the neural network and updates
        /// the variables used in the movement of different robot arm joints.
        /// </summary>
        void AutomaticControl()
        {
            // Shoulder (rotation along vertical axe)
            jointShoulder.toMove = ActuatorValueIfEnabled(
                    neuralNetwork.outputArr[0], (float)neuralNetwork.outputArr[1]);
            // Arm and joint (finger-like rotation)
            AutomaticControlArmJoint();
            // Piston movement
            jointPiston.toMove = ActuatorValueIfEnabled(
                    neuralNetwork.outputArr[5], (float)neuralNetwork.outputArr[6]);
            // Manipulator (rotation perpendicular to both shoulder and arm/joint)
            jointManipulator.toMove = ActuatorValueIfEnabled(
                    neuralNetwork.outputArr[7], (float)neuralNetwork.outputArr[8]);
            // Is painting active?
            if ((float)neuralNetwork.outputArr[9] > 0.5f)
            {
                paintNow = true;
            }
            else
            {
                paintNow = false;
            }
            // Brush size selection
            SelectBrushSize();
        }
    //-----------------------------------------------------------------------------
        void AutomaticControlArmJoint()
        {
            if (neuralNetwork.outputArr[2] > 0.5)
            {
                // The arm joints move with the output from the neural network
                // Output is between 0 and 1: we need it from -1 to +1
                jointArm.toMove = (float)neuralNetwork.outputArr[3] * 2f - 1f;
                jointElbow.toMove = (float)neuralNetwork.outputArr[4] * 2f - 1f;
            }
            else
            {
                jointArm.toMove = 0f;
                jointElbow.toMove = 0f;
            }
        }
    //-----------------------------------------------------------------------------
        /// <summary>
        /// Takes inputs directly from the keyboard. Mostly for debugging.
        /// </summary>
        void ManualControl()
        {
            jointShoulder.toMove = Input.GetAxis("Horizontal");
            jointArm.toMove = Input.GetAxis("Vertical");
            jointElbow.toMove = Input.GetAxis("MyInputIK");
            jointPiston.toMove = Input.GetAxis("MyInputJL");
            jointManipulator.toMove = Input.GetAxis("MyInputTG");

            paintNow = true;
            brushSize = 0;
        }
    //------------------------------------------------------------------------------
        /// <summary>
        /// Used in AutomaticControl to avoid repetition of code.
        /// Output is given between 0 and 1: we need it from -1 to +1
        /// </summary>
        float ActuatorValueIfEnabled(double enabler, float convertMe)
        {
            if (enabler > 0.5)
            {
                return convertMe * 2f - 1f;
            }
            return 0f;
        }
    //------------------------------------------------------------------------------
        void UpdateInputArray()
        {
            ComputeTargetPosition();

    		// Extra inputs: proprioception (information about the state of different joints)
    		//inputArr[] = NormalizeMe(joint);

    		// The last input is the time, so that the painting can evolve with time!
    		neuralNetwork.inputArr[3] = (Time.time - startTime) * maxTimeInverse;
    	}
    //------------------------------------------------------------------------------
    	void ComputeTargetPosition()
    	{
    		neuralNetwork.inputArr[0] = 0f;
    		float sensor_range = 0.6f;
            pointingAtCanvas = false;

    		// So the ray points forward from the tip
    		Vector3 ray_direction = new Vector3(0f, -1f, 0f).normalized;

    		RaycastHit hit;         
    		if (Physics.Raycast(manipulatorTip.transform.position,
    			-manipulatorTip.transform.up,
    			out hit, sensor_range))
    		{
    			neuralNetwork.inputArr[0] = 1f - hit.distance / sensor_range;

    			// Is the arm pointing at the canvas?
    			if (hit.collider.tag == "UnitChild")
    			{
    				// ID of the point in the texture that was hit by the ray:
                    targetId = PointToIdAndUpdatePositionInput(hit.point);
                    pointingAtCanvas = true;
    			}
    		}
    		else
    		{
    			UndoMovement();
    		}
    	}
    //------------------------------------------------------------------------------
        void PaintPixel()
        {
            if (pointingAtCanvas)
            {
                if (paintNow)
                {
                    // (!) This is hard on performance if brushSize > 0!
                    for (int idx = 0; idx < pixelNeighbours[targetId][brushSize].Count; ++idx)
                    {
                        PixelPoint pixel = pixelNeighbours[targetId][brushSize][idx];
                        benchTexture.SetPixel(pixel.x, pixel.y, Color.red);
                        benchTexture.Apply();
                        benchMaterial.mainTexture = benchTexture;
                    }
                }                
            }
        
        }
    //------------------------------------------------------------------------------
    	/// <summary>
    	/// Very simple method that only exists to avoid some code repetition.
    	/// It is used allow to provide the state of the actuators as inputs.
    	/// </summary>
    	float NormalizeMe(Joint joint)
        {
            float normalizedValue = joint.currentValue;
            normalizedValue -= joint.min;
    		// Note maxValue is greater than minValue (otherwise we would use Abs)
            normalizedValue /= (joint.max - joint.min);
            return normalizedValue;
    	}
    //------------------------------------------------------------------------------
        /// <summary>
        /// Multiplies movements by their speed and timestep, then calls MoveAll.
        /// </summary>
        void ProcessOutput()
        {
            jointShoulder.toMove = jointShoulder.toMove * jointShoulder.speed * Time.deltaTime;
            jointArm.toMove = jointArm.toMove * jointArm.speed * Time.deltaTime;
            jointElbow.toMove = jointElbow.toMove * jointElbow.speed * Time.deltaTime;
            jointPiston.toMove = jointPiston.toMove * jointPiston.speed * Time.deltaTime;
            jointManipulator.toMove = jointManipulator.toMove * jointManipulator.speed * Time.deltaTime;
            MoveAll();
        }
    //------------------------------------------------------------------------------ 
        /// <summary>
        /// Moves all parts. Doing it like this so it is easy to also revert the
        /// movement (by calling this with negative increments). (It is unimportant
        /// if the result is not mathematically the inverse because of compound rotations.)
        /// </summary>
        void MoveAll()
        {
            // SHOULDER
            MoveShoulder();
            // ARM
            jointArm.AddAndClamp();
            jointArm.RotateX();
            // ELBOW
            jointElbow.AddAndClamp();
            jointElbow.RotateZ();
            // PISTON
            jointPiston.AddAndClamp();
            jointPiston.MoveX();
            // MANIPULATOR
            jointManipulator.AddAndClamp();
            jointManipulator.RotateZ(); 
        }
    //------------------------------------------------------------------------------ 
        /// <summary>
        /// Moves the shoulder and keeps track of its current angle, making sure
        /// it is within +-180 degrees..
        /// </summary>
        void MoveShoulder()
        {
            // With += it works just as well
            jointShoulder.currentValue -= jointShoulder.toMove;
            if (jointShoulder.currentValue < -180.0f)
            {
                jointShoulder.currentValue = 360.0f + jointShoulder.currentValue ;
            }
            else if (jointShoulder.currentValue > 180.0f)
            {
                jointShoulder.currentValue = -360.0f + jointShoulder.currentValue ;
            }
            jointShoulder.jointObject.transform.Rotate(0, jointShoulder.toMove, 0);
        }
    //------------------------------------------------------------------------------ 
        /// <summary>
        /// Instantiates the painting canvas (bench) and sets the robot arm as parent
        /// (so that if we click on a canvas we know which neural network was
        /// involved in that painting).
        /// </summary>
        void InstantiateBench()
        {
            bench = (GameObject)Instantiate(Resources.Load("Prefabs/ArtistArmAccesories/Paintbench"));
    		bench.transform.SetParent(this.transform);
            Vector3 temp = transform.position;
            temp.y = 1f;
            temp.z += 1.9f;
    		bench.transform.position = temp;
    		benchMaterial = bench.GetComponent<Renderer>().material;
    		benchMaterial.shader = Shader.Find("Unlit/Texture");
    		benchTexture = benchMaterial.mainTexture as Texture2D;
            benchTexture = new Texture2D(canvasX, canvasY);

            // Initially all white.
    		for (int j = 0; j < canvasY; ++j)
    		{
    			for (int i = 0; i < canvasX; ++i)
    			{
    				benchTexture.SetPixel(i, j, Color.white);
    			}           
    		}
    	}
    //------------------------------------------------------------------------------
    	/// <summary>
        /// Translates a hit position on the canvas to the ID of the corresponding
        /// pixel.
        /// 
        /// We will use multiplications (by the inverse) instead of division for
        /// extra performance. We also want to get the position as 0-1 coordinates.
        /// </summary>
        int PointToIdAndUpdatePositionInput(Vector3 position)
    	{
            float row = (virtualCanvasCornerZ - position.z) * pixelSizeZinv;
            int returnId = canvasX * (int)Mathf.Floor(row);
            // Adds the position in the row (using x increment)
            float column = (virtualCanvasCornerX - position.x) * pixelSizeXinv;
            returnId += (int)Mathf.Floor(column);

            // We divide the row and column number by the total number of rows and
            // columns to get position from 0 to 1.
            neuralNetwork.inputArr[1] = row * canvasYinv;
            neuralNetwork.inputArr[2] = column * canvasXinv;   

            // In case some weird cases produce any negative results!
            // return Mathf.Max(0, returnId);
            return returnId;
    	}
    //------------------------------------------------------------------------------ 
        /// <summary>
        /// Creates a quick-access list with all the pixels that should be painted
        /// around each pixel for different brush sizes. For brush size 0 returns
        /// the (x, y) position of the pixel.
        /// </summary>
        void CreateNeighbourList()
        {
            Debug.Log("Creating neighbour list");

            // The first two loops go through all pixels in the bench.
            for (int j = 0; j < canvasY; ++j)
            {
                for (int i = 0; i < canvasX; ++i)
                {
                    PixelPoint currentPixel = new PixelPoint(i, j);

                    // For each pixel in the canvas we create a list with its
                    // neighbours. The list of neighbours depends on the brush size
                    // so this is given as a list of lists.
                    pixelNeighbours.Add(NeighboursForPixel(currentPixel));
                }           
            }
        }
    //------------------------------------------------------------------------------
        List<List<PixelPoint>> NeighboursForPixel(PixelPoint pixel)
        {
            List<List<PixelPoint>> neighboursForThisPixel = new List<List<PixelPoint>>();

            // Each brush size will have its own neighbours list.
            int maxBrushSize = 1;
            for (int brush = 0; brush < maxBrushSize + 1; ++brush)
            {
                neighboursForThisPixel.Add(NeighboursForBrush(brush, pixel));
            }
            return neighboursForThisPixel;
        }
    //------------------------------------------------------------------------------
        List<PixelPoint> NeighboursForBrush(int brushSize, PixelPoint pixel)
        {
            List<PixelPoint> neighboursForThisBrsuh = new List<PixelPoint>();

            // Here we go through all positions within +-brush distance 
            // from the x, y coordinates of the current pixel.
            for (int neighbourX = pixel.x - brushSize;
                 neighbourX < pixel.x + brushSize + 1; ++neighbourX)
            {
                for (int neighbourY = pixel.y - brushSize;
                     neighbourY < pixel.y + brushSize + 1; ++neighbourY)
                {
                    PixelPoint neighbourCandidate = new PixelPoint(neighbourX, neighbourY);

                    if (TestIfNeighbour(pixel, neighbourCandidate, brushSize))
                    {
                        neighboursForThisBrsuh.Add(neighbourCandidate);                    
                    }

                }
            }
            return neighboursForThisBrsuh;
        }
    //------------------------------------------------------------------------------
        bool TestIfNeighbour(PixelPoint currentPixel, PixelPoint neighbourCandidate, int brush)
        {
            // Is it out of bounds?
            if (neighbourCandidate.x >= 0 && neighbourCandidate.y >= 0 &&
                neighbourCandidate.x < canvasX && neighbourCandidate.y < canvasY)
            {
                // How far is this point? (To avoid a square brush)
                // The "+0.5" results in more satisfactory shapes
                // but it is entirely subjective.
                if (currentPixel.Distance(neighbourCandidate) <= (float)brush + 0.5f)
                {
                    // Returns the candidate for addition to the list.
                    return true;                               
                }                               
            }
            return false;
        }
    //------------------------------------------------------------------------------
        void SelectBrushSize()
        {
            if ((float)neuralNetwork.outputArr[10] < 0.5f)
            {
                brushSize = 0;
            }
            else
            {
                brushSize = 1;
            }
        }
    }
}
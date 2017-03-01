using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SharpNeat.Phenomes;

public class ArtistWelderController : UnitController {

    private static int ID_counter = 0;
    private int ID;

    private GameObject evolutionCamera;
    private Vector3 evolCameraOldPosition;
    private Quaternion evolCameraOldOrientation;
    private Vector3 evolCameraNewPosition = new Vector3(0f, 20f, 0f);

    private GameObject paintingsBackground;

    // When creating the canvas time is heavily slowed down. We want to remember
    // the original value to left things as they were (this is done at destruction).
    private float oldTimeScale = -1f;
    private float startTime;
    private float timeToMeasureTime = 15.9f;
    private float timeForPainting = 16f;
    //private float timeToMeasureTime = 1.9f;
    //private float timeForPainting = 2f;
    private bool isShowingPainting = false;
    private bool isTimeRateGot = false;

	private GameObject bench;
	private Material benchMaterial;
	private List<GameObject> pixels = new List<GameObject>();
    private const float pixelSizeX = 0.04f;
    private const float pixelSizeZ = 0.032916667f;
    private const float canvasPixelSizeX = 0.06217f;
    private const float canvasPixelSizeZ = 0.07296f;
    private List<float> pixelColours = new List<float>();

	// We want to know as fast as possible to which input each canvas pixel belongs
    private static Dictionary<int, int> pixelIndexToInputIndex = new Dictionary<int, int>();

    // We want to create a dictionary that tells us to which input pixel
    // corresponds each canvas pixel. However, input pixel 1 will not
    // correspond to input 1 of the neural network, because there are 
    // other inputs before! InputOffset says exacltly how many.
    int inputOffset = 3;

    private const int canvasX = 75;
    private const int canvasY = 48;
    // We need a constant with the number of canvas pixels in an input pixel.
    // Because this will be used to divide in an average, we get the inverse for
    // a bit of extra performance! (Better to multiply than to divide.)
    //private const float canvasPixelsInInputPixelInverse = 0.00555556f; // canvasX*canvasY / 5*4
    private const float canvasPixelsInInputPixelInverse = 0.0555556f; // canvasX*canvasY / 5*4

	// We want a virtual representation of the canvas (which is made of pixel gameObjects)
    private float virtualCanvasCornerX = 0f;
    private float virtualCanvasCornerZ = 0f;

    private AudioSource audio;

    // We keep all the properties of each joint nicely together.
    private struct Joint
    {
        // This is the actual game object (needed to move it!)
        public GameObject jointObject;
        // We remember the position (or angle) of each joint. Needed to ensure
        // we don't move beyond boundaries.
        public float currentValue;
        public float speed;
        // Min and max are the boundaries for the joint.
        public float min;
        public float max;
        // This is the amount (position, angle) we want to move the joint next
        // time MoveAll is called.
        public float toMove;

        public Joint(GameObject p1, float p2, float p3, float p4, float p5, float p6)
        {
            jointObject = p1;
            currentValue = p2;
            speed = p3;
            min = p4;
            max = p5; 
            toMove = p6;
        }
    }
    // Note the shoulder has no boundaries!
    Joint jointShoulder = new Joint(null, 0f, 5f, 0f, 0f, 0f);
    Joint jointArm = new Joint(null, 0f, 5f, -38f, 35f, 0f);
    Joint jointElbow = new Joint(null, 0f, 5f, -68f, 69f, 0f);
    Joint jointPiston = new Joint(null, 0f, 1f, -0.72f, 0.34f, 0f);
    Joint jointManipulator = new Joint(null, 0f, 60f, 210f, 330f, 0f);

    // The manipulator tip is not a joint!
    private GameObject manipulatorTip;

    // box is the neural network which will control the unit
    IBlackBox box;  
    bool IsRunning;
//------------------------------------------------------------------------------
	void Start()
    {
        // Let's find the camera and save the current transform.
        evolutionCamera = GameObject.Find("EvolutionCamera");
		// During evolution we want "EvolutionCamera", otherwise we want
        // "EditingCamera" (and "EvolutionCamera" will not be found outside
        // of evolution, because it will be inactive, so we can test if the
        // previous .Find returned an empty object to distinguish both situations!
        if (!evolutionCamera)
        {
            Debug.Log("camera not found!");
            evolutionCamera = GameObject.Find("EditingCamera");
        }
        evolCameraOldPosition = evolutionCamera.transform.position;
        evolCameraOldOrientation = evolutionCamera.transform.rotation;

        paintingsBackground = GameObject.Find("BackGround").transform.
                              Find("PaintingsBackground").gameObject;

        audio = transform.GetComponent<AudioSource>();

        // Here we find references for all joint elements
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

		SetPosition();
        ++ID_counter;
        ID = ID_counter;

        // When the arm points at the canvas, we need to know to which pixel this
        // position will correspond. This is faster if we have stored the coordinates
        // of the canvas corner.
        Vector3 temp = transform.position;
        virtualCanvasCornerX = temp.x + ((float)canvasX / 2f) * pixelSizeX;
        virtualCanvasCornerZ = temp.z + 1.9f + ((float)canvasY / 2f) * pixelSizeZ; 

		InstantiateBench();
        InstantiateColourMarkers();

        // Let's give the joints a little initial offset (to avoid overfitting!)
        jointPiston.toMove = Random.Range(-0.5f, 0.1f);
        MoveAll();

        // We want to know the time elapsed since the robot arm started drawing.
        // After the allocated time painting will stop and we will display the
        // result!
        startTime = Time.time;
		oldTimeScale = Time.timeScale;

        // There is no need to repeat this step!
        if (pixelIndexToInputIndex.Count == 0)
        {
            ComputePixelIndexToInputIndex();
        }
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
        evolutionCamera.transform.position = evolCameraNewPosition;
        evolutionCamera.transform.eulerAngles = new Vector3(90f, 180f, 0f);

        // Special background!
        paintingsBackground.SetActive(true);

        Destroy(bench);
        isShowingPainting = true;
        Time.timeScale = 0.4f;
        // Instead of instantiating/destroying everytime we could consider
        // pooling: activating/deactivating the same objects over and over again.
        InstantiatePixels(); 
    }
//------------------------------------------------------------------------------
    void SetPosition()
    {
        Vector3 temp = transform.position;
        // x: between 20 and -20, at intervals of 8 (fits 4)
        // x: between 10.8 and -10.8, at intervals of 7.2 (fits 2)
        // x: between 10.8 and -10.8, at intervals of 5.4 (fits 3)
        temp.x = 12.0f - (ID_counter % 4f) * 8f;
        // There is probably a more elegant way to write this...
        //temp.z = -10.8f + (Mathf.Floor(ID_counter / 4f) + 1f) * 5.4f;
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
        evolutionCamera.transform.position = evolCameraOldPosition;
        evolutionCamera.transform.rotation = evolCameraOldOrientation;

        // Resets background.
        paintingsBackground.SetActive(false);

        // Sets timeScale as it was (we should probably only do this for the last
        // element that is destroyed!
        if (oldTimeScale > 0f)
        {
            Time.timeScale = oldTimeScale;
        }

        // In case we are destroying the element before the painting was displayed
        // (in which case the bench is still there!)
        if (bench)
        {
            Destroy(bench);
        }

        --ID_counter;
    }
//------------------------------------------------------------------------------
    // Used (hopefully well) in Optimizer --> DestroyBest
    public override IBlackBox GetBox() {
        return box;
    }
//------------------------------------------------------------------------------
    public override void Stop() {
        this.IsRunning = false;
    }
//------------------------------------------------------------------------------  
    public override void Activate(IBlackBox box) {
        this.box = box;
        this.IsRunning = true;
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
        ISignalArray inputArr = box.InputSignalArray;

        // Arm input:
        float frontSensor;
		IsTowardsTarget(out frontSensor);
        SetBenchColour(frontSensor, ref inputArr);
        // Extra inputs: proprioception (information about the state of different joints)
        //inputArr[] = NormalizeMe(jointValue, jointMin, jointMax);

        // Goes through all pixels in the canvas, and averages their values for
        // all the pixels that belong to the same input.
        ReadCanvas(ref inputArr);

        // The neural controller is activated
        box.Activate();
        // And produces output signals (also in an array)
        ISignalArray outputArr = box.OutputSignalArray;     

        // The first processing step returns outputs from -1 to +1 (instead of
        // from 0 to 1) and also checks if inputs needed to enable some joints
        // are active.
        AutomaticControl(ref outputArr);
        //ManualControl(ref outputArr);

        // Multiplies outputs by the speed of joints and the elapsed time and
        // then calls MoveAll.
        ProcessOutput();

        // Sound effects!
/*        float totalMovement = AddMovements(rotateShoulderAngle, rotateArmAngle,
                                           rotateJointAngle, movePistonDelta,
                                           rotateManipulatorAngle);
        ToggleAudio(audio, totalMovement);*/
    }
//------------------------------------------------------------------------------
    /// <summary>
    /// Checks the elapsed time. After timeToMeasureTime we record the current
    /// time speed. This is needed because we will almost freeze time for the
    /// display of the painting, and we want to undo this at the end.
    /// We wait a bit extra after taking this time (otherwise some individuals will
    /// likely record as current timeRate the now slowed timeRate by other units!)
    /// </summary>
    void CheckTime()
    {
        if (Time.time - startTime > timeToMeasureTime)
        {
            if (!isTimeRateGot)
            {
                isTimeRateGot = true;
                oldTimeScale = Time.timeScale;
            }
            else if (!isShowingPainting)
            {
                if (Time.time - startTime > timeForPainting)
                {
                    ShowPainting();
                }
            }
        }
    }
//------------------------------------------------------------------------------
    void SetBenchColour(float frontSensor, ref ISignalArray inputArr)
    {
        if (frontSensor < 0.35f)
        {
            inputArr[0] = 1f;
            inputArr[1] = 0f;   
            inputArr[2] = 0f;
            benchMaterial.color = Color.blue;
        }
        else if (frontSensor > 0.45f)
        {
            inputArr[0] = 0f;
            inputArr[1] = 1f;   
            inputArr[2] = 0f;
            benchMaterial.color = Color.green;
        }
        else
        {
            inputArr[0] = 0f;
            inputArr[1] = 0f;   
            inputArr[2] = 1f;
            benchMaterial.color = Color.red;
        }
    }
//-----------------------------------------------------------------------------
    /// <summary>
    /// This method takes the output array from the neural network and processes
    /// it to update the variables used in the movement of different robot arm
    /// joints.
    /// </summary>
    void AutomaticControl(ref ISignalArray outputArr)
    {
        // Shoulder (rotation along vertical axe)
        jointShoulder.toMove = ActuatorValueIfEnabled(outputArr[0], (float)outputArr[1]);
        // Arm and joint (finger-like rotation)
        if (outputArr[2] > 0.5)
        {
            // The arm joints move with the output from the neural network
            // Output is between 0 and 1: we need it from -1 to +1
            jointArm.toMove = (float)outputArr[3] * 2f - 1f;
            jointElbow.toMove = (float)outputArr[4] * 2f - 1f;
        }
        else
        {
            jointArm.toMove = 0f;
            jointElbow.toMove = 0f;
        }
        // Piston movement
        jointPiston.toMove = ActuatorValueIfEnabled(outputArr[5], (float)outputArr[6]);
        // Manipulator (rotation perpendicular to both shoulder and arm/joint)
        jointManipulator.toMove = ActuatorValueIfEnabled(outputArr[7], (float)outputArr[8]);        
    }
//-----------------------------------------------------------------------------
    /// <summary>
    /// Takes inputs directly from the keyboard. Mostly for debugging.
    /// </summary>
    void ManualControl(ref ISignalArray outputArr)
    {
        jointShoulder.toMove = Input.GetAxis("Horizontal");
        jointArm.toMove = Input.GetAxis("Vertical");
        jointElbow.toMove = Input.GetAxis("MyInputIK");
        jointPiston.toMove = Input.GetAxis("MyInputJL");
        jointManipulator.toMove = Input.GetAxis("MyInputTG");
    }
//------------------------------------------------------------------------------
    // Simply to avoid repetition (used 3 times)
    float ActuatorValueIfEnabled(double enabler, float convertMe)
    {
        if (enabler > 0.5)
        {
            // The arm joints move with the output from the neural network
            // Output is between 0 and 1: we need it from -1 to +1
            return convertMe * 2f - 1f;
        }
        return 0f;
    }
//------------------------------------------------------------------------------
    // If the arm is pointing towards a relevant object (say, the simulated canvas)
    // returns true, and the distance.
    bool IsTowardsTarget(out float distance)
    {
		float sensor_range = 0.6f;
        distance = 0f;

        // So the ray points forward from the tip
        Vector3 ray_direction = new Vector3(0f, -1f, 0f).normalized;

		RaycastHit hit;         
		if (Physics.Raycast(manipulatorTip.transform.position,
                            -manipulatorTip.transform.up,
                            out hit, sensor_range))
        {
            distance = 1f - hit.distance / sensor_range;

            // We only try to paint pixels if the output tells us so!
            //if ()
            //{
                // Is the arm pointing at a pixel?
                if (hit.collider.tag == "ArtCanvas")
                {
                    // Annotate the pixel as painted (value 1, given as float so that
                    // we can take averages for areas)
                    pixelColours[PointToId(hit.point)] = 1f;
                    return true;
                }               
            //}
        }
        else
        {
            UndoMovement();
        }

        return false;
	}  
//------------------------------------------------------------------------------
	/// <summary>
	/// Very simple method that only exists to avoid some code repetition.
	/// It is used allow to provide the state of the actuators as inputs.
	/// </summary>
	float NormalizeMe(float value, float minValue, float maxValue)
    {
		value -= minValue;
		// Note maxValue is ALWAYS greater than minValue (otherwise we would use Abs)
		value /= (maxValue - minValue);
        return value;
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
        jointArm.currentValue = AddAndClamp(jointArm.currentValue, jointArm.toMove,
                                            jointArm.min, jointArm.max);
        RotateX(jointArm.jointObject.transform, jointArm.currentValue);

        // ELBOW
        jointElbow.currentValue = AddAndClamp(jointElbow.currentValue, jointElbow.toMove,
                                              jointElbow.min, jointElbow.max);
        RotateZ(jointElbow.jointObject.transform, jointElbow.currentValue);

        // PISTON
        jointPiston.currentValue = AddAndClamp(jointPiston.currentValue, jointPiston.toMove,
                jointPiston.min, jointPiston.max);
        jointPiston.jointObject.transform.localPosition = new Vector3(
                jointPiston.currentValue, jointPiston.jointObject.transform.localPosition.y,
                jointPiston.jointObject.transform.localPosition.z);

        // MANIPULATOR
        jointManipulator.currentValue = AddAndClamp(
                jointManipulator.currentValue, jointManipulator.toMove,
                jointManipulator.min, jointManipulator.max);
        RotateZ(jointManipulator.jointObject.transform, jointManipulator.currentValue); 
    }
//------------------------------------------------------------------------------ 
    /// <summary>
    /// Rotates the object (with its children).
    /// </summary>
    void RotateX(Transform objectTransform, float newAngle)
    {
        objectTransform.localEulerAngles = new Vector3(
            newAngle, objectTransform.localEulerAngles.y,
            objectTransform.localEulerAngles.z);
    }
//------------------------------------------------------------------------------ 
    /// <summary>
    /// Rotates the object (with its children).
    /// </summary>
    void RotateZ(Transform objectTransform, float newAngle)
    {
        objectTransform.localEulerAngles = new Vector3(
            objectTransform.localEulerAngles.x, objectTransform.localEulerAngles.y,
            newAngle);
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
    /// Updates a variable, ensuring the new value is within limits.
    /// </summary>
    float AddAndClamp(float variable, float addThis, float limit1, float limit2)
    {
        variable += addThis;
        return Mathf.Clamp(variable, limit1, limit2);
    }
//------------------------------------------------------------------------------ 
    float AddMovements(float shoulderM, float armM, float joint, float piston,
                       float manipulatorM)
    {
        return Mathf.Sqrt(shoulderM * shoulderM + armM * armM + joint * joint +
                          piston * piston + manipulatorM * manipulatorM);
    }
//------------------------------------------------------------------------------ 
    void InstantiateBench()
    {
        bench = (GameObject)Instantiate(Resources.Load("Prefabs/ArtistArmAccesories/Paintbench"));
        Vector3 temp = transform.position;
        temp.y = 1f;
        temp.z += 1.9f;
		bench.transform.position = temp;
		benchMaterial = bench.GetComponent<Renderer>().material;
	}
//------------------------------------------------------------------------------ 
	void InstantiatePixels()
    {
		pixels = new List<GameObject>();

        // Let's first determine the position of the whole picture.
        // Our display will (in principle) fit 4 x 3 pieces.

        // Admitedly, this is NOT the most intuitive way to do this...

        // Note the first case is ID = 1, so we need "ID - 1" so that the first
        // in the second column happens for ID = 5.
        float row = (float)(1 + (ID - 1) / 4);
        float column = (float)(ID - 4 * ((int)row - 1));

        // This will be the reference position to build the canvas.
        Vector3 temp = new Vector3(8f, 6.5f, -6.75f);
        // The working width is 20, and this must be split into 4 areas (columns).
        // The centre of each is in the half, so we split 20 into 8 (simplified
        // 2.5). The first is at 1/8, the second at 3/8...
        temp.x -= (1f + (column - 1f) * 2f) * 2.5f;
        // The working heigth is 12.25, and this must be split into 3 areas (rows).
        // The centre of each is in the half, so we split 12.25 into 6 (simplified
        // 2.0417).
        temp.z += (1f + (row - 1f) * 2f) * 2.0417f;

        // The first corner is:
        // there are 75 in a row, with one in the middle and 74/2 to each side.
        // (canvasPixelSizeX * 37) is the first corner (X axe value)
        float canvasCornerX = temp.x + 37f * canvasPixelSizeX;
        // there are 48 in a column, the first to one side
        // is only offset by half a length!:
        // (canvasPixelSizeZ * 23.5) is the first corner (Z axe value)
        float canvasCornerZ = temp.z + 23.5f * canvasPixelSizeZ; 

        // The canvas is made of 48 rows and 75 columns (which is too many elements
        // to have around all the time for all individuals in the genome!)
        for (int i = 0; i < canvasY*canvasX; ++i)
        {
			pixels.Add((GameObject)Instantiate(Resources.Load("Prefabs/ArtistArmAccesories/Pixel")));

            Vector3 pixelPosition = new Vector3();

            // Required x offset
            pixelPosition.x = canvasCornerX - canvasPixelSizeX * (i % canvasX);
            pixelPosition.y = temp.y;
            // Plus z offset:
            pixelPosition.z = canvasCornerZ - canvasPixelSizeZ * (i / canvasX);

            pixels[i].transform.position = pixelPosition;

            // If the pixel active?
            if (pixelColours[i] > 0.5f)
            {
                pixels[i].GetComponent<Renderer>().material.color = Color.red;
            }

			// Let's set the robot arm as the parent (in this way we will be able to
			// use the pixels to select the correct individual!)
            pixels[i].transform.SetParent(this.transform);
        }
	}
//------------------------------------------------------------------------------
    /// <summary>
    /// This dictionary will store, for each pixel index, the input index to
    /// which it corresponds (the canvas is split in larger (binned) pixels
    /// that are used as input)
    /// There is no need to repeat this step!
    /// </summary>
    void ComputePixelIndexToInputIndex()
    {
        //pixelIndexToInputIndex
        for (int i = 0; i < canvasY*canvasX; ++i)
        {
            // canvasX is the number of pixels that fit the width of the canvas
            // so that every #canvasX pixels make one row

            // From above:
            int xCoordCanvas = (i % canvasX);
            //int zCoordCanvas = (i / canvasX);

            // Input pixels divide the canvas into 5x4. In each of these pixels
            // we can fit 75/5 * 48/4 canvas pixels (total: 15x12 = 180 pixels)
            int inputPixelWidth = canvasX / 5;
            int inputPixelHeight = canvasY / 4;

            // If we divide input pixels also into x-z coords, then
            int xCoordInput = (xCoordCanvas / inputPixelWidth);

            // A new row of input pixels starts every 5 input pixels, so that is
            // after every 5 * 180 = 900 canvas pixels.
            // int canvasPixelsInRow = 5 * inputPixelWidth * inputPixelHeight;
            // Note this is just:
            int canvasPixelsInRow = canvasX * inputPixelHeight;

            int zCoordInput = (i / canvasPixelsInRow);

            int inputIndex = xCoordInput + 5 * zCoordInput;

            // Beware: will attempt 3600 screen prints (slow)
            //Debug.Log("pixel index " + i + " " + inputIndex);

            inputIndex += inputOffset;

            // Finally adds the relationship between the canvas index and the
            // input index:
            pixelIndexToInputIndex.Add(i,inputIndex);
        }
    }
//------------------------------------------------------------------------------
    /// <summary>
    /// Goes through all pixels in the canvas, and averages their values for
    /// all the pixels that belong to the same input.
    /// </summary>
    void ReadCanvas(ref ISignalArray inputArr)
    {
        for (int i = 0; i < canvasY*canvasX; ++i)
        {
            inputArr[pixelIndexToInputIndex[i]] += pixelColours[i];
        }

        // After adding the value of each pixel, we need to divide by the total
        // number of canvas pixels in each input pixel: canvasY*canvasX / 5*4
        //canvasPixelsInInputPixel
        for (int i = inputOffset; i < inputArr.Length; ++i)
        {
            inputArr[i] *= canvasPixelsInInputPixelInverse;

            //Debug.Log("input " + i + " value " + inputArr[i]);
        }
    }
//------------------------------------------------------------------------------
    /// <summary>
    /// We need the colour markers from the beginning, before we instantiate the
    /// pixels. We will use this markers to set the colour of the pixels when
    /// they are instantiated.
    /// </summary>
    void InstantiateColourMarkers()
    {
        pixelColours = new List<float>();
        for (int i = 0; i < canvasY*canvasX; ++i)
        {
            pixelColours.Add(0f);
        }
    }
//------------------------------------------------------------------------------
	/// <summary>
    /// Translates a hit position on the canvas to the ID of the corresponding
    /// pixel. In this way we can determine which pixels should change colour
    /// before we actually have to create them!
    /// </summary>
    int PointToId(Vector3 position)
	{
        int returnId = 0;

        // Dividing and rounding the z increment by the size.Z of the pixel we 
        // get the number of rows we need to add.
        // Note the corner has the maximum values for z and x position (see
        // in InstantiatePixels how it is built).
        returnId += canvasX * (int)Mathf.Floor((virtualCanvasCornerZ - position.z) / pixelSizeZ);
        // Finally adds the increment in the row (using x increment)
        returnId += (int)Mathf.Floor((virtualCanvasCornerX - position.x) / pixelSizeX);

        // In case some weird cases produce any negative results!
        // return Mathf.Max(0, returnId);
        return returnId;
	}
//------------------------------------------------------------------------------ 
    void ToggleAudio(AudioSource audio, float movement)
    {
        float threshold = 0.01f;

        if (Mathf.Abs(movement) > threshold)
        {
            if (!audio.isPlaying)
            {
                audio.Play();
            }
        }
        else
        {
            if (audio.isPlaying)
            {
                audio.Pause();
            }
        }
    }
}
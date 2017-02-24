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
    private float timeToMeasureTime = 1.9f;
    private float timeForPainting = 2f;
    private bool isShowingPainting = false;
    private bool isTimeRateGot = false;

	private GameObject bench;
	private Material benchMaterial;
	private List<GameObject> pixels = new List<GameObject>();
    private const float pixelSizeX = 0.03876f; // for x76: 0.03928f;
    private const float pixelSizeZ = 0.030336f; // for x50: 0.0316f;
    private const float canvasPixelSizeX = 0.06217f; // for x76: 0.063f;
    private const float canvasPixelSizeZ = 0.07296f; // for x50: 0.076f;
    private List<float> pixelColours = new List<float>();

	// We want to know as fast as possible to which input each canvas pixel belongs
    private static Dictionary<int, int> pixelIndexToInputIndex = new Dictionary<int, int>();

    private const int canvasX = 75;
    private const int canvasY = 48;

	// We want a virtual representation of the canvas (which is made of pixel gameObjects)
    private float virtualCanvasCornerX = 0f;
    private float virtualCanvasCornerZ = 0f;

    private AudioSource audio;

	private GameObject shoulder;
	private float shoulderSpeed = 5.0f;
	// This is used to keep track of the rotation angle (not trivial otherwise!)
    private float shoulderXaxe = 0.0f;

    private GameObject arm;
    private float armSpeed = 5.0f;
    // This is used to avoid rotating past the limits
    private float armXaxe = 0.0f;
    private const float armXaxeMin = -38.0f;
    private const float armXaxeMax = 35.0f;

    private GameObject joint;
    private float jointSpeed = 5.0f;
    // This is used to avoid rotating past the limits
    private float jointZaxe = 0.0f;
    private const float jointZaxeMin = -68.0f;
    private const float jointZaxeMax = 69.0f;

    private GameObject piston;
    private float pistonSpeed = 1.0f;
    // This is used to avoid moving past the limits
    private float pistonPositionX = 0.0f;
    private const float pistonPositionXMin = -0.72f;
    private const float pistonPositionXMax = 0.34f;

    private GameObject manipulator;
    private float manipulatorSpeed = 60.0f;
    // This is used to avoid moving past the limits
    private float manipulatorZaxe = 0.0f;
    private const float manipulatorZaxeMin = 210.0f;
    private const float manipulatorZaxeMax = 330.0f;

    // Here we will only need the script in this child
    private GameObject manipulatorTip;

    private float rotateShoulderAngle;
    private float rotateArmAngle;
    private float rotateJointAngle;
    private float movePistonDelta;
    private float rotateManipulatorAngle;

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

        shoulder = transform.Find("RobotArm_Welder_Base/RobotArm_Welder_Shoulder").gameObject;
        arm = shoulder.transform.Find("RobotArm_Welder_Arm").gameObject;
        armXaxe = arm.transform.localEulerAngles.x;
        joint = arm.transform.Find("RobotArm_Welder_Joint").gameObject;
        jointZaxe = joint.transform.localEulerAngles.z;
        piston = joint.transform.Find("RobotArm_Welder_Piston").gameObject;
        pistonPositionX = piston.transform.localPosition.x;
        manipulator = piston.transform.Find("RobotArm_Welder_Manipulator").gameObject;
        manipulatorZaxe = manipulator.transform.localEulerAngles.z;
		manipulatorTip = manipulator.transform.Find("ManipulatorTip").gameObject;

		SetPosition();
        ++ID_counter;
        ID = ID_counter;

        // When the arm points at the canvas, we need to know to which pixel this
        // position will correspond. This is faster if we have stored the coordinates
        // of the canvas corner.
        Vector3 temp = transform.position;
        virtualCanvasCornerX = temp.x + 38f * pixelSizeX;
        virtualCanvasCornerZ = temp.z + 1.9f + 25f * pixelSizeZ; 

		InstantiateBench();
        InstantiateColourMarkers();

        // Let's give the joints a little initial offset (to avoid overfitting!)
        // Let's make the range about 10% of the desired limits (just about)
        // If movement in the manipulator (last entry) is to be allowed, then
        // GetAngle needs to be updated!
/*        MoveAll(Random.Range(-180.0f, 180.0f), Random.Range(-10.0f, 10.0f),
                Random.Range(-10.0f, 10.0f), Random.Range(-0.1f, 0.3f),
                Random.Range(0.0f, 0.0f));*/
		MoveAll(Random.Range(0.0f, 0.0f), Random.Range(0.0f, 0.0f),
			    Random.Range(0.0f, 0.0f), Random.Range(-0.5f, 0.1f),
		        Random.Range(0.0f, 0.0f));

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
        MoveAll(-rotateShoulderAngle, -rotateArmAngle, -rotateJointAngle,
            -movePistonDelta, -rotateManipulatorAngle);
    }
//------------------------------------------------------------------------------ 
    void FixedUpdate()
    {
        CheckTime();

        // Arm output:
        float rotateShoulder;
        float rotateArm;
        float rotateJoint;
        float movePiston;
        float moveManipulator;

		// Arm input:
		float frontSensor = 0f;

        // Input signals are used in the neural controller
        ISignalArray inputArr = box.InputSignalArray;

		IsTowardsTarget(out frontSensor);

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

        // Extra inputs: proprioception (information about the state of different
        // joints)
        //inputArr[3] = NormalizeMe(armXaxe, armXaxeMin, armXaxeMax);
        //inputArr[4] = NormalizeMe(jointZaxe, jointZaxeMin, jointZaxeMax);
        //inputArr[5] = NormalizeMe(pistonPositionX, pistonPositionXMin, pistonPositionXMax);
        //inputArr[6] = NormalizeMe(manipulatorZaxe, manipulatorZaxeMin, manipulatorZaxeMax);

        // Which is activated
        box.Activate();
        // And produces output signals (also in an array)
        ISignalArray outputArr = box.OutputSignalArray;     

        // The arm joints move with the output from the neural network
        // Output is between 0 and 1: we need it from -1 to +1

        // Automatic control:
        // Shoulder (rotation along vertical axe)
        rotateShoulder = ActuatorValueIfEnabled(outputArr[0], (float)outputArr[1]);
        // Arm and joint (finger-like rotation)
        if (outputArr[2] > 0.5)
        {
            rotateArm = (float)outputArr[3] * 2f - 1f;
            rotateJoint = (float)outputArr[4] * 2f - 1f;
        }
        else
        {
            rotateArm = 0f;
            rotateJoint = 0f;
        }
        // Piston movement
        movePiston = ActuatorValueIfEnabled(outputArr[5], (float)outputArr[6]);
        // Manipulator (rotation perpendicular to both shoulder and arm/joint)
        moveManipulator = ActuatorValueIfEnabled(outputArr[7], (float)outputArr[8]);

        // Manual control:
/*        rotateShoulder = Input.GetAxis("Horizontal");
        rotateArm = Input.GetAxis("Vertical");
        rotateJoint = Input.GetAxis("MyInputIK");
        //movePiston = Input.GetAxis("MyInputJL");
        moveManipulator = Input.GetAxis("MyInputTG");*/

        // We multiply movements by their speed and timestep.
        rotateShoulderAngle = rotateShoulder * shoulderSpeed * Time.deltaTime;
        rotateArmAngle = rotateArm * armSpeed * Time.deltaTime;
        rotateJointAngle = rotateJoint * jointSpeed * Time.deltaTime;
        movePistonDelta = movePiston * pistonSpeed * Time.deltaTime;
        rotateManipulatorAngle = moveManipulator * manipulatorSpeed * Time.deltaTime;

        MoveAll(rotateShoulderAngle, rotateArmAngle, rotateJointAngle,
                movePistonDelta, rotateManipulatorAngle);


        // Sound effects!
/*        float totalMovement = AddMovements(rotateShoulderAngle, rotateArmAngle,
                                           rotateJointAngle, movePistonDelta,
                                           rotateManipulatorAngle);
        ToggleAudio(audio, totalMovement);*/
    }
//------------------------------------------------------------------------------
    void CheckTime()
    {
        // Let's check the elapsed time. After timeToMeasureTime we record the
        // current time speed. This is needed because we will almost freeze time
        // for the display of the painting, and we want to undo this at the end.
        // We wait a bit extra after taking this time (otherwise some individuals
        // will likely record as current timeRate the now slowed timeRate by
        // other units!)
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
    // Simply to avoid repetition (used 3 times)
    float ActuatorValueIfEnabled(double enabler, float convertMe)
    {
        if (enabler > 0.5)
        {
            return convertMe * 2f - 1f;
        }
        return 0f;
    }
//------------------------------------------------------------------------------
	// This are the range sensors for the input
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
    /// Moves all parts. Doing it like this so it is easy to also revert the
    /// movement (by calling this with negative increments). (It is unimportant
    /// if the result is not mathematically the inverse because of compound rotations.)
    /// </summary>
    void MoveAll(float localShoulderAngle, float localArmAngle, float localJointAngle,
                 float localPistonDelta, float localManipulatorAngle)
    {
        // With += it works just as well
        shoulderXaxe -= localShoulderAngle;
        // Let's make sure the angle is within +-180 degrees
        if (shoulderXaxe < -180.0f)
        {
            shoulderXaxe = 360.0f + shoulderXaxe ;
        }
        else if (shoulderXaxe > 180.0f)
        {
            shoulderXaxe = -360.0f + shoulderXaxe ;
        }

        shoulder.transform.Rotate(0, localShoulderAngle, 0);
        armXaxe = AddAndClamp(armXaxe, localArmAngle, armXaxeMin, armXaxeMax);
        RotateX(arm.transform, armXaxe);
        jointZaxe = AddAndClamp(jointZaxe, localJointAngle, jointZaxeMin, jointZaxeMax);
        RotateZ(joint.transform, jointZaxe);
        MovePiston(localPistonDelta);
        manipulatorZaxe = AddAndClamp(manipulatorZaxe, localManipulatorAngle,
                                      manipulatorZaxeMin, manipulatorZaxeMax);
        RotateZ(manipulator.transform, manipulatorZaxe);        
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
    /// Moves the piston (with its children). Enforces movement within limits.
    /// </summary>
    void MovePiston(float movePistonDelta)
    {
        pistonPositionX += movePistonDelta;
        pistonPositionX = Mathf.Clamp(pistonPositionX, pistonPositionXMin,
                                      pistonPositionXMax);

        piston.transform.localPosition = new Vector3(
            pistonPositionX, piston.transform.localPosition.y,
            piston.transform.localPosition.z);
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
            // after every 5*180 = 900 canvas pixels.
            // int canvasPixelsInRow = 5 * inputPixelWidth * inputPixelHeight;
            // Note this is just:
            int canvasPixelsInRow = canvasX * inputPixelHeight;

            int zCoordInput = (i / canvasPixelsInRow);

            int inputIndex = xCoordInput + 5 * zCoordInput;

            // Beware: will attempt 3600 screen prints (slow)
            // Debug.Log("pixel index " + i + " " + inputIndex);

            // Finally adds the relationship between the canvas index and the
            // input index:
            pixelIndexToInputIndex.Add(i,inputIndex);
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
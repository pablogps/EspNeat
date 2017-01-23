using UnityEngine;
using System.Collections;
using SharpNeat.Phenomes;

public class WelderController : UnitController {

    private static int ID_counter = 0;

    private GameObject target;
    private GameObject bench;

    private AudioSource audio;

	private GameObject shoulder;
	private float shoulderSpeed = 60.0f;
	// This is used to keep track of the rotation angle (not trivial otherwise!)
    private float shoulderXaxe = 0.0f;

    private GameObject arm;
    private float armSpeed = 60.0f;
    // This is used to avoid rotating past the limits
    private float armXaxe = 0.0f;
    private const float armXaxeMin = -38.0f;
    private const float armXaxeMax = 35.0f;

    private GameObject joint;
    private float jointSpeed = 60.0f;
    // This is used to avoid rotating past the limits
    private float jointZaxe = 0.0f;
    private const float jointZaxeMin = -68.0f;
    private const float jointZaxeMax = 69.0f;

    private GameObject piston;
    private float pistonSpeed = 2.0f;
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
    private ManipulatorTip manipulatorTip;

    private float rotateShoulderAngle;
    private float rotateArmAngle;
    private float rotateJointAngle;
    private float movePistonDelta;
    private float rotateManipulatorAngle;

    // box is the neural network which will control the unit
    IBlackBox box;  
    bool IsRunning;
//------------------------------------------------------------------------------
	void Start() {
        audio = transform.GetComponent<AudioSource>();

        shoulder = transform.Find("RobotArm_Welder_Base/RobotArm_Welder_Shoulder").gameObject;
        // shoulder speed is made a bit variable, to avoid overfitting in simple
        // scenarios using fitness evolution:
        // This multiplies the speed by a factor within (0.9, 1.1)
        //shoulderSpeed *= 1.0f + Random.value / 0.1f;
        arm = shoulder.transform.Find("RobotArm_Welder_Arm").gameObject;
        armXaxe = arm.transform.localEulerAngles.x;
        joint = arm.transform.Find("RobotArm_Welder_Joint").gameObject;
        jointZaxe = joint.transform.localEulerAngles.z;
        piston = joint.transform.Find("RobotArm_Welder_Piston").gameObject;
        pistonPositionX = piston.transform.localPosition.x;
        manipulator = piston.transform.Find("RobotArm_Welder_Manipulator").gameObject;
        manipulatorZaxe = manipulator.transform.localEulerAngles.z;

        manipulatorTip = manipulator.transform.Find("ManipulatorTip").
                         GetComponent<ManipulatorTip>();
        
        SetPosition();
        ++ID_counter;

        InstantiateBench();
        InstantiateTarget();

        // Let's give the joints a little initial offset (to avoid overfitting!)
        // Let's make the range about 10% of the desired limits (just about)
        MoveAll(Random.Range(-15.0f, 15.0f), Random.Range(-10.0f, 10.0f),
                Random.Range(-10.0f, 10.0f), Random.Range(-0.2f, 0.1f),
                Random.Range(-10.0f, 10.0f));
/*        MoveAll(Random.Range(-0.0f, 0.0f), Random.Range(-10.0f, 10.0f),
                Random.Range(-10.0f, 10.0f), Random.Range(-0.2f, 0.1f),
                Random.Range(-0.0f, 0.0f));;*/
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
        Destroy(bench);
        Destroy(target);
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
        float distanceX;
        float distance = 0f;
        //float distanceY;
        //float distanceZ;
        //float totalDistance = 0.0f;
        float totalFit = 0.0f;

		//GetDistance(out distanceX, out distanceY, out distanceZ);
		GetDistance(out distanceX);

        // Remember distance is given between 0 and 1, the closest being 0.5
        // for x and z, and 0.25 for y.
        // Here we set it so minimum distance is value 0. 
        //distanceX -= 0.5f;
		//distanceX *= distanceX;
        //distanceY -= 0.25f;
        //distanceZ -= 0.5f;

        // The module of the distance is then between 0 (good) and
        // sqrt(0.5*0.5 + 0.75*0.75 + 0.5*0.5) = WORST = 1.031 (1.030776...)
        //float distanceMod = Mathf.Sqrt(distanceX*distanceX + distanceY*distanceY +
        //    distanceZ*distanceZ);

        // Returns WORST - distanceMod, so fitness is highest at closest
        // position (returns WORST - 0) and around 0 when furthest.
		//return 1.031f - distanceMod;

        // Contribution from x axe:
        //totalFit = 0.25f - distanceX;

        if (distanceX < 0.52f && distanceX > 0.48f)
        {
            totalFit += 1f;         
        }
        if (IsTowardsTarget(out distance))
        {
            totalFit += 2f;
        }
        totalFit += distance/2f;

        return totalFit;
    } 
//------------------------------------------------------------------------------
    public void UndoMovement() {
        MoveAll(-rotateShoulderAngle, -rotateArmAngle, -rotateJointAngle,
            -movePistonDelta, -rotateManipulatorAngle);
        //MoveAll(-5.0f*rotateShoulderAngle, -5.0f*rotateArmAngle, -5.0f*rotateJointAngle,
         //   -5.0f*movePistonDelta, -5.0f*rotateManipulatorAngle);
    }
//------------------------------------------------------------------------------ 
    void FixedUpdate() {
        // Arm input:
        float distanceX;
        //float distanceY;
        //float distanceZ;

        // Arm output:
        float rotateShoulder;
        float rotateArm;
        float rotateJoint;
        float movePiston;
        float moveManipulator1;
        float release;

		//GetDistance(out distanceX, out distanceY, out distanceZ);
		GetDistance(out distanceX);
        //Debug.Log("Dist: " + distanceX + "  " + distanceY + "  " + distanceZ );
        //Debug.Log("fitness " + GetFitness());
        //Debug.Log(shoulderXaxe);

        // Input signals are used in the neural controller
        ISignalArray inputArr = box.InputSignalArray;

		inputArr[0] = 0f;
		if (distanceX < 0.52f && distanceX > 0.48f)
		{
			inputArr[0] = 1f;			
		}

        float frontSensor = 0f;
        if (IsTowardsTarget(out frontSensor))
        {
            inputArr[2] = 1.0f;
        }
        else
        {
            inputArr[2] = 0.0f;
        }

        inputArr[1] = frontSensor;

        //Debug.Log("inputs " + inputArr[0] + "  " + inputArr[1] + "  " + inputArr[2]);

/*		inputArr[0] = distanceX;
        inputArr[1] = distanceY;
        inputArr[2] = distanceZ;*/

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

        // The arm jooints move with the output from the neural network
        // Output is between 0 and 1: we need it from -1 to +1

        // Automatic control:
/*        rotateShoulder = (float)outputArr[0] * 2f - 1f;
        rotateArm = (float)outputArr[1] * 2f - 1f;
        rotateJoint = (float)outputArr[2] * 2f - 1f;
        movePiston = (float)outputArr[3] * 2f - 1f;
        moveManipulator1 = (float)outputArr[4] * 2f - 1f;
        release = (float)outputArr[5] * 2f - 1f;*/

        if ((float)outputArr[1] < 0.75f)
        {
            rotateShoulder = (float)outputArr[0] * 2f - 1f;
        }
        else
        {
            rotateShoulder = 0f;
        }
/*        rotateArm = 0f;
        rotateJoint = 0f;
        movePiston = 0f;
        moveManipulator1 = 0f;
        release = 0f;*/
        rotateArm = (float)outputArr[2] * 2f - 1f;
        rotateJoint = (float)outputArr[3] * 2f - 1f;
        movePiston = (float)outputArr[4] * 2f - 1f;
        moveManipulator1 = 0f;
        release = 0f;

        // Manual control:
/*        rotateShoulder = Input.GetAxis("Horizontal");
        rotateArm = Input.GetAxis("Vertical");
        rotateJoint = Input.GetAxis("MyInputIK");
        movePiston = Input.GetAxis("MyInputJL");
        moveManipulator1 = Input.GetAxis("MyInputTG");
        if (Input.GetMouseButton(0))
        {
            release = 1.0f; 
        }
        else
        {
            release = 0.0f; 
        }*/

        // We multiply movements by their speed and timestep.
        rotateShoulderAngle = rotateShoulder * shoulderSpeed * Time.deltaTime;
        rotateArmAngle = rotateArm * armSpeed * Time.deltaTime;
        rotateJointAngle = rotateJoint * jointSpeed * Time.deltaTime;
        movePistonDelta = movePiston * pistonSpeed * Time.deltaTime;
        rotateManipulatorAngle = moveManipulator1 * manipulatorSpeed * Time.deltaTime;

        MoveAll(rotateShoulderAngle, rotateArmAngle, rotateJointAngle,
                movePistonDelta, rotateManipulatorAngle);
        
        if (release > 0.7f) {
            // HERE INCLUDE IF CURRENTLY HOLDING!
            ReleaseObject();
        }

        // Sound effects!
/*        float totalMovement = AddMovements(rotateShoulderAngle, rotateArmAngle,
                                           rotateJointAngle, movePistonDelta,
                                           rotateManipulatorAngle);
        ToggleAudio(audio, totalMovement);*/
    }
//------------------------------------------------------------------------------
	//void GetDistance(out float distanceX, out float distanceY, out float distanceZ) {
    void GetDistance(out float distanceX) {
        distanceX = 0.0f;
/*        distanceY = 0.0f;
        distanceZ = 0.0f;*/

        distanceX = manipulatorTip.transform.position.x - target.transform.position.x;
/*        distanceY = manipulatorTip.transform.position.y - target.transform.position.y;
        distanceZ = manipulatorTip.transform.position.z - target.transform.position.z;*/

        // distanceX seems right even if the arm is pointing 180 degrees wrong.
        // Let's fix that here (although it is technically correct it's probably
        // confusing for evolution...)
        if (shoulderXaxe > 90.0f)
        {
            // Below this value is converted into 0, the minimum distance.
            distanceX = -3.0f;
        }
        else if (shoulderXaxe < -90.0f)
        {
            // Below this value is converted into 1, the maximum distance.
            distanceX = 3.0f;
        }

        // Normalizes the result. If the distance is beyond some arbitrary bounds
        // the method will return 0 or 1.
        // 0 and 1 are equally far limits. Closest is 0.5 for x and z, 0.25 for y.
        distanceX = Mathf.Clamp(distanceX, -3.0f, 3.0f);
        distanceX = NormalizeMe(distanceX, -3.0f, 3.0f);

/*        distanceY = Mathf.Clamp(distanceY, -0.5f, 1.5f);
        distanceY = NormalizeMe(distanceY, -0.5f, 1.5f);

        distanceZ = Mathf.Clamp(distanceZ, -2.0f, 2.0f);
        distanceZ = NormalizeMe(distanceZ, -2.0f, 2.0f);*/
	}
//------------------------------------------------------------------------------
	// This are the range sensors for the input
    bool IsTowardsTarget(out float distance) {
		// The starting point of the robot is just in front of it
		float just_ahead = 0.25f;
		float sensor_range = 1.3f;
        distance = 0f;

        // So the ray points forward from the tip
        Vector3 ray_direction = new Vector3(0f, -1f, 0f).normalized;

		RaycastHit hit;
/*        if (Physics.Raycast(manipulatorTip.transform.position +
            manipulatorTip.transform.forward * just_ahead,
            manipulatorTip.transform.TransformDirection(ray_direction),
            out hit, sensor_range))  */           
		if (Physics.Raycast(manipulatorTip.transform.position,
                            -manipulatorTip.transform.up,
                            out hit, sensor_range))
        {
            distance = 1f - hit.distance / sensor_range;
            if (hit.collider.gameObject == target)
            {
                return true;
            }
		}
        return false;
	}  
//------------------------------------------------------------------------------
	/// <summary>
	/// Very simple method that only exists to avoid some code repetition.
	/// </summary>
	float NormalizeMe(float value, float minValue, float maxValue) {
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
                 float localPistonDelta, float localManipulatorAngle) {
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
    void RotateX(Transform objectTransform, float newAngle) {
        objectTransform.localEulerAngles = new Vector3(
            newAngle, objectTransform.localEulerAngles.y,
            objectTransform.localEulerAngles.z);
    }
//------------------------------------------------------------------------------ 
    /// <summary>
    /// Rotates the object (with its children).
    /// </summary>
    void RotateZ(Transform objectTransform, float newAngle) {
        objectTransform.localEulerAngles = new Vector3(
            objectTransform.localEulerAngles.x, objectTransform.localEulerAngles.y,
            newAngle);
    }
//------------------------------------------------------------------------------ 
    /// <summary>
    /// Moves the piston (with its children). Enforces movement within limits.
    /// </summary>
    void MovePiston(float movePistonDelta) {
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
    float AddAndClamp(float variable, float addThis, float limit1, float limit2) {
        variable += addThis;
        return Mathf.Clamp(variable, limit1, limit2);
    }
//------------------------------------------------------------------------------ 
    float AddMovements(float shoulderM, float armM, float joint, float piston,
                       float manipulatorM) {
        return Mathf.Sqrt(shoulderM * shoulderM + armM * armM + joint * joint +
                          piston * piston + manipulatorM * manipulatorM);
    }
//------------------------------------------------------------------------------ 
    void ReleaseObject() {
        manipulatorTip.Release();
    }
//------------------------------------------------------------------------------ 
    void InstantiateBench() {
        bench = (GameObject)Instantiate(Resources.Load("Prefabs/RobotArmAccesories/Workbench"));
        Vector3 temp = transform.position;
        temp.y = 0.5f;
        temp.z += 1.9f;
        bench.transform.position = temp;
    }
    void InstantiateTarget() {
        target = (GameObject)Instantiate(Resources.Load("Prefabs/RobotArmAccesories/Cylinder"));
        Vector3 temp = transform.position;
		temp.y = 1.0f;
		// z + 1.9 places the target on the centre of the workbench.
        //temp.z += 1.9f;
        temp.z += 1.9f + Random.Range(-0.5f, 0.5f);
        temp.x += Random.Range(-1.3f, 1.3f);
        target.transform.position = temp;
    }
//------------------------------------------------------------------------------ 
    void ToggleAudio(AudioSource audio, float movement) {
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
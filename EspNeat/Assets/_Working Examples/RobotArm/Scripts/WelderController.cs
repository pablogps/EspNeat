using UnityEngine;
using System.Collections;
using SharpNeat.Phenomes;

public class WelderController : UnitController {

    private static int ID_counter = 0;

    private GameObject target;
    private Material targetMaterial;
    private Vector3 prevTargetPosition;
    private float targetAngle;

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

    // How accurate must x-Axe be in order to produce signals. In degrees.
    private float isXpointingThreshold = 1.5f;

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
        // If movement in the manipulator (last entry) is to be allowed, then
        // GetAngle needs to be updated!
        MoveAll(Random.Range(-15.0f, 15.0f), Random.Range(-10.0f, 10.0f),
                Random.Range(-10.0f, 10.0f), Random.Range(-0.1f, 0.3f),
                Random.Range(0.0f, 0.0f));
/*        MoveAll(Random.Range(-0.0f, 0.0f), Random.Range(-10.0f, 10.0f),
                Random.Range(-10.0f, 10.0f), Random.Range(-0.1f, 0.4f),
                Random.Range(-0.0f, 0.0f));*/
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
        float totalFit = 0.0f;

        // Fitness bonus if x axe is looking towards the target.
        if (Mathf.Abs(GetDistanceXaxe()) < isXpointingThreshold)
        {
            totalFit += 1f;         
        }

        // If the arm is pointing towars the target, receive a large bonus!
        if (IsTowardsTarget(out distance))
        {
            totalFit += 2f;
        }

        // Finally, it is better to be closer to the target.
        totalFit += distance;

        return totalFit;
    } 
//------------------------------------------------------------------------------
    public void UndoMovement() {
        MoveAll(-rotateShoulderAngle, -rotateArmAngle, -rotateJointAngle,
            -movePistonDelta, -rotateManipulatorAngle);
    }
//------------------------------------------------------------------------------ 
    void FixedUpdate() {
        // Arm input:
        float distanceX;

        // Arm output:
        float rotateShoulder;
        float rotateArm;
        float rotateJoint;
        float movePiston;
        float moveManipulator;
        float release;

        if (target.transform.position != prevTargetPosition)
        {
            prevTargetPosition = target.transform.position;
            GetTargetAngle();
        }

        // Input signals are used in the neural controller
        ISignalArray inputArr = box.InputSignalArray;

		inputArr[0] = 0f;
        if (Mathf.Abs(GetDistanceXaxe()) < isXpointingThreshold)
		{
			inputArr[0] = 1f;			
		}

        float frontSensor = 0f;
        if (IsTowardsTarget(out frontSensor))
        {
            inputArr[1] = 1.0f;
            targetMaterial.color = Color.red;
        }
        else
        {
            inputArr[1] = 0.0f;
            targetMaterial.color = Color.white;
        }

        inputArr[2] = frontSensor;

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
        // Release cargo? (Binary output)
        if (outputArr[9] > 0.5)
        {
            release = 1f;
        }
        else
        {
            release = 0f;
        }

        // Manual control:
/*        rotateShoulder = Input.GetAxis("Horizontal");
        rotateArm = Input.GetAxis("Vertical");
        rotateJoint = Input.GetAxis("MyInputIK");
        movePiston = Input.GetAxis("MyInputJL");
        moveManipulator = Input.GetAxis("MyInputTG");
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
        rotateManipulatorAngle = moveManipulator * manipulatorSpeed * Time.deltaTime;

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
    // Simply to avoid repetition (used 3 times)
    float ActuatorValueIfEnabled(double enabler, float convertMe) {
        if (enabler > 0.5)
        {
            return convertMe * 2f - 1f;
        }
        return 0f;
    }
//------------------------------------------------------------------------------
    float GetDistanceXaxe() {
        // NOTE: If the manipulator moves, then this is no longer true.
        // If my math was correct, the extra angle (the sign of course should be
        // checked as well) for shoulder angle will be:
        // atan( sin(A) / ((l/d) + cos(A)) )
        // where A is the local angle the manipulator has moved, d is the length
        // of the manipulator and l is the length of the arm up to the manipulator
        // joint. Note that l is a variable that depends on the piston!
        float angDist = targetAngle - shoulderXaxe;

        if (angDist < -180f)
        {
            angDist += 360f;
        }
        else if (angDist > 180f)
        {
            angDist -= 360f;
        }

        return angDist;
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
        temp.z += 1.4f;
        //temp.z += 1.9f + Random.Range(-0.5f, 0.5f);
        temp.x += Random.Range(-1.3f, 1.3f);
        target.transform.position = temp;
        targetMaterial = target.GetComponent<Renderer>().material;
        prevTargetPosition = target.transform.position;
        GetTargetAngle();
    }
//------------------------------------------------------------------------------
    void GetTargetAngle() {
        float distX = prevTargetPosition.x - transform.position.x;
        float distZ = prevTargetPosition.z - transform.position.z;

        // The particular order of distX and distZ accomodates the convention
        // we want for angles (so that they are the same as the angles used to
        // track the rotation state of the shoulder, shoulderXaxe)
        float angle = Mathf.Atan2(distX, distZ);

        // From radians to degrees (plus inversion for our convention)
        angle *= -57.2958f;

        targetAngle = angle;
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
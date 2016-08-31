using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SharpNeat.Phenomes;
using Cars;

public class CarController : UnitController {

	private float speed = 5f;
    private float turn_speed = 180f;
    private float front_sensors_range = 10f;
    private float side_sensors_range = 5f;
    private float lights_range = 5f;
    private float directions_range = 15f;

    public LayerMask onlyDefaultLayer;
    public LayerMask carsLayer;

	// We need to consider the trial lenght so fitness does not depend
    // on the trial length! (See Start())
	private static float fit_experiment_length = 0f;
	private float fit_advance_multiplier = 20f;
	private float fit_wall_hit_multiplier = 0.5f;
    private float traffic_lights_multiplier = 5f;
    private float directions_multiplier = 20f;

	// Upon start cars will substract 1 point when they detect the first
    // road piece, so we offset the value
	private int advanced = 1; 
	private int wall_hits = 0;
    private int traffic_light_violations = 0;
    private int junctions_count = 0;
	private int last_piece = 35;
	private int current_road_piece = 0;

	// This variable is the "brain" of the unit
	private IBlackBox box;
	private bool IsRunning;

    // Variables used to determine if the directions are correct
    private bool isDirectionPending = false;
    private DirectionTaken currentDirection = new DirectionTaken();
    private DirectionTaken expectedDirection = new DirectionTaken();

	public override IBlackBox GetBox()
	{
		return box;
	}

	public override void Stop()
	{
		this.IsRunning = false;
	}

	public override void Activate(IBlackBox box)
	{
		this.box = box;
		this.IsRunning = true;
	}
  
	public override float GetFitness()
	{
		float fit = (advanced * fit_advance_multiplier -
			         wall_hits * fit_wall_hit_multiplier - 
			         traffic_light_violations * traffic_lights_multiplier +
                     junctions_count * directions_multiplier) *
                     fit_experiment_length;


		Debug.Log("Advance term: " + (advanced * fit_advance_multiplier) + " advanced: " + advanced);
        Debug.Log("Hits term: " + (wall_hits * fit_wall_hit_multiplier) + " hits: " + wall_hits);
        Debug.Log("Lights term: " + (traffic_light_violations * traffic_lights_multiplier) + " lights: " + traffic_light_violations);
        Debug.Log("Directions term: " + (junctions_count * directions_multiplier) + " junctions_count: " + junctions_count);


		// After evaluation the unit is destroyed, so we do not need to reset
        // values, but it does not hurt either!
		advanced = 0;
		wall_hits = 0;
		traffic_light_violations = 0;
        junctions_count = 0;
		if (fit > 0)
		{
			return fit;
		}
		return 0;
	}

	void Start()
	{        
		// We are evolving a controller for a car driving alone: we do not want it to collide with
		// other cars during evolution! (This is specially damaging at start, where all units start
		// at the same point!)
        // Note: carsLayer.value = 256 (2^8) but name to layer will return 8, 
        // which is the value needed in IgnoreLayerCollision.
        int carsLayerInt = LayerMask.NameToLayer("Car");
        Physics.IgnoreLayerCollision(carsLayerInt, carsLayerInt, true);

		// Looking for objects is expensive, so we make this static and only look for 
		// them in the first instance!
		if (fit_experiment_length == 0f)
		{
			fit_experiment_length = 1f / 
				GameObject.Find("Evaluator").GetComponent<Optimizer>().TrialDuration;
		}

        RandomizeStartConditions();
        //InitializeLookUpTable();
	}

	// Update is called once per frame
	void FixedUpdate()
	{		
		//MANUAL DRIVING
		/*
		//grab the input axes
		float steer = Input.GetAxis("Horizontal");
		float gas = Input.GetAxis("Vertical");

	    //take the throttle level (with keyboard, generally +1 if up, -1 if down)
	    //  and multiply by speed and the timestep to get the distance moved this frame
		float move_dist = gas * speed * Time.deltaTime;

	    //now the turn amount, similar drill, just turnSpeed instead of speed
	    //   we multiply in gas as well, which properly reverses the steering when going 
	    //   backwards, and scales the turn amount with the speed
		float turn_angle = steer * turn_speed * Time.deltaTime * gas;   
		transform.Rotate(0, turn_angle, 0);
	    //and now move forward by moveVect
		transform.Translate(Vector3.forward * move_dist);
		*/

		// Automatic driving
		// Five sensors: Front, left front, left, right front, right
		if (IsRunning)
		{
			float frontSensor = 0f;
			float leftFrontSensor = 0f;
			float leftSensor = 0f;
			float rightFrontSensor = 0f;
			float rightSensor = 0f;
            float trafficLightsSensor = 0f;
            float directionSignalSensor = 0f;

            frontSensor = CastRay(
                    transform.TransformDirection(new Vector3(0, 0, 1).normalized),
                    front_sensors_range, "Wall");
            rightFrontSensor = CastRay(
                    transform.TransformDirection(new Vector3(0.5f, 0, 1).normalized),
                    front_sensors_range, "Wall");
            rightSensor = CastRay(
                    transform.TransformDirection(new Vector3(1, 0, 0).normalized),
                    side_sensors_range, "Wall");
            leftFrontSensor = CastRay(
                    transform.TransformDirection(new Vector3(-0.5f, 0, 1).normalized),
                    front_sensors_range, "Wall");
            leftSensor = CastRay(
                    transform.TransformDirection(new Vector3(-1, 0, 0).normalized),
                    side_sensors_range, "Wall");
            
            trafficLightsSensor = LookForLights();
            directionSignalSensor = LookForDirection();

            /*            Debug.Log("front " + frontSensor);
            Debug.Log("rightFrontSensor " + rightFrontSensor);
            Debug.Log("rightSensor " + rightSensor);
            Debug.Log("leftFrontSensor " + leftFrontSensor);
            Debug.Log("leftSensor " + leftSensor);
            Debug.Log("directionSignalSensor " + directionSignalSensor);*/

			//Input signals are used in the neural controller
			ISignalArray inputArr = box.InputSignalArray;
			inputArr[0] = frontSensor;
			inputArr[1] = leftFrontSensor;
			inputArr[2] = leftSensor;
			inputArr[3] = rightFrontSensor;
            inputArr[4] = rightSensor;
            inputArr[5] = trafficLightsSensor;
            inputArr[6] = directionSignalSensor;
			//Which is activated
			box.Activate();
			//And produces output signals (also in an array)
			ISignalArray outputArr = box.OutputSignalArray;

			//The vehicle moves with the output from the neural network
			float steer = (float)outputArr[0] * 2f - 1f;
			float gas =   (float)outputArr[1] * 2f - 1f;

            // Is it complying with traffic lights?
            // Green (0) and orange (0.5) are Ok, red (1) means stop.
            // Gas goes from -1 to 1, so we want its absolute value below 0.05.
            if (trafficLightsSensor > 0.55f &&
                System.Math.Abs(gas) > 0.05)
            {
                ++traffic_light_violations;
            }

			float move_dist = gas * speed * Time.deltaTime;
			// A car can only turn if it advances (this is why we multiply by gas)
			float turn_angle = steer * turn_speed * Time.deltaTime * gas;

			transform.Rotate(new Vector3(0, turn_angle, 0));
			transform.Translate(Vector3.forward * move_dist);
		}
	}

    /// <summary>
    /// Casts rays for the different sensors
    /// </summary>
    float CastRay(Vector3 direction, float range, string target)
    {
        Vector3 fromPosition = transform.position + transform.forward * 1.1f;

        RaycastHit hit;
        if (Physics.Raycast(fromPosition, direction, out hit, range, onlyDefaultLayer))
        {
            if (hit.collider.tag.Equals(target))
            {
                return 1f - hit.distance / range;
            }
        }
        return 0f;
    }

    /// <summary>
    /// Casts a ray looking for traffic lights. If any are found, returns its state.
    /// </summary>
    float LookForLights()
    {
        float returnValue = 0f;

        RaycastHit hit;
        Vector3 fromPosition = transform.position + transform.forward * 1.1f;
        Vector3 direction = transform.TransformDirection(new Vector3(0, 0, 1).normalized);

        if (Physics.Raycast(fromPosition, direction, out hit, lights_range, ~carsLayer))
        {
            if (hit.collider.tag.Equals("TrafficLightDetector"))
            {
                // TrafficLight found!
                returnValue = 
                        hit.collider.gameObject.
                        GetComponentInParent<TrafficLightController>().GetLightStateAsFloat();
            }
        }

        return returnValue;
    }

    /// <summary>
    /// Casts a ray looking for direction signals. If any are found, returns its state.
    /// </summary>
    float LookForDirection()
    {
        float returnValue = 0f;

        RaycastHit hit;
        Vector3 fromPosition = transform.position + transform.forward * 1.1f;
        Vector3 direction = transform.TransformDirection(new Vector3(0, 0, 1).normalized);

        if (Physics.Raycast(fromPosition, direction, out hit, directions_range, ~carsLayer))
        {
            if (hit.collider.tag.Equals("IntersectionTrigger"))
            {
                // Direction sign found!
                returnValue = 
                    hit.collider.gameObject.
                    GetComponentInParent<DirectionSignalController>().GetDirectionAsFloat();
            
                // Write down the expected outcome:
                isDirectionPending = true;

                // Left is returnValue 0.33, more than that is straight or right
                if (returnValue < 0.4)
                {
                    expectedDirection = DirectionTaken.left;
                }
                else
                {
                    expectedDirection = DirectionTaken.rightOrStraight;
                }
            }
        }

        return returnValue;
    }

	// This is only to see if the cars are advancing from a road segment to another. For collisions
	// we want "OnCollisionStay"
	void OnCollisionEnter(Collision collision)
	{
		if (collision.collider.tag.Equals("Road"))
		{
			// We access the script of the new road piece (so we can access its ID)
			RoadPiece road_piece = collision.collider.GetComponent<RoadPiece>();

			if (road_piece.PieceNumber > current_road_piece)
			{
				++advanced;
			}
			else
			{
                --advanced;
			}

			// Special situation: from start (0) backwards (last piece, 35) or vice versa
			if (road_piece.PieceNumber == 0 && current_road_piece == last_piece)
			{
				// We are actually advancing! (Add 1 and correct the previous -1,
                // because 0 < last_piece)
                advanced += 2;
			}
            // From first to last (we check 0 AND 1 to prevent evolution from
            // spamming time-precision glitches)
            else if (road_piece.PieceNumber == last_piece &&
                     (current_road_piece == 0 || current_road_piece == 1))
			{
				// We are actually going in reverse! Extra penalty if done at the start.
                advanced -= 3;				
			}

			current_road_piece = road_piece.PieceNumber;

            // If we have to determine the result of a junction, and we are 
            // outside of the junction...
            if (isDirectionPending && road_piece.roadType != DirectionTaken.isJunction)
            {
                if (road_piece.roadType != expectedDirection)
                {
                    //Debug.Log("APPLY PENALTY");
                    // Take into account that in a simple track with 2 real 
                    // junctions and 2 easy ones (where two roads meet) a
                    // car going "always right" would get 3 right and 1 wrong,
                    // but we need this to be counted as a mistake, so 1 penalty
                    // must, at least, make up for 3 rewards.
                    junctions_count -= 8;
                }
                else
                {
                    //Debug.Log("APPLY REWARD");
                    ++junctions_count;
                }
                isDirectionPending = false;
            }

		}

	}

	// IMPORTANT: This penalty currently depends on the frame rate! (We do not want that). Remember optimizer
	// calculates fps, so it can be corrected with that value.
	void OnCollisionStay(Collision collision)
	{
		// We call this function so cars do not slide along walls with little cost!
		if (collision.collider.tag.Equals("Wall"))
		{
			++wall_hits;
		}		
	}

    /// <summary>
    /// This is used so cars start from slightly different points and directions.
    /// Noise is good!
    /// </summary>
    void RandomizeStartConditions()
    {
        Vector3 currentPosition = transform.position;
        // Shift x position by (-2 < rand < 2)
        currentPosition.x += Random.value * 4f - 2f;
        transform.position = currentPosition;

        float maxRotation = 25f;
        // So we get something between (-maxRotation, maxRotation)
        float rotationValue = Random.value * maxRotation * 2 - maxRotation;
        transform.Rotate(new Vector3(0, rotationValue, 0));
    }

    /// <summary>
    /// Here we make a look up table with the correct road segments that must
    /// follow in a junction
    /// </summary>
/*    void InitializeLookUpTable()
    {
        directionLookUp = new Dictionary<int, List<int>>();
        // There are 9 junctions, somo of them can be seen from different road tiles
    }*/
}

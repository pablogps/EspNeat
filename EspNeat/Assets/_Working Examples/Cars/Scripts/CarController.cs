using UnityEngine;
using System.Collections;
using SharpNeat.Phenomes;

public class CarController : UnitController {

	public float speed = 5f;
	public float turn_speed = 180f;
	public float sensor_range = 10f;

    public LayerMask onlyDefaultLayer;
    public LayerMask carsLayer;

	// We need to consider the trial lenght so fitness does not depend
    // on the trial length! (See Start())
	private static float fit_experiment_length = 0f;
	private float fit_advance_multiplier = 0.3f;
	private float fit_wall_hit_multiplier = 0.1f;

	// Upon start cars will substract 1 point when they detect the first
    // road piece, so we offset the value
	private int advanced = 1; 
	private int wall_hits = 0; 
	private int last_piece = 17;
	private int current_road_piece = 0;

	// This variable is the "brain" of the unit
	private IBlackBox box;
	private bool IsRunning;

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
		float fit =  (advanced * fit_advance_multiplier - wall_hits *
                     fit_wall_hit_multiplier) * fit_experiment_length;
		//Debug.Log(fit + " " + (advanced * fit_advance_multiplier - wall_hits *
        //          fit_wall_hit_multiplier)  + " " + 
		//	        (advanced * fit_advance_multiplier + " " + wall_hits *
        //          fit_wall_hit_multiplier) );
		// After evaluation the unit is destroyed, so we do not need to reset
        // values, but it does not hurt either!
		advanced = 0;
		wall_hits = 0;
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
                    sensor_range, "Wall");
            rightFrontSensor = CastRay(
                    transform.TransformDirection(new Vector3(0.5f, 0, 1).normalized),
                    sensor_range, "Wall");
            rightSensor = CastRay(
                    transform.TransformDirection(new Vector3(1, 0, 0).normalized),
                    sensor_range, "Wall");
            leftFrontSensor = CastRay(
                transform.TransformDirection(new Vector3(-0.5f, 0, 1).normalized),
                sensor_range, "Wall");
            leftSensor = CastRay(
                transform.TransformDirection(new Vector3(-1, 0, 0).normalized),
                sensor_range, "Wall");

            trafficLightsSensor = LookForLights();
            directionSignalSensor = LookForDirection();

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
                return 1f - hit.distance / sensor_range;
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
        float range = 10f;

        if (Physics.Raycast(fromPosition, direction, out hit, range, ~carsLayer))
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
        float range = 15f;

        if (Physics.Raycast(fromPosition, direction, out hit, range, ~carsLayer))
        {
            if (hit.collider.tag.Equals("IntersectionTrigger"))
            {
                // Direction sign found!
                returnValue = 
                    hit.collider.gameObject.
                    GetComponentInParent<DirectionSignalController>().GetDirectionAsFloat();
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
				advanced -= 2;
			}

			// Special situation: from start (0) backwards (last piece, 17) or vice versa
			if (road_piece.PieceNumber == 0 && current_road_piece == last_piece)
			{
				// We are actually advancing! (Add 1 and correct the previous -2, because 0 < 17)
				advanced += 3;
			}
			else if (road_piece.PieceNumber == last_piece && current_road_piece == 0)
			{
				// We are actually going in reverse! (Substract 2 and correct the previous +1)
				advanced -= 3;				
			}

			current_road_piece = road_piece.PieceNumber;
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
}

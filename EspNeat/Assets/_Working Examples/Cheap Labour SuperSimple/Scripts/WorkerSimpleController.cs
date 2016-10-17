using UnityEngine;
using System.Collections;
using SharpNeat.Phenomes;

public class WorkerSimpleController : UnitController { 
  // These values determine how fast our units move and turn
  public float acceleration = 50f;
  public float max_speed = 1f;
  public float rotation_speed = 100f;
  // These variable characterize the unit's sensors
  public float sensor_range = 2f; // proximity sensor's reach
  //  How far the eyes see. The original value was 10, about half the arena. 
  public float seeing_range = 35f; // 
  public float field_of_view = 20f; // in degrees
  public LayerMask see_layer; // will only see objects in this layer (performance)
  public LayerMask no_sense_layer; // will NOT sense objects in this layer (performance)
  public GameObject cargo_drop; 

  private static ClockSimple clock_script; // sets blue or red clock periods
  // These are needed to compute the fitness 
  private float av_speed;
  private float collision_counter;
  private float my_time;
  private float fit_speed_multiplier = 0.3f;
  private float fit_hit_multiplier = 0.03f;
  private static float fit_experiment_length = 0f;
  private float fit; // here we include the fitness related to cargo-delivery
  // Cargo-related fitness penaties and rewards
  private float bonus_pick_up_cargo = +40f;
  private float penalty_wrong_dock = -80f;
  private float penalty_full_on_dock = -20f;
  private float bonus_delivery = +50f;
  private float penalty_delivery_empty = -10f;

  private Rigidbody rigid_body; // used for movement
  private float input_cargo = 0f; // 0 means no cargo
  // renderers to change body part colours
  private Renderer rend_cargo_bay;
  // private Color rojo = new Color(1f,0.318f,0.318f,1f);
  // private Color verde = new Color(0.189f,0.639f,0.189f,1f);
  private Color empty_cargo = new Color(0.067f,0.522f,0.467f,0.624f);

  // Would allow to change the renderer color, but all of it at once!
  // (Not only the new parts.)
  // private Renderer trail_renderer;

  // box is the neural network which will control the unit
  IBlackBox box;  
  bool IsRunning;
//------------------------------------------------------------------------------
  void Start () {
    // First we get some components for fast access
    rigid_body = GetComponent<Rigidbody>();
    rend_cargo_bay = this.transform.Find("CargoBay").GetComponent<Renderer>();
    // trail_renderer = this.transform.Find("TrailRenderer").GetComponent<TrailRenderer>();
    // Looking for objects is expensive, so we make this static and only look for 
    // them in the first instance! (if we already have looked for the experiment 
    // length we already have the clock, so we don't need to check both!)
    if (fit_experiment_length == 0f) {
      // Let's look for the clock!
      clock_script = GameObject.Find("Clock").GetComponent<ClockSimple>();
      fit_experiment_length = 1f / 
          GameObject.Find("Evaluator").GetComponent<Optimizer>().TrialDuration;
    }

	// Avoids collisions between units (they will be invisible to each other)
	int unit_layer = LayerMask.NameToLayer("Units");
	Physics.IgnoreLayerCollision(unit_layer, unit_layer, true);

    // We also initialize some values (could be done in the definition...)
    collision_counter = 0f;
    my_time = 0f;
    av_speed = 0f;
    // fit will be added to fitness
    fit = 0f;

    // Start in a random position.
    // -6 < x < 6 and -3 < z < 3.5 is a good guide.
    transform.rotation = Quaternion.Euler(0f, 0f + 360f * Random.value, 0f) * 
                         transform.rotation;
    Vector3 initialPosition = rigid_body.position;
    initialPosition.x = -9.4f + 18.8f * Random.value;
    initialPosition.z = -4f + 10f * Random.value;
    rigid_body.position = initialPosition;
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
    float fitness = fit_speed_multiplier * av_speed - 
                    collision_counter * fit_hit_multiplier * fit_experiment_length + 
					fit * fit_experiment_length;
    // Different contributions to fitnes!
     Debug.Log("Speed component: " + fit_speed_multiplier * av_speed +
               " where average speed is: " + av_speed);
     Debug.Log("Collisions component: " + collision_counter * fit_hit_multiplier * fit_experiment_length +
               " where collision number is: " + collision_counter);
     Debug.Log("Events component: " + fit * fit_experiment_length +
               " where fit is: " + fit);
		
    fit = 0f;
    collision_counter = 0f;
    my_time = 0f;
	av_speed = 0f;

    if (fitness > 0f) {
      return fitness;
    }
    return 0f;
  }  
//------------------------------------------------------------------------------
  // What happens in a cargo dock: load if empty (apply fitness bonusses and penalties)
  public void OnCargoDock(int Dock_ID) {
    // First checking if there is already cargo! (value 1)
    if (input_cargo < 1f) {
      // Which dock is this? (1 blue)
      if (Dock_ID == 1) {
        // It is only possible to load during the correct clock value!
        if (clock_script.GetState() == 0) { 
          LoadCargo(Color.blue);
        } else {    
          // incorrect clock: fitness penalty
          AddToFitness(penalty_wrong_dock);
        }
      } else { // dock_ID = 2: red dock
        if (clock_script.GetState() == 1) { 
          LoadCargo(Color.red); 
        } else {    
          AddToFitness(penalty_wrong_dock);
        }
      }
    } else {
      // There IS cargo! You should not be here!  
      AddToFitness(penalty_full_on_dock);
    }   
  }  
//------------------------------------------------------------------------------
  // What happens over the chasm: unload cargo and apply fitness modifiers
  public void OnChasm() {
    if (input_cargo > 0f) {
      // Drop the cargo!
      Vector3 drop_position = new Vector3(transform.position.x, 0.2f, 4.75f);
      Quaternion drop_rotation = Quaternion.Euler(0f, transform.rotation.y, 90f);
      GameObject drop = Instantiate(cargo_drop, drop_position, drop_rotation) 
                 as GameObject;
      drop.GetComponent<Renderer>().material.color = rend_cargo_bay.material.color;
      // Reset cargo bay colour
      rend_cargo_bay.material.color = empty_cargo;
      // Well done! Take this fitness reward!    
      AddToFitness(bonus_delivery);
      input_cargo = 0f;
    } else {
      // There is no cargo yet! You should not be here!
      AddToFitness(penalty_delivery_empty);
	} 
  }   
//------------------------------------------------------------------------------
  void LoadCargo(Color color) {             
    AddToFitness(bonus_pick_up_cargo);
    input_cargo = 1f; // cargo input updated
    rend_cargo_bay.material.color = color;
  }  
//------------------------------------------------------------------------------
  void AddToFitness(float add_this) {
    fit += add_this;
  }  
//------------------------------------------------------------------------------
  void FixedUpdate () {   
        // Uncomment for PHENOME TESTING
/*        ISignalArray inputArr = box.InputSignalArray;
        inputArr[0] = 0.01;
        inputArr[1] = 0.1;
        inputArr[2] = 0.2;
        inputArr[3] = 0.4;
        inputArr[4] = 0.6;
        inputArr[5] = 0.8;
        inputArr[6] = 0.99;
        inputArr[7] = 0.999;

        // Which is activated
        box.Activate();
        // And produces output signals (also in an array)
        ISignalArray outputArr = box.OutputSignalArray;  
        // The vehicle moves with the output from the neural network
        // Output is between 0 and 1: we need it from -1 to +1
        float gas =   (float)outputArr[0] * 2f - 1f;
        float steer = (float)outputArr[1] * 2f - 1f; 
        Debug.Log(" ");
        Debug.Log("Output array and gas: " + outputArr[0] + " " + gas);
        Debug.Log("Output array and steer: " + outputArr[1] + " " + steer);
        UnityEditor.EditorApplication.isPlaying = false;*/


    // These are the robot's range sensors
    float front_eco_sensor = 0f;
    float left_front_eco_sensor = 0f;
    float right_front_eco_sensor = 0f;
    float blue_eye = 0f;
	float red_eye = 0f;
	float pink_eye = 0f;
    // Range sensors go from 0 (no object detected) to about 1 (collision)
    front_eco_sensor = RangeRay(new Vector3(0f, 0f, 1f).normalized);
    right_front_eco_sensor = RangeRay(new Vector3(1f, 0f, 1f).normalized);
    left_front_eco_sensor = RangeRay(new Vector3(-1f, 0f, 1f).normalized);   
    // Each eye will see a different cargo dock, and both will also respond to the chasm
    red_eye = See("CargoDock2");
    blue_eye = See("CargoDock1");
	pink_eye = See("ChasmTrigger");

    // Input signals are used in the neural controller
    ISignalArray inputArr = box.InputSignalArray;
    inputArr[0] = front_eco_sensor;
    inputArr[1] = left_front_eco_sensor;
    inputArr[2] = right_front_eco_sensor;
    inputArr[3] = blue_eye;
    inputArr[4] = red_eye;
    inputArr[5] = pink_eye;
    inputArr[6] = input_cargo; // is cargo bay loaded?
    inputArr[7] = (float)clock_script.GetState(); // clock value (blue/red)

    // Which is activated
    box.Activate();
    // And produces output signals (also in an array)
    ISignalArray outputArr = box.OutputSignalArray;	    

    // The vehicle moves with the output from the neural network
    // Output is between 0 and 1: we need it from -1 to +1
    float gas =   (float)outputArr[0] * 2f - 1f;
    float steer = (float)outputArr[1] * 2f - 1f;  
    Move(gas, steer);
  }  
//------------------------------------------------------------------------------
  void Move(float gas, float steer) {
    // USING NEURAL NETWORK OUTPUT:
    // First the unit rotates
    transform.rotation = Quaternion.Euler(0f, steer * rotation_speed * 
                                          Time.deltaTime, 0f) * transform.rotation; 
    // then it advances in the new direction
    rigid_body.AddForce(acceleration * gas * transform.forward * Time.deltaTime);
    // Now a speed limit is enforced. This is done in this order so even at max
    // speed it is possible to turn! (there is likely a better way)
    if (rigid_body.velocity.magnitude > max_speed) {
      // This fixes the magnitude of the vector but still allows to change direction!
      rigid_body.velocity = rigid_body.velocity.normalized * max_speed;
    }

    // USING USER INPUT (for debugging)    
    // Use user input to move the robot (for debugging)
/*   
    float rotate = Input.GetAxis("Horizontal");
    float advance = Input.GetAxis("Vertical");    
    transform.rotation = Quaternion.Euler(0f, rotate * rotation_speed *
                                          Time.deltaTime, 0f) * transform.rotation;
    rigid_body.AddForce(acceleration * advance * transform.forward * Time.deltaTime);
    // There MUST be a more efficient way to set a max speed (allowing changing 
    // direction at max speed!)
    if (rigid_body.velocity.magnitude > max_speed) {
      // This fixes the magnitude of the vector but still allows to change direction!
      rigid_body.velocity = rigid_body.velocity.normalized * max_speed;
    }*/

	// Average speed is computed for fitness.
	//av_speed = ((av_speed * my_time) + (rigid_body.velocity.magnitude * 
	//                                      Mathf.Sign(gas) * Time.deltaTime)) / 
	//                                      (my_time + Time.deltaTime);
	av_speed = ((av_speed * my_time) + (rigid_body.velocity.magnitude * 
	  Time.deltaTime)) / 
	  (my_time + Time.deltaTime);
	my_time += Time.deltaTime; 
  }  
//------------------------------------------------------------------------------
  // This are the range sensors for the input
  float RangeRay(Vector3 ray_direction) {
    // The starting point of the robot is just in front of it
    float just_ahead = 0.25f;
    RaycastHit hit;
    if (Physics.Raycast(transform.position + transform.forward * just_ahead,
                        transform.TransformDirection(ray_direction),
			            out hit, sensor_range, ~no_sense_layer)) {
      // Here we update "hits", which is used to compute the fitness value
      collision_counter += 1f - hit.distance / sensor_range;
      return 1f - hit.distance / sensor_range;
    } else {
      // no collision: 0
      return 0f;
    }
  }  
//------------------------------------------------------------------------------ 
  float See(string target) {
    RaycastHit[] hits;
    hits = Physics.RaycastAll(transform.position, transform.forward, 
                              seeing_range, see_layer);    
    for (int i = 0; i < hits.Length; ++i) {
      RaycastHit hit = hits[i];
      // ALL OBJECTS ARE SEEN THROUGH
      // To change this, we need to know the distance to the different objects
      /*
      // The two-eyes model worked with only two eyes, each reacting to either
      // the red or blue area AND the chasm (unload region).
      if (hit.transform.name == target || hit.transform.name == "ChasmTrigger") {
        // Cambiar color ojos?
        return 1f - (hit.distance / seeing_range);
      }
      */
      if (hit.transform.name == target) {
        return 1f - (hit.distance / seeing_range);
      } 
    }
    return 0f;
  }   
//------------------------------------------------------------------------------
  void OnCollisionStay() {
    // Extra penalty for collisions!
    ++collision_counter;
  }
}
using UnityEngine;
using System.Collections;

public class Clock : MonoBehaviour {
	public float period = 10f;

	private int state;
	private float elapsed_time = 0f;
  private Renderer rend_clock;
  private Color blue_clock = new Color(0f,0.129f,1f,1f);
  private Color red_clock = new Color(1f,0f,0f,1f);
  private Camera main_camera;
  private GUIStyle transparent_style = new GUIStyle();

  //We need to access this value from other objects
  public int GetState() {
    return state;
  }

  // Gets the main camera (needed in GUI to create a button where the clock is).
  // Gets the renderer to change the clock colour and start it with a random 
  // state.
  void Start() {   
    main_camera = (GameObject.FindWithTag("MainCamera")).GetComponent<Camera>();
    rend_clock = this.GetComponent<Renderer>();
    if (Random.value > 0.5f) {
      state = 1;
    } else {
      state = 0;
    }
    // This is a lazy way to start the colour (setting the state value does not
    // do this!)
    Change();
  }

  // Changes the clock state with the given period
	void Update() {
    elapsed_time += Time.deltaTime;
    if (elapsed_time > period) {
      //we reset our timer
      elapsed_time = 0f;
      //and change the clock
      Change();
    }
	}

  // Simply changes the clock state from red to blue and vice versa
  void Change() {
    if (state == 0) {
      state = 1;
      rend_clock.material.color = red_clock;
    } else {
      state = 0;
      rend_clock.material.color = blue_clock;
    }
  }

  // Allows to manually change the clock by clicking on it. The position of the
  // clock adapts to dynamic screen changes (but the size remains constant, so
  // if the clock looks big it may not cover the whole thing, which is fine).
  void OnGUI() {
    // We need the clock position in Viewport coordinates, which are normalized
    // so left-down is (0,0) and up-right is (1,1). We then get the position
    // in pixels by multiplying by the screen size. And offset with half of the
    // button size so it is centered.
    Vector3 temp = main_camera.WorldToViewportPoint(transform.position);
    if (GUI.Button(new Rect(Screen.width * temp.x - 20f, 
                            Screen.height * (1f - temp.y) - 20f, 40, 40), 
                   " ", transparent_style))
    {
      Change();
    }     
  }
}


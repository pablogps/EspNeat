using UnityEngine;
using System.Collections;

public class ClockSimple : MonoBehaviour {
    public float period = 10f;

	private int state;
	private float elapsed_time = 0f;
    private Renderer rend_clock;
    private Color blue_clock = new Color(0f,0.129f,1f,1f);
    private Color red_clock = new Color(1f,0f,0f,1f);
    private Camera main_camera;
    private GUIStyle transparent_style = new GUIStyle();

    /// <summary>
    /// We need to access this value from other objects
    /// </summary>
    public int GetState()
    {
        return state;
    }

    /// <summary>
    /// Gets the main camera (needed in GUI to create a button where the clock is).
    /// Gets the renderer to change the clock colour and start it with a random 
    /// state.
    /// </summary>
    void Start()
    {   
        main_camera = (GameObject.FindWithTag("MainCamera")).GetComponent<Camera>();
        rend_clock = this.GetComponent<Renderer>();
        if (Random.value > 0.5f)
        {
            state = 1;
        }
        else
        {
            state = 0;
        }
        // This is a lazy way to start the colour (setting the state value does not
        // do this!)
        Change();
    }

    /// <summary>
    /// Changes the clock state with the given period
    /// </summary>
    void Update()
    {
        elapsed_time += Time.deltaTime;
        if (elapsed_time > period)
        {
            //we reset our timer
            elapsed_time = 0f;
            //and change the clock
            Change();
        }
    }
 
    /// <summary>
    /// Changes the clock state from red to blue and vice versa
    /// </summary>
    public void Change()
    {
        if (state == 0)
        {
            state = 1;
            rend_clock.material.color = red_clock;
        }
        else
        {
            state = 0;
            rend_clock.material.color = blue_clock;
        }
    }
}


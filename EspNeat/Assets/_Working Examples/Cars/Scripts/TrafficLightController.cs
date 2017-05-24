using UnityEngine;
using System.Collections;

public class TrafficLightController : MonoBehaviour {
	
    private GameObject redLight = null;
    private GameObject orangeLight = null;
    private GameObject greenLight = null;

    // Traffic light period (note the maximum period will actually be
    // maxPeriod + minPeriod)
    private float maxPeriod = 6f; // 6f
    private float minPeriod = 2f; // 2f
    private float period;
    private float elapsed_time = 0f;
    private static float fitnessBasedPeriod = 6.0f;

	private enum lightState
	{
		Red,
		Orange,
		Green,
		off
	}

    private lightState currentLight = new lightState();

	void Awake()
	{
        // Gets the length for the first clock period
        NewPeriod();

        // We need all traffic lights with the same period!
        if (fitnessBasedPeriod < 0.01f)
        {
            fitnessBasedPeriod = (Random.value * 6f) + 35f;
            Debug.Log("Period: " + fitnessBasedPeriod);
        }

        // Gets a reference to the actual lights!
        redLight = transform.FindChild("LightRed").gameObject;
        orangeLight = transform.FindChild("LightOrange").gameObject;
        greenLight = transform.FindChild("LightGreen").gameObject;

		// Starts the traffic light to either orange or green
		float myRandom = Random.value;
		if (myRandom < 0.5f)
		{
            currentLight = lightState.Orange;
            // For fitness-based evolution
            period = fitnessBasedPeriod * 0.5f;
		}
		else
        {
            currentLight = lightState.Green;
            // For fitness-based evolution
            period = fitnessBasedPeriod;
		}

        LigthChange();
	}

    /// <summary>
    /// Used to change the traffic light after the waiting period
    /// </summary>
    void Update()
    {
        elapsed_time += Time.deltaTime;
        if (elapsed_time > period)
        {
            //we reset our timer
            elapsed_time = 0f;
            NewPeriod();
            //and change the clock
            NextLight();
        }   
    }

    /// <summary>
    /// This returns a float to be used as input by neural networks.
    /// </summary>
    public float GetLightStateAsFloat()
    {
        switch (currentLight)
        {
        case lightState.Red:
            return 1f;
            break;
        case lightState.Orange:
            return 0.5f;
            break;
        case lightState.Green:
            return 0f;  
            break;
        default:
            return 0f;  
            break;
        }        
    }

    /// <summary>
    /// Sets the next period for the traffic light
    /// </summary>
    private void NewPeriod()
    {
        period = Random.value * maxPeriod + minPeriod;
    }

    /// <summary>
    /// Fixed period values used only for fitness-based evolution.
    /// </summary>
    private void NextLight()
    {
        switch (currentLight)
        {
        case lightState.Red:
            currentLight = lightState.Green;

            // For fitness-based evolution
            period = fitnessBasedPeriod;

            break;
        case lightState.Orange:
            currentLight = lightState.Red;

            // For fitness-based evolution
            period = fitnessBasedPeriod * 0.5f;

            break;
        case lightState.Green:
            currentLight = lightState.Orange;
            // Orange always has a fixed short period:
            period = minPeriod;

            // For fitness-based evolution
            period = fitnessBasedPeriod * 0.5f;

            break;
        default:  
            break;
        }

        LigthChange();
    }

    /// <summary>
    /// The light has changed, so the actual lights are changed (only one on!)
    /// </summary>
	private void LigthChange()
    {
        redLight.SetActive(false);
        orangeLight.SetActive(false);
        greenLight.SetActive(false); 

        switch (currentLight)
        {
            case lightState.Red:
                redLight.SetActive(true);
                break;
            case lightState.Orange:
                orangeLight.SetActive(true);
                break;
            case lightState.Green:
                greenLight.SetActive(true);  
                break;
            default:
                break;
        }
	}
}

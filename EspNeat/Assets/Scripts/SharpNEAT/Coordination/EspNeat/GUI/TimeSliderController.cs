using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TimeSliderController : MonoBehaviour {

	private Slider slider = null;

	void Awake()
	{
		slider = GetComponent<Slider>();
	}

	public void SetTimeScale()
	{
        // It may be the case (as it is, it happens for the evolution screen
        // slider) that SetTimeScale may be called before the game object is
        // awake (it is set inactive at creation).
		if (slider != null)
		{
			Time.timeScale = slider.value;
		}
		else
		{
			Time.timeScale = 1f;
		}
	}
}

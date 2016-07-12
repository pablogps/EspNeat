using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TimeSliderController : MonoBehaviour {

	private Slider slider;

	void Awake()
	{
		slider = GetComponent<Slider>();
	}

	public void SetTimeScale()
	{
		Time.timeScale = slider.value;
	}
}

using UnityEngine;
using System.Collections;

public class Destroy : MonoBehaviour {
	void OnTriggerEnter(Collider other) {
		Destroy(other.gameObject);
	}
}
